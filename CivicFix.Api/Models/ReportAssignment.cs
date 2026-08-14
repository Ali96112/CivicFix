namespace CivicFix.Api.Models
{
    public class ReportAssignment
    {
        public int rpa_Id { get; set; }
        public DateTime rpa_AssignedAt { get; set; }
        public bool rpa_IsHandler { get; set; }
        public DateTime? rpa_AcceptedAt { get; set; }
        public int rpa_Points { get; set; }
        public int rpa_ReportId { get; set; }
        public Report Report { get; set; }
        public int rpa_MunicipalityId { get; set; }
        public Municipality Municipality { get; set; }
    }
}