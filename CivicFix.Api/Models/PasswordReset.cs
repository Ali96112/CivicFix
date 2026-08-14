namespace CivicFix.Api.Models
{
    public class PasswordReset
    {
        public int pwr_Id { get; set; }
        public string pwr_Token { get; set; }        // the random one-time code
        public DateTime pwr_ExpiresAt { get; set; }  // 1 hour from creation
        public bool pwr_IsUsed { get; set; }         // true = already used, can't reuse
        public int pwr_UserId { get; set; }          // which user requested the reset
        public User User { get; set; }
    }
}