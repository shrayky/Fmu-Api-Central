using System.Security.Claims;

namespace Domain.Authentication;

public static class UserAuthPolicy
{
    public static bool IsUser(ClaimsPrincipal user)
        => user.FindFirst(AgentAuthClaims.Type)?.Value != AgentAuthClaims.Agent;
}
