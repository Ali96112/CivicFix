using Microsoft.AspNetCore.Mvc;
using CivicFix.Api.Data;
using CivicFix.Api.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CivicFix.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")] //urls.py so path here api/Users/
    public class UsersController : ControllerBase//userController is just inherting from controler lie inherting views functions
    {
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context)
        {
            _context = context;
        }
    

        [HttpPost("register")]//like urls.py file it became api/Users/register
        public IActionResult Register([FromBody] User newUser)//isAction method return some web response (success, error, etc.)FromBody] User newUser means: take the incoming JSON data and automatically fill a User object with it, called newUser.
        {
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(newUser.PasswordHash);

            var sql = @"INSERT INTO Users (FullName, Email, PasswordHash, Role)
                        VALUES (@FullName, @Email, @PasswordHash, @Role)";

            _context.Database.ExecuteSqlRaw(sql,
                new SqlParameter("@FullName", newUser.FullName),
                new SqlParameter("@Email", newUser.Email),
                new SqlParameter("@PasswordHash", hashedPassword),
                new SqlParameter("@Role", "Resident")
            );

            return Ok("User registered successfully");
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)//takes the incoming email + password and fills a LoginRequest object called request
        {
            var users = _context.Users
                .FromSqlRaw("SELECT * FROM Users WHERE Email = {0}", request.Email)
                .ToList();

            if (users.Count == 0)
                return Unauthorized("Invalid email or password");

            var user = users[0];

            bool passwordMatches = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

            if (!passwordMatches)
                return Unauthorized("Invalid email or password");

            return Ok(new { user.Id, user.FullName, user.Role });
        }




    }
}
