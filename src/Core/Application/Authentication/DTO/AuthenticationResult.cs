namespace Application.Authentication.DTO
{
    public record AuthenticationResult
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; } = DateTime.MinValue;
    }
}
