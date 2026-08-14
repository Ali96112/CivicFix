namespace CivicFix.Api.Models
{
    public class StatusHistory
    {
        public int sth_Id { get; set; }
        public string sth_OldStatus { get; set; }
        public string sth_NewStatus { get; set; }
        public DateTime sth_ChangedAt { get; set; }
        public int sth_ReportId { get; set; }
        public Report Report { get; set; }
        public int sth_ChangedByUserId { get; set; }
        public User ChangedBy { get; set; }
    }
}