namespace Domain.Authentication.Dto
{
    public record TokenPair(string AccessToken, string RefreshToken, DateTime ExpiresAt);
}
