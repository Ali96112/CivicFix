namespace CivicFix.Api.Models
{
    public class AgreementRequest
    {
        public int UserId { get; set; }
        public bool IsAgreement { get; set; }  // true = agree, false = disagree
    }
}