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

        [Authorize(Roles = "Admin")]
        [HttpGet("shared")]
        public async Task<IActionResult> GetSharedReports()
        {
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

            if (sharedReports.Count == 0)
                return Ok(new List<object>());

            
            var reportIds = new List<int>();
            foreach (var row in sharedReports)
                reportIds.Add(Convert.ToInt32((object)row.rpt_Id));

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

            var candidatesByReport = new Dictionary<int, List<HandlerCandidate>>();

            foreach (var candidate in candidateRows)
            {
                int candidateReportId = Convert.ToInt32((object)candidate.rpa_ReportId);
                if (!candidatesByReport.ContainsKey(candidateReportId))
                    candidatesByReport[candidateReportId] = new List<HandlerCandidate>();

                object? acceptedAtRaw = (object?)candidate.rpa_AcceptedAt;

                candidatesByReport[candidateReportId].Add(new HandlerCandidate
                {
                    mun_Id = Convert.ToInt32((object)candidate.mun_Id),
                    mun_Name = (object?)candidate.mun_Name as string ?? "",
                    IsHandler = Convert.ToBoolean((object)candidate.rpa_IsHandler),
                    AcceptedAt = acceptedAtRaw == null || acceptedAtRaw is DBNull
                        ? (DateTime?)null
                        : Convert.ToDateTime(acceptedAtRaw)
                });
            }

           
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
                    rpt_Priority = (object?)row.rpt_Priority as string,
                    rpt_AgreementCount = Convert.ToInt32((object)row.rpt_AgreementCount),
                    CategoryName = (object?)row.CategoryName as string,
                    Candidates = candidates,
                    NeedsDecision = !candidates.Any(c => c.IsHandler)
                });
            }

            return Ok(result);
        }


        [Authorize(Roles = "Staff,Admin")]
        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateReportStatus(int id, [FromBody] UpdateStatusRequest request)
        {
           
            var currentUserId = int.Parse(User.FindFirst("Id")!.Value);


            var currentRole = User.FindFirst(ClaimTypes.Role)?.Value;

            var allowedStatuses = new[] { "Submitted", "In Progress", "Resolved", "Rejected" };
            if (string.IsNullOrWhiteSpace(request.NewStatus) || !allowedStatuses.Contains(request.NewStatus))
                return BadRequest($"Status must be one of: {string.Join(", ", allowedStatuses)}.");

            if (request.NewStatus == "Resolved" && string.IsNullOrWhiteSpace(request.ResolvedPhotoUrl))
                return BadRequest("A resolved photo is required when setting the status to Resolved.");

            var checkSql = "SELECT rpt_Id, rpt_Status FROM tbl_Reports WHERE rpt_Id = @Id";
            var report = await _connection.QueryFirstOrDefaultAsync<dynamic>(checkSql, new { Id = id });

            if (report == null)
                return NotFound("Report not found.");

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

                if (await _connection.QueryFirstAsync<int>(
                    "SELECT COUNT(*) FROM tbl_ReportAssignments WHERE rpa_ReportId = @ReportId",
                    new { ReportId = id }) > 1)
                    return StatusCode(403, "This report is shared between several baladiyat. An admin must decide who handles it before it can be updated.");
            }

            if (Convert.ToString((object)report.rpt_Status) == request.NewStatus)
                return Ok("Report already has this status — nothing changed.");

            var updateSql = @"
                UPDATE tbl_Reports
                SET rpt_Status = @NewStatus,
                    rpt_ResolvedPhotoUrl = @ResolvedPhotoUrl
                WHERE rpt_Id = @Id";

            await _connection.ExecuteAsync(updateSql, new
            {
                NewStatus = request.NewStatus,
                ResolvedPhotoUrl = request.ResolvedPhotoUrl,
                Id = id
            });

            var historySql = @"
                INSERT INTO tbl_StatusHistories (sth_OldStatus, sth_NewStatus, sth_ChangedAt, sth_ReportId, sth_ChangedByUserId)
                VALUES (@OldStatus, @NewStatus, @ChangedAt, @ReportId, @ChangedByUserId)";

            await _connection.ExecuteAsync(historySql, new
            {
                OldStatus = report.rpt_Status,
                NewStatus = request.NewStatus,
                ChangedAt = DateTime.Now,
                ReportId = id,
                ChangedByUserId = currentUserId
            });

            if (request.NewStatus == "Resolved")
            {
                var assignmentsSql = "SELECT rpa_Id, rpa_MunicipalityId, rpa_IsHandler, rpa_Points FROM tbl_ReportAssignments WHERE rpa_ReportId = @ReportId";
                var assignments = (await _connection.QueryAsync<dynamic>(assignmentsSql, new { ReportId = id })).ToList();
                bool anyHandler = assignments.Any(a => Convert.ToBoolean((object)a.rpa_IsHandler));

                if (!anyHandler)
                {
                    int? handlerMunicipalityId = null;

                    if (currentRole == "Staff")
                    {
                        handlerMunicipalityId = await _connection.QueryFirstOrDefaultAsync<int?>(
                    "SELECT usr_MunicipalityId FROM tbl_Users WHERE usr_Id = @Id", new { Id = currentUserId });
                    }
                    else if (assignments.Count == 1)
                    {
                        handlerMunicipalityId = Convert.ToInt32((object)assignments[0].rpa_MunicipalityId);
                    }

                    if (handlerMunicipalityId != null)
                    {
                        await _connection.ExecuteAsync(@"
                            UPDATE tbl_ReportAssignments
                            SET rpa_IsHandler = 1, rpa_AcceptedAt = @AcceptedAt
                            WHERE rpa_ReportId = @ReportId AND rpa_MunicipalityId = @MunicipalityId",
                            new { AcceptedAt = DateTime.Now, ReportId = id, MunicipalityId = handlerMunicipalityId });

                        assignments = (await _connection.QueryAsync<dynamic>(
                            assignmentsSql, new { ReportId = id })).ToList();

                        anyHandler = true;
                    }
                }

                if (!anyHandler)
                {
                    return Ok("Report marked as Resolved. No points were awarded, because no baladiye is marked as the handler — choose one on the Shared Reports screen first.");
                }

                foreach (var assignment in assignments)
                {
                    bool isHandler = Convert.ToBoolean((object)assignment.rpa_IsHandler);
                    if (!isHandler)
                    {
                        continue;
                    }
                    int alreadyAwarded = Convert.ToInt32((object)assignment.rpa_Points);
                    if (alreadyAwarded != 0)
                    {
                        continue;
                    }

                    int points = 10;

                    await _connection.ExecuteAsync(
                        "UPDATE tbl_ReportAssignments SET rpa_Points = @Points WHERE rpa_Id = @Id",
                        new { Points = points, Id = assignment.rpa_Id });

                    await _connection.ExecuteAsync(
                        "UPDATE tbl_Municipalities SET mun_TotalPoints = mun_TotalPoints + @Points WHERE mun_Id = @MunicipalityId",
                        new { Points = points, MunicipalityId = assignment.rpa_MunicipalityId });
                }
            }

            return Ok("Report status updated successfully");
        }



        [Authorize(Roles = "Admin")]
        [HttpPut("{id:int}/assign-handler")]
        public async Task<IActionResult> AssignHandler(int id, [FromBody] MunicipalityRequest request)
        {

            var reportSql = "SELECT rpt_Id, rpt_Status FROM tbl_Reports WHERE rpt_Id = @Id";
            var report = await _connection.QueryFirstOrDefaultAsync<dynamic>(reportSql, new { Id = id });

            if (report == null)
                return NotFound("Report not found.");

            if (Convert.ToString((object)report.rpt_Status) == "Resolved")
                return BadRequest("This report is already resolved — the handling baladiye can no longer be changed.");

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

                await _connection.ExecuteAsync(@"
                    DELETE FROM tbl_ReportAssignments
                    WHERE rpa_ReportId = @ReportId AND rpa_MunicipalityId <> @KeepId",
                    new { ReportId = id, KeepId = request.MunicipalityId }, transaction);

                await _connection.ExecuteAsync(@"
                    UPDATE tbl_ReportAssignments
                    SET rpa_IsHandler = 1, rpa_AcceptedAt = @AcceptedAt
                    WHERE rpa_ReportId = @ReportId AND rpa_MunicipalityId = @MunicipalityId",
                    new { AcceptedAt = DateTime.Now, ReportId = id, MunicipalityId = request.MunicipalityId },
                    transaction);

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



        [Authorize(Roles = "Admin")]
        [HttpPut("{id:int}/move")]
        public async Task<IActionResult> MoveReport(int id, [FromBody] MunicipalityRequest request)
        {

            var report = await _connection.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT rpt_Id, rpt_Title, rpt_Status FROM tbl_Reports WHERE rpt_Id = @Id",
                new { Id = id });

            if (report == null)
                return NotFound("Report not found.");

            if (Convert.ToString((object)report.rpt_Status) == "Resolved")
                return BadRequest("This report is already resolved — it can no longer be moved.");

            
            var destination = await _connection.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT mun_Id, mun_Name FROM tbl_Municipalities WHERE mun_Id = @Id",
                new { Id = request.MunicipalityId });

            if (destination == null)
                return NotFound("That baladiye does not exist.");

            string destinationName = (object?)destination.mun_Name as string ?? "";

            var existingAssignments = (await _connection.QueryAsync<dynamic>(
                "SELECT rpa_Id, rpa_MunicipalityId, rpa_Points, rpa_IsHandler FROM tbl_ReportAssignments WHERE rpa_ReportId = @ReportId",
                new { ReportId = id })).ToList();

            if (existingAssignments.Count == 1 &&
                Convert.ToInt32((object)existingAssignments[0].rpa_MunicipalityId) == request.MunicipalityId)
            {
                return Ok(new { message = $"This report is already assigned to {destinationName} only.", moved = false });
            }

           
            if (_connection.State != System.Data.ConnectionState.Open)
                await _connection.OpenAsync();

            using var transaction = _connection.BeginTransaction();

            try
            {
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

                await _connection.ExecuteAsync(
                    "DELETE FROM tbl_ReportAssignments WHERE rpa_ReportId = @ReportId",
                    new { ReportId = id }, transaction);

                await _connection.ExecuteAsync(
                    @"
                    INSERT INTO tbl_ReportAssignments (rpa_ReportId, rpa_MunicipalityId, rpa_AssignedAt, rpa_IsHandler, rpa_Points)
                    VALUES (@ReportId, @MunicipalityId, @AssignedAt, 1, 0)",
                    new { ReportId = id, MunicipalityId = request.MunicipalityId, AssignedAt = DateTime.Now },
                    transaction);

                transaction.Commit();
            }
            catch
            {
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



        [Authorize(Roles = "Admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteReport(int id)
        {
            var report = await _connection.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT rpt_Id, rpt_Title FROM tbl_Reports WHERE rpt_Id = @Id", new { Id = id });

            if (report == null)
                return NotFound("Report not found.");


            if (_connection.State != System.Data.ConnectionState.Open)
                await _connection.OpenAsync();

            using var transaction = _connection.BeginTransaction();

            try
            {
                var assignments = await _connection.QueryAsync<dynamic>(
                    "SELECT rpa_MunicipalityId, rpa_Points FROM tbl_ReportAssignments WHERE rpa_ReportId = @ReportId",
                    new { ReportId = id }, transaction);

                foreach (var assignment in assignments)
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

                await _connection.ExecuteAsync("DELETE FROM tbl_Comments WHERE cmt_ReportId = @Id", new { Id = id }, transaction);
                await _connection.ExecuteAsync("DELETE FROM tbl_StatusHistories WHERE sth_ReportId = @Id", new { Id = id }, transaction);
                await _connection.ExecuteAsync("DELETE FROM tbl_PriorityVotes WHERE pvt_ReportId = @Id", new { Id = id }, transaction);
                await _connection.ExecuteAsync("DELETE FROM tbl_ReportAgreements WHERE rga_ReportId = @Id", new { Id = id }, transaction);
                await _connection.ExecuteAsync("DELETE FROM tbl_ReportAssignments WHERE rpa_ReportId = @Id", new { Id = id }, transaction);
                await _connection.ExecuteAsync("DELETE FROM tbl_Reports WHERE rpt_Id = @Id", new { Id = id }, transaction);


                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }

            return Ok(new { reportId = id });
        }
    }
}
