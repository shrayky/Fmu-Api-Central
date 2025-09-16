using CSharpFunctionalExtensions;
using Domain.Authentication.Dto;

namespace Domain.Authentication.Interfaces
{
    public interface ITokenService
    {
        string GenerateToken(string login);
        Result<TokenPair> GenerateTokenPair(string login);
        Result<TokenPair> RefreshAccessToken(string refreshToken);
        bool ValidateRefreshToken(string refreshToken);
    }
}
