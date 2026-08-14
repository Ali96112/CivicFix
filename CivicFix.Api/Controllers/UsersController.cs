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
    [ApiController]//that controles an missing email so you dont write error message
    [Route("api/[controller]")] //urls.py so path here api/Users/
    public class UsersController : ControllerBase //ControllerBase is inheriting ready made behavior like: sending responds ok(),unauthorized()
    {
        private readonly SqlConnection _connection; // direct raw SQL connection Dapper use to know where to go with querys
        private readonly IConfiguration _configuration; // reads appsettings.json

        public UsersController(SqlConnection connection, IConfiguration configuration) //so the UserController is created it run this method
        {//NET creates the controller, calls the constructor once, both objects are stored in _connection and _configuration — every method in the class can then use them freely without setting them up again.
            _connection = connection;  // the one object your controller uses to talk to the database.
            _configuration = configuration;//the one object your controller uses to talk to the setting
        }

        [HttpPost("register")] // api/Users/register
        public async Task<IActionResult> Register([FromBody] User newUser) // [FromBody]the user that was registered well be under name newuser.fullname
        {//async Task<IActionResult> dont let system frezze waiting for that one request
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(newUser.usr_PasswordHash);// scramble the password before saving
            var sql = @"INSERT INTO tbl_Users (usr_FullName, usr_Email, usr_PasswordHash, usr_Role, usr_NationalId)
            VALUES (@FullName, @Email, @PasswordHash, @Role, @NationalId)";
            await _connection.ExecuteAsync(sql, new
            {//ExecuteAsync is used for INSERT/UPDATE/DELETE
                FullName = newUser.usr_FullName,
                Email = newUser.usr_Email,
                PasswordHash = hashedPassword,
                Role = "Resident",
                NationalId = newUser.usr_NationalId
            });
            return Ok("User registered successfully");
        }

        [HttpPost("login")]
        // This method handles POST requests sent to: api/Users/login
        public async Task<IActionResult> Login([FromBody] LoginRequest request)//// "request" now holds the Email and Password that was sent in

        {
            var sql = "SELECT * FROM tbl_Users WHERE usr_Email = @Email";
            //QueryFirstOrDefaultAsync → returns one row or null
            var user = await _connection.QueryFirstOrDefaultAsync<User>(sql, new { request.Email });//user is assigned to database
            //We used <User> because we knew exactly what shape was coming back — one row from the Users table, which maps perfectly to your User class
            if (user == null)//Nobody found with that email → stop here, reject with 401.
                return Unauthorized("Invalid email or password");

            bool passwordMatches = BCrypt.Net.BCrypt.Verify(request.Password, user.usr_PasswordHash);//it scramble the entered pass to compare it with hashed
            if (!passwordMatches)
                return Unauthorized("Invalid email or password");
            // If it's false, reject here

            // If we reach this line, both email and password were correct
            // now generate JWT token
            var claims = new[] //the info well be inside the token so that it doesnt go to database each time
            {
                new Claim("Id", user.usr_Id.ToString()),//claim just stores strings
                new Claim("FullName", user.usr_FullName),
                new Claim(ClaimTypes.Role, user.usr_Role)
            };

            var key = new SymmetricSecurityKey(//Reads the secret key(system key) from appsettings convert it to bytes since jwt like this works wraps it to an object jwt understand
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));//_configuration["Jwt:Key"] — reads "CivicFixSuperSecretKey2026Lebanon encoding convert it

            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);//the seal if someone try to change tooken it brokes

            var token = new JwtSecurityToken(  // create the token object with all its pieces
                issuer: _configuration["Jwt:Issuer"],       // who created this token → reads "CivicFix" from appsettings.json
                audience: _configuration["Jwt:Audience"],   // who this token is for → reads "CivicFixUsers" from appsettings.json
                claims: claims,                             // the user's facts baked inside (Id, FullName, Role)
                expires: DateTime.Now.AddHours(12),         // token dies 12 hours from now — stolen tokens don't work forever
                signingCredentials: credentials             // the seal — if token is tampered with, this breaks and token is rejected
            );

            string tokenString = new JwtSecurityTokenHandler().WriteToken(token);//token object is not a real text so we convert it to string(tokenString) to travel to frontend

            // Send back the token plus basic user info to frontend
            return Ok(new { token = tokenString, user.usr_Id, user.usr_FullName, user.usr_Role });
        }


        [HttpPost("forgot-password")] // address: api/Users/forgot-password
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
    // Step 1 — check if user exists with this email
    var sql = "SELECT * FROM tbl_Users WHERE usr_Email = @Email";
    var user = await _connection.QueryFirstOrDefaultAsync<User>(sql, new { request.Email });

    if (user == null)
        return NotFound("No account found with this email.");

    // Step 2 — generate a random one-time token
    string token = Guid.NewGuid().ToString(); // generates a unique random string like "d4f8a3b2-1c6e-..."

    // Step 3 — save the token to the database with 1 hour expiry
    var insertSql = @"
        INSERT INTO tbl_PasswordReset (pwr_Token, pwr_ExpiresAt, pwr_IsUsed, pwr_UserId)
        VALUES (@Token, @ExpiresAt, 0, @UserId)";

    await _connection.ExecuteAsync(insertSql, new
    {
        Token = token,
        ExpiresAt = DateTime.Now.AddHours(1), // expires in 1 hour
        UserId = user.usr_Id
    });

    // for now return the token directly
    return Ok(new { message = "Password reset token generated.", token = token });
    }


        [HttpPost("reset-password")] // address: api/Users/reset-password
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
{
    // Step 1 — find the token in the database
    var tokenSql = @"
        SELECT * FROM tbl_PasswordReset 
        WHERE pwr_Token = @Token";//when here it found the token it well know the user id

    var resetRecord = await _connection.QueryFirstOrDefaultAsync<PasswordReset>(
        tokenSql, new { request.Token });

    // Step 2 — check token exists
    if (resetRecord == null)
        return BadRequest("Invalid token.");

    // Step 3 — check token hasn't expired
    if (resetRecord.pwr_ExpiresAt < DateTime.Now)
        return BadRequest("Token has expired.");

    // Step 4 — check token hasn't been used before
    if (resetRecord.pwr_IsUsed)
        return BadRequest("Token has already been used.");

    // Step 5 — hash the new password and update it
    string hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

    var updateSql = "UPDATE tbl_Users SET usr_PasswordHash = @PasswordHash WHERE usr_Id = @UserId";
    await _connection.ExecuteAsync(updateSql, new
    {
        PasswordHash = hashedPassword,
        UserId = resetRecord.pwr_UserId //resetRecord contin the full row of table tbl_PasswordReset ehich already contain user id
    });

    // Step 6 — mark the token as used so it can't be reused for security reasons
    var markUsedSql = "UPDATE tbl_PasswordReset SET pwr_IsUsed = 1 WHERE pwr_Id = @Id";
    await _connection.ExecuteAsync(markUsedSql, new { Id = resetRecord.pwr_Id });

    return Ok("Password reset successfully.");
}





    }
}