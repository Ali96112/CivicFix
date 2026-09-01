using Microsoft.Data.SqlClient;
using Dapper;

namespace CivicFix.Api.Services
{
    // a background service — runs on its own timer, separate from web requests
    public class LatePenaltyService : BackgroundService
    {
        private readonly IConfiguration _configuration;

        public LatePenaltyService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // runs automatically when the app starts, then loops forever
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested) // keep going until app shuts down
            {
                await ApplyPenalties();   // do the penalty pass

                await Task.Delay(TimeSpan.FromHours(24), stoppingToken); // wait 24h, then repeat
            }
        }

        // takes 1 point per overdue report from each handler baladiye
        private async Task ApplyPenalties()
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (var connection = new SqlConnection(connectionString)) // open our own connection
            {
                // for each handler baladiye, count how many of its reports are overdue,
                // then subtract that many points (1 per overdue report)
                var sql = @"
                    UPDATE tbl_Municipalities
                    SET mun_TotalPoints = mun_TotalPoints - overdue.LateCount
                    FROM tbl_Municipalities
                    INNER JOIN (                     
                        SELECT rpa_MunicipalityId, COUNT(*) AS LateCount 
                        FROM tbl_ReportAssignments
                        INNER JOIN tbl_Reports ON rpa_ReportId = rpt_Id
                        WHERE rpa_IsHandler = 1
                        AND rpt_Status != 'Resolved'
                        AND rpt_CreatedAt < DATEADD(day, -7, GETDATE())
                        GROUP BY rpa_MunicipalityId
                    ) AS overdue ON tbl_Municipalities.mun_Id = overdue.rpa_MunicipalityId";//For each baladiye, count how many reports it is responsible for that are still not resolved and have been open for more than 7 days.

                await connection.ExecuteAsync(sql);
            }
        }
    }
}