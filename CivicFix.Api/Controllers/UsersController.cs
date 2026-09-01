using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CivicFix.Api.Models;
using Dapper;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CivicFix.Api.Services;

namespace CivicFix.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")] 
    public class UsersController : ControllerBase
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

        [HttpPost("register")] 
        public async Task<IActionResult> Register([FromBody] User newUser)
         {
            if (string.IsNullOrWhiteSpace(newUser.usr_FullName) ||
                string.IsNullOrWhiteSpace(newUser.usr_Email) ||
                string.IsNullOrWhiteSpace(newUser.usr_PasswordHash))
                return BadRequest("Full name, email, and password are required.");

            if (!newUser.usr_Email.Contains("@") || !newUser.usr_Email.Contains("."))
                return BadRequest("Please enter a valid email.");

            if (newUser.usr_PasswordHash.Length < 8)
                return BadRequest("Password must be at least 8 characters.");

            var existing = await _connection.QueryFirstOrDefaultAsync<int?>(
                "SELECT usr_Id FROM tbl_Users WHERE usr_Email = @Email",
                new { Email = newUser.usr_Email });

            if (existing != null)
                return BadRequest("An account with this email already exists.");

            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(newUser.usr_PasswordHash);

            var sql = @"INSERT INTO tbl_Users (usr_FullName, usr_Email, usr_PasswordHash, usr_Role, usr_PhoneNumber)
        OUTPUT INSERTED.usr_Id
        VALUES (@FullName, @Email, @PasswordHash, @Role, @PhoneNumber)";

            var userId = await _connection.QueryFirstAsync<int>(sql, new
            {
                FullName = newUser.usr_FullName,
                Email = newUser.usr_Email,
                PasswordHash = hashedPassword,
                Role = "Resident",
                PhoneNumber = newUser.usr_PhoneNumber
            });

            var claims = new[]
            {
        new Claim("Id", userId.ToString()),
        new Claim("FullName", newUser.usr_FullName),
        new Claim(ClaimTypes.Role, "Resident")
    };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));

            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(12),
                signingCredentials: credentials
            );

            string tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new
            {
                token = tokenString,
                usr_Id = userId,
                usr_FullName = newUser.usr_FullName,
                usr_Role = "Resident"
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var sql = "SELECT * FROM tbl_Users WHERE usr_Email = @Email";
            var user = await _connection.QueryFirstOrDefaultAsync<User>(sql, new { request.Email });
            if (user == null)
                return Unauthorized("Invalid email or password");

            bool passwordMatches = BCrypt.Net.BCrypt.Verify(request.Password, user.usr_PasswordHash);
            if (!passwordMatches)
                return Unauthorized("Invalid email or password");
       
            if (user.usr_IsBlocked)
                return Unauthorized("Your account has been blocked.");

            var claims = new[] 
            {
                new Claim("Id", user.usr_Id.ToString()),
                new Claim("FullName", user.usr_FullName),
                new Claim(ClaimTypes.Role, user.usr_Role)
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));

            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(12),
                signingCredentials: credentials
            );

            string tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new { token = tokenString, user.usr_Id, user.usr_FullName, user.usr_Role });
        }


        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var sql = "SELECT * FROM tbl_Users WHERE usr_Email = @Email";
            var user = await _connection.QueryFirstOrDefaultAsync<User>(sql, new { request.Email });

            if (user == null)
                return NotFound("No account found with this email.");

            string token = Guid.NewGuid().ToString();

            var insertSql = @"
        INSERT INTO tbl_PasswordReset (pwr_Token, pwr_ExpiresAt, pwr_IsUsed, pwr_UserId)
        VALUES (@Token, @ExpiresAt, 0, @UserId)";

            await _connection.ExecuteAsync(insertSql, new
            {
                Token = token,
                ExpiresAt = DateTime.Now.AddHours(1),
                UserId = user.usr_Id
            });


            var resetLink = $"http://localhost:5173/reset-password?token={token}";

            var emailBody = $@"
    <h2>CivicFix Password Reset</h2>
    <p>You requested to reset your password. Click below to set a new one:</p>
    <p><a href='{resetLink}'>Reset My Password</a></p>
