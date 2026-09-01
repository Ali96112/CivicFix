namespace CivicFix.Api.Models
{
    public class User
    {
        public int usr_Id { get; set; }
        public string usr_FullName { get; set; }
        public string usr_Email { get; set; }
        public string usr_PasswordHash { get; set; }
        public string usr_PhoneNumber { get; set; }
        public bool usr_IsBlocked { get; set; }   // Admin blocked this account — Login rejects it
        public string? usr_Role { get; set; }
        public int? usr_MunicipalityId { get; set; }
        public Municipality? Municipality { get; set; }
    }

    public class LoginRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }
}
