using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CivicFix.Api.Models;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace CivicFix.Api.Controllers
{
    [ApiController]
    [Route("api/Reports")]
    public class ReportsFeedbackController : ControllerBase
    {
        private readonly SqlConnection _connection;

        public ReportsFeedbackController(SqlConnection connection)
        {
            _connection = connection;
        }


        [Authorize(Roles = "Resident,Staff,Admin")]
        [HttpPost("{id:int}/comments")]
        public async Task<IActionResult> AddComment(int id, [FromBody] AddCommentRequest request)
        {
            var currentUserId = int.Parse(User.FindFirst("Id")!.Value);

            if (string.IsNullOrWhiteSpace(request.Text))
                return BadRequest("Comment text cannot be empty.");

            var sql = @"
                INSERT INTO tbl_Comments (cmt_Text, cmt_CreatedAt, cmt_ReportId, cmt_UserId)
                VALUES (@Text, @CreatedAt, @ReportId, @UserId)";

            await _connection.ExecuteAsync(sql, new
            {
                request.Text,
                CreatedAt = DateTime.Now,
                ReportId = id,
                UserId = currentUserId
            });

            return Ok("Comment added successfully");
        }


        [Authorize(Roles = "Resident")]
        [HttpPost("{id:int}/agree")]

        public async Task<IActionResult> AgreeOnReport(int id, [FromBody] AgreementRequest request)
        {
            var currentUserId = int.Parse(User.FindFirst("Id")!.Value);

            var checkSql = @"
                SELECT rpt_Id, rpt_ReporterId FROM tbl_Reports
                WHERE rpt_Id = @Id";
            var report = await _connection.QueryFirstOrDefaultAsync<dynamic>(checkSql, new { Id = id });

            if (report == null)
                return NotFound("Report not found.");

            var reporterSql = "SELECT usr_Role FROM tbl_Users WHERE usr_Id = @Id";
            var reporterRole = await _connection.QueryFirstOrDefaultAsync<string>(
                reporterSql, new { Id = (int)report.rpt_ReporterId });

            if (reporterRole != "Staff")
                return BadRequest("You can only agree on staff-submitted reports.");

            var existingSql = @"
                SELECT rga_Id FROM tbl_ReportAgreements
                WHERE rga_ReportId = @ReportId AND rga_UserId = @UserId";

            var existing = await _connection.QueryFirstOrDefaultAsync<int?>(
                existingSql, new { ReportId = id, UserId = currentUserId });

            if (existing != null)
                return BadRequest("You have already submitted your agreement on this report.");

            var insertSql = @"
                INSERT INTO tbl_ReportAgreements (rga_ReportId, rga_UserId, rga_IsAgreement)
                VALUES (@ReportId, @UserId, @IsAgreement)";

            await _connection.ExecuteAsync(insertSql, new
            {
                ReportId = id,
                UserId = currentUserId,
                IsAgreement = request.IsAgreement
            });

            if (request.IsAgreement)
            {
                await _connection.ExecuteAsync(
                    "UPDATE tbl_Reports SET rpt_AgreementCount = rpt_AgreementCount + 1 WHERE rpt_Id = @Id",
                    new { Id = id });

                var countSql = "SELECT rpt_AgreementCount FROM tbl_Reports WHERE rpt_Id = @Id";
                var agreementCount = await _connection.QueryFirstAsync<int>(countSql, new { Id = id });

                if (agreementCount >= 3)
                {
                    var assignmentSql = @"
                        SELECT rpa_Id, rpa_MunicipalityId, rpa_Points
                        FROM tbl_ReportAssignments
                        WHERE rpa_ReportId = @ReportId AND rpa_IsHandler = 1";

                    var assignment = await _connection.QueryFirstOrDefaultAsync<dynamic>(
                        assignmentSql, new { ReportId = id });


                    if (assignment != null && Convert.ToInt32((object)assignment.rpa_Points) == 0)
                    {
                        await _connection.ExecuteAsync(
                            "UPDATE tbl_ReportAssignments SET rpa_Points = 10 WHERE rpa_Id = @Id",
                            new { Id = assignment.rpa_Id });

                        await _connection.ExecuteAsync(
                            "UPDATE tbl_Municipalities SET mun_TotalPoints = mun_TotalPoints + 10 WHERE mun_Id = @MunicipalityId",
                            new { MunicipalityId = assignment.rpa_MunicipalityId });
                    }
                }
            }
            else
            {
                await _connection.ExecuteAsync(
                    "UPDATE tbl_Reports SET rpt_DisagreementCount = rpt_DisagreementCount + 1 WHERE rpt_Id = @Id",
                    new { Id = id });
            }

            return Ok(request.IsAgreement ? "Agreement submitted." : "Disagreement submitted.");
        }


        [Authorize(Roles = "Resident")]
        [HttpPost("{id:int}/priority")]
        public async Task<IActionResult> VoteOnPriority(int id, [FromBody] PriorityVoteRequest request)
        {
            var currentUserId = int.Parse(User.FindFirst("Id")!.Value);

            var checkSql = @"SELECT rpt_Id, rpt_ReporterId FROM tbl_Reports WHERE rpt_Id = @Id";
            var report = await _connection.QueryFirstOrDefaultAsync<dynamic>(checkSql, new { Id = id });

            if (report == null)
                return NotFound("Report not found.");

            var reporterSql = "SELECT usr_Role FROM tbl_Users WHERE usr_Id = @Id";
            var reporterRole = await _connection.QueryFirstOrDefaultAsync<string>(
                reporterSql, new { Id = (int)report.rpt_ReporterId });

            if (reporterRole != "Resident")
                return BadRequest("You can only vote on priority of resident-submitted reports.");

            var existingSql = @"
                SELECT pvt_Id FROM tbl_PriorityVotes
                WHERE pvt_ReportId = @ReportId AND pvt_UserId = @UserId";

            var existing = await _connection.QueryFirstOrDefaultAsync<int?>(
                existingSql, new { ReportId = id, UserId = currentUserId });


            if (existing != null)
            {
                await _connection.ExecuteAsync(
                    "UPDATE tbl_PriorityVotes SET pvt_Priority = @Priority WHERE pvt_Id = @Id",
                    new { Priority = request.Priority, Id = existing.Value });
            }
            else
            {
                var insertSql = @"
                    INSERT INTO tbl_PriorityVotes (pvt_ReportId, pvt_UserId, pvt_Priority)
                    VALUES (@ReportId, @UserId, @Priority)";

                await _connection.ExecuteAsync(insertSql, new
                {
                    ReportId = id,
                    UserId = currentUserId,
                    Priority = request.Priority
                });
            }

           
            return Ok(new
            {
                message = existing != null ? "Priority vote changed." : "Priority vote submitted.",
               
            });
        }
    }
}
