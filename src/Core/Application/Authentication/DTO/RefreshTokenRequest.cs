namespace Application.Authentication.DTO
{
    public record RefreshTokenRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
    }
}
