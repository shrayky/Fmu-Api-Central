using Authentication.Services;
using Domain.Authentication;
using Domain.Authentication.Dto;
using System.Text;
using System.Text.Json;

namespace Application.Tests;

public class JwtAgentTokenTests
{
    /// <summary>
    /// JWT узла помечен typ=agent, чтобы не пройти в пользовательские API.
    /// </summary>
    [Fact]
    public void GenerateAgentToken_ставит_claim_agent()
    {
        var sut = new JwtTokenService(
            new JwtSettings
            {
                Issuer = "test",
                Audience = "api",
                Key = Convert.ToBase64String(new byte[64]),
                AgentLifetimeMinutes = 15
            },
            new FakeRefresh());

        var token = sut.GenerateAgentToken("instance-1");
        var payload = ReadJwtPayload(token.AccessToken);

        Assert.Equal(AgentAuthClaims.Agent, Claim(payload, AgentAuthClaims.Type));
        Assert.Equal("instance-1", Claim(payload, AgentAuthClaims.InstanceId));
        Assert.Contains("instance-1", payload.ToString());
    }

    private static string? Claim(JsonElement payload, string name)
        => payload.TryGetProperty(name, out var value) ? value.GetString() : null;

    private static JsonElement ReadJwtPayload(string jwt)
    {
        var parts = jwt.Split('.');
        Assert.Equal(3, parts.Length);

        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
        }

        return JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload))).RootElement;
    }

    private sealed class FakeRefresh : Domain.Authentication.Interfaces.IRefreshTokenService
    {
        public CSharpFunctionalExtensions.Result<string> LoginByRefreshToken(string refreshToken)
            => CSharpFunctionalExtensions.Result.Success("x");
        public bool IsRefreshTokenValid(string refreshToken) => true;
        public void RemoveRefreshToken(string refreshToken) { }
        public void SaveRefreshToken(string refreshToken, string login, DateTime expiresAt) { }
    }
}
