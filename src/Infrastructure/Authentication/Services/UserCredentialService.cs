using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Authentication.Interfaces;
using Domain.Entitys;
using Domain.Entitys.Users.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Authentication.Services
{
    [AutoRegisterService(ServiceLifetime.Scoped)]
    public class UserCredentialService : IUserCredentialService
    {
        private readonly IUserRepository _usersRepository;
        private readonly IPasswordHasher _passwordHasher;

        public UserCredentialService(IUserRepository usersRepository, IPasswordHasher passwordHasher)
        {
            _usersRepository = usersRepository;
            _passwordHasher = passwordHasher;
        }

        public async Task<Result<UserEntity>> GetUserByLogin(string login)
        {
            return await _usersRepository.ByName(login);
        }

        public async Task<bool> ValidatePassword(UserEntity user, string password)
        {
            await Task.Delay(1);
            return _passwordHasher.Verify(password, user.Password);
        }
    }
}
