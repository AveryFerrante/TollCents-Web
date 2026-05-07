using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Text.Json;

namespace TollCents.Api.Authentication
{
    public interface IAccessCodeValidationService
    {
        Task<bool> IsValidAccessCode(string? accessCode);
    }

    public class AccessCodeValidationService(IMemoryCache memoryCache,
        IOptions<AuthenticationConfiguration> configuration) : IAccessCodeValidationService
    {
        private const string _memoryCacheKey = "access-code-information";
        private readonly IMemoryCache _memoryCache = memoryCache;

        public async Task<bool> IsValidAccessCode(string? accessCode)
        {
            if (string.IsNullOrEmpty(accessCode))
                return false;

            var authenticationInfo = await GetAuthenticationInformation();
            return authenticationInfo.Any(info => info.AccessCode.Equals(accessCode, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<IEnumerable<AuthenticationInformation>> GetAuthenticationInformation()
        {
            var authenticationInformation = await _memoryCache.GetOrCreateAsync(_memoryCacheKey, async cacheEntry =>
            {
                cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                return await LoadAuthenticationInformationFromConfig();
            });
            return authenticationInformation ?? throw new InvalidDataException("Null object returned from memory cache read");
        }

        private async Task<IEnumerable<AuthenticationInformation>> LoadAuthenticationInformationFromConfig()
        {
            var filePath = configuration.Value.FilePath;
            if (!File.Exists(filePath))
                throw new FileLoadException("Expected authentication file to exist at path " + filePath);

            using var fileStream = File.OpenRead(filePath);
            var authenticationInformation = await JsonSerializer.DeserializeAsync<IEnumerable<AuthenticationInformation>>(fileStream);

            return authenticationInformation ?? throw new InvalidOperationException("Authentication information cannot be null");
        }
    }
}
