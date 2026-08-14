namespace CivicFix.Api.Models
{
    public class UpdateStatusRequest
    {
        public string NewStatus { get; set; }
        public string? ResolvedPhotoUrl { get; set; } // only required when status is Resolved
        public int ChangedByUserId { get; set; }
    }
}