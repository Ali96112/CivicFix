using NetTopologySuite.Geometries;

namespace CivicFix.Api.Models
{
    public class Report
    {
        public int rpt_Id { get; set; }
        public string rpt_Title { get; set; }
        public string rpt_Description { get; set; }
        public string rpt_Status { get; set; }
        public DateTime rpt_CreatedAt { get; set; }
        public string rpt_ReportedPhotoUrl { get; set; }
        public string? rpt_ResolvedPhotoUrl { get; set; }
        public Point rpt_Location { get; set; }
        public int rpt_ReporterId { get; set; }
        public User Reporter { get; set; }
        public int rpt_CategoryId { get; set; }
        public Category Category { get; set; }
        public int rpt_AgreementCount { get; set; }      // total agreements
        public int rpt_DisagreementCount { get; set; }   // total disagreements
        public string rpt_Priority { get; set; }  // Low, Medium, High
    }
}