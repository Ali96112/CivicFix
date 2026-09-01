using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;

namespace CivicFix.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MunicipalitiesController : ControllerBase
    {
        private readonly SqlConnection _connection;

        public MunicipalitiesController(SqlConnection connection)
        {
            _connection = connection;
        }

        [HttpGet] 
        public async Task<IActionResult> GetDashboard()
        {
            
            var sql = @"
                SELECT mun_Id, mun_Name, mun_TotalPoints
                FROM tbl_Municipalities
                ORDER BY mun_TotalPoints DESC";
            var municipalities = await _connection.QueryAsync<dynamic>(sql); 

            return Ok(municipalities);
        }
    }
}