namespace Domain.Authentication.Dto;

public record AgentHandshakeRequest
{
    public string Token { get; init; } = string.Empty;
    public long Timestamp { get; init; }
    public string Nonce { get; init; } = string.Empty;
    public string Signature { get; init; } = string.Empty;
}
