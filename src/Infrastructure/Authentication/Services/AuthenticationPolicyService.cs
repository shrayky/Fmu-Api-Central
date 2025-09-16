using Domain.Attributes;
using Domain.Authentication.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Authentication.Services
{
    [AutoRegisterService(ServiceLifetime.Singleton)]
    public class AuthenticationPolicyService : IAuthenticationPolicyService
    {
        private readonly ILogger<AuthenticationPolicyService> _logger;

        public AuthenticationPolicyService(ILogger<AuthenticationPolicyService> logger)
        {
            _logger = logger;
        }

        public async Task<bool> ValidateFallbackCredentials(string login, string password)
        {
            _logger.LogInformation("Используется fallback аутентификация для пользователя {Login}", login);

            await Task.Delay(1);
            
            return login == "admin" && password == "admin";
        }

        public bool IsFallbackModeEnabled()
        {
            return true;
        }
    }
}
