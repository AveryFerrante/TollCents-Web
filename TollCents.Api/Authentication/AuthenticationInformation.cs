namespace TollCents.Api.Authentication
{
    public class AuthenticationInformation
    {
        public required string Owner { get; set; }
        public required string AccessCode { get; set; }
    }
}
