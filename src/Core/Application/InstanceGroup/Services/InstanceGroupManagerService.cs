using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Configuration.Interfaces;
using Domain.Dto.Responces;
using Domain.Entitys.Instance.Dto;
using Domain.Entitys.Instance.Interfaces;
using Domain.Entitys.InstanceGroup;
using Domain.Entitys.InstanceGroup.Dto;
using Domain.Entitys.InstanceGroup.Interfaces;
using Domain.Entitys.Interfaces;
using Application.SettingsSchema;
using Domain.Entitys.SettingsSchema.Dto;
using Domain.Entitys.SettingsSchema.Interfaces;

namespace Application.InstanceGroup.Services;

[AutoRegisterService]
public class InstanceGroupManagerService : IInstanceGroupManagerService
{
    private const int OnlineIntervalMultiplier = 3;

    private readonly IInstanceGroupRepository _repository;
    private readonly IInstanceRepository _instanceRepository;
    private readonly IInstanceManagerService _instanceManagerService;
    private readonly ISettingsSchemaRepository _settingsSchemaRepository;
    private readonly IParametersService _parametersService;

    public InstanceGroupManagerService(
        IInstanceGroupRepository repository,
        IInstanceRepository instanceRepository,
        IInstanceManagerService instanceManagerService,
        ISettingsSchemaRepository settingsSchemaRepository,
        IParametersService parametersService)
    {
        _repository = repository;
        _instanceRepository = instanceRepository;
        _instanceManagerService = instanceManagerService;
        _settingsSchemaRepository = settingsSchemaRepository;
        _parametersService = parametersService;
    }

    public async Task<Result> Create(InstanceGroupView data)
    {
        var exist = await _repository.GetById(data.Id);

        if (exist.IsSuccess)
        {
            Apply(exist.Value, data);
            return await _repository.Update(exist.Value);
        }

        var entity = new InstanceGroupEntity { Id = data.Id };
        Apply(entity, data);
        return await _repository.Create(entity);
    }

    public async Task<Result> Update(InstanceGroupView data)
    {
        var exist = await _repository.GetById(data.Id);
        if (exist.IsFailure)
            return Result.Failure(exist.Error);

        Apply(exist.Value, data);
        return await _repository.Update(exist.Value);
    }

    public async Task<Result> Delete(string id)
    {
        var clearLinks = await _instanceRepository.ClearGroupLink(id);
        if (clearLinks.IsFailure)
            return Result.Failure($"Ошибка очистки ссылок на группу {id}: {clearLinks.Error}");

        var exist = await _repository.GetById(id);
        if (exist.IsFailure)
            return Result.Failure(exist.Error);

        return await _repository.Delete(id);
    }

    public async Task<PaginatedResponse<InstanceGroupView>> List(int pageNumber, int pageSize)
    {
        var entityList = await _repository.List(pageNumber, pageSize);
        var counts = await CountInstancesByGroup();
        var schemaNames = await ResolveSchemaNames(entityList.Content.Select(entity => entity.SettingsSchemaId));

        var content = entityList.Content.Select(entity => ToView(
            entity,
            counts.TryGetValue(entity.Id, out var groupCounts) ? groupCounts : (0, 0),
            schemaNames.GetValueOrDefault(entity.SettingsSchemaId, string.Empty)));

        return new PaginatedResponse<InstanceGroupView>
        {
            Description = entityList.Description,
            ListEnabled = entityList.ListEnabled,
            TotalCount = entityList.TotalCount,
            PageSize = entityList.PageSize,
            CurrentPage = entityList.CurrentPage,
            Content = content
        };
    }

