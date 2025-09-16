namespace Domain.Authentication.Interfaces
{
    public interface IAuthenticationPolicyService
    {
        Task<bool> ValidateFallbackCredentials(string login, string password);
        bool IsFallbackModeEnabled();
    }
}
