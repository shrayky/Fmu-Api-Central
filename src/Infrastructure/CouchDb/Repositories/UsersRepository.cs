using CouchDB.Driver.Extensions;
using CSharpFunctionalExtensions;
using Domain.Dto;
using Domain.Entitys;
using Microsoft.Extensions.DependencyInjection;

namespace CouchDb.Repositories
{
    //[AutoRegisterService(ServiceLifetime.Scoped)]
    public class UsersRepository : BaseCouchDbRepository<UserEntity>
    {
        public UsersRepository(IServiceProvider services) : base(services.GetRequiredService<Context>().Users, services)
        {
        }

        public async Task<Result<UserEntity>> ByName(string name)
        {
            var document = await _database.FirstOrDefaultAsync(p => p.Data.Name == name);

            if (document == null)
                return Result.Failure<UserEntity>($"Не найден пользователь с именем {name}");

            return Result.Success(document.Data);
        }
    }
}
