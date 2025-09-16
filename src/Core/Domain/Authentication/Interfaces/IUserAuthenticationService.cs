namespace Domain.Authentication.Interfaces
{
    public interface IUserAuthenticationService
    {
        Task<bool> ValidateCredentials(string login, string password);
    }
}
