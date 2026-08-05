namespace CivicFix.Api.Models// this file also belong to model group of the project
{
    public class Report
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public string PhotoUrl { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }

        public int ReporterId { get; set; }
        public User Reporter { get; set; }
    }
} 