namespace CivicFix.Api.Models //is a way of grouping related codes .Models
{
    public class User
    {
        public int Id { get; set; }//EF core automatically well give id to be a primary key
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string Role { get; set; }
    }

    public class LoginRequest
    {
        public string Email{get; set;}
        public string Password{get; set;}
    }
}