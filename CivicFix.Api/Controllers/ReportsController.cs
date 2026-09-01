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


        [Authorize(Roles = "Resident,Staff,Admin")]
        [HttpPost] // address: api/Reports
        public async Task<IActionResult> CreateReport([FromBody] CreateReportRequest request)
        {
            // Who is filing this report? Both facts come from the signed JWT, never
            // from the request body — a body field can be faked, a signed token cannot.
            // "User" is built into ControllerBase and represents the logged-in caller.
            var reporterRole = User.FindFirst(ClaimTypes.Role)?.Value;

            // TryParse, not int.Parse: a token without an "Id" claim gives a clean 400
            // instead of throwing and returning a 500.
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

                // check if report location is within staff's baladiye boundary + 100m tolerance
                //
                // ══════════════════════════════════════════════════════════════
                // FIXED — every spatial query in this file used to build the point
                // by gluing strings together:
                //
                //   geography::STPointFromText(
                //       'POINT(' + CAST(@Longitude AS NVARCHAR) + ' ' +
                //                  CAST(@Latitude  AS NVARCHAR) + ')', 4326)
                //
                // Two problems with that:
                //
                // 1) PRECISION LOSS. CAST(<float> AS NVARCHAR) with no style keeps
                //    only about 6 significant digits, so 35.501789 becomes "35.5018".
                //    Every location was rounded to roughly 4 decimal places — about
                //    10 metres — before it ever reached the polygon test. Near a
                //    boundary that is enough to land in the wrong baladiye, or to
                //    trip the 100m rule when it should not.
                //
                // 2) IT IS EASY TO GET THE ORDER BACKWARDS. WKT is POINT(long lat),
                //    the opposite of how people say "lat, long".
                //
                // geography::Point(@Latitude, @Longitude, 4326) takes the two numbers
                // as real floats — no text conversion, no rounding — and its argument
                // order is Latitude FIRST, which reads the way coordinates are spoken.
                // ══════════════════════════════════════════════════════════════
                // CHANGED: this used to be SELECT COUNT(*), which answered only
                // "yes" or "no". When it said no, the staff member got
                // "You can only submit reports within your baladiye boundaries."
                // and had no way to tell WHY — were they 5 metres out, or 40 km out,
                // or is the baladiye's polygon simply wrong? Now the query returns
                // the actual numbers, so the error message can say.
                var locationCheckSql = @"
                    SELECT
                        mun_Name,
                        -- 1 when the point is inside the polygon
                        mun_Boundary.STContains(
                            geography::Point(@Latitude, @Longitude, 4326)) AS ContainsPoint,
                        -- metres from the point to the polygon (0 when inside)
                        mun_Boundary.STDistance(
                            geography::Point(@Latitude, @Longitude, 4326)) AS DistanceMeters,
                        -- used to spot a polygon that was imported wound the wrong way
                        mun_Boundary.STArea() / 1000000.0 AS AreaSquareKm
                    FROM tbl_Municipalities
                    WHERE mun_Id = @MunicipalityId";

                var boundaryCheck = await _connection.QueryFirstOrDefaultAsync<dynamic>(
                    locationCheckSql, new { MunicipalityId = municipalityId, request.Longitude, request.Latitude });

                if (boundaryCheck == null)
                    return BadRequest("Your baladiye could not be found in the database.");

                string myBaladiyeName = (object?)boundaryCheck.mun_Name as string ?? "your baladiye";

                // STContains and STDistance both return NULL when mun_Boundary is NULL,
                // so guard before converting or this throws instead of explaining.
                object? containsRaw = (object?)boundaryCheck.ContainsPoint;
                object? distanceRaw = (object?)boundaryCheck.DistanceMeters;

                if (containsRaw == null || distanceRaw == null)
                    return BadRequest($"{myBaladiyeName} has no boundary polygon saved, so its area cannot be checked. Re-run the boundary seeder.");

                bool isInsideBoundary = Convert.ToInt32(containsRaw) == 1;
                double distanceMeters = Convert.ToDouble(distanceRaw);
                double areaSquareKm = Convert.ToDouble((object)boundaryCheck.AreaSquareKm);

                if (!isInsideBoundary && distanceMeters >= 100)
                {
                    // ADDED: say exactly how far off the location is. A few metres means
                    // the boundary is slightly imprecise; kilometres means either the
                    // staff member really is outside their baladiye, or the polygon is
                    // in the wrong place (usually lat/long swapped when it was imported).
                    var distanceText = distanceMeters >= 1000
                        ? $"{(distanceMeters / 1000):N1} km"
                        : $"{distanceMeters:N0} m";

                    // Lebanon is about 10,450 km2 in total. A single baladiye that
                    // claims more than 20,000 km2 has an inverted ring, which SQL Server
                    // reads as "the whole Earth except this area".
                    var polygonWarning = areaSquareKm > 20000
                        ? $" WARNING: {myBaladiyeName}'s boundary covers {areaSquareKm:N0} km2, which is impossible — that polygon is wound the wrong way and needs ReorientObject()."
                        : "";

                    return BadRequest(
                        $"You can only submit reports within your baladiye boundaries. " +
                        $"Your location is {distanceText} away from {myBaladiyeName} " +
                        $"(you must be inside it, or within 100 m of it).{polygonWarning}");
                }

                // assign only staff's own baladiye — no spatial query needed
                var staffMunicipalitySql = "SELECT mun_Id, mun_Name FROM tbl_Municipalities WHERE mun_Id = @Id";
                var staffMunicipality = await _connection.QueryFirstAsync<Municipality>(
                    staffMunicipalitySql, new { Id = municipalityId });

                // list has ONE item — staff's own baladiye only
                municipalities = new List<Municipality> { staffMunicipality };
            }
            else
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

                // ADDED: a Resident is allowed to SET THE PRIORITY when creating the report,
                // but only one of the three legal values. Before, any string went straight
                // into the database ("banana" would have been saved as a priority) and then
                // the ORDER BY CASE in the list queries silently dropped it to "ELSE 4".
                if (!string.IsNullOrEmpty(request.Priority) &&
                    request.Priority != "Low" && request.Priority != "Medium" && request.Priority != "High")
                {
                    return BadRequest("Priority must be Low, Medium, or High.");
                }
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
                // NOTE (ADDED, kept on purpose): the Staff query above has NO
                // "rpt_Status != 'Resolved'" filter, because staff reports are inserted
                // as 'Resolved' straight away — adding that filter would make duplicate
                // detection useless for staff. This is intentional, not a bug.
                : @"
                    SELECT TOP 1 rpt_Id
                    FROM tbl_Reports
                    INNER JOIN tbl_ReportAssignments ON rpt_Id = rpa_ReportId
                    WHERE rpt_Status != 'Resolved'
                    AND rpt_CategoryId = @CategoryId
                    AND rpt_CreatedAt > DATEADD(day, -20, GETDATE())
                    AND rpa_MunicipalityId IN (SELECT mun_Id FROM tbl_Municipalities
                        WHERE mun_Boundary.STContains(
                            geography::Point(@Latitude, @Longitude, 4326)) = 1)
                    AND rpt_Location.STDistance(
                        geography::Point(@Latitude, @Longitude, 4326)) < 30";

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
                // FIXED: was request.ReporterId (came from the body, so a user could
                // file a report in someone else's name). Now taken from the signed token.
                ReporterId = currentUserId,
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
            {
                // Staff only: keep a report if at least ONE of its assignments points
                // at the staff member's own baladiye. EXISTS is used (not a plain
                // WHERE on the join) so STRING_AGG still lists every baladiye.
                //
                // ADDED — the second condition hides UNDECIDED SHARED REPORTS.
                //
                // A report that landed on a border is assigned to 2 or 3 baladiyat at
                // once and nobody owns it yet. Showing it to all of them means three
                // staff members each thinking it might be someone else's job. So it
                // stays hidden from every baladiye until an Admin allocates it on the
                // Shared Reports screen.
                //
                // Why counting rows is enough: AssignHandler DELETES the losing
                // baladiyat when the Admin picks one. So the count tells you the state
                // directly —
                //     2 or more rows = still shared, nobody decided
                //     exactly 1 row  = decided (or was never shared)
                // which is why there is no need to look at rpa_IsHandler here.
                whereClause = @"WHERE EXISTS (
                            SELECT 1 FROM tbl_ReportAssignments AS my_assignment
                            WHERE my_assignment.rpa_ReportId = tbl_Reports.rpt_Id
                            AND my_assignment.rpa_MunicipalityId =
                                (SELECT usr_MunicipalityId FROM tbl_Users WHERE usr_Id = @userId))
                          AND (SELECT COUNT(*) FROM tbl_ReportAssignments
                               WHERE rpa_ReportId = tbl_Reports.rpt_Id) = 1 ";
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

            // ADDED: stitch the three parts together, same idea as GetMyReports
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
                // resident sees only reports they submitted
                whereClause = "WHERE tbl_Reports.rpt_ReporterId = @userId ";
            }
            else if (role == "Staff")
            {
                // staff sees only reports assigned to their baladiye
                // subquery gets their MunicipalityId from tbl_Users
                //
                // FIXED: this used to be
                //   WHERE tbl_ReportAssignments.rpa_MunicipalityId = (SELECT ...)
                // which filtered the JOINED rows. Because STRING_AGG runs AFTER the
                // WHERE, a report shared by 2 baladiyat would only show ONE name in
                // AssignedMunicipalities. EXISTS filters the REPORT instead of the
                // joined rows, so the full list of baladiyat is still aggregated.
                //
                // ADDED — same "hide undecided shared reports" rule as GetAllReports.
                // Both list endpoints need it, or the report simply reappears on the
                // other tab and the rule achieves nothing.
                whereClause = @"WHERE EXISTS (
                            SELECT 1 FROM tbl_ReportAssignments AS my_assignment
                            WHERE my_assignment.rpa_ReportId = tbl_Reports.rpt_Id
                            AND my_assignment.rpa_MunicipalityId =
                                (SELECT usr_MunicipalityId FROM tbl_Users WHERE usr_Id = @userId))
                          AND (SELECT COUNT(*) FROM tbl_ReportAssignments
                               WHERE rpa_ReportId = tbl_Reports.rpt_Id) = 1 ";
            }
            else
            {
                // admin sees everything — no filter
                whereClause = "";
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
        // FIXED — added the ":int" route constraint.
        //
        // Without it this route matched ANY text, including the word "shared".
        // So if a named route above (mine / shared) is missing — for example when
        // the API has not been rebuilt after adding one — the request silently fell
        // through to here, ASP.NET tried to stuff "shared" into `int id`, model
        // binding failed, and [ApiController] answered 400 Bad Request. That error
        // says nothing about the real problem and is very confusing to debug.
        //
        // With ":int" this route only matches numbers, so a missing named route
        // now returns a plain, honest 404 instead of a misleading 400.
        [HttpGet("{id:int}")] // address: GET api/Reports/1
        public async Task<IActionResult> GetReportById(int id)
        {
            // ADDED: same rule as the list above — a Staff member must not be able to
            // open a report from another baladiye just by typing its Id in the URL.
            // Resident and Admin can open any report.
            // who is asking? Straight from the signed JWT, never the request body.
            // TryParse, not int.Parse: a missing claim gives a 400, not a 500 crash.
            var idClaim = User.FindFirst("Id")?.Value;
            if (!int.TryParse(idClaim, out int currentUserId))
                return BadRequest("Could not read user Id from token. Claim 'Id' not found.");

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
                    return StatusCode(403, "This report does not belong to your baladiye."); // 403 = logged in, but not allowed

                // ADDED — an undecided shared report is hidden from Staff in the lists,
                // so it must be blocked here too. Hiding a card does nothing if the
                // report can still be opened by typing /report/13 in the address bar.
                // 2 or more assignment rows = the Admin has not allocated it yet.
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
            -- ADDED: the point is stored as a geography type, which cannot be sent
            -- as JSON directly. .Lat and .Long pull out the plain numbers so React
            -- can show them (and later drop a pin on a map).
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

            // ADDED: copy the `dynamic` rows into a plain Dictionary<string,int> first.
            // The old code compared dynamics inside LINQ lambdas
            // (v.pvt_Priority == "High" and v.Sum(v => (int)v.VoteCount)), which the
            // compiler cannot check — those only fail at RUNTIME with a
            // RuntimeBinderException. A simple foreach with explicit Convert calls is
            // boring, but it is checked at build time and it is obvious what it does.
            var voteCounts = new Dictionary<string, int>();
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
            // detail page can read them top to bottom like a conversation.
            // Joined to tbl_Users so each comment shows a name, not a user Id.
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

            // ADDED — the full status trail (Submitted → In Progress → Resolved),
            // written by UpdateReportStatus every time the status changes.
            // This is what makes the detail page show the report's whole life,
            // and it is the accountability record: who changed what, and when.
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

            // ══════════════════════════════════════════════════════════════════
            // ADDED — what has THIS user already done on THIS report?
            //
            // WHY: a resident may vote on priority once, and agree once. The
            // backend already enforces that (VoteOnPriority and AgreeOnReport both
            // reject a second attempt), but the browser had no way of knowing
            // beforehand — so the buttons looked available and only failed after
            // being clicked. Returning the user's own vote lets React show
            // "You voted High" instead of a button that is guaranteed to fail.
            //
            // Both are read with the Id from the TOKEN, so this only ever tells you
            // about your own vote, never anyone else's.
            // ══════════════════════════════════════════════════════════════════
            var myPriorityVote = await _connection.QueryFirstOrDefaultAsync<string>(
                "SELECT pvt_Priority FROM tbl_PriorityVotes WHERE pvt_ReportId = @Id AND pvt_UserId = @UserId",
                new { Id = id, UserId = currentUserId });

            // bool? — true = agreed, false = disagreed, null = has not voted at all.
            // The three states matter: null must not be confused with "disagreed".
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
                Comments = comments,             // ADDED
                StatusHistory = statusHistory,   // ADDED
                MyPriorityVote = myPriorityVote, // ADDED: "Low"/"Medium"/"High", or null
                MyAgreement = myAgreement        // ADDED: true / false / null
            });
        }
    }
}
