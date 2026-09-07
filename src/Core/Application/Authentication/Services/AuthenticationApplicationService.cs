using Application.Authentication.DTO;
using Application.Authentication.Interfaces;
using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Authentication;
using Domain.Authentication.Interfaces;
using Domain.Configuration.Interfaces;
using Domain.Configuration.Options;
using Domain.Entitys;
using Domain.Entitys.Users.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.Authentication.Services
{
    [AutoRegisterService(ServiceLifetime.Scoped)]
    public class AuthenticationApplicationService : IAuthenticationApplicationService
    {
        private const string InvalidCredentialsMessage = "Неверный логин или пароль";

        private readonly Lazy<IUserAuthenticationService> _userAuthenticationService;
        private readonly Lazy<IUserCredentialService> _userCredentialService;
        private readonly Lazy<ITokenService> _tokenService;
        private readonly Lazy<IRefreshTokenService> _refreshTokenService;
        private readonly Lazy<ILoginAttemptService> _loginAttemptService;
        private readonly Lazy<IUserRepository> _userRepository;
        private readonly Lazy<IPasswordHasher> _passwordHasher;
        private readonly Lazy<IParametersService> _parametersService;

        private readonly ILogger<AuthenticationApplicationService> _logger;

        public AuthenticationApplicationService(IServiceProvider serviceProvider, ILogger<AuthenticationApplicationService> logger)
        {
            _userAuthenticationService = new Lazy<IUserAuthenticationService>(serviceProvider.GetRequiredService<IUserAuthenticationService>);
            _userCredentialService = new Lazy<IUserCredentialService>(serviceProvider.GetRequiredService<IUserCredentialService>);
            _tokenService = new Lazy<ITokenService>(serviceProvider.GetRequiredService<ITokenService>);
            _refreshTokenService = new Lazy<IRefreshTokenService>(serviceProvider.GetRequiredService<IRefreshTokenService>);
            _loginAttemptService = new Lazy<ILoginAttemptService>(serviceProvider.GetRequiredService<ILoginAttemptService>);
            _userRepository = new Lazy<IUserRepository>(serviceProvider.GetRequiredService<IUserRepository>);
            _passwordHasher = new Lazy<IPasswordHasher>(serviceProvider.GetRequiredService<IPasswordHasher>);
            _parametersService = new Lazy<IParametersService>(serviceProvider.GetRequiredService<IParametersService>);

            _logger = logger;
        }

        public async Task<Result<AuthenticationResult>> Authenticate(string login, string password)
        {
            _logger.LogDebug("Начинаю аутентификацию пользователя {Login}", login);

            if (_loginAttemptService.Value.IsLocked(login))
            {
                _logger.LogWarning("Вход заблокирован для пользователя {Login}", login);
                return Result.Failure<AuthenticationResult>(InvalidCredentialsMessage);
            }

            bool validateResult = await _userAuthenticationService.Value.ValidateCredentials(login, password);

            if (!validateResult)
            {
                _loginAttemptService.Value.RegisterFailure(login);
                _logger.LogWarning("Неудачная попытка аутентификации пользователя {Login}", login);
                return Result.Failure<AuthenticationResult>(InvalidCredentialsMessage);
            }

            _loginAttemptService.Value.RegisterSuccess(login);

            if (await _userAuthenticationService.Value.IsFallbackActive())
            {
                if ((await _parametersService.Value.Current()).Security.PasswordConfigured)
                    return Result.Failure<AuthenticationResult>(InvalidCredentialsMessage);

                return IssueTokens(login);
            }

            var userResult = await _userCredentialService.Value.GetUserByLogin(login);
            if (userResult.IsFailure)
                return Result.Failure<AuthenticationResult>(InvalidCredentialsMessage);

            if (RequiresPasswordChange(userResult.Value))
            {
                _logger.LogWarning("Требуется смена пароля пользователя {Login}", login);
                return Result.Success(new AuthenticationResult { MustChangePassword = true });
            }

            return IssueTokens(login);
        }

        public Result<AuthenticationResult> RefreshToken(string refreshToken)
        {
            var refreshResult = _tokenService.Value.RefreshAccessToken(refreshToken);
            if (refreshResult.IsFailure)
            {
                _logger.LogWarning("Неудачное обновление токена: {Error}", refreshResult.Error);
                return Result.Failure<AuthenticationResult>(refreshResult.Error);
            }

            return Result.Success(ToAuthenticationResult(refreshResult.Value));
        }

        /// <summary>
        /// Меняет пароль по текущему без JWT. Новый не может быть admin.
        /// </summary>
        public async Task<Result> ChangePassword(string login, string currentPassword, string newPassword)
        {
            if (await _userAuthenticationService.Value.IsFallbackActive())
                return Result.Failure("Смена пароля недоступна без базы данных");

            if (_loginAttemptService.Value.IsLocked(login))
                return Result.Failure(InvalidCredentialsMessage);

            var valid = await _userAuthenticationService.Value.ValidateCredentials(login, currentPassword);
            if (!valid)
            {
                _loginAttemptService.Value.RegisterFailure(login);
                return Result.Failure(InvalidCredentialsMessage);
            }

            _loginAttemptService.Value.RegisterSuccess(login);

            var newPasswordResult = ValidateNewPassword(newPassword);
            if (newPasswordResult.IsFailure)
                return newPasswordResult;

            var userResult = await _userCredentialService.Value.GetUserByLogin(login);
            if (userResult.IsFailure)
                return Result.Failure(userResult.Error);

            var user = userResult.Value;
            user.Password = _passwordHasher.Value.Hash(newPassword.Trim());
            user.MustChangePassword = false;

            var updateResult = await _userRepository.Value.Update(user);
            if (updateResult.IsFailure)
                return updateResult;

            return await MarkPasswordConfigured();
        }

        public Result Logout(string refreshToken)
        {
            _refreshTokenService.Value.RemoveRefreshToken(refreshToken);

            _logger.LogInformation("Пользователь успешно вышел из системы");
            return Result.Success();
        }

        private Result<AuthenticationResult> IssueTokens(string login)
        {
            var tokenResult = _tokenService.Value.GenerateTokenPair(login);

            if (tokenResult.IsFailure)
            {
                _logger.LogError("Ошибка генерации токенов для пользователя {Login}: {Error}", login, tokenResult.Error);
                return Result.Failure<AuthenticationResult>("Ошибка генерации токена пользователя");
            }

            _logger.LogInformation("Успешная аутентификация пользователя {Login}", login);
            return Result.Success(ToAuthenticationResult(tokenResult.Value));
        }

        private bool RequiresPasswordChange(UserEntity user)
        {
            if (user.MustChangePassword)
                return true;

            if (_passwordHasher.Value.IsHashed(user.Password))
                return false;

            return string.Equals(user.Password, DefaultUserCredentials.Password, StringComparison.Ordinal);
        }

        private static Result ValidateNewPassword(string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword))
                return Result.Failure("Укажите новый пароль");

            if (string.Equals(newPassword.Trim(), DefaultUserCredentials.Password, StringComparison.Ordinal))
                return Result.Failure("Нельзя оставить пароль по умолчанию");

            return Result.Success();
        }

        private async Task<Result> MarkPasswordConfigured()
        {
            var parameters = await _parametersService.Value.Current();
            parameters.Security ??= new();
            if (parameters.Security.PasswordConfigured)
                return Result.Success();

            parameters.Security.PasswordConfigured = true;
            if (!await _parametersService.Value.Update(parameters))
                return Result.Failure("Не удалось сохранить признак установленного пароля");

            return Result.Success();
        }

        private static AuthenticationResult ToAuthenticationResult(Domain.Authentication.Dto.TokenPair tokens)
            => new()
            {
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
                ExpiresAt = tokens.ExpiresAt
            };
    }
}
