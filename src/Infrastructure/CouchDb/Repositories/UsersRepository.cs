using CouchDb.DatabaseScheme;
using CouchDB.Driver.Extensions;
using CSharpFunctionalExtensions;
using Domain.Dto.Responces;
using Domain.Entitys;
using Domain.Entitys.Users.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CouchDb.Repositories
{
    public class UsersRepository : BaseCouchDbRepository<UserEntity>, IUserRepository
    {
        private const string LogRepository = "пользователя";

        public UsersRepository(IServiceProvider services) : base(services.GetRequiredService<Context>().Users, services)
        {
        }

        public string DatabaseName() => DatabaseNames.Users;

        public async Task<Result<UserEntity>> ByName(string name)
        {
            var document = await _database.FirstOrDefaultAsync(p => p.Data.Name == name);

            if (document == null)
                return Result.Failure<UserEntity>($"Не найден пользователь с именем {name}");

            return Result.Success(document.Data);
        }

        public async Task<Result<UserEntity>> GetById(string id)
        {
            if (!_appState.DbState())
                return Result.Failure<UserEntity>(DatabaseUnavailable);

            try
            {
                var searchResult = await GetByIdAsync(id);
                if (searchResult is null)
                    return Result.Failure<UserEntity>($"Не найдена запись {LogRepository} с id {id}");

                return Result.Success(searchResult);
            }
            catch (Exception ex)
            {
                return Result.Failure<UserEntity>($"Не удалось прочитать запись {LogRepository} с {id} в БД {ex.Message}");
            }
        }

        public async Task<Result> Create(UserEntity entity)
        {
            if (!_appState.DbState())
                return Result.Failure(DatabaseUnavailable);

            try
            {
                var createResult = await CreateAsync(entity);
                return createResult
                    ? Result.Success()
                    : Result.Failure($"Не удалось создать запись {LogRepository} с {entity.Id} в БД");
            }
            catch (Exception ex)
            {
                return Result.Failure($"Не удалось создать запись {LogRepository} с {entity.Id} в БД {ex.Message}");
            }
        }

        public async Task<Result> Update(UserEntity entity)
        {
            if (!_appState.DbState())
                return Result.Failure(DatabaseUnavailable);

            try
            {
                var updateResult = await CreateAsync(entity);
                return updateResult
                    ? Result.Success()
                    : Result.Failure($"Не удалось создать или обновить запись {LogRepository} с {entity.Id}");
            }
            catch (Exception ex)
            {
                return Result.Failure($"Не удалось обновить запись {LogRepository} с {entity.Id} в БД {ex.Message}");
            }
        }

        public async Task<Result> Delete(string id)
        {
            if (!_appState.DbState())
                return Result.Failure(DatabaseUnavailable);

            try
            {
                var searchResult = await GetByIdAsync(id);
                if (searchResult is null)
                    return Result.Failure($"Не найдена запись {LogRepository} с id {id}");

                var deleteResult = await DeleteAsync(searchResult.Id);
                return deleteResult
                    ? Result.Success()
                    : Result.Failure($"Не удалось удалить запись {LogRepository} с {id} в БД.");
            }
            catch (Exception ex)
            {
                return Result.Failure($"Не удалось удалить запись {LogRepository} с {id} в БД {ex.Message}");
            }
        }

        public async Task<PaginatedResponse<UserEntity>> List(int pageNumber, int pageSize)
        {
            if (!_appState.DbState())
            {
                return new PaginatedResponse<UserEntity>
                {
                    Description = DatabaseUnavailable,
                    ListEnabled = false,
                    TotalCount = 1,
                    PageSize = pageSize,
                    CurrentPage = 1,
                    Content = []
                };
            }

            try
            {
                var query = _database.AsQueryable();
                var entities = await query
                    .OrderBy(p => p.Data.Name)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return new PaginatedResponse<UserEntity>
                {
                    Content = entities.Select(record => record.Data),
                    CurrentPage = pageNumber,
                    PageSize = pageSize,
                    TotalCount = await RecordCount(),
                    SearchTerm = ""
                };
            }
            catch (Exception ex)
            {
                return new PaginatedResponse<UserEntity>
                {
                    Description = ex.Message,
                    ListEnabled = false,
                    TotalCount = 1,
                    PageSize = pageSize,
                    CurrentPage = 1,
                    Content = []
                };
            }
        }

        public async Task<List<UserEntity>> All()
        {
            if (!_appState.DbState())
                return [];

            var appConfig = await _parameters.Current();
            var queryLimit = appConfig.DatabaseConnection.QueryLimit;
            var dbDocs = await _database.Take(queryLimit).ToListAsync();

            return dbDocs.Select(doc => doc.Data)
                .OrderBy(user => user.Name)
                .ToList();
        }
    }
}