";

            await _emailSender.SendEmailAsync(
                user.usr_Email,
                "CivicFix Password Reset",
                emailBody
            );

            return Ok(new
            {
                message = "A password reset link has been sent to your email."
            });
        }


        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var tokenSql = @"
        SELECT * FROM tbl_PasswordReset 
        WHERE pwr_Token = @Token";

            var resetRecord = await _connection.QueryFirstOrDefaultAsync<PasswordReset>(
                tokenSql, new { request.Token });

            if (resetRecord == null)
                return BadRequest("Invalid token.");

            if (resetRecord.pwr_ExpiresAt < DateTime.Now)
                return BadRequest("Token has expired.");

            if (resetRecord.pwr_IsUsed)
                return BadRequest("Token has already been used.");

            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

            var updateSql = "UPDATE tbl_Users SET usr_PasswordHash = @PasswordHash WHERE usr_Id = @UserId";
            await _connection.ExecuteAsync(updateSql, new
            {
                PasswordHash = hashedPassword,
                UserId = resetRecord.pwr_UserId
            });

            var markUsedSql = "UPDATE tbl_PasswordReset SET pwr_IsUsed = 1 WHERE pwr_Id = @Id";
            await _connection.ExecuteAsync(markUsedSql, new { Id = resetRecord.pwr_Id });

            return Ok("Password reset successfully.");
        }



        [Authorize(Roles = "Staff")]
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var userId = int.Parse(User.FindFirst("Id")!.Value);

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
        [HttpPut("{id:int}/block")]
        public async Task<IActionResult> BlockUser(int id)
        {
            var user = await _connection.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT usr_Id, usr_FullName, usr_Role, usr_IsBlocked FROM tbl_Users WHERE usr_Id = @Id",
                new { Id = id });

            if (user == null)
                return NotFound("User not found.");

            if (Convert.ToString((object)user.usr_Role) == "Admin")
                return BadRequest("An Admin account cannot be blocked.");


            var reportIds = (await _connection.QueryAsync<int>(
                "SELECT rpt_Id FROM tbl_Reports WHERE rpt_ReporterId = @Id",
                new { Id = id })).ToList();

            if (_connection.State != System.Data.ConnectionState.Open)
                await _connection.OpenAsync();

            using var transaction = _connection.BeginTransaction();

            try
            {
                await _connection.ExecuteAsync(
                    "UPDATE tbl_Users SET usr_IsBlocked = 1 WHERE usr_Id = @Id",
                    new { Id = id }, transaction);

                if (reportIds.Count > 0)
                {
                    var assignments = await _connection.QueryAsync<dynamic>(
                        "SELECT rpa_MunicipalityId, rpa_Points FROM tbl_ReportAssignments WHERE rpa_ReportId IN @Ids",
                        new { Ids = reportIds }, transaction);

                    foreach (var assignment in assignments)
                    {
                        int pointsToUndo = Convert.ToInt32((object)assignment.rpa_Points);

                        if (pointsToUndo != 0)
                        {
                            await _connection.ExecuteAsync(
                                "UPDATE tbl_Municipalities SET mun_TotalPoints = mun_TotalPoints - @Points WHERE mun_Id = @MunicipalityId",
                                new { Points = pointsToUndo, MunicipalityId = assignment.rpa_MunicipalityId },
                                transaction);
                        }
                    }

                    await _connection.ExecuteAsync("DELETE FROM tbl_Comments WHERE cmt_ReportId IN @Ids", new { Ids = reportIds }, transaction);
                    await _connection.ExecuteAsync("DELETE FROM tbl_StatusHistories WHERE sth_ReportId IN @Ids", new { Ids = reportIds }, transaction);
                    await _connection.ExecuteAsync("DELETE FROM tbl_PriorityVotes WHERE pvt_ReportId IN @Ids", new { Ids = reportIds }, transaction);
                    await _connection.ExecuteAsync("DELETE FROM tbl_ReportAgreements WHERE rga_ReportId IN @Ids", new { Ids = reportIds }, transaction);
                    await _connection.ExecuteAsync("DELETE FROM tbl_ReportAssignments WHERE rpa_ReportId IN @Ids", new { Ids = reportIds }, transaction);
                    await _connection.ExecuteAsync("DELETE FROM tbl_Reports WHERE rpt_Id IN @Ids", new { Ids = reportIds }, transaction);
                }



                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }

            return Ok(new
            {
                userId = id,
                fullName = Convert.ToString((object)user.usr_FullName),
                reportsDeleted = reportIds.Count,
                message = "User blocked and their reports removed."
            });
        }


    }
}