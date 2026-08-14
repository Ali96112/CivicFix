namespace CivicFix.Api.Models
{
    public class PriorityVote
    {
        public int pvt_Id { get; set; }
        public int pvt_ReportId { get; set; }   // which report
        public int pvt_UserId { get; set; }     // which resident voted
        public string pvt_Priority { get; set; } // Low, Medium, High
        public Report Report { get; set; }
        public User User { get; set; }
    }
}