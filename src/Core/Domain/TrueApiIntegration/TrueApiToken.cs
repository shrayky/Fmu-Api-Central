namespace Domain.TrueApiIntegration;

public class TrueApiToken
{
    public string Inn { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public DateTime LiveUntil { get; set; }
}
