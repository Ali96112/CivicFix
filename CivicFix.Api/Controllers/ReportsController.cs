using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CivicFix.Api.Models;
using Dapper;
using NetTopologySuite.Geometries;

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


[HttpPost] // address: api/Reports
public async Task<IActionResult> CreateReport([FromBody] CreateReportRequest request)
{
   var municipalitySql = @"
    SELECT Id, Name " +                          // get baladiye Id and Name
    "FROM Municipalities " +                     // from the Municipalities table
    "WHERE Boundary.STContains(" +               // does this baladiye's polygon contain...
    "geography::STPointFromText(" +              // convert plain numbers to a real map pin
    "'POINT(' + " +                              // start building the point text
    "CAST(@Longitude AS NVARCHAR) + ' ' + " +   // longitude number → text
    "CAST(@Latitude AS NVARCHAR) + ')'," +       // latitude number → text
    "4326)) = 1";                                // 4326 = GPS system, =1 means yes → responsible baladiye

    var municipality = await _connection.QueryFirstOrDefaultAsync<Municipality>(
        municipalitySql, new { request.Longitude, request.Latitude });//req.long the value that well be given to the @long...

    if (municipality == null)
        return BadRequest("Location does not fall within any registered baladiye.");

    // Step 2 — check if point is near a border (shared responsibility)
    var secondaryMunicipalitySql = @"
        SELECT Id, Name 
        FROM Municipalities 
        WHERE Boundary.STDistance(geography::STPointFromText(
            'POINT(' + CAST(@Longitude AS NVARCHAR) + ' ' + CAST(@Latitude AS NVARCHAR) + ')', 4326)) < 100
        AND Id != @MunicipalityId";

    var secondaryMunicipality = await _connection.QueryFirstOrDefaultAsync<Municipality>(
        secondaryMunicipalitySql, new { request.Longitude, request.Latitude, MunicipalityId = municipality.Id });

    // Step 3 — save the report
    var insertSql = @"
        INSERT INTO Reports (Title, Description, Status, CreatedAt, ReportedPhotoUrl, 
                            Location, ReporterId, MunicipalityId, SecondaryMunicipalityId, CategoryId)
        VALUES (@Title, @Description, @Status, @CreatedAt, @ReportedPhotoUrl,
                geography::STPointFromText('POINT(' + CAST(@Longitude AS NVARCHAR) + ' ' + CAST(@Latitude AS NVARCHAR) + ')', 4326),
                @ReporterId, @MunicipalityId, @SecondaryMunicipalityId, @CategoryId)";

    await _connection.ExecuteAsync(insertSql, new
    {
        request.Title,
        request.Description,
        Status = "Submitted",
        CreatedAt = DateTime.Now,
        request.ReportedPhotoUrl,
        request.Longitude,
        request.Latitude,
        request.ReporterId,
        MunicipalityId = municipality.Id,
        SecondaryMunicipalityId = secondaryMunicipality?.Id,
        request.CategoryId
    });

    return Ok("Report submitted successfully");
}



    }

}