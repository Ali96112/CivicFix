using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CivicFix.Api.Models;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace CivicFix.Api.Controllers
{
    [ApiController]
    [Route("api/Reports")]
    public class ReportsAdminController : ControllerBase
    {
        private readonly SqlConnection _connection;

        public ReportsAdminController(SqlConnection connection)
        {
            _connection = connection;
        }

        [Authorize(Roles = "Admin")] // only Admin — Staff/Resident have no business here
        [HttpGet("shared")] // address: GET api/Reports/shared
        public async Task<IActionResult> GetSharedReports()
        {
            // Step 1 — get every report that is assigned to 2 OR MORE baladiyat.
            // The subquery counts the assignment rows for each report; >= 2 means shared.
            var reportsSql = @"
                SELECT
                    tbl_Reports.rpt_Id,
                    tbl_Reports.rpt_Title,
                    tbl_Reports.rpt_Description,
                    tbl_Reports.rpt_Status,
                    tbl_Reports.rpt_CreatedAt,
                    tbl_Reports.rpt_ReportedPhotoUrl,
                    tbl_Reports.rpt_Priority,
                    tbl_Reports.rpt_AgreementCount,
                    tbl_Categories.ctg_Name AS CategoryName
                FROM tbl_Reports
                INNER JOIN tbl_Categories ON tbl_Reports.rpt_CategoryId = tbl_Categories.ctg_Id
                WHERE (
                    SELECT COUNT(*)
                    FROM tbl_ReportAssignments
                    WHERE rpa_ReportId = tbl_Reports.rpt_Id
                ) >= 2
                ORDER BY
                    CASE tbl_Reports.rpt_Priority
                        WHEN 'High' THEN 1
                        WHEN 'Medium' THEN 2
                        WHEN 'Low' THEN 3
                        ELSE 4
                    END,
                    tbl_Reports.rpt_CreatedAt DESC";

            var sharedReports = (await _connection.QueryAsync<dynamic>(reportsSql)).ToList();

            // nothing shared — send back an empty list, not an error
            if (sharedReports.Count == 0)
                return Ok(new List<object>());

            
            var reportIds = new List<int>();//create a list that well have report ids
            foreach (var row in sharedReports)
                reportIds.Add(Convert.ToInt32((object)row.rpt_Id));//it get only reports id numbers [2,7,..]

            // Dapper expands the @Ids list into IN (1, 2, 3, ...) automatically
            //give me all the municipalities assigned to each one.
            var candidatesSql = @"
                SELECT
                    tbl_ReportAssignments.rpa_ReportId,
                    tbl_Municipalities.mun_Id,
                    tbl_Municipalities.mun_Name,
                    tbl_ReportAssignments.rpa_IsHandler,
                    tbl_ReportAssignments.rpa_AcceptedAt
                FROM tbl_ReportAssignments
                INNER JOIN tbl_Municipalities
                    ON tbl_ReportAssignments.rpa_MunicipalityId = tbl_Municipalities.mun_Id
                WHERE tbl_ReportAssignments.rpa_ReportId IN @Ids --Only get municipality assignments for each reports id
                ORDER BY tbl_Municipalities.mun_Name";

            var candidateRows = await _connection.QueryAsync<dynamic>(candidatesSql, new { Ids = reportIds });

            // Step 3 — group the candidates by report Id, so each report can carry its own list.
            var candidatesByReport = new Dictionary<int, List<HandlerCandidate>>();
            //assume canadiantrow contain : Report 1 → Beirut Report 1 → Baabda Report 3 → Zahle

            foreach (var candidate in candidateRows)
            {
                int candidateReportId = Convert.ToInt32((object)candidate.rpa_ReportId);//Get the report ID from the current row=1
                if (!candidatesByReport.ContainsKey(candidateReportId))//at first it is empty so it well be false! so true it well run
                    candidatesByReport[candidateReportId] = new List<HandlerCandidate>();//Report 1 now has an empty list ready to store its candidate municipalities

                object? acceptedAtRaw = (object?)candidate.rpa_AcceptedAt;//Example if no handler has been chosen: if well be acceptedat=null stil no one handled it

                candidatesByReport[candidateReportId].Add(new HandlerCandidate//add a municipality object into Report 1's list
                {
                    mun_Id = Convert.ToInt32((object)candidate.mun_Id),
                    mun_Name = (object?)candidate.mun_Name as string ?? "",
                    IsHandler = Convert.ToBoolean((object)candidate.rpa_IsHandler), // true = this baladiye already owns it
                    AcceptedAt = acceptedAtRaw == null || acceptedAtRaw is DBNull
                        ? (DateTime?)null
                        : Convert.ToDateTime(acceptedAtRaw)
                });
            }

            // Step 4 This part takes the shared reports and combines each report with the municipality list we created earlie
           
            var result = new List<SharedReportDto>();

            foreach (var row in sharedReports)
            {
                int currentReportId = Convert.ToInt32((object)row.rpt_Id);

                var candidates = candidatesByReport.ContainsKey(currentReportId)
                    ? candidatesByReport[currentReportId]
                    : new List<HandlerCandidate>();

                result.Add(new SharedReportDto
                {
                    rpt_Id = currentReportId,
                    rpt_Title = (object?)row.rpt_Title as string,
                    rpt_Description = (object?)row.rpt_Description as string,
                    rpt_Status = (object?)row.rpt_Status as string,
                    rpt_CreatedAt = Convert.ToDateTime((object)row.rpt_CreatedAt),
                    rpt_ReportedPhotoUrl = (object?)row.rpt_ReportedPhotoUrl as string,
                    rpt_Priority = (object?)row.rpt_Priority as string, // null for staff reports
                    rpt_AgreementCount = Convert.ToInt32((object)row.rpt_AgreementCount),
                    CategoryName = (object?)row.CategoryName as string,
                    Candidates = candidates,          // the baladiyat the admin can choose between
                    // true when nobody owns it yet — React uses this to highlight
                    // the reports that still need a decision
                    NeedsDecision = !candidates.Any(c => c.IsHandler)
                });
            }

            return Ok(result);
        }


        [Authorize(Roles = "Staff,Admin")]
        [HttpPut("{id:int}/status")] // address: PUT api/Reports/1/status
        public async Task<IActionResult> UpdateReportStatus(int id, [FromBody] UpdateStatusRequest request)
        {
            // CHANGED (ADDED BLOCK) — new rule you asked for:
            //   Admin    → can update the status of ANY report
            //   Staff    → can update ONLY reports assigned to their own baladiye
            //   Resident → already blocked by the [Authorize] line above

            //It does not trust the frontend to tell it who the user is
            var idClaim = User.FindFirst("Id")?.Value;
            if (!int.TryParse(idClaim, out int currentUserId))
                return BadRequest("Could not read user Id from token. Claim 'Id' not found.");

            var currentRole = User.FindFirst(ClaimTypes.Role)?.Value;

            // ADDED: reject junk status values before touching the database.
            // Edit this array if you add more statuses later.
            var allowedStatuses = new[] { "Submitted", "In Progress", "Resolved", "Rejected" };
            if (string.IsNullOrWhiteSpace(request.NewStatus) || !allowedStatuses.Contains(request.NewStatus))
                return BadRequest($"Status must be one of: {string.Join(", ", allowedStatuses)}.");

            //Resolved photo is required
            if (request.NewStatus == "Resolved" && string.IsNullOrWhiteSpace(request.ResolvedPhotoUrl))
                return BadRequest("A resolved photo is required when setting the status to Resolved.");

            // Step 1 — check the report exists
            var checkSql = "SELECT rpt_Id, rpt_Status FROM tbl_Reports WHERE rpt_Id = @Id";
            var report = await _connection.QueryFirstOrDefaultAsync<dynamic>(checkSql, new { Id = id });

            if (report == null)
                return NotFound("Report not found.");

            // ADDED: Step 1b — the baladiye ownership check for Staff.
            // Admin skips this whole block — that is what makes Admin "can update everything".
            if (currentRole == "Staff")
            {
                var myMunicipalityId = await _connection.QueryFirstOrDefaultAsync<int?>(
                    "SELECT usr_MunicipalityId FROM tbl_Users WHERE usr_Id = @Id", new { Id = currentUserId });
                if (myMunicipalityId == null)
                    return BadRequest("Staff member is not assigned to any baladiye.");

                if (await _connection.QueryFirstAsync<int>(
                    @"SELECT COUNT(*) FROM tbl_ReportAssignments
                      WHERE rpa_ReportId = @ReportId AND rpa_MunicipalityId = @MunicipalityId",
                    new { ReportId = id, MunicipalityId = myMunicipalityId.Value }) == 0)
                    return StatusCode(403, "You can only update reports assigned to your baladiye.");

                //Staff cannot resolve a report the Admin has not allocated yet.
                if (await _connection.QueryFirstAsync<int>(
                    "SELECT COUNT(*) FROM tbl_ReportAssignments WHERE rpa_ReportId = @ReportId",
                    new { ReportId = id }) > 1)
                    return StatusCode(403, "This report is shared between several baladiyat. An admin must decide who handles it before it can be updated.");
            }

            // ADDED: nothing to do if the status is already what was requested.
            // Stops fake StatusHistory rows and stops points being awarded twice
            // when someone presses "Resolved" a second time.
            if (Convert.ToString((object)report.rpt_Status) == request.NewStatus)
                return Ok("Report already has this status — nothing changed.");

            // Step 2 — update the report's status
            var updateSql = @"
                UPDATE tbl_Reports
                SET rpt_Status = @NewStatus,
                    rpt_ResolvedPhotoUrl = @ResolvedPhotoUrl
                WHERE rpt_Id = @Id";

            await _connection.ExecuteAsync(updateSql, new //ExecuteAsync because we're changing data, not fetching it.
            {
                NewStatus = request.NewStatus,
                ResolvedPhotoUrl = request.ResolvedPhotoUrl,
                Id = id
            });

            // Step 3 — log the status change in StatusHistory
            var historySql = @"
                INSERT INTO tbl_StatusHistories (sth_OldStatus, sth_NewStatus, sth_ChangedAt, sth_ReportId, sth_ChangedByUserId)
                VALUES (@OldStatus, @NewStatus, @ChangedAt, @ReportId, @ChangedByUserId)";

            await _connection.ExecuteAsync(historySql, new
            {
                OldStatus = report.rpt_Status,           // fetched from step 1 in database
                NewStatus = request.NewStatus,            // the new status
                ChangedAt = DateTime.Now,                 // when the change happened
                ReportId = id,                            // which report changed
                // FIXED: was request.ChangedByUserId (from the body — anyone could blame
                // another user for a status change). Now it is the token's Id.
                ChangedByUserId = currentUserId     // who made the change
            });

            // Step 4 When a report becomes Resolved, figure out which baladiye actually handled it and give that baladiye 10 points — but don't give the same 10 points twice
            if (request.NewStatus == "Resolved")//runs only when resolved
            {//Get every baladiye assigned to this report
                var assignmentsSql = "SELECT rpa_Id, rpa_MunicipalityId, rpa_IsHandler, rpa_Points FROM tbl_ReportAssignments WHERE rpa_ReportId = @ReportId";
                var assignments = (await _connection.QueryAsync<dynamic>(assignmentsSql, new { ReportId = id })).ToList();
                //Is there already a handler
                bool anyHandler = assignments.Any(a => Convert.ToBoolean((object)a.rpa_IsHandler));

                if (!anyHandler)//nobody is the handler
                {
                    int? handlerMunicipalityId = null;

                    if (currentRole == "Staff")
                    {
                        // a staff member resolving and make that baladiye the handler
                        handlerMunicipalityId = await _connection.QueryFirstOrDefaultAsync<int?>(
                    "SELECT usr_MunicipalityId FROM tbl_Users WHERE usr_Id = @Id", new { Id = currentUserId });
                    }
                    else if (assignments.Count == 1)
                    {
                        // an Admin resolved it, and only one baladiye was ever assigned 
                        handlerMunicipalityId = Convert.ToInt32((object)assignments[0].rpa_MunicipalityId);
                    }

                    if (handlerMunicipalityId != null)//after the code has figured out which baladiye should be the handler
                    {
                        await _connection.ExecuteAsync(@"
                            UPDATE tbl_ReportAssignments
                            SET rpa_IsHandler = 1, rpa_AcceptedAt = @AcceptedAt
                            WHERE rpa_ReportId = @ReportId AND rpa_MunicipalityId = @MunicipalityId",
                            new { AcceptedAt = DateTime.Now, ReportId = id, MunicipalityId = handlerMunicipalityId });

                        // Mark that baladiye as the handler
                        assignments = (await _connection.QueryAsync<dynamic>(
                            assignmentsSql, new { ReportId = id })).ToList();

                        anyHandler = true;
                    }
                }

                // Still nobody? That means an Admin resolved a report shared between
                // several baladiyat without saying which one did the work. There is
                // no way to know who to credit, so nothing is awarded and the admin
                // is told to make the call on the Shared Reports screen.
                if (!anyHandler)
                {
                    return Ok("Report marked as Resolved. No points were awarded, because no baladiye is marked as the handler — choose one on the Shared Reports screen first.");
                }

                foreach (var assignment in assignments)//assignments contains the baladiyat assigned to this report
                {
                    bool isHandler = Convert.ToBoolean((object)assignment.rpa_IsHandler);//isHandler=true
                    if (!isHandler)
                    {
                        continue;//skip the loop for this assigment baladeye and move to other baladeye to check
                    }
                    int alreadyAwarded = Convert.ToInt32((object)assignment.rpa_Points);//Check whether this report already gave points
                    if (alreadyAwarded != 0)
                    {
                        continue;
                    }

                    int points = 10; // the baladiye that resolved the issue

                    // update points on ReportAssignment
                    await _connection.ExecuteAsync(
                        "UPDATE tbl_ReportAssignments SET rpa_Points = @Points WHERE rpa_Id = @Id",
                        new { Points = points, Id = assignment.rpa_Id });

                    // update TotalPoints on Municipality
                    await _connection.ExecuteAsync(
                        "UPDATE tbl_Municipalities SET mun_TotalPoints = mun_TotalPoints + @Points WHERE mun_Id = @MunicipalityId",
                        new { Points = points, MunicipalityId = assignment.rpa_MunicipalityId });
                }
            }

            return Ok("Report status updated successfully");
        }



        [Authorize(Roles = "Admin")] // Admin only — this overwrites other baladiyat's flags
        [HttpPut("{id:int}/assign-handler")] // address: PUT api/Reports/1/assign-handler
        public async Task<IActionResult> AssignHandler(int id, [FromBody] MunicipalityRequest request)
        {
            // MunicipalityRequest carries exactly one field:
            // MunicipalityId — the baladiye the admin chose.

            // Step 1 — the report must exist
            var reportSql = "SELECT rpt_Id, rpt_Status FROM tbl_Reports WHERE rpt_Id = @Id";
            var report = await _connection.QueryFirstOrDefaultAsync<dynamic>(reportSql, new { Id = id });

            if (report == null)
                return NotFound("Report not found.");

            // Step 2 — a resolved report is frozen.
            if (Convert.ToString((object)report.rpt_Status) == "Resolved")
                return BadRequest("This report is already resolved — the handling baladiye can no longer be changed.");

            // Step 3 — the chosen baladiye must be one of THIS report's candidates.
            //
            // Without this check the transaction below is destructive: Step 5 deletes
            // every assignment row whose municipality is NOT the chosen one, and Step 6
            // then updates the chosen one. Pass an id that was never assigned to this
            // report and the delete removes ALL the rows while the update matches none —
            // the report ends up with zero assignments, disappears from every list query
            // (they all INNER JOIN tbl_ReportAssignments), and can never be resolved.
            if (await _connection.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(*) FROM tbl_ReportAssignments
                      WHERE rpa_ReportId = @ReportId AND rpa_MunicipalityId = @MunicipalityId",
                    new { ReportId = id, MunicipalityId = request.MunicipalityId }) == 0)
                return BadRequest("That baladiye is not one of the baladiyat assigned to this report.");

            if (_connection.State != System.Data.ConnectionState.Open)
                await _connection.OpenAsync();

            using var transaction = _connection.BeginTransaction();

            string? municipalityName;

            try
            {
                // Step 4 — take back any points the baladiyat being removed were
                // given for this report. rpa_Points is 0 on an unresolved report,
                // so normally this loop does nothing — but it keeps the leaderboard
                // honest if a report ever reaches here with points already awarded.
                var losingAssignments = await _connection.QueryAsync<dynamic>(@"
                    SELECT rpa_MunicipalityId, rpa_Points
                    FROM tbl_ReportAssignments
                    WHERE rpa_ReportId = @ReportId AND rpa_MunicipalityId <> @KeepId",
                    new { ReportId = id, KeepId = request.MunicipalityId }, transaction);

                foreach (var losing in losingAssignments)
                {
                    int pointsToUndo = Convert.ToInt32((object)losing.rpa_Points);

                    if (pointsToUndo != 0)
                    {
                        await _connection.ExecuteAsync(
                            "UPDATE tbl_Municipalities SET mun_TotalPoints = mun_TotalPoints - @Points WHERE mun_Id = @MunicipalityId",
                            new { Points = pointsToUndo, MunicipalityId = losing.rpa_MunicipalityId },
                            transaction);
                    }
                }

                // Step 5 — remove every OTHER baladiye from this report
                await _connection.ExecuteAsync(@"
                    DELETE FROM tbl_ReportAssignments
                    WHERE rpa_ReportId = @ReportId AND rpa_MunicipalityId <> @KeepId",
                    new { ReportId = id, KeepId = request.MunicipalityId }, transaction);

                // Step 6 — mark the survivor as the handler
                await _connection.ExecuteAsync(@"
                    UPDATE tbl_ReportAssignments
                    SET rpa_IsHandler = 1, rpa_AcceptedAt = @AcceptedAt
                    WHERE rpa_ReportId = @ReportId AND rpa_MunicipalityId = @MunicipalityId",
                    new { AcceptedAt = DateTime.Now, ReportId = id, MunicipalityId = request.MunicipalityId },
                    transaction);

                // Step 7 — read back the name so the response can say what happened
                municipalityName = await _connection.QueryFirstOrDefaultAsync<string>(
                    "SELECT mun_Name FROM tbl_Municipalities WHERE mun_Id = @Id",
                    new { Id = request.MunicipalityId }, transaction);

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }

            return Ok(new
            {
                message = $"Report given to {municipalityName}. The other baladiyat were removed from it.",
                reportId = id,
                municipalityId = request.MunicipalityId,
                municipalityName = municipalityName
            });
        }



        [Authorize(Roles = "Admin")] // Admin only — this overrides the spatial assignment
        [HttpPut("{id:int}/move")] // address: PUT api/Reports/1/move
        public async Task<IActionResult> MoveReport(int id, [FromBody] MunicipalityRequest request)
        {

            // Step 1 — the report must exist
            var report = await _connection.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT rpt_Id, rpt_Title, rpt_Status FROM tbl_Reports WHERE rpt_Id = @Id",
                new { Id = id });

            if (report == null)
                return NotFound("Report not found.");

            if (Convert.ToString((object)report.rpt_Status) == "Resolved")
                return BadRequest("This report is already resolved — it can no longer be moved.");

            // Step 3 — the destination baladiye must exist.
            
            var destination = await _connection.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT mun_Id, mun_Name FROM tbl_Municipalities WHERE mun_Id = @Id",
                new { Id = request.MunicipalityId });

            if (destination == null)
                return NotFound("That baladiye does not exist.");

            string destinationName = (object?)destination.mun_Name as string ?? "";//here is just saving baladeye name destination contains the row of this baladeye in sql 

            // Step 4 — is it already there and nowhere else? Then there is nothing to do.
            var existingAssignments = (await _connection.QueryAsync<dynamic>(
                "SELECT rpa_Id, rpa_MunicipalityId, rpa_Points, rpa_IsHandler FROM tbl_ReportAssignments WHERE rpa_ReportId = @ReportId",
                new { ReportId = id })).ToList();//all current assignment rows for this report

            if (existingAssignments.Count == 1 &&
                Convert.ToInt32((object)existingAssignments[0].rpa_MunicipalityId) == request.MunicipalityId)
            {
                return Ok(new { message = $"This report is already assigned to {destinationName} only.", moved = false });//if you are changing the baladeye to same baladeye
            }

           
            if (_connection.State != System.Data.ConnectionState.Open)//check connection
                await _connection.OpenAsync();

            using var transaction = _connection.BeginTransaction();//start transaction

            try//since inside transaction we use try block
            {
                foreach (var assignment in existingAssignments)//Go through every current assignment for this report, one by one.
                {
                    int pointsToUndo = Convert.ToInt32((object)assignment.rpa_Points);//take each rpapoints and saving it in thius varable pointsToUndo

                    if (pointsToUndo != 0)
                    {//Subtract those report points from the old municipality's total score.
                        await _connection.ExecuteAsync(
                            "UPDATE tbl_Municipalities SET mun_TotalPoints = mun_TotalPoints - @Points WHERE mun_Id = @MunicipalityId",
                            new { Points = pointsToUndo, MunicipalityId = assignment.rpa_MunicipalityId },
                            transaction);
                    }
                }

                await _connection.ExecuteAsync(//removes all assignment rows for report id
                    "DELETE FROM tbl_ReportAssignments WHERE rpa_ReportId = @ReportId",
                    new { ReportId = id }, transaction);

                await _connection.ExecuteAsync(//This line inserts the new assignment row for the report.
                    @"
                    INSERT INTO tbl_ReportAssignments (rpa_ReportId, rpa_MunicipalityId, rpa_AssignedAt, rpa_IsHandler, rpa_Points)
                    VALUES (@ReportId, @MunicipalityId, @AssignedAt, 1, 0)",
                    new { ReportId = id, MunicipalityId = request.MunicipalityId, AssignedAt = DateTime.Now },
                    transaction);

                transaction.Commit();
            }
            catch
            {
                // a failure halfway would leave the report with no baladiye at all,
                // so undo everything and let the error surface as a 500.
                transaction.Rollback();
                throw;
            }

            return Ok(new
            {
                message = $"Report moved to {destinationName}.",
                moved = true,
                reportId = id,
                municipalityId = request.MunicipalityId,
                municipalityName = destinationName
            });
        }



        [Authorize(Roles = "Admin")] // Admin only — Staff and Resident cannot delete anything
        [HttpDelete("{id:int}")] // address: DELETE api/Reports/1
        public async Task<IActionResult> DeleteReport(int id)
        {
            // Step 1 — make sure the report exists, so we can return a clean 404
            // instead of silently "succeeding" on an Id that was never there.
            var report = await _connection.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT rpt_Id, rpt_Title FROM tbl_Reports WHERE rpt_Id = @Id", new { Id = id });

            if (report == null)
                return NotFound("Report not found.");


            // Step 2 — Dapper opens and closes the connection by itself for a single
            // query, but a transaction needs the connection to stay open across several
            // queries, so we open it explicitly here.
            if (_connection.State != System.Data.ConnectionState.Open)//keep database connection open since we want to delet many thing from database at once assigment coments....
                await _connection.OpenAsync();

            using var transaction = _connection.BeginTransaction();//if closed this reopen it

            try
            {
                // Step 3 — read the assignments BEFORE deleting them, so we still know
                // how many points each baladiye was given for this report.
                var assignments = await _connection.QueryAsync<dynamic>(
                    "SELECT rpa_MunicipalityId, rpa_Points FROM tbl_ReportAssignments WHERE rpa_ReportId = @ReportId",
                    new { ReportId = id }, transaction);//Before deleting the report assignments, first get all the municipalities connected to this report and how many points each one got from it.

                // Step 4 — undo the points on each baladiye.
                foreach (var assignment in assignments)//loop on every report assigment
                {
                    int pointsToUndo = Convert.ToInt32((object)assignment.rpa_Points);//gets that assignment's points and converts them to an int

                    if (pointsToUndo != 0) // nothing to undo for an unresolved report
                    {
                        await _connection.ExecuteAsync(//Find that municipality and subtract the points that came from this report
                            "UPDATE tbl_Municipalities SET mun_TotalPoints = mun_TotalPoints - @Points WHERE mun_Id = @MunicipalityId",
                            new { Points = pointsToUndo, MunicipalityId = assignment.rpa_MunicipalityId },
                            transaction);
                    }
                }

                // Step 5 — delete the children, then the report.
                // The order matters: nothing may still reference the report when the
                // final DELETE runs, or the Restrict foreign keys will block it.
                await _connection.ExecuteAsync("DELETE FROM tbl_Comments WHERE cmt_ReportId = @Id", new { Id = id }, transaction);
                await _connection.ExecuteAsync("DELETE FROM tbl_StatusHistories WHERE sth_ReportId = @Id", new { Id = id }, transaction);
                await _connection.ExecuteAsync("DELETE FROM tbl_PriorityVotes WHERE pvt_ReportId = @Id", new { Id = id }, transaction);
                await _connection.ExecuteAsync("DELETE FROM tbl_ReportAgreements WHERE rga_ReportId = @Id", new { Id = id }, transaction);
                await _connection.ExecuteAsync("DELETE FROM tbl_ReportAssignments WHERE rpa_ReportId = @Id", new { Id = id }, transaction);
                // finally the report itself
                await _connection.ExecuteAsync("DELETE FROM tbl_Reports WHERE rpt_Id = @Id", new { Id = id }, transaction);


                transaction.Commit();//keep all changes 
            }
            catch
            {
                // something failed halfway — undo every delete above so the database
                transaction.Rollback();//cancel all changes if one of the delets didnt work
                throw;
            }

            return Ok(new { reportId = id });
        }
    }
}
