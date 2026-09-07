namespace Domain.Entitys.Users.Dto;

public record ChangeUserPasswordRequest
{
    public string Password { get; set; } = string.Empty;
}
