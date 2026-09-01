using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;

namespace CivicFix.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly SqlConnection _connection;

        public CategoriesController(SqlConnection connection)
        {
            _connection = connection;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllCategories()
        {
            
            var sql = "SELECT ctg_Id, ctg_Name FROM tbl_Categories ORDER BY ctg_Name";
            var categories = await _connection.QueryAsync<dynamic>(sql);
            return Ok(categories);
        }
    }
}