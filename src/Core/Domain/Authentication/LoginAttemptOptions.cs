namespace Domain.Authentication;

/// <summary>
/// Порог и длительность блока входа.
/// </summary>
public class LoginAttemptOptions
{
    public int MaxFailedAttempts { get; set; } = 5;
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);
}
