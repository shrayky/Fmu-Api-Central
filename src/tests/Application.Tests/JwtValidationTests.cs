using Authentication;
using Domain.Authentication;

namespace Application.Tests;

public class JwtValidationTests
{
    /// <summary>
    /// Истёкший JWT не принимается из‑за запаса часов.
    /// </summary>
    [Fact]
    public void TokenValidation_ClockSkew_ноль()
    {
        var parameters = AuthenticationExtensions.CreateTokenValidationParameters(new JwtSettings
        {
            Issuer = "test",
            Audience = "api",
            Key = Convert.ToBase64String(new byte[64])
        });

        Assert.Equal(TimeSpan.Zero, parameters.ClockSkew);
    }
}
