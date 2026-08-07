namespace CivicFix.Api.Models
{
    public class CreateReportRequest
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public int CategoryId { get; set; }
        public string ReportedPhotoUrl { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int ReporterId { get; set; }
    }
}