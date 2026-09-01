using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CivicFix.Api.Models;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace CivicFix.Api.Controllers
{
    // ══════════════════════════════════════════════════════════════════════════
    // ACTIONS ON AN EXISTING REPORT — Staff and Admin only.
    //
    // Everything here changes a report rather than reading it: its status, which
    // baladiye owns it, or whether it exists at all. Grouped together because they
    // share one audience — every endpoint below is [Authorize(Roles = "Staff,Admin")]
    // or Admin-only, so the whole file has a single, obvious reader.
    // ══════════════════════════════════════════════════════════════════════════
    [ApiController]
    // NOT [Route("api/[controller]")] — that token expands to the class name, so
    // this file would answer on api/ReportsAdmin and every URL below would change.
    // Written out in full, these endpoints keep the exact addresses they had when
    // they all lived in one ReportsController.
    [Route("api/Reports")]
    public class ReportsAdminController : ControllerBase
    {
        private readonly SqlConnection _connection;

        public ReportsAdminController(SqlConnection connection)
        {
            _connection = connection;
        }


        // ══════════════════════════════════════════════════════════════════════
        // NEW ENDPOINT — the Admin's "shared reports" screen.
        //
        // WHY IT EXISTS: when a Resident reports a problem, CreateReport runs a
        // spatial query and inserts one tbl_ReportAssignments row for EVERY baladiye
        // whose polygon contains the point (or is within 100m of it). A pothole on a
        // road that sits on the border between two baladiyat therefore gets assigned
        // to BOTH, and neither of them owns it — rpa_IsHandler is 0 on both rows.
        //
        // This endpoint lists exactly those reports so the Admin can look at them and
        // push each one to a single baladiye (see AssignHandler below).
        //
        // It returns the report PLUS its list of candidate baladiyat, so React can
        // draw one button per candidate without making a second call per report.
        // ══════════════════════════════════════════════════════════════════════
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

            // Step 2 — collect the Ids we just found, so we can fetch all their
            // candidate baladiyat in ONE query instead of one query per report.
            var reportIds = new List<int>();
            foreach (var row in sharedReports)
                reportIds.Add(Convert.ToInt32((object)row.rpt_Id));

            // Dapper expands the @Ids list into IN (1, 2, 3, ...) automatically
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
                WHERE tbl_ReportAssignments.rpa_ReportId IN @Ids
                ORDER BY tbl_Municipalities.mun_Name";

            var candidateRows = await _connection.QueryAsync<dynamic>(candidatesSql, new { Ids = reportIds });

            // Step 3 — group the candidates by report Id, so each report can carry its own list.
            // A Dictionary keyed by report Id is the fast way to do this in memory.
            // HandlerCandidate (defined at the bottom of this class) is used instead of an
            // anonymous type so that the .Any(c => c.IsHandler) check below is real,
            // compile-checked C# rather than reflection.
            var candidatesByReport = new Dictionary<int, List<HandlerCandidate>>();

            foreach (var candidate in candidateRows)
            {
                int candidateReportId = Convert.ToInt32((object)candidate.rpa_ReportId);

                // first time we see this report, create its empty list
                if (!candidatesByReport.ContainsKey(candidateReportId))
                    candidatesByReport[candidateReportId] = new List<HandlerCandidate>();

                // AcceptedAt is NULL in the database until a baladiye is chosen, so it
                // is read through AsDateTime which turns null/DBNull into a real null.
                object? acceptedAtRaw = (object?)candidate.rpa_AcceptedAt;

                candidatesByReport[candidateReportId].Add(new HandlerCandidate
                {
                    mun_Id = Convert.ToInt32((object)candidate.mun_Id),
                    mun_Name = (object?)candidate.mun_Name as string ?? "",
                    IsHandler = Convert.ToBoolean((object)candidate.rpa_IsHandler), // true = this baladiye already owns it
                    AcceptedAt = acceptedAtRaw == null || acceptedAtRaw is DBNull
                        ? (DateTime?)null
                        : Convert.ToDateTime(acceptedAtRaw)
                });
            }

            // Step 4 — stitch each report together with its candidate list.
            // CHANGED: builds real SharedReportDto objects (see Models/SharedReportDto.cs)
            // instead of `new { ... }`. An anonymous type built from Dapper `dynamic`
            // values ends up with dynamic-typed properties, which are only checked at
            // runtime and serialize unpredictably. Real classes keep this honest.
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
            // ══════════════════════════════════════════════════════════════════
            // CHANGED (ADDED BLOCK) — new rule you asked for:
            //   Admin    → can update the status of ANY report
            //   Staff    → can update ONLY reports assigned to their own baladiye
            //   Resident → already blocked by the [Authorize] line above
            // ══════════════════════════════════════════════════════════════════

            // who is asking? Straight from the signed JWT, never the request body.
            // TryParse, not int.Parse: a missing claim gives a 400, not a 500 crash.
            var idClaim = User.FindFirst("Id")?.Value;
            if (!int.TryParse(idClaim, out int currentUserId))
                return BadRequest("Could not read user Id from token. Claim 'Id' not found.");

            var currentRole = User.FindFirst(ClaimTypes.Role)?.Value;

            // ADDED: reject junk status values before touching the database.
            // Edit this array if you add more statuses later.
            var allowedStatuses = new[] { "Submitted", "In Progress", "Resolved", "Rejected" };
            if (string.IsNullOrWhiteSpace(request.NewStatus) || !allowedStatuses.Contains(request.NewStatus))
                return BadRequest($"Status must be one of: {string.Join(", ", allowedStatuses)}.");

            // ADDED: a "Resolved" report should carry the proof photo.
            // Without this, the old code happily wrote NULL over an existing photo.
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

                // ADDED — Staff cannot resolve a report the Admin has not allocated yet.
                // This is the one that matters most: without it, a staff member on a
                // shared report could mark it Resolved and take the +10 points before
                // the Admin ever decided whose job it was. 2+ assignment rows = undecided.
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

            // Step 4 — if resolved, update points in ReportAssignments and Municipality TotalPoints
            if (request.NewStatus == "Resolved")
            {
                // get all assignments for this report
                // CHANGED: also selects rpa_Points, so we can tell whether this report
                // has already paid out (see the double-award guard further down).
                var assignmentsSql = "SELECT rpa_Id, rpa_MunicipalityId, rpa_IsHandler, rpa_Points FROM tbl_ReportAssignments WHERE rpa_ReportId = @ReportId";
                var assignments = (await _connection.QueryAsync<dynamic>(assignmentsSql, new { ReportId = id })).ToList();

                // ══════════════════════════════════════════════════════════════
                // CHANGED — THE RULE IS NOW SIMPLY: THE BALADIYE THAT FIXES IT GAINS.
                //
                // It used to be "+10 for the handler, -5 for everyone else". The -5
                // was meant to punish a baladiye that ignored a shared report, but it
                // backfired: rpa_IsHandler starts at 0 for every RESIDENT-submitted
                // report (see CreateReport) and only becomes 1 if somebody calls
                // /accept or /assign-handler — which nothing forces. So the normal
                // flow was:
                //
                //   resident reports a pothole in Beirut
                //     → one assignment row, Beirut, rpa_IsHandler = 0
                //   Beirut fixes it and marks the report Resolved
                //     → the loop saw IsHandler = 0 and took 5 points OFF Beirut
                //
                // The baladiye that did the work lost points for doing it — that is
                // why 150 became 145.
                //
                // THE PENALTY IS GONE. Now the baladiye that resolves a report gains
                // +10 and nobody else is touched. Two things still have to be right:
                //
                //   1. we must know WHICH baladiye resolved it, so the points land on
                //      the correct one (worked out just below)
                //   2. a report must not pay out twice if it is resolved, reopened and
                //      resolved again (the rpa_Points guard in the loop)
                // ══════════════════════════════════════════════════════════════

                bool anyHandler = assignments.Any(a => Convert.ToBoolean((object)a.rpa_IsHandler));

                if (!anyHandler)
                {
                    int? handlerMunicipalityId = null;

                    if (currentRole == "Staff")
                    {
                        // a staff member resolving a report IS the baladiye doing the work
                        handlerMunicipalityId = await _connection.QueryFirstOrDefaultAsync<int?>(
                    "SELECT usr_MunicipalityId FROM tbl_Users WHERE usr_Id = @Id", new { Id = currentUserId });
                    }
                    else if (assignments.Count == 1)
                    {
                        // an Admin resolved it, and only one baladiye was ever assigned —
                        // there is nobody else it could have been
                        handlerMunicipalityId = Convert.ToInt32((object)assignments[0].rpa_MunicipalityId);
                    }

                    if (handlerMunicipalityId != null)
                    {
                        await _connection.ExecuteAsync(@"
                            UPDATE tbl_ReportAssignments
                            SET rpa_IsHandler = 1, rpa_AcceptedAt = @AcceptedAt
                            WHERE rpa_ReportId = @ReportId AND rpa_MunicipalityId = @MunicipalityId",
                            new { AcceptedAt = DateTime.Now, ReportId = id, MunicipalityId = handlerMunicipalityId });

                        // re-read so the loop below sees the handler we just set
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

                foreach (var assignment in assignments)
                {
                    // FIXED: rpa_IsHandler arrives as a dynamic BIT. Using it straight
                    // inside a `? :` is resolved at runtime and throws
                    // RuntimeBinderException if the column type ever changes.
                    // The (object) cast forces the compiler to pick Convert.ToBoolean(object)
                    // at BUILD time, and Convert handles bit / int / bool all the same.
                    bool isHandler = Convert.ToBoolean((object)assignment.rpa_IsHandler);

                    // CHANGED: the other baladiyat on a shared report are simply left
                    // alone now. They used to lose 5 points here — that penalty is gone.
                    if (!isHandler)
                    {
                        continue;
                    }

                    // CHANGED: don't pay the same report twice. rpa_Points is 0 until a
                    // report pays out, so a non-zero value means this baladiye has already
                    // been credited — which happens if a report is resolved, reopened and
                    // resolved again. Without this, each round trip would hand out another
                    // +10 and the leaderboard could be inflated at will.
                    int alreadyAwarded = Convert.ToInt32((object)assignment.rpa_Points);
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


        // ══════════════════════════════════════════════════════════════════════
        // NEW ENDPOINT — the Admin picks WHICH baladiye a shared report goes to.
        //
        // This is the button behind each baladiye name on the "shared" tab.
        // It sets rpa_IsHandler = 1 on the chosen baladiye and, importantly,
        // clears it on all the others, so a report always has exactly ONE owner.
        //
        // Why a separate endpoint instead of reusing PUT {id}/accept:
        // /accept is what a STAFF member calls to claim a report for their OWN
        // baladiye, and it must never clear another baladiye's handler flag —
        // otherwise any staff member could steal ownership (and the +10 points)
        // from a baladiye that already accepted. This endpoint is Admin-only and
        // is allowed to overwrite, so the two jobs stay separate.
        // ══════════════════════════════════════════════════════════════════════
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
            // UpdateReportStatus already handed out points based on who the handler was
            // (+10 to the baladiye that resolved it). Changing the handler now would
            // leave those points pointing at the wrong baladiye, so we refuse.
            if (Convert.ToString((object)report.rpt_Status) == "Resolved")
                return BadRequest("This report is already resolved — the handling baladiye can no longer be changed.");

            // Step 3 — the chosen baladiye must actually be one of the report's candidates.
            // Without this check an admin could push a report to a baladiye that is
            // nowhere near the location.
            if (await _connection.QueryFirstAsync<int>(
                    @"SELECT COUNT(*) FROM tbl_ReportAssignments
                      WHERE rpa_ReportId = @ReportId AND rpa_MunicipalityId = @MunicipalityId",
                    new { ReportId = id, MunicipalityId = request.MunicipalityId }) == 0)
                return BadRequest("That baladiye is not one of the baladiyat assigned to this report.");

            // ══════════════════════════════════════════════════════════════════
            // CHANGED — choosing a baladiye now GIVES THE REPORT TO IT OUTRIGHT.
            //
            // Before, this only flipped rpa_IsHandler: the chosen baladiye got 1
            // and the others got 0, but their assignment rows stayed. So in the
            // database a report "given to Furn ech Chebak" was still linked to
            // Beirut and Chiayah too, and it kept appearing on the Shared Reports
            // tab as though nothing had been decided.
            //
            // Now the other baladiyat are removed entirely. After this call the
            // report has exactly ONE assignment row — the chosen baladiye, marked
            // as handler — so the database says plainly who owns it, and the report
            // drops off the Shared Reports tab because it is no longer shared.
            //
            // Changing your mind later is still possible: use the
            // "↪️ Move to another baladiye" panel on the report's detail page,
            // which can hand it to any baladiye in the country.
            // ══════════════════════════════════════════════════════════════════

            // several tables change below, so it runs as one transaction — either
            // the whole handover happens or none of it does. A failure halfway
            // could otherwise leave the report with no baladiye at all.
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


        // ══════════════════════════════════════════════════════════════════════
        // NEW ENDPOINT — the Admin MOVES a report to a different baladiye.
        //
        // HOW THIS DIFFERS FROM /assign-handler:
        //   /assign-handler picks a winner from the baladiyat the report is ALREADY
        //   assigned to. It cannot introduce a new one.
        //   /move replaces the assignments entirely with one baladiye of the admin's
        //   choosing — ANY baladiye in the country, whether the spatial query found
        //   it or not.
        //
        // WHY IT IS NEEDED: the automatic assignment is only as good as the boundary
        // polygons. If a report lands on the wrong baladiye — bad boundary data, a
        // GPS reading that drifted, or a problem that is genuinely another baladiye's
        // responsibility despite where it sits — there was previously no way to
        // correct it short of deleting the report and asking the resident to file
        // it again, which loses its comments, votes and history.
        //
        // THE POINTS HAVE TO BE UNWOUND FIRST. If the report was already resolved,
        // the old baladiye was credited +10 and that is on the public leaderboard.
        // Moving the report without reversing that would leave a baladiye holding
        // points for work now attributed to somebody else. So every existing
        // assignment's rpa_Points is subtracted back off its baladiye before the
        // rows are replaced.
        // ══════════════════════════════════════════════════════════════════════
        [Authorize(Roles = "Admin")] // Admin only — this overrides the spatial assignment
        [HttpPut("{id:int}/move")] // address: PUT api/Reports/1/move
        public async Task<IActionResult> MoveReport(int id, [FromBody] MunicipalityRequest request)
        {
            // MunicipalityRequest carries the one field needed:
            // MunicipalityId, the baladiye the admin is moving the report to.

            // Step 1 — the report must exist
            var report = await _connection.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT rpt_Id, rpt_Title, rpt_Status FROM tbl_Reports WHERE rpt_Id = @Id",
                new { Id = id });

            if (report == null)
                return NotFound("Report not found.");

            // Step 2 — the destination baladiye must exist.
            // Unlike /assign-handler this is NOT limited to the report's current
            // candidates, so the only check is that the baladiye is real.
            var destination = await _connection.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT mun_Id, mun_Name FROM tbl_Municipalities WHERE mun_Id = @Id",
                new { Id = request.MunicipalityId });

            if (destination == null)
                return NotFound("That baladiye does not exist.");

            string destinationName = (object?)destination.mun_Name as string ?? "";

            // Step 3 — is it already there and nowhere else? Then there is nothing to do.
            var existingAssignments = (await _connection.QueryAsync<dynamic>(
                "SELECT rpa_Id, rpa_MunicipalityId, rpa_Points, rpa_IsHandler FROM tbl_ReportAssignments WHERE rpa_ReportId = @ReportId",
                new { ReportId = id })).ToList();

            if (existingAssignments.Count == 1 &&
                Convert.ToInt32((object)existingAssignments[0].rpa_MunicipalityId) == request.MunicipalityId)
            {
                return Ok(new { message = $"This report is already assigned to {destinationName} only.", moved = false });
            }

            // everything below changes several tables, so it runs as one transaction:
            // either the whole move happens, or none of it does.
            if (_connection.State != System.Data.ConnectionState.Open)
                await _connection.OpenAsync();

            using var transaction = _connection.BeginTransaction();

            try
            {
                // Step 4 — take back any points the old baladiyat were given for this
                // report, so the leaderboard does not keep crediting work that has
                // been reassigned. rpa_Points is 0 on an unresolved report, so for
                // those this loop does nothing.
                foreach (var assignment in existingAssignments)
                {
                    int pointsToUndo = Convert.ToInt32((object)assignment.rpa_Points);

                    if (pointsToUndo != 0)
                    {
                        await _connection.ExecuteAsync(
                            "UPDATE tbl_Municipalities SET mun_TotalPoints = mun_TotalPoints - @Points WHERE mun_Id = @MunicipalityId",
                            new { Points = pointsToUndo, MunicipalityId = assignment.rpa_MunicipalityId },
                            transaction);
                    }
                }

                // Step 5 — remove the old assignments and put the chosen baladiye in
                // their place. It is inserted as the handler, because an admin moving
                // a report here is saying "this is the baladiye responsible for it".
                await _connection.ExecuteAsync(
                    "DELETE FROM tbl_ReportAssignments WHERE rpa_ReportId = @ReportId",
                    new { ReportId = id }, transaction);

                await _connection.ExecuteAsync(@"
                    INSERT INTO tbl_ReportAssignments (rpa_ReportId, rpa_MunicipalityId, rpa_AssignedAt, rpa_IsHandler, rpa_Points)
                    VALUES (@ReportId, @MunicipalityId, @AssignedAt, 1, 0)",
                    new { ReportId = id, MunicipalityId = request.MunicipalityId, AssignedAt = DateTime.Now },
                    transaction);

                // Step 6 — if the report was already resolved, credit the new baladiye
                // straight away. Its points were just zeroed by the insert above, and
                // the work is finished, so the +10 belongs to whoever owns it now.
                if (Convert.ToString((object)report.rpt_Status) == "Resolved")
                {
                    await _connection.ExecuteAsync(
                        "UPDATE tbl_ReportAssignments SET rpa_Points = 10 WHERE rpa_ReportId = @ReportId AND rpa_MunicipalityId = @MunicipalityId",
                        new { ReportId = id, MunicipalityId = request.MunicipalityId }, transaction);

                    await _connection.ExecuteAsync(
                        "UPDATE tbl_Municipalities SET mun_TotalPoints = mun_TotalPoints + 10 WHERE mun_Id = @MunicipalityId",
                        new { MunicipalityId = request.MunicipalityId }, transaction);
                }

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


        // ══════════════════════════════════════════════════════════════════════
        // NEW ENDPOINT — the Admin deletes a report for good.
        //
        // This is a HARD delete: the row really leaves tbl_Reports, it does not just
        // get hidden. Because of that there are two things it has to do carefully.
        //
        // 1) CHILD ROWS FIRST.
        //    In AppDbContext every relationship is set to DeleteBehavior.Restrict,
        //    which means SQL Server REFUSES to delete a report while anything still
        //    points at it. So the children are deleted first, in the right order:
        //    comments, status history, priority votes, agreements, assignments —
        //    and only then the report itself.
        //
        // 2) GIVE THE POINTS BACK.
        //    If the report was already resolved, each baladiye's mun_TotalPoints was
        //    changed (+10 for the baladiye that resolved it). Deleting the report
        //    without undoing that would leave the public leaderboard permanently wrong,
        //    showing points for a report that no longer exists. So each assignment's
        //    rpa_Points is subtracted back out before the rows are removed.
        //
        // The whole thing runs inside a TRANSACTION: if any step fails, everything is
        // rolled back and nothing is half-deleted.
        // ══════════════════════════════════════════════════════════════════════
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

            string reportTitle = (object?)report.rpt_Title as string ?? "";

            // Step 2 — Dapper opens and closes the connection by itself for a single
            // query, but a transaction needs the connection to stay open across several
            // queries, so we open it explicitly here.
            if (_connection.State != System.Data.ConnectionState.Open)
                await _connection.OpenAsync();

            using var transaction = _connection.BeginTransaction();

            try
            {
                // Step 3 — read the assignments BEFORE deleting them, so we still know
                // how many points each baladiye was given for this report.
                var assignments = await _connection.QueryAsync<dynamic>(
                    "SELECT rpa_MunicipalityId, rpa_Points FROM tbl_ReportAssignments WHERE rpa_ReportId = @ReportId",
                    new { ReportId = id }, transaction);

                // Step 4 — undo the points on each baladiye.
                // Subtracting rpa_Points works for both directions: the handler got +10
                // so it loses 10; the others were awarded 0, so subtracting 0 is a no-op.
                foreach (var assignment in assignments)
                {
                    int pointsToUndo = Convert.ToInt32((object)assignment.rpa_Points);

                    if (pointsToUndo != 0) // nothing to undo for an unresolved report
                    {
                        await _connection.ExecuteAsync(
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

                // everything worked — make it permanent
                transaction.Commit();
            }
            catch
            {
                // something failed halfway — undo every delete above so the database
                // is left exactly as it was, then let the error bubble up as a 500.
                transaction.Rollback();
                throw;
            }

            return Ok(new { message = $"Report \"{reportTitle}\" deleted.", reportId = id });
        }
    }
}
