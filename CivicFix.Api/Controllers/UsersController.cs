using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CivicFix.Api.Models;
using Dapper;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace CivicFix.Api.Controllers
{
    [ApiController]//that contoles an missing email so you dont write error message
    [Route("api/[controller]")] //urls.py so path here api/Users/
    public class UsersController : ControllerBase //ControllerBase is inheriting ready made behavior like:ok(),unauthorized()
    {
        private readonly SqlConnection _connection; // direct raw SQL connection Dapper use
        private readonly IConfiguration _configuration; // reads appsettings.json

        public UsersController(SqlConnection connection, IConfiguration configuration) //so the UserController is created it run this method
        {
            _connection = connection;  // the one object your controller uses to talk to the database.
            _configuration = configuration;//the one object your controller uses to talk to the setting
        }



        [HttpPost("register")] // api/Users/register
        public async Task<IActionResult> Register([FromBody] User newUser) // [FromBody]the user that was registered well be under name newuser.fullname
        {//async Task<IActionResult> dont let system frezze waiting for that one request
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(newUser.PasswordHash);// scramble the password before saving
            var sql = @"INSERT INTO Users (FullName, Email, PasswordHash, Role)
                        VALUES (@FullName, @Email, @PasswordHash, @Role)";
            await _connection.ExecuteAsync(sql, new //// Dapper fills placeholders automatically from the anonymous object
            {
                newUser.FullName,
                newUser.Email,
                PasswordHash = hashedPassword,
                Role = "Resident" // always Resident, never from user input
            });
            return Ok("User registered successfully");
        }



        [HttpPost("login")]
        // This method handles POST requests sent to: api/Users/login
        public async Task<IActionResult> Login([FromBody] LoginRequest request)//// "request" now holds the Email and Password that was sent in
        {
            var sql = "SELECT * FROM Users WHERE Email = @Email";
            // Dapper runs the SQL and maps result directly into a User object
            // returns null if no match found              QueryFirstOrDefaultAsync → returns one row or null
            var user = await _connection.QueryFirstOrDefaultAsync<User>(sql, new { request.Email });//user is assigned to database
            //We used <User> because we knew exactly what shape was coming back — one row from the Users table, which maps perfectly to your User class
            if (user == null)//Nobody found with that email → stop here, reject with 401.
                return Unauthorized("Invalid email or password");

            bool passwordMatches = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);//it scramble the entered pass to compare it with hashed
            if (!passwordMatches)
                return Unauthorized("Invalid email or password");
            // If it's false, reject here


            // If we reach this line, both email and password were correct
            // now generate JWT token
            var claims = new[] //the info well be inside the token so that it doesnt go to database each time
            {
                new Claim("Id", user.Id.ToString()),
                new Claim("FullName", user.FullName),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var key = new SymmetricSecurityKey(//Reads the secret key from appsettings convert it to bytes since jwt like this works wraps it to an object jwt understand
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));

            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);//the seal if someone try to change tooken it brokes

            var token = new JwtSecurityToken(//Assembles everything into one token object
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(12),
                signingCredentials: credentials
            );

            string tokenString = new JwtSecurityTokenHandler().WriteToken(token);//tokwn object is not a real text so we convet it to string(tokenString) to travel to frontend

            // Send back the token plus basic user info to frontyend
            return Ok(new { token = tokenString, user.Id, user.FullName, user.Role });
        }
    }
}