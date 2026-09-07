namespace Application.Authentication.DTO;

public record ChangePasswordRequest
{
    public string Login { get; set; } = string.Empty;
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
