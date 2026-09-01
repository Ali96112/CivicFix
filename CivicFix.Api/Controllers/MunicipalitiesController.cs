using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;

namespace CivicFix.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // base address: api/Municipalities
    public class MunicipalitiesController : ControllerBase
    {
        private readonly SqlConnection _connection;

        public MunicipalitiesController(SqlConnection connection)
        {
            _connection = connection;
        }

        [HttpGet] // address: GET api/Municipalities — public, no login required
        public async Task<IActionResult> GetDashboard()
        {
            // get all baladiyat ordered by total points — highest score first
            var sql = @"
                SELECT mun_Id, mun_Name, mun_TotalPoints
                FROM tbl_Municipalities
                ORDER BY mun_TotalPoints DESC";
            //Use dynamic when the SQL result does not fit neatly into one existing model, or when you only need a small custom result and don’t want to create a new class for it.
            var municipalities = await _connection.QueryAsync<dynamic>(sql);//“Dapper, don’t map this result to a specific C# class. Just give me objects with whatever columns this SQL returned.”

            return Ok(municipalities);//Ok(municipalities) sends the whole list back as JSON for react
        }
    }
}