using System.Security.Claims;
using Domain.Authentication;

namespace Domain.Tests;

public class UserAuthPolicyTests
{
    /// <summary>
    /// JWT агента не считается пользовательским.
    /// </summary>
    [Fact]
    public void IsUser_false_для_агента()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "instance-1"),
            new Claim(AgentAuthClaims.Type, AgentAuthClaims.Agent)
        ], "test"));

        Assert.False(UserAuthPolicy.IsUser(user));
    }

    /// <summary>
    /// Обычный логин без typ=agent проходит.
    /// </summary>
    [Fact]
    public void IsUser_true_без_claim_агента()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "admin")
        ], "test"));

        Assert.True(UserAuthPolicy.IsUser(user));
    }
}
