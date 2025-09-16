using CSharpFunctionalExtensions;

namespace Domain.Authentication.Interfaces
{
    public interface IRefreshTokenService
    {
        void SaveRefreshToken(string refreshToken, string login, DateTime expiresAt);
        Result<string> LoginByRefreshToken(string refreshToken);
        void RemoveRefreshToken(string refreshToken);
        bool IsRefreshTokenValid(string refreshToken);
    }
}
