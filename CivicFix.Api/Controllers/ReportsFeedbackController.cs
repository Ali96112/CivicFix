using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CivicFix.Api.Models;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace CivicFix.Api.Controllers
{
    // ══════════════════════════════════════════════════════════════════════════
    // WHAT RESIDENTS DO TO A REPORT — comment, agree, vote on priority.
    //
    // This is the participation side of CivicFix: residents confirming that a
    // baladiye really did the work, and deciding together how urgent something is.
    // ══════════════════════════════════════════════════════════════════════════
    [ApiController]
    // NOT [Route("api/[controller]")] — that token expands to the class name, so
    // this file would answer on api/ReportsFeedback and every URL below would change.
    // Written out in full, these endpoints keep the exact addresses they had when
    // they all lived in one ReportsController.
    [Route("api/Reports")]
    public class ReportsFeedbackController : ControllerBase
    {
        private readonly SqlConnection _connection;

        public ReportsFeedbackController(SqlConnection connection)
        {
            _connection = connection;
        }


        [Authorize(Roles = "Resident,Staff,Admin")]
        [HttpPost("{id:int}/comments")] // address: POST api/Reports/1/comments
        public async Task<IActionResult> AddComment(int id, [FromBody] AddCommentRequest request)
        {
            // who is asking? Straight from the signed JWT, never the request body.
            // TryParse, not int.Parse: a missing claim gives a 400, not a 500 crash.
            var idClaim = User.FindFirst("Id")?.Value;
            if (!int.TryParse(idClaim, out int currentUserId))
                return BadRequest("Could not read user Id from token. Claim 'Id' not found.");

            //empty comments are not useful
            if (string.IsNullOrWhiteSpace(request.Text))
                return BadRequest("Comment text cannot be empty.");

            // Step 1 — check the report exists
            var checkSql = "SELECT rpt_Id FROM tbl_Reports WHERE rpt_Id = @Id";
            var reportId = await _connection.QueryFirstOrDefaultAsync<int?>(checkSql, new { Id = id });
            // int? — returns the Id number if found, or null if no report with that Id exists

            if (reportId == null)
                return NotFound("Report not found.");

            // Step 2 — insert the comment
            var sql = @"
                INSERT INTO tbl_Comments (cmt_Text, cmt_CreatedAt, cmt_ReportId, cmt_UserId)
                VALUES (@Text, @CreatedAt, @ReportId, @UserId)";

            await _connection.ExecuteAsync(sql, new
            {
                request.Text,              // the comment text written by staff/admin
                CreatedAt = DateTime.Now,  // when the comment was added — set by system
                ReportId = id,             // which report this comment belongs to — from URL
                // was: request.UserId  // who wrote the comment — from request body
                UserId = currentUserId // FIXED: who wrote the comment — now from the TOKEN, not the body (body was spoofable)
            });

            return Ok("Comment added successfully");
        }


        [Authorize(Roles = "Resident")]
        [HttpPost("{id:int}/agree")] // address: POST api/Reports/1/agree

        public async Task<IActionResult> AgreeOnReport(int id, [FromBody] AgreementRequest request)
        {
            var idClaim = User.FindFirst("Id")?.Value;//find which resdient is answering
            if (!int.TryParse(idClaim, out int currentUserId))
                return BadRequest("Could not read user Id from token. Claim 'Id' not found.");

            // Step 1 — check the report exists and was submitted by staff
            var checkSql = @"
                SELECT rpt_Id, rpt_ReporterId FROM tbl_Reports
                WHERE rpt_Id = @Id";
            var report = await _connection.QueryFirstOrDefaultAsync<dynamic>(checkSql, new { Id = id });

            if (report == null)
                return NotFound("Report not found.");

            // check if reporter is staff so resdient just agree on reporter
            var reporterSql = "SELECT usr_Role FROM tbl_Users WHERE usr_Id = @Id";
            var reporterRole = await _connection.QueryFirstOrDefaultAsync<string>(
                reporterSql, new { Id = (int)report.rpt_ReporterId });

            if (reporterRole != "Staff")
                return BadRequest("You can only agree on staff-submitted reports.");

            // Step 2 — check if this resident already agreed/disagreed on this report
            var existingSql = @"
                SELECT rga_Id FROM tbl_ReportAgreements
                WHERE rga_ReportId = @ReportId AND rga_UserId = @UserId";

            var existing = await _connection.QueryFirstOrDefaultAsync<int?>(
                existingSql, new { ReportId = id, UserId = currentUserId }); // FIXED: token Id, not body Id

            if (existing != null)
                return BadRequest("You have already submitted your agreement on this report.");

            // Step 3 — save the agreement
            var insertSql = @"
                INSERT INTO tbl_ReportAgreements (rga_ReportId, rga_UserId, rga_IsAgreement)
                VALUES (@ReportId, @UserId, @IsAgreement)";

            await _connection.ExecuteAsync(insertSql, new
            {
                ReportId = id,
                UserId = currentUserId, // FIXED: was request.UserId from the body
                IsAgreement = request.IsAgreement
            });

            // Step 4 — increment the correct counter on the report
            if (request.IsAgreement)
            {
                // increment agreement count
                await _connection.ExecuteAsync(
                    "UPDATE tbl_Reports SET rpt_AgreementCount = rpt_AgreementCount + 1 WHERE rpt_Id = @Id",
                    new { Id = id });

                // check if agreement count reached threshold of 3
                var countSql = "SELECT rpt_AgreementCount FROM tbl_Reports WHERE rpt_Id = @Id";
                var agreementCount = await _connection.QueryFirstAsync<int>(countSql, new { Id = id });

                if (agreementCount >= 3)
                {
                    // get the assignment for this report
                    var assignmentSql = @"
                        SELECT rpa_Id, rpa_MunicipalityId, rpa_Points
                        FROM tbl_ReportAssignments
                        WHERE rpa_ReportId = @ReportId AND rpa_IsHandler = 1";

                    var assignment = await _connection.QueryFirstOrDefaultAsync<dynamic>(
                        assignmentSql, new { ReportId = id });

                 
                    if (assignment != null && Convert.ToInt32((object)assignment.rpa_Points) == 0)
                    {
                        // award points only once — rpa_Points == 0 prevents double awarding
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
                // increment disagreement count
                await _connection.ExecuteAsync(
                    "UPDATE tbl_Reports SET rpt_DisagreementCount = rpt_DisagreementCount + 1 WHERE rpt_Id = @Id",
                    new { Id = id });
            }

            return Ok(request.IsAgreement ? "Agreement submitted." : "Disagreement submitted.");
        }


        [Authorize(Roles = "Resident")]
        [HttpPost("{id:int}/priority")] // address: POST api/Reports/1/priority
        public async Task<IActionResult> VoteOnPriority(int id, [FromBody] PriorityVoteRequest request)//the frontend send to here priority:High 
        {
            var idClaim = User.FindFirst("Id")?.Value;//find how is voting
            if (!int.TryParse(idClaim, out int currentUserId))
                return BadRequest("Could not read user Id from token. Claim 'Id' not found.");

            // Step 1 — check report exists //give me its report ID and the ID of the person who created it
            var checkSql = @"SELECT rpt_Id, rpt_ReporterId FROM tbl_Reports WHERE rpt_Id = @Id";
            var report = await _connection.QueryFirstOrDefaultAsync<dynamic>(checkSql, new { Id = id });//report id from url

            if (report == null)
                return NotFound("Report not found.");

            // check reporter how created the report is resident
            var reporterSql = "SELECT usr_Role FROM tbl_Users WHERE usr_Id = @Id";
            var reporterRole = await _connection.QueryFirstOrDefaultAsync<string>(
                reporterSql, new { Id = (int)report.rpt_ReporterId });//getting the reporter to here 

            if (reporterRole != "Resident")
                return BadRequest("You can only vote on priority of resident-submitted reports.");

            // Step 2 — check if this resident already voted on this report
            var existingSql = @"
                SELECT pvt_Id FROM tbl_PriorityVotes
                WHERE pvt_ReportId = @ReportId AND pvt_UserId = @UserId";

            var existing = await _connection.QueryFirstOrDefaultAsync<int?>(//if existingSql =1 mean already voted if =null still not voted
                existingSql, new { ReportId = id, UserId = currentUserId }); 


            // Step 3 — validate priority value
            if (request.Priority != "Low" && request.Priority != "Medium" && request.Priority != "High")
                return BadRequest("Priority must be Low, Medium, or High.");

            // Step 4 — save the vote: update the existing one, or insert a first one
            if (existing != null)
            {
                // this person has voted before — overwrite their choice
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

            // Step 5 —After all residents have voted, which priority currently has the most votes?
            var votesSql = @"
                SELECT pvt_Priority, COUNT(*) AS VoteCount  --Give me each priority and count how many votes it hasa and put it in VoteCount
                FROM tbl_PriorityVotes
                WHERE pvt_ReportId = @ReportId  --count the votes belonging to this report only
                GROUP BY pvt_Priority   --without this we cant have separate counts for High, Medium, and Low.
                ORDER BY VoteCount DESC,  --Sort from the highest number of votes to the lowest.
                    CASE pvt_Priority  --if priorites are equal it well show in this order H M L
                        WHEN 'High' THEN 1
                        WHEN 'Medium' THEN 2
                        WHEN 'Low' THEN 3
                        ELSE 4
                    END";

            var votes = await _connection.QueryAsync<dynamic>(votesSql, new { ReportId = id });  //high:2 Medium:3 ..

            var topVote = votes.FirstOrDefault();//takes first row
            if (topVote == null)//it weell never be null in my case just delet happen or something
                return Ok(new { message = "Priority vote submitted.", currentPriority = (string?)null });

            string topPriority = Convert.ToString((object)topVote.pvt_Priority) ?? ""; // the priority with most votes//conversion just making sure it is in c#

            // update report priority to majority vote
            await _connection.ExecuteAsync(
                "UPDATE tbl_Reports SET rpt_Priority = @Priority WHERE rpt_Id = @Id",
                new { Priority = topPriority, Id = id });

            // CHANGED: the message now says whether this was a first vote or a change,
            // so the user gets confirmation that their new choice actually replaced
            // the old one rather than being silently ignored.
            return Ok(new
            {
                message = existing != null ? "Priority vote changed." : "Priority vote submitted.",
                currentPriority = topPriority
            });
        }
    }
}
