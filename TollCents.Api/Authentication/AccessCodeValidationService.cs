using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace TollCents.Api.Authentication
{
    public interface IAccessCodeValidationService
    {
        Task<bool> IsValidAccessCode(string? accessCode);
    }

    public class AccessCodeValidationService(IMemoryCache memoryCache) : IAccessCodeValidationService
    {
        private const string _memoryCacheKey = "access-code-information";
        private readonly IMemoryCache _memoryCache = memoryCache;

        public async Task<bool> IsValidAccessCode(string? accessCode)
        {
            if (string.IsNullOrEmpty(accessCode)) return false;
            var authenticationInfo = await GetAuthenticationInformation();
            return authenticationInfo.Any(info => info.AccessCode.Equals(accessCode, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<IEnumerable<AuthenticationInformation>> GetAuthenticationInformation()
        {
            if (!_memoryCache.TryGetValue(_memoryCacheKey, out IEnumerable<AuthenticationInformation>? authenticationInformation))
            {
                authenticationInformation = await LoadAuthenticationInformationFromConfig();
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromHours(1));
                _memoryCache.Set(_memoryCacheKey, authenticationInformation, cacheEntryOptions);
            }
            return authenticationInformation ?? Enumerable.Empty<AuthenticationInformation>();
        }

        private async Task<IEnumerable<AuthenticationInformation>> LoadAuthenticationInformationFromConfig()
        {
            // TODO: Make this work with w/e cloud provider storage solution used. Not local file.
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var directory = Path.GetDirectoryName(assemblyLocation) ?? string.Empty;
            var filePath = Path.Combine(directory, "Authentication\\authenticationInformation.json");

            if (!File.Exists(filePath))
                return Enumerable.Empty<AuthenticationInformation>();

            using var fileStream = File.OpenRead(filePath);
            var authenticationInformation = await JsonSerializer.DeserializeAsync<List<AuthenticationInformation>>(fileStream);

            return authenticationInformation ?? Enumerable.Empty<AuthenticationInformation>();
        }
    }
}
