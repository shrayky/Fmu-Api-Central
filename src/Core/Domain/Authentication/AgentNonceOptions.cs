namespace Domain.Authentication;

public class AgentNonceOptions
{
    public TimeSpan TimestampWindow { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan NonceLifetime { get; set; } = TimeSpan.FromMinutes(10);
}
