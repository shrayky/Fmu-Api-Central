namespace Domain.Authentication.Dto;

public record AgentAccessToken(string AccessToken, DateTime ExpiresAt);
