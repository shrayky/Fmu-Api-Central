namespace Domain.Authentication.Interfaces;

public interface ILoginAttemptService
{
    bool IsLocked(string login);
    void RegisterFailure(string login);
    void RegisterSuccess(string login);
}
