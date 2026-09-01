using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CivicFix.Api.Models;
using Dapper;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization; // ADDED: needed for [Authorize] on GetMe below
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CivicFix.Api.Services;

namespace CivicFix.Api.Controllers
{
    [ApiController]//that controles an missing email so you dont write error message
    [Route("api/[controller]")] //urls.py so path here api/Users/
    public class UsersController : ControllerBase //ControllerBase is inheriting ready made behavior like: sending responds ok(),unauthorized()
    {
        private readonly SqlConnection _connection;
        private readonly IConfiguration _configuration;
        private readonly EmailSender _emailSender;

        public UsersController(SqlConnection connection, IConfiguration configuration, EmailSender emailSender)
        {
            _connection = connection;
            _configuration = configuration;
            _emailSender = emailSender;
        }

        [HttpPost("register")] // api/Users/register
        public async Task<IActionResult> Register([FromBody] User newUser) // [FromBody] the user that was registered will be under name newUser.usr_FullName
        {// async Task<IActionResult> dont let system freeze waiting for that one request

            // scramble the password before saving
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(newUser.usr_PasswordHash);

            // insert the new user and return their Id immediately
            var sql = @"INSERT INTO tbl_Users (usr_FullName, usr_Email, usr_PasswordHash, usr_Role, usr_NationalId)
                OUTPUT INSERTED.usr_Id
                VALUES (@FullName, @Email, @PasswordHash, @Role, @NationalId)";

            var userId = await _connection.QueryFirstAsync<int>(sql, new
            {// ExecuteAsync is used for INSERT/UPDATE/DELETE — QueryFirstAsync because we need the Id back
                FullName = newUser.usr_FullName,
                Email = newUser.usr_Email,
                PasswordHash = hashedPassword,
                Role = "Resident",
                NationalId = newUser.usr_NationalId
            });

            // generate JWT token immediately after registration — same as login
            var claims = new[]
            {
        new Claim("Id", userId.ToString()),           // user's Id
        new Claim("FullName", newUser.usr_FullName),  // user's full name
        new Claim(ClaimTypes.Role, "Resident")         // role is always Resident on register
    };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));

            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(12), // token valid for 12 hours
                signingCredentials: credentials
            );

            string tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            // return token + user info so React can store and redirect
            return Ok(new
            {
                token = tokenString,
                usr_Id = userId,
                usr_FullName = newUser.usr_FullName,
                usr_Role = "Resident"
            });
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

            if (user.usr_IsBlocked)
                return Unauthorized("Your account has been blocked.");

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

           
            // build the reset link pointing to your frontend reset page, with the token attached
            var resetLink = $"http://localhost:5173/reset-password?token={token}";

            // the email body — HTML so the link is clickable
            var emailBody = $@"
                <h2>CivicFix Password Reset</h2>
                <p>Hello {user.usr_FullName},</p>
                <p>You requested to reset your password. Click below to set a new one:</p>
                <p><a href='{resetLink}'>Reset My Password</a></p>
                <p>This link expires in 1 hour.</p>
                <p>If you didn't request this, ignore this email.</p>";

            // send the email to the user's own email address (from the database)
            await _emailSender.SendEmailAsync(user.usr_Email, "CivicFix Password Reset", emailBody);

            // tell the frontend it worked — the token is NOT returned anymore, it's in the email
            return Ok(new { message = "A password reset link has been sent to your email." });
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



        [Authorize(Roles = "Staff")]
        [HttpGet("me")] // address: GET api/Users/me
        public async Task<IActionResult> GetMe()
        {
            var idClaim = User.FindFirst("Id")?.Value;

            if (!int.TryParse(idClaim, out int userId))//id chanding icliam from string to int send bade request
                return BadRequest("Could not read user Id from token. Claim 'Id' not found.");

            var sql = @"
                SELECT
                    tbl_Users.usr_Id,
                    tbl_Users.usr_FullName,
                    tbl_Users.usr_Role,
                    tbl_Users.usr_MunicipalityId,
                    tbl_Municipalities.mun_Name AS MunicipalityName,
                    tbl_Municipalities.mun_TotalPoints AS MunicipalityPoints
                FROM tbl_Users
                LEFT JOIN tbl_Municipalities
                    ON tbl_Users.usr_MunicipalityId = tbl_Municipalities.mun_Id
                WHERE tbl_Users.usr_Id = @Id";

            var me = await _connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { Id = userId });

            if (me == null)
                return NotFound("User not found.");

            return Ok(me);
        }



        [Authorize(Roles = "Admin")]
        [HttpPut("{id}/block")] // PUT api/Users/1/block
        public async Task<IActionResult> BlockUser(int id)
        {
            // 1 — mark the user as blocked
            await _connection.ExecuteAsync(
                "UPDATE tbl_Users SET usr_IsBlocked = 1 WHERE usr_Id = @Id",
                new { Id = id });

            // 2 — find their "Submitted" reports
            var reportIdsSql = @"SELECT rpt_Id FROM tbl_Reports 
                         WHERE rpt_ReporterId = @Id AND rpt_Status = 'Submitted'";
            var reportIds = (await _connection.QueryAsync<int>(reportIdsSql, new { Id = id })).ToList();

            // 3 — delete the children of those reports, then the reports (order matters)
            if (reportIds.Count > 0)
            {
                await _connection.ExecuteAsync(
                    "DELETE FROM tbl_ReportAgreements WHERE rga_ReportId IN @Ids", new { Ids = reportIds });
                await _connection.ExecuteAsync(
                    "DELETE FROM tbl_PriorityVotes WHERE pvt_ReportId IN @Ids", new { Ids = reportIds });
                await _connection.ExecuteAsync(
                    "DELETE FROM tbl_Comments WHERE cmt_ReportId IN @Ids", new { Ids = reportIds });
                await _connection.ExecuteAsync(
                    "DELETE FROM tbl_StatusHistories WHERE sth_ReportId IN @Ids", new { Ids = reportIds });
                await _connection.ExecuteAsync(
                    "DELETE FROM tbl_ReportAssignments WHERE rpa_ReportId IN @Ids", new { Ids = reportIds });
                await _connection.ExecuteAsync(
                    "DELETE FROM tbl_Reports WHERE rpt_Id IN @Ids", new { Ids = reportIds });
            }

            return Ok("User blocked and their submitted reports deleted.");
        }


    }
}