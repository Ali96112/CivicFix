using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CivicFix.Api.Models;
using Dapper;
using NetTopologySuite.Geometries;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace CivicFix.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // base address: api/Reports
    public class ReportsController : ControllerBase
    {
        private readonly SqlConnection _connection;
        private readonly IConfiguration _configuration;

        public ReportsController(SqlConnection connection, IConfiguration configuration)
        {
            _connection = connection;
            _configuration = configuration;
        }

        [Authorize(Roles = "Resident,Staff,Admin")]
        [HttpPost] // address: api/Reports
        public async Task<IActionResult> CreateReport([FromBody] CreateReportRequest request)
        {
            // check if reporter is Staff — if yes, verify location is within their baladiye
            var reporterRole = User.FindFirst(ClaimTypes.Role)?.Value;//User is bult in objeect here  that present logged-in user
            var reporterIdClaim = User.FindFirst("Id")?.Value;

            IEnumerable<Municipality> municipalities;//this list is empty it well hold baladiye names

            if (reporterRole == "Staff")
            {
                // get staff's MunicipalityId from database
                var staffSql = "SELECT usr_MunicipalityId FROM tbl_Users WHERE usr_Id = @Id";
                var municipalityId = await _connection.QueryFirstOrDefaultAsync<int?>(
                    staffSql, new { Id = int.Parse(reporterIdClaim) });

                if (municipalityId == null)
                    return BadRequest("Staff member is not assigned to any baladiye.");

                // check if report location is within staff's baladiye boundary + 100m tolerance
                var locationCheckSql = @"
                    SELECT COUNT(*) FROM tbl_Municipalities-- here count well return 0 or 1
                    WHERE mun_Id = @MunicipalityId
                    AND (
                        mun_Boundary.STContains(
                            geography::STPointFromText(
                                'POINT(' + CAST(@Longitude AS NVARCHAR) + ' ' + CAST(@Latitude AS NVARCHAR) + ')'
                            , 4326)) = 1
                        OR mun_Boundary.STDistance(
                            geography::STPointFromText(
                                'POINT(' + CAST(@Longitude AS NVARCHAR) + ' ' + CAST(@Latitude AS NVARCHAR) + ')'
                            , 4326)) < 100
                    )";

                var isWithinBoundary = await _connection.QueryFirstAsync<int>(
                    locationCheckSql, new { MunicipalityId = municipalityId, request.Longitude, request.Latitude });

                if (isWithinBoundary == 0)
                    return BadRequest("You can only submit reports within your baladiye boundaries.");

                // assign only staff's own baladiye — no spatial query needed
                var staffMunicipalitySql = "SELECT mun_Id, mun_Name FROM tbl_Municipalities WHERE mun_Id = @Id";
                var staffMunicipality = await _connection.QueryFirstAsync<Municipality>(
                    staffMunicipalitySql, new { Id = municipalityId });

                // list has ONE item — staff's own baladiye only
                municipalities = new List<Municipality> { staffMunicipality };
            }
            else
            {
                // Step 1 — resident: find all baladiyat whose polygon contains or is near this point
                var municipalitySql = @"--this query runs for each row in database so it it may return multiple baladiyes
                    SELECT mun_Id, mun_Name
                    FROM tbl_Municipalities 
                    WHERE mun_Boundary.STContains(
                        geography::STPointFromText(
                            'POINT(' + CAST(@Longitude AS NVARCHAR) + ' ' + CAST(@Latitude AS NVARCHAR) + ')'
                        , 4326)) = 1
                    OR mun_Boundary.STDistance(
                        geography::STPointFromText(
                            'POINT(' + CAST(@Longitude AS NVARCHAR) + ' ' + CAST(@Latitude AS NVARCHAR) + ')'
                        , 4326)) < 100";

                municipalities = await _connection.QueryAsync<Municipality>(
                    municipalitySql, new { request.Longitude, request.Latitude });

                if (!municipalities.Any())
                    return BadRequest("Location does not fall within any registered baladiye.");
            }

            // Duplicate check — for ALL users including staff
            // ask the database: is there already an open report
            // within 30 meters, same category, same baladiye, last 30 days?
            var duplicateSql = reporterRole == "Staff"//if staff use first query
                ? @"
                    SELECT TOP 1 rpt_Id --just when finid one matches stop fetching
                    FROM tbl_Reports
                    INNER JOIN tbl_ReportAssignments ON rpt_Id = rpa_ReportId--joins 2 tables depending on common rpt_ID=rpsa_ReportID
                    WHERE rpt_CategoryId = @CategoryId
                    AND rpt_CreatedAt > DATEADD(day, -20, GETDATE())
                    AND rpa_MunicipalityId IN (SELECT mun_Id FROM tbl_Municipalities 
                        WHERE mun_Boundary.STContains(
                            geography::STPointFromText(
                                'POINT(' + CAST(@Longitude AS NVARCHAR) + ' ' + CAST(@Latitude AS NVARCHAR) + ')'
                            , 4326)) = 1)
                    AND rpt_Location.STDistance(
                        geography::STPointFromText(
                            'POINT(' + CAST(@Longitude AS NVARCHAR) + ' ' + CAST(@Latitude AS NVARCHAR) + ')'
                        , 4326)) < 30"
                : @"
                    SELECT TOP 1 rpt_Id 
                    FROM tbl_Reports
                    INNER JOIN tbl_ReportAssignments ON rpt_Id = rpa_ReportId
                    WHERE rpt_Status != 'Resolved'
                    AND rpt_CategoryId = @CategoryId
                    AND rpt_CreatedAt > DATEADD(day, -20, GETDATE())
                    AND rpa_MunicipalityId IN (SELECT mun_Id FROM tbl_Municipalities 
                        WHERE mun_Boundary.STContains(
                            geography::STPointFromText(
                                'POINT(' + CAST(@Longitude AS NVARCHAR) + ' ' + CAST(@Latitude AS NVARCHAR) + ')'
                            , 4326)) = 1)
                    AND rpt_Location.STDistance(
                        geography::STPointFromText(
                            'POINT(' + CAST(@Longitude AS NVARCHAR) + ' ' + CAST(@Latitude AS NVARCHAR) + ')'
                        , 4326)) < 30";

            var existingReportId = await _connection.QueryFirstOrDefaultAsync<int?>(
                duplicateSql, new { request.CategoryId, request.Longitude, request.Latitude });

            if (existingReportId != null)
            {
                if (reporterRole == "Resident")
                {
                    // duplicate found — redirect resident to existing report
                    // React will take them to that report's page to vote on priority
                    return Ok(new
                    {
                        message = "This issue was already reported.",
                        existingReportId = existingReportId
                    });
                }
                else
                {
                    // staff duplicate — just reject
                    return BadRequest(new
                    {
                        message = "You have already submitted a report for this issue.",
                        existingReportId = existingReportId
                    });
                }
            }

            // Step 2 — insert the report
            var insertReportSql = @"
                INSERT INTO tbl_Reports (rpt_Title, rpt_Description, rpt_Status, rpt_CreatedAt, rpt_ReportedPhotoUrl,
                            rpt_ResolvedPhotoUrl, rpt_Location, rpt_ReporterId, rpt_CategoryId, rpt_Priority)
                OUTPUT INSERTED.rpt_Id
                VALUES (@Title, @Description, @Status, @CreatedAt, @ReportedPhotoUrl,
                        @ResolvedPhotoUrl,
                        geography::STPointFromText(
                            'POINT(' + CAST(@Longitude AS NVARCHAR) + ' ' + CAST(@Latitude AS NVARCHAR) + ')'
                        , 4326),
                        @ReporterId, @CategoryId, @Priority)";

            var reportId = await _connection.QueryFirstAsync<int>(insertReportSql, new
            {
                request.Title,
                request.Description,
                Status = reporterRole == "Staff" ? "Resolved" : "Submitted", // staff = already resolved, resident = submitted
                CreatedAt = DateTime.Now,
                request.ReportedPhotoUrl,
                ResolvedPhotoUrl = reporterRole == "Staff" ? request.ResolvedPhotoUrl : null, // only staff provides this
                request.Longitude,
                request.Latitude,
                request.ReporterId,
                request.CategoryId,
                Priority = reporterRole == "Staff" ? null : request.Priority // staff reports dont need priority
            });

            // Step 3 — insert one ReportAssignment row per baladiye
            var assignmentSql = @"
                INSERT INTO tbl_ReportAssignments (rpa_ReportId, rpa_MunicipalityId, rpa_AssignedAt, rpa_IsHandler, rpa_Points)
                VALUES (@ReportId, @MunicipalityId, @AssignedAt, @IsHandler, 0)";

            foreach (var municipality in municipalities)
            {
                await _connection.ExecuteAsync(assignmentSql, new
                {
                    ReportId = reportId,
                    MunicipalityId = municipality.mun_Id,
                    AssignedAt = DateTime.Now,
                    IsHandler = reporterRole == "Staff" ? 1 : 0 // staff is always the handler of their own report
                });
            }

            return Ok(new { ReportId = reportId, AssignedMunicipalities = municipalities.Select(m => m.mun_Name) });
        }

        [HttpGet] // public — no login required, everyone can see
        public async Task<IActionResult> GetAllReports()
        {
            var sql = @"
        SELECT 
            tbl_Reports.rpt_Id,
            tbl_Reports.rpt_Title,
            tbl_Reports.rpt_Description,
            tbl_Reports.rpt_Status,
            tbl_Reports.rpt_CreatedAt,
            tbl_Reports.rpt_ReportedPhotoUrl,
            tbl_Reports.rpt_ResolvedPhotoUrl,
            tbl_Reports.rpt_Priority,
            tbl_Reports.rpt_AgreementCount,
            tbl_Categories.ctg_Name AS CategoryName,
            STRING_AGG(tbl_Municipalities.mun_Name, ', ') AS AssignedMunicipalities
        FROM tbl_Reports
        INNER JOIN tbl_Categories ON tbl_Reports.rpt_CategoryId = tbl_Categories.ctg_Id
        INNER JOIN tbl_ReportAssignments ON tbl_Reports.rpt_Id = tbl_ReportAssignments.rpa_ReportId
        INNER JOIN tbl_Municipalities ON tbl_ReportAssignments.rpa_MunicipalityId = tbl_Municipalities.mun_Id
        GROUP BY 
            tbl_Reports.rpt_Id, tbl_Reports.rpt_Title, tbl_Reports.rpt_Description,
            tbl_Reports.rpt_Status, tbl_Reports.rpt_CreatedAt,
            tbl_Reports.rpt_ReportedPhotoUrl, tbl_Reports.rpt_ResolvedPhotoUrl,
            tbl_Reports.rpt_Priority, tbl_Reports.rpt_AgreementCount,
            tbl_Categories.ctg_Name
        ORDER BY 
            CASE tbl_Reports.rpt_Priority 
                WHEN 'High' THEN 1 
                WHEN 'Medium' THEN 2 
                WHEN 'Low' THEN 3 
                ELSE 4
            END,
            tbl_Reports.rpt_CreatedAt DESC";

            var reports = await _connection.QueryAsync<dynamic>(sql);
            return Ok(reports);
        }

        [Authorize]
        [HttpGet("{id}")] // address: GET api/Reports/1
        public async Task<IActionResult> GetReportById(int id)
        {
            // get the report details
            var reportSql = @"
        SELECT 
            tbl_Reports.rpt_Id, tbl_Reports.rpt_Title, tbl_Reports.rpt_Description,
            tbl_Reports.rpt_Status, tbl_Reports.rpt_CreatedAt,
            tbl_Reports.rpt_ReportedPhotoUrl, tbl_Reports.rpt_ResolvedPhotoUrl,
            tbl_Reports.rpt_ReporterId, tbl_Reports.rpt_CategoryId,
            tbl_Reports.rpt_Priority, tbl_Reports.rpt_AgreementCount,
            tbl_Reports.rpt_DisagreementCount,
            tbl_Categories.ctg_Name AS CategoryName
        FROM tbl_Reports
        INNER JOIN tbl_Categories ON tbl_Reports.rpt_CategoryId = tbl_Categories.ctg_Id
        WHERE tbl_Reports.rpt_Id = @Id";

            var report = await _connection.QueryFirstOrDefaultAsync<dynamic>(reportSql, new { Id = id });

            if (report == null)
                return NotFound("Report not found.");

            // get all assigned municipalities for this report
            var assignmentsSql = @"
        SELECT 
            tbl_Municipalities.mun_Name AS MunicipalityName,
            tbl_ReportAssignments.rpa_IsHandler,
            tbl_ReportAssignments.rpa_AcceptedAt,
            tbl_ReportAssignments.rpa_Points
        FROM tbl_ReportAssignments
        INNER JOIN tbl_Municipalities ON tbl_ReportAssignments.rpa_MunicipalityId = tbl_Municipalities.mun_Id
        WHERE tbl_ReportAssignments.rpa_ReportId = @Id";

            var assignments = await _connection.QueryAsync<dynamic>(assignmentsSql, new { Id = id });

            // get priority votes breakdown — how many voted High, Medium, Low
            var priorityVotesSql = @"
        SELECT 
            pvt_Priority,
            COUNT(*) AS VoteCount
        FROM tbl_PriorityVotes
        WHERE pvt_ReportId = @Id
        GROUP BY pvt_Priority";

            var priorityVotesRaw = await _connection.QueryAsync<dynamic>(priorityVotesSql, new { Id = id });

            // build the breakdown — default 0 for any priority that has no votes
            var priorityBreakdown = new
            {
                High = priorityVotesRaw.FirstOrDefault(v => v.pvt_Priority == "High")?.VoteCount ?? 0,
                Medium = priorityVotesRaw.FirstOrDefault(v => v.pvt_Priority == "Medium")?.VoteCount ?? 0,
                Low = priorityVotesRaw.FirstOrDefault(v => v.pvt_Priority == "Low")?.VoteCount ?? 0,
                Total = priorityVotesRaw.Sum(v => (int)v.VoteCount)
            };

            return Ok(new { Report = report, Assignments = assignments, PriorityVotes = priorityBreakdown });
        }

        [Authorize(Roles = "Staff,Admin")]
        [HttpPut("{id}/status")] // address: PUT api/Reports/1/status
        public async Task<IActionResult> UpdateReportStatus(int id, [FromBody] UpdateStatusRequest request)
        {
            // Step 1 — check the report exists
            var checkSql = "SELECT rpt_Id, rpt_Status FROM tbl_Reports WHERE rpt_Id = @Id";
            var report = await _connection.QueryFirstOrDefaultAsync<dynamic>(checkSql, new { Id = id });

            if (report == null)
                return NotFound("Report not found.");

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
                ChangedByUserId = request.ChangedByUserId // who made the change
            });

            // Step 4 — if resolved, update points in ReportAssignments and Municipality TotalPoints
            if (request.NewStatus == "Resolved")
            {
                // get all assignments for this report
                var assignmentsSql = "SELECT rpa_Id, rpa_MunicipalityId, rpa_IsHandler FROM tbl_ReportAssignments WHERE rpa_ReportId = @ReportId";
                var assignments = await _connection.QueryAsync<dynamic>(assignmentsSql, new { ReportId = id });

                foreach (var assignment in assignments)
                {
                    int points = assignment.rpa_IsHandler ? 10 : -5;

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

        [Authorize(Roles = "Resident,Staff,Admin")]
        [HttpPost("{id}/comments")] // address: POST api/Reports/1/comments
        public async Task<IActionResult> AddComment(int id, [FromBody] AddCommentRequest request)
        {
            // Step 1 — check the report exists
            var checkSql = "SELECT rpt_Id FROM tbl_Reports WHERE rpt_Id = @Id";
            var reportId = await _connection.QueryFirstOrDefaultAsync<int?>(checkSql, new { Id = id });
            // int? — returns the Id number if found, or null if no report with that Id exists

            if (reportId == null)
                return NotFound("Report not found.");

            // Step 2 — insert the comment
            var sql = @"
                INSERT INTO tbl_Comments (cmt_Text, cmt_CreatedAt, cmt_ReportId, cmt_UserId)
                VALUES (@Text, @CreatedAt, @ReportId, @UserId)";

            await _connection.ExecuteAsync(sql, new
            {
                request.Text,              // the comment text written by staff/admin
                CreatedAt = DateTime.Now,  // when the comment was added — set by system
                ReportId = id,             // which report this comment belongs to — from URL
                request.UserId             // who wrote the comment — from request body
            });

            return Ok("Comment added successfully");
        }

        [Authorize(Roles = "Staff,Admin")]
        [HttpPut("{id}/accept")] // address: PUT api/Reports/1/accept
        public async Task<IActionResult> AcceptReport(int id, [FromBody] AcceptReportRequest request)
        {
            // check assignment exists for this municipality
            var checkSql = @"
                SELECT rpa_Id FROM tbl_ReportAssignments 
                WHERE rpa_ReportId = @ReportId AND rpa_MunicipalityId = @MunicipalityId";

            var assignmentId = await _connection.QueryFirstOrDefaultAsync<int?>(
                checkSql, new { ReportId = id, MunicipalityId = request.MunicipalityId });

            if (assignmentId == null)
                return NotFound("This baladiye is not assigned to this report.");

            // mark this baladiye as the handler
            var updateSql = @"
                UPDATE tbl_ReportAssignments 
                SET rpa_IsHandler = 1, rpa_AcceptedAt = @AcceptedAt
                WHERE rpa_ReportId = @ReportId AND rpa_MunicipalityId = @MunicipalityId";

            await _connection.ExecuteAsync(updateSql, new
            {
                AcceptedAt = DateTime.Now,
                ReportId = id,
                MunicipalityId = request.MunicipalityId
            });

            return Ok("Report accepted successfully.");
        }

        [Authorize(Roles = "Resident")]
        [HttpPost("{id}/agree")] // address: POST api/Reports/1/agree
        public async Task<IActionResult> AgreeOnReport(int id, [FromBody] AgreementRequest request)
        {
            // Step 1 — check the report exists and was submitted by staff
            var checkSql = @"
                SELECT rpt_Id, rpt_ReporterId FROM tbl_Reports 
                WHERE rpt_Id = @Id";
            var report = await _connection.QueryFirstOrDefaultAsync<dynamic>(checkSql, new { Id = id });

            if (report == null)
                return NotFound("Report not found.");

            // check if reporter is staff
            var reporterSql = "SELECT usr_Role FROM tbl_Users WHERE usr_Id = @Id";
            var reporterRole = await _connection.QueryFirstOrDefaultAsync<string>(
                reporterSql, new { Id = (int)report.rpt_ReporterId });

            if (reporterRole != "Staff")
                return BadRequest("You can only agree on staff-submitted reports.");

            // Step 2 — check if this resident already agreed/disagreed on this report
            var existingSql = @"
                SELECT rga_Id FROM tbl_ReportAgreements 
                WHERE rga_ReportId = @ReportId AND rga_UserId = @UserId";

            var existing = await _connection.QueryFirstOrDefaultAsync<int?>(
                existingSql, new { ReportId = id, UserId = request.UserId });

            if (existing != null)
                return BadRequest("You have already submitted your agreement on this report.");

            // Step 3 — save the agreement
            var insertSql = @"
                INSERT INTO tbl_ReportAgreements (rga_ReportId, rga_UserId, rga_IsAgreement)
                VALUES (@ReportId, @UserId, @IsAgreement)";

            await _connection.ExecuteAsync(insertSql, new
            {
                ReportId = id,
                UserId = request.UserId,
                IsAgreement = request.IsAgreement
            });

            // Step 4 — increment the correct counter on the report
            if (request.IsAgreement)
            {
                // increment agreement count
                await _connection.ExecuteAsync(
                    "UPDATE tbl_Reports SET rpt_AgreementCount = rpt_AgreementCount + 1 WHERE rpt_Id = @Id",
                    new { Id = id });

                // check if agreement count reached threshold of 3
                var countSql = "SELECT rpt_AgreementCount FROM tbl_Reports WHERE rpt_Id = @Id";
                var agreementCount = await _connection.QueryFirstAsync<int>(countSql, new { Id = id });

                if (agreementCount >= 3)
                {
                    // get the assignment for this report
                    var assignmentSql = @"
                        SELECT rpa_Id, rpa_MunicipalityId, rpa_Points 
                        FROM tbl_ReportAssignments 
                        WHERE rpa_ReportId = @ReportId AND rpa_IsHandler = 1";

                    var assignment = await _connection.QueryFirstOrDefaultAsync<dynamic>(
                        assignmentSql, new { ReportId = id });

                    if (assignment != null && assignment.rpa_Points == 0)
                    {
                        // award points only once — rpa_Points == 0 prevents double awarding
                        await _connection.ExecuteAsync(
                            "UPDATE tbl_ReportAssignments SET rpa_Points = 10 WHERE rpa_Id = @Id",
                            new { Id = assignment.rpa_Id });

                        await _connection.ExecuteAsync(
                            "UPDATE tbl_Municipalities SET mun_TotalPoints = mun_TotalPoints + 10 WHERE mun_Id = @MunicipalityId",
                            new { MunicipalityId = assignment.rpa_MunicipalityId });
                    }
                }
            }
            else
            {
                // increment disagreement count
                await _connection.ExecuteAsync(
                    "UPDATE tbl_Reports SET rpt_DisagreementCount = rpt_DisagreementCount + 1 WHERE rpt_Id = @Id",
                    new { Id = id });
            }

            return Ok(request.IsAgreement ? "Agreement submitted." : "Disagreement submitted.");
        }

        [Authorize(Roles = "Resident")]
        [HttpPost("{id}/priority")] // address: POST api/Reports/1/priority
        public async Task<IActionResult> VoteOnPriority(int id, [FromBody] PriorityVoteRequest request)
        {
            // Step 1 — check report exists and is resident-submitted
            var checkSql = @"SELECT rpt_Id, rpt_ReporterId FROM tbl_Reports WHERE rpt_Id = @Id";
            var report = await _connection.QueryFirstOrDefaultAsync<dynamic>(checkSql, new { Id = id });

            if (report == null)
                return NotFound("Report not found.");

            // check reporter is resident
            var reporterSql = "SELECT usr_Role FROM tbl_Users WHERE usr_Id = @Id";
            var reporterRole = await _connection.QueryFirstOrDefaultAsync<string>(
                reporterSql, new { Id = (int)report.rpt_ReporterId });

            if (reporterRole != "Resident")
                return BadRequest("You can only vote on priority of resident-submitted reports.");

            // Step 2 — check if this resident already voted on this report
            var existingSql = @"
                SELECT pvt_Id FROM tbl_PriorityVotes 
                WHERE pvt_ReportId = @ReportId AND pvt_UserId = @UserId";

            var existing = await _connection.QueryFirstOrDefaultAsync<int?>(
                existingSql, new { ReportId = id, UserId = request.UserId });

            if (existing != null)
                return BadRequest("You have already voted on this report's priority.");

            // Step 3 — validate priority value
            if (request.Priority != "Low" && request.Priority != "Medium" && request.Priority != "High")
                return BadRequest("Priority must be Low, Medium, or High.");

            // Step 4 — save the vote
            var insertSql = @"
                INSERT INTO tbl_PriorityVotes (pvt_ReportId, pvt_UserId, pvt_Priority)
                VALUES (@ReportId, @UserId, @Priority)";

            await _connection.ExecuteAsync(insertSql, new
            {
                ReportId = id,
                UserId = request.UserId,
                Priority = request.Priority
            });

            // Step 5 — recalculate priority based on majority vote
            var votesSql = @"
                SELECT pvt_Priority, COUNT(*) AS VoteCount
                FROM tbl_PriorityVotes
                WHERE pvt_ReportId = @ReportId
                GROUP BY pvt_Priority
                ORDER BY VoteCount DESC";

            var votes = await _connection.QueryAsync<dynamic>(votesSql, new { ReportId = id });
            var topPriority = votes.First().pvt_Priority; // the priority with most votes

            // update report priority to majority vote
            await _connection.ExecuteAsync(
                "UPDATE tbl_Reports SET rpt_Priority = @Priority WHERE rpt_Id = @Id",
                new { Priority = topPriority, Id = id });

            return Ok(new { message = "Priority vote submitted.", currentPriority = topPriority });
        }
    }
}