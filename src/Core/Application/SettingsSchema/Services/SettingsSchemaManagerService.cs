using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Dto.Responces;
using Domain.Entitys.Interfaces;
using Domain.Entitys.SettingsSchema;
using Domain.Entitys.SettingsSchema.Dto;
using Domain.Entitys.SettingsSchema.Interfaces;

namespace Application.SettingsSchema.Services;

[AutoRegisterService]
public class SettingsSchemaManagerService : ISettingsSchemaManagerService
{
    private readonly ISettingsSchemaRepository _repository;
    private readonly IInstanceGroupRepository _instanceGroupRepository;

    public SettingsSchemaManagerService(
        ISettingsSchemaRepository repository,
        IInstanceGroupRepository instanceGroupRepository)
    {
        _repository = repository;
        _instanceGroupRepository = instanceGroupRepository;
    }

    public async Task<Result> Create(SettingsSchemaView data)
    {
        var exist = await _repository.GetById(data.Id);

        if (exist.IsSuccess)
        {
            Apply(exist.Value, data);
            return await _repository.Update(exist.Value);
        }

        var entity = new SettingsSchemaEntity { Id = data.Id };
        Apply(entity, data);
        EnsureMappings(entity);
        return await _repository.Create(entity);
    }

    public async Task<Result> Update(SettingsSchemaView data)
    {
        var exist = await _repository.GetById(data.Id);
        if (exist.IsFailure)
            return Result.Failure(exist.Error);

        Apply(exist.Value, data);
        return await _repository.Update(exist.Value);
    }

    public async Task<Result> Delete(string id)
    {
        var clearLinks = await _instanceGroupRepository.ClearSettingsSchemaLink(id);
        if (clearLinks.IsFailure)
            return Result.Failure($"Ошибка очистки ссылок на схему {id}: {clearLinks.Error}");

        var exist = await _repository.GetById(id);
        if (exist.IsFailure)
            return Result.Failure(exist.Error);

        return await _repository.Delete(id);
    }

    public async Task<PaginatedResponse<SettingsSchemaView>> List(int pageNumber, int pageSize)
    {
        var entityList = await _repository.List(pageNumber, pageSize);

        return new PaginatedResponse<SettingsSchemaView>
        {
            Description = entityList.Description,
            ListEnabled = entityList.ListEnabled,
            TotalCount = entityList.TotalCount,
            PageSize = entityList.PageSize,
            CurrentPage = entityList.CurrentPage,
            Content = entityList.Content.Select(ToView)
        };
    }

    public async Task<List<SettingsSchemaLink>> AllLinks()
    {
        var schemas = await _repository.All();

        return schemas.Select(schema => new SettingsSchemaLink
        {
            Id = schema.Id,
            Name = schema.Name
        }).ToList();
    }

    public SettingsSchemaView Defaults() => new()
    {
        HttpRequestTimeouts = new HttpRequestTimeouts(),
        GisMtProductMappings = AtolToTrueApiGroupMap.CopyDefaults()
    };

    private static void Apply(SettingsSchemaEntity entity, SettingsSchemaView data)
    {
        entity.Name = data.Name;
        entity.HttpRequestTimeouts = data.HttpRequestTimeouts ?? new HttpRequestTimeouts();
        entity.GisMtProductMappings = data.GisMtProductMappings ?? [];
        entity.HostsToPing = data.HostsToPing ?? [];
    }

    private static void EnsureMappings(SettingsSchemaEntity entity)
    {
        if (entity.GisMtProductMappings.Count == 0)
            entity.GisMtProductMappings = AtolToTrueApiGroupMap.CopyDefaults();
    }

    private static SettingsSchemaView ToView(SettingsSchemaEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        HttpRequestTimeouts = entity.HttpRequestTimeouts,
        GisMtProductMappings = entity.GisMtProductMappings,
        HostsToPing = entity.HostsToPing
    };
}
