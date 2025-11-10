using Domain.Attributes;
using Domain.Authentication.Interfaces;
using Domain.Database.Interfaces;
using Domain.Entitys.Users.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Authentication.Services
{
    [AutoRegisterService(ServiceLifetime.Scoped)]
    public class UserAuthenticationService : IUserAuthenticationService
    {
        private readonly Lazy<IDbHealthService> _dbHealthService;
        private readonly Lazy<IRepositoryHealthService> _repositoryHealthService;
        private readonly Lazy<IUserCredentialService> _userCredentialService;
        private readonly Lazy<IAuthenticationPolicyService> _authenticationPolicyService;
        
        private readonly ILogger<UserAuthenticationService> _logger;
        private readonly IUserRepository _userRepository;

        public UserAuthenticationService(IServiceProvider serviceProvider, ILogger<UserAuthenticationService> logger, IUserRepository userRepository)
        {
            _dbHealthService = new Lazy<IDbHealthService>(serviceProvider.GetRequiredService<IDbHealthService>);
            _repositoryHealthService = new Lazy<IRepositoryHealthService>(serviceProvider.GetRequiredService<IRepositoryHealthService>);
            _userCredentialService = new Lazy<IUserCredentialService>(serviceProvider.GetRequiredService<IUserCredentialService>);
            _authenticationPolicyService = new Lazy<IAuthenticationPolicyService>(serviceProvider.GetRequiredService<IAuthenticationPolicyService>);
            
            _logger = logger;
            _userRepository = userRepository;
        }

        public async Task<bool> ValidateCredentials(string login, string password)
        {
            var dbEnabled = await _dbHealthService.Value.IsDatabaseEnabled();
            var haseUsers = await _repositoryHealthService.Value.HasRecords(_userRepository.DatabaseName());
            bool isValid;

            if (dbEnabled && haseUsers)
            {
                var userResult = await _userCredentialService.Value.GetUserByLogin(login);

                if (userResult.IsFailure)
                {
                    _logger.LogWarning("Пользователь {Login} не найден в БД", login);
                    return false;
                }

                isValid = await _userCredentialService.Value.ValidatePassword(userResult.Value, password);
            }
            else
            {
                isValid = await _authenticationPolicyService.Value.ValidateFallbackCredentials(login, password);
            }

            return isValid;
        }

    }
}
