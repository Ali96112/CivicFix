namespace CivicFix.Api.Models
{
    public class Comment
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public DateTime CreatedAt { get; set; }

        public int ReportId { get; set; }
        public Report Report { get; set; }

        public int UserId { get; set; }
        public User User { get; set; }
    }
}