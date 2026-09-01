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
    // DIAGNOSTICS — a debugging tool, not a feature.
    //
    // location-check answers "which baladiyat does this point belong to, and why",
    // which is how the Akkar/Beirut assignment problem was tracked down. Kept in
    // its own file so it is obvious that nothing in the app depends on it.
    // ══════════════════════════════════════════════════════════════════════════
    [ApiController]
    // NOT [Route("api/[controller]")] — that token expands to the class name, so
    // this file would answer on api/ReportsDiagnostics and every URL below would change.
    // Written out in full, these endpoints keep the exact addresses they had when
    // they all lived in one ReportsController.
    [Route("api/Reports")]
    public class ReportsDiagnosticsController : ControllerBase
    {
        private readonly SqlConnection _connection;

        public ReportsDiagnosticsController(SqlConnection connection)
        {
            _connection = connection;
        }


        // ══════════════════════════════════════════════════════════════════════
        // NEW ENDPOINT — "why did my location go to the wrong baladiye?"
        //
        // This is a DIAGNOSTIC endpoint. It does not create or change anything —
        // it runs exactly the same spatial tests CreateReport runs, and shows you
        // the raw numbers instead of just accepting or rejecting the report.
        //
        // Call it straight from the browser address bar while logged in, or from
        // the DevTools console:
        //
        //   fetch("http://localhost:5140/api/Reports/location-check?latitude=33.8938&longitude=35.5018",
        //     { headers: { Authorization: "Bearer " + localStorage.getItem("token") } })
        //     .then(r => r.json()).then(console.log)
        //
        // WHAT THE ANSWER TELLS YOU:
        //   * MyBaladiye.ContainsPoint = false and DistanceMeters in the thousands
        //       → you really are outside your baladiye, OR its polygon sits in the
        //         wrong place (usually lat/long swapped when the boundaries were seeded)
        //   * a baladiye with AreaSquareKm in the hundreds of thousands
        //       → that polygon is wound the wrong way. SQL Server reads it as
        //         "the whole Earth except this area", so it matches everything —
        //         this is what makes every report land on the same baladiye
        //   * MatchingBaladiyat is empty
        //       → the seed data does not cover this point at all
        // ══════════════════════════════════════════════════════════════════════
        [Authorize] // any logged-in role — it only reads, and only what you ask about
        [HttpGet("location-check")] // address: GET api/Reports/location-check?latitude=..&longitude=..
        public async Task<IActionResult> LocationCheck([FromQuery] double latitude, [FromQuery] double longitude)
        {
            // basic sanity check — a swapped pair often shows up here immediately,
            // because a longitude value put into latitude is usually still in range,
            // but the resulting point lands in the sea.
            if (latitude < -90 || latitude > 90)
                return BadRequest("Latitude must be between -90 and 90. If you passed a longitude here, the two are swapped.");

            if (longitude < -180 || longitude > 180)
                return BadRequest("Longitude must be between -180 and 180.");

            // the baladiyat that CreateReport would pick for this point:
            // the exact same "contains OR within 100 m" rule
            var matchingSql = @"
                SELECT
                    mun_Id,
                    mun_Name,
                    mun_Boundary.STContains(geography::Point(@Latitude, @Longitude, 4326)) AS ContainsPoint,
                    mun_Boundary.STDistance(geography::Point(@Latitude, @Longitude, 4326)) AS DistanceMeters,
                    mun_Boundary.STArea() / 1000000.0 AS AreaSquareKm
                FROM tbl_Municipalities
                WHERE mun_Boundary.STContains(geography::Point(@Latitude, @Longitude, 4326)) = 1
                   OR mun_Boundary.STDistance(geography::Point(@Latitude, @Longitude, 4326)) < 100
                ORDER BY mun_Boundary.STDistance(geography::Point(@Latitude, @Longitude, 4326))";

            var matching = await _connection.QueryAsync<dynamic>(
                matchingSql, new { Latitude = latitude, Longitude = longitude });

            // the five nearest baladiyat regardless of the 100 m rule, so you can see
            // what SHOULD have matched and how far away it is
            var nearestSql = @"
                SELECT TOP 5
                    mun_Id,
                    mun_Name,
                    mun_Boundary.STContains(geography::Point(@Latitude, @Longitude, 4326)) AS ContainsPoint,
                    mun_Boundary.STDistance(geography::Point(@Latitude, @Longitude, 4326)) AS DistanceMeters,
                    mun_Boundary.STArea() / 1000000.0 AS AreaSquareKm,
                    mun_Boundary.EnvelopeCenter().Lat  AS BoundaryCenterLat,
                    mun_Boundary.EnvelopeCenter().Long AS BoundaryCenterLong
                FROM tbl_Municipalities
                ORDER BY mun_Boundary.STDistance(geography::Point(@Latitude, @Longitude, 4326))";

            var nearest = await _connection.QueryAsync<dynamic>(
                nearestSql, new { Latitude = latitude, Longitude = longitude });

            // any polygon big enough to be impossible. Lebanon is ~10,450 km2 in
            // total, and the whole Earth is ~510,000,000 km2, so anything over
            // 20,000 km2 is an inverted ring rather than a big baladiye.
            var invertedSql = @"
                SELECT mun_Id, mun_Name, mun_Boundary.STArea() / 1000000.0 AS AreaSquareKm
                FROM tbl_Municipalities
                WHERE mun_Boundary.STArea() / 1000000.0 > 20000
                ORDER BY mun_Boundary.STArea() DESC";

            var inverted = await _connection.QueryAsync<dynamic>(invertedSql);

            // if the caller is Staff, also test their OWN baladiye specifically —
            // this is the exact check that blocks them from submitting
            object? myBaladiye = null;
            // read the Id from the token; 0 means the claim was missing or unreadable
            int.TryParse(User.FindFirst("Id")?.Value, out int currentUserId);

            if (User.FindFirst(ClaimTypes.Role)?.Value == "Staff" && currentUserId > 0)
            {
                var myMunicipalityId = await _connection.QueryFirstOrDefaultAsync<int?>(
                    "SELECT usr_MunicipalityId FROM tbl_Users WHERE usr_Id = @Id", new { Id = currentUserId });

                if (myMunicipalityId != null)
                {
                    var mineSql = @"
                        SELECT
                            mun_Id,
                            mun_Name,
                            mun_Boundary.STContains(geography::Point(@Latitude, @Longitude, 4326)) AS ContainsPoint,
                            mun_Boundary.STDistance(geography::Point(@Latitude, @Longitude, 4326)) AS DistanceMeters,
                            mun_Boundary.STArea() / 1000000.0 AS AreaSquareKm,
                            mun_Boundary.EnvelopeCenter().Lat  AS BoundaryCenterLat,
                            mun_Boundary.EnvelopeCenter().Long AS BoundaryCenterLong
                        FROM tbl_Municipalities
                        WHERE mun_Id = @Id";

                    myBaladiye = await _connection.QueryFirstOrDefaultAsync<dynamic>(
                        mineSql, new { Id = myMunicipalityId, Latitude = latitude, Longitude = longitude });
                }
            }

            return Ok(new
            {
                PointTested = new { Latitude = latitude, Longitude = longitude },
                // Lebanon sits roughly at lat 33.0–34.7, long 35.1–36.6.
                // If this says false, the two numbers are probably swapped.
                PointLooksLikeLebanon = latitude >= 33.0 && latitude <= 34.8
                                        && longitude >= 35.0 && longitude <= 36.7,
                MyBaladiye = myBaladiye,             // null unless you are Staff
                MatchingBaladiyat = matching,        // what CreateReport would assign
                FiveNearestBaladiyat = nearest,      // what should have matched
                InvertedPolygons = inverted          // any of these breaks everything
            });
        }
    }
}
