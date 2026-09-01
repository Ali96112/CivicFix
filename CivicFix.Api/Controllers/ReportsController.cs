using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CivicFix.Api.Models;
using Dapper;
using NetTopologySuite.Geometries;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace CivicFix.Api.Controllers
{
    // ══════════════════════════════════════════════════════════════════════════
    // CORE REPORT ENDPOINTS — creating a report, listing them, opening one.
    // These are the endpoints every role touches.
    //
    // Split out of the original 1,900-line ReportsController. Each of the four
    // report controllers is self-contained — it carries its own connection and its
    // own copy of the small helpers it needs, so no file depends on another.
    // ══════════════════════════════════════════════════════════════════════════
    [ApiController]
    // NOT [Route("api/[controller]")] — that token expands to the class name, so
    // this file would answer on api/Reports and every URL below would change.
    // Written out in full, these endpoints keep the exact addresses they had when
    // they all lived in one ReportsController.
    [Route("api/Reports")]
    public class ReportsController : ControllerBase
    {
        private readonly SqlConnection _connection;

        public ReportsController(SqlConnection connection)
        {
            _connection = connection;
        }


        [Authorize]
        [HttpPost] // address: api/Reports
        public async Task<IActionResult> CreateReport([FromBody] CreateReportRequest request)
        {

            var reporterRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (!int.TryParse(User.FindFirst("Id")?.Value, out int currentUserId))
                return BadRequest("Could not read user Id from token. Claim 'Id' not found.");

            IEnumerable<Municipality> municipalities;//this list is empty it well hold baladiye names

            if (reporterRole == "Staff")
            {
                // get staff's MunicipalityId from database
                var staffSql = "SELECT usr_MunicipalityId FROM tbl_Users WHERE usr_Id = @Id";
                var municipalityId = await _connection.QueryFirstOrDefaultAsync<int?>(
                    staffSql, new { Id = currentUserId });

                if (municipalityId == null)
                    return BadRequest("Staff member is not assigned to any baladiye.");

                var locationCheckSql = @"
                    SELECT
                        mun_Name,
                        mun_Boundary.STContains(                                               --Does this municipality's boundary contain the user's location?torf
                            geography::Point(@Latitude, @Longitude, 4326)) AS ContainsPoint,
                        mun_Boundary.STDistance(                                                --How far is the user's location from the municipality boundary?example 80
                            geography::Point(@Latitude, @Longitude, 4326)) AS DistanceMeters,
                        mun_Boundary.STArea() / 1000000.0 AS AreaSquareKm                         --Gets the total area of the municipality.
                    FROM tbl_Municipalities
                    WHERE mun_Id = @MunicipalityId";
                //The SQL takes one municipality and the user's GPS location, then asks:
                //What is the municipality's name? Is the user inside it? How far is the user from its boundary? And how large is the municipality?
                //returns mun_Name→ "Aley"    ContainsPoint→ true  DistanceMeters→ 80    AreaSquareKm→ 15.5


                var boundaryCheck = await _connection.QueryFirstOrDefaultAsync<dynamic>(
                    locationCheckSql, new { MunicipalityId = municipalityId, request.Longitude, request.Latitude });

                if (boundaryCheck == null)
                    return BadRequest("Your baladiye could not be found in the database.");

                //now we save the db ouput in object
                string myBaladiyeName = (object?)boundaryCheck.mun_Name as string ?? "your baladiye";//if mun name in db is null return your baladeye
                object? containsRaw = (object?)boundaryCheck.ContainsPoint;//checking if point inside the boundaey true or false
                object? distanceRaw = (object?)boundaryCheck.DistanceMeters;//the distance the point from the boundary

                if (containsRaw == null || distanceRaw == null)
                    return BadRequest($"{myBaladiyeName} has no boundary polygon saved, so its area cannot be checked. Re-run the boundary seeder.");

                //converting object values into c# 
                bool isInsideBoundary = Convert.ToInt32(containsRaw) == 1;
                double distanceMeters = Convert.ToDouble(distanceRaw);
                double areaSquareKm = Convert.ToDouble((object)boundaryCheck.AreaSquareKm);

                if (!isInsideBoundary && distanceMeters >= 100)//reject the report if outside and more than 100m
                {
                    //This formats the distance nicely:
                    var distanceText = distanceMeters >= 1000
                        ? $"{(distanceMeters / 1000):N1} km"
                        : $"{distanceMeters:N0} m";

                    //Area > 20,000 → show a warning that the boundary polygon is probably wrong.
                    var polygonWarning = areaSquareKm > 20000
                        ? $" WARNING: {myBaladiyeName}'s boundary covers {areaSquareKm:N0} km2, which is impossible — that polygon is wound the wrong way and needs ReorientObject()."
                        : "";

                    return BadRequest(
                        $"You can only submit reports within your baladiye boundaries. " +
                        $"Your location is {distanceText} away from {myBaladiyeName} " +
                        $"(you must be inside it, or within 100 m of it).{polygonWarning}");
                }

                //here report continue so it is within the boundiers mun staff
                // assign only staff's own baladiye — no spatial query needed
                var staffMunicipalitySql = "SELECT mun_Id, mun_Name FROM tbl_Municipalities WHERE mun_Id = @Id";//gets the staff member's own municipality and puts it into a list
                var staffMunicipality = await _connection.QueryFirstAsync<Municipality>(
                    staffMunicipalitySql, new { Id = municipalityId });
                // list has ONE item — staff's own baladiye only
                municipalities = new List<Municipality> { staffMunicipality };
            }
            else //if not staff
            {
                // NOTE (ADDED): this branch runs for BOTH Resident and Admin.
                // Admin is allowed to create reports and is treated exactly like a Resident
                // (no boundary restriction). If you ever want to block Admin from creating,
                // change the [Authorize] line above to Roles = "Resident,Staff".

                // Step 1 — resident: find all baladiyat whose polygon contains or is near this point
                var municipalitySql = @"--this query runs for each row in database so it it may return multiple baladiyes
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

                //priority field
                if (!string.IsNullOrEmpty(request.Priority) &&
                    request.Priority != "Low" && request.Priority != "Medium" && request.Priority != "High")
                {
                    return BadRequest("Priority must be Low, Medium, or High.");
                }
            }

            // daily limit — residents only, max 3 reports per day
            if (reporterRole == "Resident")
            {
                var todayCountSql = @"
                    SELECT COUNT(*) FROM tbl_Reports
                    WHERE rpt_ReporterId = @ReporterId
                    AND CAST(rpt_CreatedAt AS DATE) = CAST(GETDATE() AS DATE)";

                // currentUserId (from the token), NOT request.ReporterId — the body is
                // whatever the caller typed, so counting on it lets someone dodge their
                // own limit by sending a different id.
                var todayCount = await _connection.QueryFirstAsync<int>(
                    todayCountSql, new { ReporterId = currentUserId });

                if (todayCount >= 3)
                    return BadRequest("You have reached the daily limit of 3 reports. Try again tomorrow.");
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
                            geography::Point(@Latitude, @Longitude, 4326)) = 1)
                    AND rpt_Location.STDistance(
                        geography::Point(@Latitude, @Longitude, 4326)) < 30"
                    :
                 //Is not resolved.
                 //Has the same category as the new report.
                 //Was created in the last 20 days.//Is in the same baladiye as the user's location.//Is less than 30 meters away from the user's location.
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
                        geography::Point(@Latitude, @Longitude, 4326)) < 30";//The existing report must be less than 30 meters from the user's location.

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
                        geography::Point(@Latitude, @Longitude, 4326),
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
                ReporterId = currentUserId,
                request.CategoryId,
                Priority = reporterRole == "Staff" ? null : request.Priority // staff reports dont need priority
            });

            // Step 3 — insert one ReportAssignment row per baladiye
            var assignmentSql = @"
                INSERT INTO tbl_ReportAssignments (rpa_ReportId, rpa_MunicipalityId, rpa_AssignedAt, rpa_IsHandler, rpa_AcceptedAt, rpa_Points)
                VALUES (@ReportId, @MunicipalityId, @AssignedAt, @IsHandler, @AcceptedAt, 0)";

            // owned immediately when staff reported it, OR when only ONE baladiye matched
            // (nothing to decide). 2+ baladiyat = genuinely shared, stays unowned until
            // the Admin picks one on the Shared Reports screen.
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

            return Ok(new { ReportId = reportId, AssignedMunicipalities = municipalities.Select(m => m.mun_Name) });//sended back to frontend
        }


        [Authorize] // a token IS required — every role, but you must be logged in
        [HttpGet] // address: GET api/Reports
        public async Task<IActionResult> GetAllReports()
        {
            // What each role gets back:
            //   Admin    → all reports
            //   Resident → all reports (so they can agree and vote on priority)
            //   Staff    → only reports assigned to their own baladiye
            // The filter is the WHERE clause built further down.

            // who is asking? Straight from the signed JWT, never the request body.
            if (!int.TryParse(User.FindFirst("Id")?.Value, out int userId))
                return BadRequest("Could not read user Id from token. Claim 'Id' not found.");

            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            if (string.IsNullOrEmpty(role))
                return BadRequest("Could not read role from token.");

            // CHANGED: the query is now built in 3 pieces (base + where + group/order)
            // exactly like GetMyReports, so a role filter can be slotted in the middle.
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

            // ADDED: the role filter
            string whereClause = "";

            if (role == "Staff")
            {//Show the report only if it belongs to the logged-in staff member’s municipality AND it is assigned to only one municipality total.
                //select 1: mean I don’t care what data is in the row. Just return something if a matching row exists
                whereClause = @"WHERE EXISTS (
                            SELECT 1 FROM tbl_ReportAssignments AS my_assignment
                            WHERE my_assignment.rpa_ReportId = tbl_Reports.rpt_Id
                            AND my_assignment.rpa_MunicipalityId =
                                (SELECT usr_MunicipalityId FROM tbl_Users WHERE usr_Id = @userId)) --Is the current report assigned to my municipality
                          AND (SELECT COUNT(*) FROM tbl_ReportAssignments
                               WHERE rpa_ReportId = tbl_Reports.rpt_Id) = 1 ";//Is this report assigned to exactly one municipality
            }
            // Resident and Admin fall through with whereClause = "" → they see everything

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
        [HttpGet("mine")] // address: GET api/Reports/mine
        public async Task<IActionResult> GetMyReports()
        {
            // read the Id claim safely
            var idClaim = User.FindFirst("Id")?.Value;
            if (string.IsNullOrEmpty(idClaim))
                return BadRequest("Could not read user Id from token. Claim 'Id' not found.");

            var userId = int.Parse(idClaim);
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            if (string.IsNullOrEmpty(role))
                return BadRequest("Could not read role from token.");

            // the base SELECT is the same as GetAllReports
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

            // the WHERE clause changes based on role
            string whereClause;

            if (role == "Resident")
            {
                whereClause = "WHERE tbl_Reports.rpt_ReporterId = @userId ";
            }
            else if (role == "Admin")
            {
                // admin sees everything — no filter
                whereClause = "";
            }
            else
            {
                return StatusCode(403, "This endpoint is for Residents and Admins. Staff should use GET api/Reports, which already returns their baladiye's reports.");
            }

            // the GROUP BY and ORDER BY are the same as GetAllReports
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

            // stitch the three parts together
            var fullSql = baseSql + whereClause + groupOrderSql;

            var reports = await _connection.QueryAsync<dynamic>(fullSql, new { userId });
            return Ok(reports);
        }

        [Authorize]
        [HttpGet("{id:int}")] // address: GET api/Reports/1
        public async Task<IActionResult> GetReportById(int id)
        {
            // ADDED: same rule as the list above — a Staff member must not be able to
            // open a report from another baladiye just by typing its Id in the URL.
            var idClaim = User.FindFirst("Id")?.Value;
            if (!int.TryParse(idClaim, out int currentUserId))
                return BadRequest("Could not read user Id from token. Claim 'Id' not found.");

            if (User.FindFirst(ClaimTypes.Role)?.Value == "Staff")//
            {
                var myMunicipalityId = await _connection.QueryFirstOrDefaultAsync<int?>(
                    "SELECT usr_MunicipalityId FROM tbl_Users WHERE usr_Id = @Id", new { Id = currentUserId });//find the Staff member's baladiye id
                if (myMunicipalityId == null)
                    return BadRequest("Staff member is not assigned to any baladiye.");

                if (await _connection.QueryFirstAsync<int>(
                    @"SELECT COUNT(*) FROM tbl_ReportAssignments
                      WHERE rpa_ReportId = @ReportId AND rpa_MunicipalityId = @MunicipalityId",//Check if this report belongs to that baladiye
                    new { ReportId = id, MunicipalityId = myMunicipalityId.Value }) == 0)
                    return StatusCode(403, "This report does not belong to your baladiye."); // 403 = logged in, but not allowed

                // ADDED — an undecided shared report is hidden from Staff in the lists,
                // so it must be blocked here too. 
                if (await _connection.QueryFirstAsync<int>(
                    "SELECT COUNT(*) FROM tbl_ReportAssignments WHERE rpa_ReportId = @ReportId",
                    new { ReportId = id }) > 1)
                    return StatusCode(403, "This report is shared between several baladiyat. An admin has not decided who handles it yet.");
            }

            // get the report details
            // CHANGED: also returns the reporter's name and role, and the exact
            // latitude/longitude, so the detail page can show WHO reported it and
            // where, instead of only an Id number.
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
            --nackedend send back long and latt since frontend need these not point
            tbl_Reports.rpt_Location.Lat AS Latitude,
            tbl_Reports.rpt_Location.Long AS Longitude
        FROM tbl_Reports
        INNER JOIN tbl_Categories ON tbl_Reports.rpt_CategoryId = tbl_Categories.ctg_Id
        INNER JOIN tbl_Users ON tbl_Reports.rpt_ReporterId = tbl_Users.usr_Id
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

            var voteCounts = new Dictionary<string, int>();//this sum the votes
            foreach (var voteRow in priorityVotesRaw)
            {
                string priorityName = Convert.ToString((object)voteRow.pvt_Priority) ?? "";
                int voteCount = Convert.ToInt32((object)voteRow.VoteCount);
                voteCounts[priorityName] = voteCount;
            }

            // build the breakdown — default 0 for any priority that has no votes
            // (TryGetValue writes 0 into the out variable when the key is missing)
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

            // ADDED — the comments people left on this report, newest last so the
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

            // the full status trail (Submitted → In Progress → Resolved),
            // written by UpdateReportStatus every time the status changes.
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


            var myPriorityVote = await _connection.QueryFirstOrDefaultAsync<string>(//what did user vote
                "SELECT pvt_Priority FROM tbl_PriorityVotes WHERE pvt_ReportId = @Id AND pvt_UserId = @UserId",
                new { Id = id, UserId = currentUserId });


            var myAgreement = await _connection.QueryFirstOrDefaultAsync<bool?>(
                "SELECT rga_IsAgreement FROM tbl_ReportAgreements WHERE rga_ReportId = @Id AND rga_UserId = @UserId",
                new { Id = id, UserId = currentUserId });

            // CHANGED: the response now carries Comments and StatusHistory too, so the
            // detail page can be filled from this ONE request instead of three.
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
    }
}
/*
{
  "Report": {
    "rpt_Id": 7,
    "rpt_Title": "Broken Street Light",
    "rpt_Status": "Submitted",
    "CategoryName": "Lighting",
    "ReporterName": "Ali"
  },

  "Assignments": [
    {
      "MunicipalityName": "Beirut",
      "rpa_IsHandler": true
    }
  ],

  "PriorityVotes": {
    "High": 4,
    "Medium": 2,
    "Low": 1,
    "Total": 7
  },

  "Comments": [
    ...
  ],

  "StatusHistory": [
    ...
  ],

  "MyPriorityVote": "High",

  "MyAgreement": true
}
*/
