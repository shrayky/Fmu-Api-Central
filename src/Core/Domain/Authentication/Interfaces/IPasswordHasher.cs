namespace Domain.Authentication.Interfaces;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string stored);
    bool IsHashed(string stored);
}
