using Application.Authentication.DTO;
using Application.Authentication.Interfaces;
using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Authentication.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.Authentication.Services
{
    [AutoRegisterService(ServiceLifetime.Scoped)]
    public class AuthenticationApplicationService : IAuthenticationApplicationService
    {
        private readonly Lazy<IUserAuthenticationService> _userAuthenticationService;
        private readonly Lazy<ITokenService> _tokenService;
        private readonly Lazy<IRefreshTokenService> _refreshTokenService;

        private readonly ILogger<AuthenticationApplicationService> _logger;

        public AuthenticationApplicationService(IServiceProvider serviceProvider, ILogger<AuthenticationApplicationService> logger)
        {
            
            _userAuthenticationService = new Lazy<IUserAuthenticationService>(() => serviceProvider.GetRequiredService<IUserAuthenticationService>());
            _tokenService = new Lazy<ITokenService>(() => serviceProvider.GetRequiredService<ITokenService>());
            _refreshTokenService = new Lazy<IRefreshTokenService>(() => serviceProvider.GetRequiredService<IRefreshTokenService>());

            _logger = logger;
        }

        public async Task<Result<AuthenticationResult>> Authenticate(string login, string password)
        {
            _logger.LogDebug("Начинаю аутентификацию пользователя {Login}", login);

            bool validateResult = await _userAuthenticationService.Value.ValidateCredentials(login, password);

            if (!validateResult)
            {
                _logger.LogWarning("Неудачная попытка аутентификации пользователя {Login}", login);
                return Result.Failure<AuthenticationResult>("Неверный логин или пароль");
            }

            var tokenResult = _tokenService.Value.GenerateTokenPair(login);

            if (tokenResult.IsFailure)
            {
                _logger.LogError("Ошибка генерации токенов для пользователя {Login}: {Error}", login, tokenResult.Error);
                return Result.Failure<AuthenticationResult>("Ошибка генерации токена пользователя");
            }

            var authResult = new AuthenticationResult
            {
                AccessToken = tokenResult.Value.AccessToken,
                RefreshToken = tokenResult.Value.RefreshToken,
                ExpiresAt = tokenResult.Value.ExpiresAt
            };

            _logger.LogInformation("Успешная аутентификация пользователя {Login}", login);
            return Result.Success(authResult);
        }

        public Result<AuthenticationResult> RefreshToken(string refreshToken)
        {
            var refreshResult = _tokenService.Value.RefreshAccessToken(refreshToken);
            if (refreshResult.IsFailure)
            {
                _logger.LogWarning("Неудачное обновление токена: {Error}", refreshResult.Error);
                return Result.Failure<AuthenticationResult>(refreshResult.Error);
            }

            var authResult = new AuthenticationResult
            {
                AccessToken = refreshResult.Value.AccessToken,
                RefreshToken = refreshResult.Value.RefreshToken,
                ExpiresAt = refreshResult.Value.ExpiresAt
            };

            return Result.Success(authResult);
        }

        public Result Logout(string refreshToken)
        {
            _refreshTokenService.Value.RemoveRefreshToken(refreshToken);

            _logger.LogInformation("Пользователь успешно вышел из системы");
            return Result.Success();
        }
    }
}
