using Application.Authentication.DTO;
using CSharpFunctionalExtensions;

namespace Application.Authentication.Interfaces
{
    public interface IAuthenticationApplicationService
    {
        Task<Result<AuthenticationResult>> Authenticate(string login, string password);
        Result<AuthenticationResult> RefreshToken(string refreshToken);
        Result Logout(string refreshToken);
    }
}