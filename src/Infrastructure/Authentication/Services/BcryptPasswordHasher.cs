using Domain.Attributes;
using Domain.Authentication.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Authentication.Services;

[AutoRegisterService(ServiceLifetime.Singleton)]
public class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password)
        => BCrypt.Net.BCrypt.HashPassword(password);

    public bool Verify(string password, string stored)
    {
        if (string.IsNullOrEmpty(stored))
            return false;

        if (IsHashed(stored))
            return BCrypt.Net.BCrypt.Verify(password, stored);

        return string.Equals(password, stored, StringComparison.Ordinal);
    }

    public bool IsHashed(string stored)
        => !string.IsNullOrEmpty(stored) && stored.StartsWith("$2", StringComparison.Ordinal);
}
