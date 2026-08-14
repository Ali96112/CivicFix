namespace CivicFix.Api.Models
{
    public class ReportAgreement
    {
        public int rga_Id { get; set; }
        public int rga_ReportId { get; set; }  // which report this agreement belongs to
        public int rga_UserId { get; set; }    // which resident agreed/disagreed
        public bool rga_IsAgreement { get; set; } // true = agree, false = disagree
        public Report Report { get; set; }
        public User User { get; set; }
    }
}