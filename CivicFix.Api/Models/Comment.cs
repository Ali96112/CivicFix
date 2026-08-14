namespace CivicFix.Api.Models
{
    public class Comment
    {
        public int cmt_Id { get; set; }
        public string cmt_Text { get; set; }
        public DateTime cmt_CreatedAt { get; set; }
        public int cmt_ReportId { get; set; }
        public Report Report { get; set; }
        public int cmt_UserId { get; set; }
        public User User { get; set; }
    }
}