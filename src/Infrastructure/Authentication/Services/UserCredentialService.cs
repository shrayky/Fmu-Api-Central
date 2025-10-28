using CouchDb.Repositories;
using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Authentication.Interfaces;
using Domain.Dto;
using Domain.Entitys;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Authentication.Services
{
    [AutoRegisterService(ServiceLifetime.Scoped)]
    public class UserCredentialService : IUserCredentialService
    {
        private readonly UsersRepository _usersRepository;
        private readonly ILogger<UserCredentialService> _logger;

        public UserCredentialService(UsersRepository usersRepository, ILogger<UserCredentialService> logger)
        {
            _usersRepository = usersRepository;
            _logger = logger;
        }

        public async Task<Result<UserEntity>> GetUserByLogin(string login)
        {
            return await _usersRepository.ByName(login);
        }

        public async Task<bool> ValidatePassword(UserEntity user, string password)
        {
            // функция асинхронная потому что в будующем возможна более долгая и сложная валидация

            // что бы компиляторо не ругался
            await Task.Delay(1);
            
            return user.Password == password;
        }
    }
}
