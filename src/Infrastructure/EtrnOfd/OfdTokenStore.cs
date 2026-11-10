namespace EtrnOfd;

public sealed class OfdTokenStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, OfdToken> _tokens = new(StringComparer.OrdinalIgnoreCase);

    public bool TryGet(string connectionId, DateTimeOffset now, out string accessToken)
    {
        lock (_gate)
        {
            if (_tokens.TryGetValue(connectionId, out var token) && now < token.ExpiresAt)
            {
                accessToken = token.AccessToken;
                return true;
            }
        }

        accessToken = "";
        return false;
    }

    public string RefreshToken(string connectionId)
    {
        lock (_gate)
            return _tokens.TryGetValue(connectionId, out var token) ? token.RefreshToken : "";
    }

    public void Remember(string connectionId, string accessToken, string? refreshToken, int expiresIn, DateTimeOffset now)
    {
        lock (_gate)
        {
            var previous = _tokens.GetValueOrDefault(connectionId)?.RefreshToken ?? "";
            var refresh = string.IsNullOrEmpty(refreshToken) ? previous : refreshToken;
            _tokens[connectionId] = new OfdToken(accessToken, refresh, now.AddSeconds(expiresIn));
        }
    }

    private sealed record OfdToken(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);
}
