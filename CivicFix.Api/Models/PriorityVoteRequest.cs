namespace CivicFix.Api.Models
{
    public class PriorityVoteRequest
    {
        public int UserId { get; set; }
        public string Priority { get; set; }  // Low, Medium, High
    }
}