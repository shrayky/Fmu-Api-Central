using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Authentication;
using Domain.Authentication.Dto;
using Domain.Authentication.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Authentication.Services
{
    [AutoRegisterService(ServiceLifetime.Singleton)]
    public class JwtTokenService : ITokenService
    {
        private readonly JwtSettings _settings;
        private readonly IRefreshTokenService _refreshTokenService;
        
        public JwtTokenService(JwtSettings settings, IRefreshTokenService refreshTokenService)
        {
            _settings = settings;
            _refreshTokenService = refreshTokenService;
        }

        public string GenerateToken(string login)
        {
            var jwtExpiresAt = DateTime.UtcNow.AddMinutes(_settings.LifetimeMinutes);
            return GenerateJwtToken(login, jwtExpiresAt);
        }

        public Result<TokenPair> GenerateTokenPair(string login)
        {
            var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenLifetimeDays);
            var jwtExpiresAt = DateTime.UtcNow.AddMinutes(_settings.LifetimeMinutes);

            var accessToken = GenerateJwtToken(login, jwtExpiresAt);

            var refreshToken = GenerateRefreshToken(login);

            _refreshTokenService.SaveRefreshToken(refreshToken, login, refreshTokenExpiresAt);

            TokenPair data = new(accessToken, refreshToken, jwtExpiresAt);

            return Result.Success(data);
        }

        public Result<TokenPair> RefreshAccessToken(string refreshToken)
        {
            var refreshLloginResult = _refreshTokenService.LoginByRefreshToken(refreshToken);

            if (refreshLloginResult.IsFailure)
                return Result.Failure<TokenPair>(refreshLloginResult.Error);

            var login = refreshLloginResult.Value;

            var jwtExpiresAt = DateTime.UtcNow.AddMinutes(_settings.LifetimeMinutes);

            string accessToken = GenerateJwtToken(login, jwtExpiresAt);

            TokenPair data = new(accessToken, refreshToken, jwtExpiresAt);

            return Result.Success(data);
        }

        public bool ValidateRefreshToken(string refreshToken) => _refreshTokenService.IsRefreshTokenValid(refreshToken);

        private string GenerateJwtToken(string login, DateTime expires)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, login)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _settings.Issuer,
                audience: _settings.Audience,
                claims: claims,
                expires: expires,
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateRefreshToken(string login) => Convert.ToBase64String(Guid.NewGuid().ToByteArray());

    }
}
