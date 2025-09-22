namespace TollCents.Api.Startup
{
    public class ConfigurationConstants
    {
        public static string DevCORSPolicyName = "AllowAllOrigins";
        public static string ProductionCORSPolicyName = "ProductionCORSPolicy";
        public static string[] AllowedTollCentsDomains =
        [
            "tollcents.com",
            "www.tollcents.com"
        ];
    }
}
