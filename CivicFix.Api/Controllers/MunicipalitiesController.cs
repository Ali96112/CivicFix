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

            var municipalities = await _connection.QueryAsync<dynamic>(sql);//dynamic since it return 3 columns(join) instead of the municipalities object

            return Ok(municipalities);//Ok(municipalities) sends the whole list back as JSON for react
        }
    }
}