    public async Task<Result<ForceUpdateResult>> AssignForcedUpdate(IReadOnlyList<string> groupIds, string updateId)
    {
        if (groupIds == null || groupIds.Count == 0)
            return Result.Failure<ForceUpdateResult>("Не выбраны группы");

        if (string.IsNullOrWhiteSpace(updateId))
            return Result.Failure<ForceUpdateResult>("Не указан идентификатор обновления");

        var instances = await _instanceRepository.ByGroupIds(groupIds);
        if (instances.IsFailure)
            return Result.Failure<ForceUpdateResult>(instances.Error);

        var tokens = instances.Value
            .Select(instance => instance.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (tokens.Count == 0)
            return Result.Failure<ForceUpdateResult>("В выбранных группах нет инстансов");

        return await _instanceManagerService.AssignForcedUpdate(tokens, updateId);
    }

    public async Task<Result<ForceUpdateResult>> ExportSettings(IReadOnlyList<string> groupIds)
    {
        if (groupIds == null || groupIds.Count == 0)
            return Result.Failure<ForceUpdateResult>("Не выбраны группы");

        var groups = await _repository.ByListId(groupIds.ToList());
        if (groups.Count == 0)
            return Result.Failure<ForceUpdateResult>("Группы не найдены");

        var assigned = 0;
        var skipped = 0;

        foreach (var group in groups)
        {
            if (string.IsNullOrWhiteSpace(group.SettingsSchemaId))
                return Result.Failure<ForceUpdateResult>($"У группы «{group.Name}» не назначена схема настроек");

            var schemaSearch = await _settingsSchemaRepository.GetById(group.SettingsSchemaId);
            if (schemaSearch.IsFailure)
                return Result.Failure<ForceUpdateResult>($"Схема группы «{group.Name}» не найдена: {schemaSearch.Error}");

            var instances = await _instanceRepository.ByGroupIds([group.Id]);
            if (instances.IsFailure)
                return Result.Failure<ForceUpdateResult>(instances.Error);

            if (instances.Value.Count == 0)
            {
                skipped++;
                continue;
            }

            foreach (var instance in instances.Value)
            {
                instance.Settings = GroupSettingsApply.Apply(
                    instance.Settings,
                    schemaSearch.Value.HttpRequestTimeouts,
                    schemaSearch.Value.GisMtProductMappings,
                    schemaSearch.Value.HostsToPing);
                instance.SettingsModified = true;

                var save = await _instanceRepository.Update(instance);
                if (save.IsFailure)
                {
                    skipped++;
                    continue;
                }

                assigned++;
            }
        }

        if (assigned == 0 && skipped == 0)
            return Result.Failure<ForceUpdateResult>("В выбранных группах нет инстансов");

        return Result.Success(new ForceUpdateResult
        {
            Assigned = assigned,
            Skipped = skipped,
            Description = $"Назначено: {assigned}, пропущено: {skipped}"
        });
    }

    public async Task<List<GroupLink>> AllLinks()
    {
        var groups = await _repository.All();

        return groups.Select(group => new GroupLink
        {
            Id = group.Id,
            Name = group.Name
        }).ToList();
    }

    private static void Apply(InstanceGroupEntity entity, InstanceGroupView data)
    {
        entity.Name = data.Name;
        entity.AutoUpdateAllowed = data.AutoUpdateAllowed;
        entity.SettingsSchemaId = data.SettingsSchema?.Id ?? string.Empty;
    }

    private static InstanceGroupView ToView(
        InstanceGroupEntity entity,
        (int Total, int Online) counts,
        string schemaName) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        AutoUpdateAllowed = entity.AutoUpdateAllowed,
        InstancesTotal = counts.Total,
        InstancesOnline = counts.Online,
        SettingsSchema = new SettingsSchemaLink
        {
            Id = entity.SettingsSchemaId,
            Name = schemaName
        }
    };

    private async Task<Dictionary<string, string>> ResolveSchemaNames(IEnumerable<string> schemaIds)
    {
        var ids = schemaIds
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ids.Count == 0)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var schemas = await _settingsSchemaRepository.ByListId(ids);
        return schemas.ToDictionary(schema => schema.Id, schema => schema.Name, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, (int Total, int Online)>> CountInstancesByGroup()
    {
        var counts = new Dictionary<string, (int Total, int Online)>(StringComparer.OrdinalIgnoreCase);
        var instances = await _instanceRepository.All();
        if (instances.IsFailure)
            return counts;

        var interval = (await _parametersService.Current()).SoftwareUpdateSettings.ExchangeRequestInterval;
        if (interval <= 0)
            interval = 60;

        var onlineAfter = DateTime.Now.AddSeconds(-interval * OnlineIntervalMultiplier);

        foreach (var instance in instances.Value)
        {
            if (string.IsNullOrEmpty(instance.GroupId))
                continue;

            counts.TryGetValue(instance.GroupId, out var current);
            var online = instance.UpdatedAt >= onlineAfter ? 1 : 0;
            counts[instance.GroupId] = (current.Total + 1, current.Online + online);
        }

        return counts;
    }
}
