namespace CivicFix.Api.Models
{
    public class StatusHistory
    {
        public int Id { get; set; }
        public string OldStatus { get; set; }
        public string NewStatus { get; set; }
        public DateTime ChangedAt { get; set; }

        public int ReportId { get; set; }
        public Report Report { get; set; }

        public int ChangedByUserId { get; set; }
        public User ChangedBy { get; set; }
    }
}