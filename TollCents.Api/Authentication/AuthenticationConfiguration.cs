namespace TollCents.Api.Authentication
{
    public class AuthenticationConfiguration
    {
        public const string SectionName = "AuthenticationConfiguration";

        public required string FilePath { get; init; }
    }
}
