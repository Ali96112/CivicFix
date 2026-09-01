using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;

namespace CivicFix.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // base address: api/Categories
    public class CategoriesController : ControllerBase
    {
        private readonly SqlConnection _connection;

        public CategoriesController(SqlConnection connection)
        {
            _connection = connection;
        }

        [HttpGet] // address: GET api/Categories — public, no login required
        public async Task<IActionResult> GetAllCategories()
        {
            // get all categories ordered by name
            var sql = "SELECT ctg_Id, ctg_Name FROM tbl_Categories ORDER BY ctg_Name";
            var categories = await _connection.QueryAsync<dynamic>(sql);
            return Ok(categories); // sends the whole list back as JSON for react
        }
    }
}