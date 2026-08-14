namespace CivicFix.Api.Models
{
    public class AddCommentRequest
    {
        public string Text { get; set; }  // the comment text
        public int UserId { get; set; }   // who is writing the comment
    }
}