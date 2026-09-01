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
    [Route("api/Reports")]
    public class ReportsController : ControllerBase
    {
        private readonly SqlConnection _connection;

        public ReportsController(SqlConnection connection)
        {
            _connection = connection;
        }


        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateReport([FromBody] CreateReportRequest request)
        {

            var reporterRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var currentUserId = int.Parse(User.FindFirst("Id")!.Value);

            IEnumerable<Municipality> municipalities;

            if (reporterRole == "Staff")
            {
                
                var staffSql = "SELECT usr_MunicipalityId FROM tbl_Users WHERE usr_Id = @Id";
                var municipalityId = await _connection.QueryFirstOrDefaultAsync<int?>(
                    staffSql, new { Id = currentUserId });

                if (municipalityId == null)
                    return BadRequest("Staff member is not assigned to any baladiye.");

                var locationCheckSql = @"
                    SELECT
                        mun_Name,
                        mun_Boundary.STContains(                                               
                            geography::Point(@Latitude, @Longitude, 4326)) AS ContainsPoint,
                        mun_Boundary.STDistance(                                                
                            geography::Point(@Latitude, @Longitude, 4326)) AS DistanceMeters
                    FROM tbl_Municipalities
                    WHERE mun_Id = @MunicipalityId";

                var boundaryCheck = await _connection.QueryFirstOrDefaultAsync<dynamic>(
                    locationCheckSql, new { MunicipalityId = municipalityId, request.Longitude, request.Latitude });

                if (boundaryCheck == null)
                    return BadRequest("Your baladiye could not be found in the database.");

                
                string myBaladiyeName = (object?)boundaryCheck.mun_Name as string ?? "your baladiye";
                object? containsRaw = (object?)boundaryCheck.ContainsPoint;
                object? distanceRaw = (object?)boundaryCheck.DistanceMeters;

                if (containsRaw == null || distanceRaw == null)
                    return BadRequest($"{myBaladiyeName} has no boundary polygon saved");

                bool isInsideBoundary = Convert.ToInt32(containsRaw) == 1;
                double distanceMeters = Convert.ToDouble(distanceRaw);

                if (!isInsideBoundary && distanceMeters >= 100)
                {
                    return BadRequest(
                        $"You can only submit reports within your baladiye boundaries. " );
                }

                var staffMunicipalitySql = "SELECT mun_Id, mun_Name FROM tbl_Municipalities WHERE mun_Id = @Id";
                var staffMunicipality = await _connection.QueryFirstAsync<Municipality>(
                    staffMunicipalitySql, new { Id = municipalityId });
                municipalities = new List<Municipality> { staffMunicipality };
            }
            else
            {
                var municipalitySql = @"
                    SELECT mun_Id, mun_Name
                    FROM tbl_Municipalities
                    WHERE mun_Boundary.STContains(
                        geography::Point(@Latitude, @Longitude, 4326)) = 1
                    OR mun_Boundary.STDistance(
                        geography::Point(@Latitude, @Longitude, 4326)) < 100";

                municipalities = await _connection.QueryAsync<Municipality>(
                    municipalitySql, new { request.Longitude, request.Latitude });

                if (!municipalities.Any())
                    return BadRequest("Location does not fall within any registered baladiye.");

                
                if (!string.IsNullOrEmpty(request.Priority) &&
                    request.Priority != "Low" && request.Priority != "Medium" && request.Priority != "High")
                {
                    return BadRequest("Priority must be Low, Medium, or High.");
                }
            }

            if (reporterRole == "Resident")
            {
                var todayCountSql = @"
                    SELECT COUNT(*) FROM tbl_Reports
                    WHERE rpt_ReporterId = @ReporterId
                    AND CAST(rpt_CreatedAt AS DATE) = CAST(GETDATE() AS DATE)";

                var todayCount = await _connection.QueryFirstAsync<int>(
                    todayCountSql, new { ReporterId = currentUserId });

                if (todayCount >= 3)
                    return BadRequest("You have reached the daily limit of 3 reports. Try again tomorrow.");
            }

            var duplicateSql = reporterRole == "Staff"
                ? @"
                    SELECT TOP 1 rpt_Id 
                    FROM tbl_Reports
                    INNER JOIN tbl_ReportAssignments ON rpt_Id = rpa_ReportId
                    WHERE rpt_CategoryId = @CategoryId
                    AND rpt_CreatedAt > DATEADD(day, -20, GETDATE())
                    AND rpa_MunicipalityId IN (SELECT mun_Id FROM tbl_Municipalities
                        WHERE mun_Boundary.STContains(
                            geography::Point(@Latitude, @Longitude, 4326)) = 1)
                    AND rpt_Location.STDistance(
                        geography::Point(@Latitude, @Longitude, 4326)) < 30"
                    :
                 @"
                    SELECT TOP 1 rpt_Id
                    FROM tbl_Reports
                    INNER JOIN tbl_ReportAssignments ON rpt_Id = rpa_ReportId
                    WHERE rpt_Status != 'Resolved'
                    AND rpt_CategoryId = @CategoryId
                    AND rpt_CreatedAt > DATEADD(day, -20, GETDATE())
                    AND rpa_MunicipalityId IN (SELECT mun_Id FROM tbl_Municipalities       ---User's GPS location → which municipality contains it? → get that municipality's ID → compare it with the report's municipality ID.
                        WHERE mun_Boundary.STContains(                                     --The report's municipality ID must be one of the municipality IDs returned by the query inside the parentheses.
                            geography::Point(@Latitude, @Longitude, 4326)) = 1)
                    AND rpt_Location.STDistance(
                        geography::Point(@Latitude, @Longitude, 4326)) < 30";

            var existingReportId = await _connection.QueryFirstOrDefaultAsync<int?>(
                duplicateSql, new { request.CategoryId, request.Longitude, request.Latitude });

            if (existingReportId != null)
            {
                if (reporterRole == "Resident")
                {
                    return Ok(new
                    {
                        message = "This issue was already reported.",
                        existingReportId = existingReportId
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        message = "You have already submitted a report for this issue.",
                        existingReportId = existingReportId
                    });
                }
            }

            var insertReportSql = @"
                INSERT INTO tbl_Reports (rpt_Title, rpt_Description, rpt_Status, rpt_CreatedAt, rpt_ReportedPhotoUrl,
                            rpt_ResolvedPhotoUrl, rpt_Location, rpt_ReporterId, rpt_CategoryId, rpt_Priority)
                OUTPUT INSERTED.rpt_Id
                VALUES (@Title, @Description, @Status, @CreatedAt, @ReportedPhotoUrl,
                        @ResolvedPhotoUrl,
                        geography::Point(@Latitude, @Longitude, 4326),
                        @ReporterId, @CategoryId, @Priority)";

            var reportId = await _connection.QueryFirstAsync<int>(insertReportSql, new
            {
                request.Title,
                request.Description,
                Status = reporterRole == "Staff" ? "Resolved" : "Submitted",
                CreatedAt = DateTime.Now,
                request.ReportedPhotoUrl,
                ResolvedPhotoUrl = reporterRole == "Staff" ? request.ResolvedPhotoUrl : null, 
                request.Longitude,
                request.Latitude,
                ReporterId = currentUserId,
                request.CategoryId,
                Priority = reporterRole == "Staff" ? null : request.Priority
            });

            var assignmentSql = @"
                INSERT INTO tbl_ReportAssignments (rpa_ReportId, rpa_MunicipalityId, rpa_AssignedAt, rpa_IsHandler, rpa_AcceptedAt, rpa_Points)
                VALUES (@ReportId, @MunicipalityId, @AssignedAt, @IsHandler, @AcceptedAt, 0)";

            
            bool ownedOnCreate = reporterRole == "Staff" || municipalities.Count() == 1;

            foreach (var municipality in municipalities)
            {
                await _connection.ExecuteAsync(assignmentSql, new
                {
                    ReportId = reportId,
                    MunicipalityId = municipality.mun_Id,
                    AssignedAt = DateTime.Now,
                    IsHandler = ownedOnCreate ? 1 : 0,
                    AcceptedAt = ownedOnCreate ? (DateTime?)DateTime.Now : null
                });
            }

            return Ok(new { ReportId = reportId, AssignedMunicipalities = municipalities.Select(m => m.mun_Name) });
        }


        [Authorize] 
        [HttpGet] 
        public async Task<IActionResult> GetAllReports()
        {
            
            var userId = int.Parse(User.FindFirst("Id")!.Value);
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            
            var baseSql = @"
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
        INNER JOIN tbl_Municipalities ON tbl_ReportAssignments.rpa_MunicipalityId = tbl_Municipalities.mun_Id ";

            string whereClause = "";

            if (role == "Staff")
            {
                whereClause = @"WHERE EXISTS (
                            SELECT 1 FROM tbl_ReportAssignments AS my_assignment
                            WHERE my_assignment.rpa_ReportId = tbl_Reports.rpt_Id
                            AND my_assignment.rpa_MunicipalityId =
                                (SELECT usr_MunicipalityId FROM tbl_Users WHERE usr_Id = @userId)) --Is the current report assigned to my municipality
                          AND (SELECT COUNT(*) FROM tbl_ReportAssignments
                               WHERE rpa_ReportId = tbl_Reports.rpt_Id) = 1 ";
            }

            var groupOrderSql = @"
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


            var sql = baseSql + whereClause + groupOrderSql;

            var reports = await _connection.QueryAsync<dynamic>(sql, new { userId = userId });
            return Ok(reports);
        }


        [Authorize]
        [HttpGet("mine")]
        public async Task<IActionResult> GetMyReports()
        {
            var userId = int.Parse(User.FindFirst("Id")!.Value);
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            if (string.IsNullOrEmpty(role))
                return BadRequest("Could not read role from token.");

            var baseSql = @"
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
        INNER JOIN tbl_Municipalities ON tbl_ReportAssignments.rpa_MunicipalityId = tbl_Municipalities.mun_Id ";

            string whereClause;

            if (role == "Resident")
            {
                whereClause = "WHERE tbl_Reports.rpt_ReporterId = @userId ";
            }
            else if (role == "Admin")
            {
                whereClause = "";
            }
            else
            {
                return StatusCode(403, "This endpoint is for Residents and Admins. Staff should use GET api/Reports, which already returns their baladiye's reports.");
            }

            var groupOrderSql = @"
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

            var fullSql = baseSql + whereClause + groupOrderSql;

            var reports = await _connection.QueryAsync<dynamic>(fullSql, new { userId });
            return Ok(reports);
        }

        [Authorize]
        [HttpGet("{id:int}")]

        public async Task<IActionResult> GetReportById(int id)

        {
            var currentUserId = int.Parse(User.FindFirst("Id")!.Value);


            if (User.FindFirst(ClaimTypes.Role)?.Value == "Staff")
            {
                var myMunicipalityId = await _connection.QueryFirstOrDefaultAsync<int?>(
                    "SELECT usr_MunicipalityId FROM tbl_Users WHERE usr_Id = @Id", new { Id = currentUserId });
                if (myMunicipalityId == null)
                    return BadRequest("Staff member is not assigned to any baladiye.");

                if (await _connection.QueryFirstAsync<int>(
                    @"SELECT COUNT(*) FROM tbl_ReportAssignments
                      WHERE rpa_ReportId = @ReportId AND rpa_MunicipalityId = @MunicipalityId",
                    new { ReportId = id, MunicipalityId = myMunicipalityId.Value }) == 0)
                    return StatusCode(403, "This report does not belong to your baladiye.");

                if (await _connection.QueryFirstAsync<int>(
                    "SELECT COUNT(*) FROM tbl_ReportAssignments WHERE rpa_ReportId = @ReportId",
                    new { ReportId = id }) > 1)
                    return StatusCode(403, "This report is shared between several baladiyat. An admin has not decided who handles it yet.");
            }

            var reportSql = @"
        SELECT
            tbl_Reports.rpt_Id, tbl_Reports.rpt_Title, tbl_Reports.rpt_Description,
            tbl_Reports.rpt_Status, tbl_Reports.rpt_CreatedAt,
            tbl_Reports.rpt_ReportedPhotoUrl, tbl_Reports.rpt_ResolvedPhotoUrl,
            tbl_Reports.rpt_ReporterId, tbl_Reports.rpt_CategoryId,
            tbl_Reports.rpt_Priority, tbl_Reports.rpt_AgreementCount,
            tbl_Reports.rpt_DisagreementCount,
            tbl_Categories.ctg_Name AS CategoryName,
            tbl_Users.usr_FullName AS ReporterName,   -- ADDED: who reported it
            tbl_Users.usr_Role AS ReporterRole,       -- ADDED: Resident or Staff
            --backedend send back long and latt since frontend need these not point
            tbl_Reports.rpt_Location.Lat AS Latitude,
            tbl_Reports.rpt_Location.Long AS Longitude
        FROM tbl_Reports
        INNER JOIN tbl_Categories ON tbl_Reports.rpt_CategoryId = tbl_Categories.ctg_Id
        INNER JOIN tbl_Users ON tbl_Reports.rpt_ReporterId = tbl_Users.usr_Id
        WHERE tbl_Reports.rpt_Id = @Id";

            var report = await _connection.QueryFirstOrDefaultAsync<dynamic>(reportSql, new { Id = id });

            if (report == null)
                return NotFound("Report not found.");

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

            var priorityVotesSql = @"
        SELECT
            pvt_Priority,
            COUNT(*) AS VoteCount
        FROM tbl_PriorityVotes
        WHERE pvt_ReportId = @Id
        GROUP BY pvt_Priority";

            var priorityVotesRaw = await _connection.QueryAsync<dynamic>(priorityVotesSql, new { Id = id });

            var voteCounts = new Dictionary<string, int>();
            foreach (var voteRow in priorityVotesRaw)
            {
                string priorityName = Convert.ToString((object)voteRow.pvt_Priority) ?? "";
                int voteCount = Convert.ToInt32((object)voteRow.VoteCount);
                voteCounts[priorityName] = voteCount;
            }

            voteCounts.TryGetValue("High", out int highVotes);
            voteCounts.TryGetValue("Medium", out int mediumVotes);
            voteCounts.TryGetValue("Low", out int lowVotes);

            var priorityBreakdown = new
            {
                High = highVotes,
                Medium = mediumVotes,
                Low = lowVotes,
                Total = highVotes + mediumVotes + lowVotes
            };

            var commentsSql = @"
        SELECT
            tbl_Comments.cmt_Id,
            tbl_Comments.cmt_Text,
            tbl_Comments.cmt_CreatedAt,
            tbl_Users.usr_FullName AS AuthorName,
            tbl_Users.usr_Role AS AuthorRole
        FROM tbl_Comments
        INNER JOIN tbl_Users ON tbl_Comments.cmt_UserId = tbl_Users.usr_Id
        WHERE tbl_Comments.cmt_ReportId = @Id
        ORDER BY tbl_Comments.cmt_CreatedAt ASC";

            var comments = await _connection.QueryAsync<dynamic>(commentsSql, new { Id = id });

            var historySql = @"
        SELECT
            tbl_StatusHistories.sth_OldStatus,
            tbl_StatusHistories.sth_NewStatus,
            tbl_StatusHistories.sth_ChangedAt,
            tbl_Users.usr_FullName AS ChangedByName,
            tbl_Users.usr_Role AS ChangedByRole
        FROM tbl_StatusHistories
        INNER JOIN tbl_Users ON tbl_StatusHistories.sth_ChangedByUserId = tbl_Users.usr_Id
        WHERE tbl_StatusHistories.sth_ReportId = @Id
        ORDER BY tbl_StatusHistories.sth_ChangedAt ASC";

            var statusHistory = await _connection.QueryAsync<dynamic>(historySql, new { Id = id });


            var myPriorityVote = await _connection.QueryFirstOrDefaultAsync<string>(
                "SELECT pvt_Priority FROM tbl_PriorityVotes WHERE pvt_ReportId = @Id AND pvt_UserId = @UserId",
                new { Id = id, UserId = currentUserId });


            var myAgreement = await _connection.QueryFirstOrDefaultAsync<bool?>(
                "SELECT rga_IsAgreement FROM tbl_ReportAgreements WHERE rga_ReportId = @Id AND rga_UserId = @UserId",
                new { Id = id, UserId = currentUserId });

            return Ok(new
            {
                Report = report,
                Assignments = assignments,
                PriorityVotes = priorityBreakdown,
                Comments = comments,
                StatusHistory = statusHistory,
                MyPriorityVote = myPriorityVote,
                MyAgreement = myAgreement
            });
        }



        [HttpGet("map")]
        public async Task<IActionResult> GetReportsForMap()
        {
            var sql = @"
        SELECT
            tbl_Reports.rpt_Id,
            tbl_Reports.rpt_Title,
            tbl_Reports.rpt_Status,
            MIN(tbl_Reports.rpt_Location.Lat)  AS Latitude,
            MIN(tbl_Reports.rpt_Location.Long) AS Longitude,
            tbl_Categories.ctg_Name AS CategoryName,
            STRING_AGG(tbl_Municipalities.mun_Name, ', ') AS AssignedMunicipalities
        FROM tbl_Reports
        INNER JOIN tbl_Categories
            ON tbl_Reports.rpt_CategoryId = tbl_Categories.ctg_Id
        INNER JOIN tbl_ReportAssignments
            ON tbl_Reports.rpt_Id = tbl_ReportAssignments.rpa_ReportId
        INNER JOIN tbl_Municipalities
            ON tbl_ReportAssignments.rpa_MunicipalityId = tbl_Municipalities.mun_Id
        GROUP BY
            tbl_Reports.rpt_Id,
            tbl_Reports.rpt_Title,
            tbl_Reports.rpt_Status,
            tbl_Categories.ctg_Name";

            var reports = await _connection.QueryAsync<dynamic>(sql);
            return Ok(reports);
        }


    }

}

