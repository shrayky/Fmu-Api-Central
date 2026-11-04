using CouchDB.Driver.Extensions;
using CouchDB.Driver.Types;
using CSharpFunctionalExtensions;
using Domain.Dto.Responces;
using Domain.Entitys.InstanceGroup;
using Domain.Entitys.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CouchDb.Repositories;

public class InstanceGroupsRepository : BaseCouchDbRepository<InstanceGroupEntity>, IInstanceGroupRepository
{
    private const string LogRepository = "группы инстансов";

    public InstanceGroupsRepository(IServiceProvider services) : base(
        services.GetRequiredService<Context>().InstanceGroups, services)
    {
    }

    public async Task<Result> Create(InstanceGroupEntity entity)
    {
        if (!_appState.DbState())
            return Result.Failure(DatabaseUnavailable);

        try
        {
            var createResult = await CreateAsync(entity);
            if (!createResult)
                return Result.Failure($"Не удалось создать запись {LogRepository} с {entity.Id} в БД");

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Не удалось создать запись {LogRepository} с {entity.Id} в БД {ex.Message}");
        }
    }

    public async Task<Result> Update(InstanceGroupEntity entity)
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

    public async Task<Result<InstanceGroupEntity>> GetById(string id)
    {
        if (!_appState.DbState())
            return Result.Failure<InstanceGroupEntity>(DatabaseUnavailable);

        try
        {
            var searchResult = await GetByIdAsync(id);
            if (searchResult is null)
                return Result.Failure<InstanceGroupEntity>($"Не найдена запись {LogRepository} с id {id}");

            return Result.Success(searchResult);
        }
        catch (Exception ex)
        {
            return Result.Failure<InstanceGroupEntity>($"Не удалось прочитать запись {LogRepository} с {id} в БД {ex.Message}");
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

    public async Task<PaginatedResponse<InstanceGroupEntity>> List(int pageNumber, int pageSize)
    {
        if (!_appState.DbState())
        {
            return new PaginatedResponse<InstanceGroupEntity>
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

            var content = new List<InstanceGroupEntity>();
            foreach (var record in entities)
            {
                record.Data.HasCheckerSettings = await HasSettingsAttachment(record.Data.Id);
                content.Add(record.Data);
            }

            return new PaginatedResponse<InstanceGroupEntity>
            {
                Content = content,
                CurrentPage = pageNumber,
                PageSize = pageSize,
                TotalCount = await RecordCount(),
                SearchTerm = ""
            };
        }
        catch (Exception ex)
        {
            return new PaginatedResponse<InstanceGroupEntity>
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

    public async Task<List<InstanceGroupEntity>> All()
    {
        if (!_appState.DbState())
            return [];

        var appConfig = await _parameters.Current();
        var queryLimit = appConfig.DatabaseConnection.QueryLimit;
        var dbDocs = await _database.Take(queryLimit).ToListAsync();

        return dbDocs.Select(doc => doc.Data)
            .OrderBy(group => group.Name)
            .ToList();
    }

    public async Task<List<InstanceGroupEntity>> ByListId(List<string> ids)
    {
        if (!_appState.DbState() || ids.Count == 0)
            return [];

        try
        {
            return await GetListByIdAsync(ids);
        }
        catch
        {
            return [];
        }
    }

    public async Task<Result> ClearSettingsSchemaLink(string settingsSchemaId)
    {
        if (!_appState.DbState())
            return Result.Failure(DatabaseUnavailable);

        try
        {
            var groups = await _database.Where(p => p.Data.SettingsSchemaId == settingsSchemaId).ToListAsync();
            if (groups.Count == 0)
                return Result.Success();

            // По одной группе, а не bulk: Update читает документ с заглушками вложений
            // и не стирает загруженный шаблон настроек.
            foreach (var document in groups)
            {
                document.Data.SettingsSchemaId = string.Empty;
                var update = await Update(document.Data);
                if (update.IsFailure)
                    return update;
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result> ClearOrganizationLink(string organizationId)
    {
        if (!_appState.DbState())
            return Result.Failure(DatabaseUnavailable);

        try
        {
            var groups = await All();
            foreach (var group in groups)
            {
                var ids = group.OrganizationIds;
                if (ids is null || !ids.Exists(id =>
                        string.Equals(id, organizationId, StringComparison.OrdinalIgnoreCase)))
                    continue;

                ids.RemoveAll(id =>
                    string.Equals(id, organizationId, StringComparison.OrdinalIgnoreCase));

                var update = await Update(group);
                if (update.IsFailure)
                    return update;
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result> SaveCheckerSettings(string groupId, byte[] content)
    {
        if (!_appState.DbState())
            return Result.Failure(DatabaseUnavailable);

        var directory = Directory.CreateTempSubdirectory("checker-settings");
        var path = Path.Combine(directory.FullName, CheckerSettingsAttachment.Name);
        try
        {
            await File.WriteAllBytesAsync(path, content);
            var response = await _database.ReadItemAsync(groupId);
            if (response == null)
                return Result.Failure(CheckerSettingsAttachment.GroupNotFoundMessage);

            await _database.UpsertAttachmentAsync(groupId, response.Rev, path, "application/json");
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Не удалось сохранить настройки группы: {ex.Message}");
        }
        finally
        {
            directory.Delete(true);
        }
    }

    public async Task<Result> DeleteCheckerSettings(string groupId)
    {
        if (!_appState.DbState())
            return Result.Failure(DatabaseUnavailable);

        try
        {
            var response = await _database.ReadItemAsync(groupId);
            if (response == null)
                return Result.Failure(CheckerSettingsAttachment.GroupNotFoundMessage);

            if (!HasSettings(response.Attachments))
                return Result.Success();

            await _database.DeleteAttachmentAsync(CheckerSettingsAttachment.Name, groupId, response.Rev);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Не удалось удалить настройки группы: {ex.Message}");
        }
    }

    public async Task<Result<CheckerSettingsRead>> ReadCheckerSettings(string groupId)
    {
        if (!_appState.DbState())
            return Result.Failure<CheckerSettingsRead>(DatabaseUnavailable);

        try
        {
            var response = await _database.ReadItemAsync(groupId);
            if (response == null || !HasSettings(response.Attachments))
                return Result.Success(new CheckerSettingsRead(false, []));

            // Клиент CouchDB ограничен QueryTimeout. Клиент файла обновления ждёт бесконечно.
            await using var stream = await _database.DownloadAttachmentAsStreamAsync(
                CheckerSettingsAttachment.Name, groupId, response.Rev);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            return Result.Success(new CheckerSettingsRead(true, memory.ToArray()));
        }
        catch (Exception)
        {
            return Result.Failure<CheckerSettingsRead>(CheckerSettingsAttachment.ReadFailedMessage);
        }
    }

    public async Task<Result<bool>> HasCheckerSettings(string groupId)
    {
        if (!_appState.DbState())
            return Result.Failure<bool>(DatabaseUnavailable);

        try
        {
            var response = await _database.ReadItemAsync(groupId);
            return Result.Success(HasSettings(response?.Attachments));
        }
        catch (Exception ex)
        {
            return Result.Failure<bool>($"Не удалось прочитать настройки группы: {ex.Message}");
        }
    }

    private async Task<bool> HasSettingsAttachment(string groupId)
    {
        var response = await _database.ReadItemAsync(groupId);
        return HasSettings(response?.Attachments);
    }

    private static bool HasSettings(IEnumerable<ReadItemAttachment>? attachments) =>
        attachments?.Any(item =>
            string.Equals(item.Name, CheckerSettingsAttachment.Name, StringComparison.OrdinalIgnoreCase)) == true;
}
