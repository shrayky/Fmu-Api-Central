using System.Text.Json;
using Application.Instance.DTO;
using Application.Instance.Interfaces;
using Application.SoftwareUpdates.Interfaces;
using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Dto.FmuApiExchangeData.Answer;
using Domain.Dto.FmuApiExchangeData.Request;
using Domain.Dto.Responces;
using Domain.Entitys;
using Domain.Entitys.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Json;
using Shared.Strings;

namespace Application.Instance.Services;

[AutoRegisterService()]
public class InstanceManagerService : IInstanceManagerService
{
    private readonly ILogger<IInstanceManagerService> _logger;
    private readonly IInstanceRepository _instanceRepository;
    private readonly Lazy<ISoftwareUpdatesManagerService> _softwareVersionsManager;

    public InstanceManagerService(ILogger<IInstanceManagerService> logger, IInstanceRepository instanceRepository,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _instanceRepository = instanceRepository;
        _softwareVersionsManager =
            new Lazy<ISoftwareUpdatesManagerService>(serviceProvider
                .GetRequiredService<ISoftwareUpdatesManagerService>);
    }

    public async Task<Result<FmuApiCentralResponse>> UpdateFmuApiInstanceInformation(string instanceData)
    {
        _logger.LogInformation("Обрабатываю пакет от fmu-api {InstanceData}", instanceData);

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(instanceData));
        var packet = await JsonSerializer.DeserializeAsync<DataPacket>(stream);

        if (packet == null)
            return Result.Failure<FmuApiCentralResponse>($"Не удалось преобразовать входящий пакет {instanceData}!");

        var entitySearchResult = await _instanceRepository.ByToken(packet.Token);

        if (entitySearchResult.IsFailure)
            return Result.Failure<FmuApiCentralResponse>(entitySearchResult.Error);

        var instanceEntity = entitySearchResult.Value;

        var encodedData = packet.Data;

        if (!string.IsNullOrEmpty(instanceEntity.SecretKey))
        {
            encodedData = SecretString.DecryptData(packet.Data, instanceEntity.SecretKey);
        }

        using var payload = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(encodedData));

        Payload? fmuApiState;

        try
        {
            fmuApiState = await JsonSerializer.DeserializeAsync<Payload>(payload);
        }
        catch (Exception e)
        {
            return Result.Failure<FmuApiCentralResponse>(
                $"Входящий пакет {instanceData} не соответствует ожидаемой структуре! {e.Message}");
        }

        if (fmuApiState == null)
            return Result.Failure<FmuApiCentralResponse>(
                $"Входящий пакет {instanceData} не соответствует ожидаемой структуре!");

        instanceEntity.UpdatedAt = DateTime.Now;

        instanceEntity.Cdn = fmuApiState.CdnInformation;
        instanceEntity.LocalModules = fmuApiState.LocalModuleInformation;
        instanceEntity.NodeInformation = fmuApiState.NodeInformation;

        if (!instanceEntity.SettingsModified)
            instanceEntity.Settings = fmuApiState.FmuApiSetting;

        var updateResult = await _instanceRepository.Update(instanceEntity);

        var (needUpdate, updateHash) = await _softwareVersionsManager.Value.NeedUpdate(fmuApiState.NodeInformation.Os,
            fmuApiState.NodeInformation.Architecture,
            fmuApiState.FmuApiSetting.Version,
            fmuApiState.FmuApiSetting.Assembly);

        var answer = new FmuApiCentralResponse()
        {
            SettingsUpdateAvailable = instanceEntity.SettingsModified,
            SoftwareUpdateAvailable = needUpdate,
            UpdateHash = updateHash,
            Success = true,
        };

        return updateResult.IsSuccess
            ? Result.Success(answer)
            : Result.Failure<FmuApiCentralResponse>(updateResult.Error);
    }

    public async Task<PaginatedResponse<InstanceMonitoringInformation>> InstancesList(int pageNumber, int pageSize,
        string filter = "")
    {
        var answer = await _instanceRepository.List(pageNumber, pageSize, filter);

        if (answer.IsFailure)
        {
            return new PaginatedResponse<InstanceMonitoringInformation>()
            {
                ListEnabled = false,
                Description = answer.Value.Description,
                Content = [],
                CurrentPage = 1,
                PageSize = pageSize,
                TotalCount = 0,
            };
        }

        List<InstanceMonitoringInformation> content = [];

        foreach (var entity in answer.Value.Content)
        {
            InstanceMonitoringInformation record = new()
            {
                Name = entity.Name,
                Token = entity.Id,
                Version = $"{entity.Settings.Version}.{entity.Settings.Assembly}",
                LastUpdated = entity.UpdatedAt,
                LocalModules = entity.LocalModules,
            };

            content.Add(record);
        }

        return new()
        {
            Content = content,
            CurrentPage = answer.Value.CurrentPage,
            PageSize = answer.Value.PageSize,
            TotalCount = answer.Value.TotalCount,
            ListEnabled = answer.Value.ListEnabled,
            Description = answer.Value.Description,
        };
    }

    public async Task<bool> CreateNew(InstanceMonitoringInformation instance)
    {
        InstanceEntity entity = new()
        {
            Id = instance.Token,
            Name = instance.Name,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            SecretKey = instance.SecretKey
        };

        var createResult = await _instanceRepository.CreateInstance(entity);

        return createResult.IsSuccess;
    }

    public async Task<bool> Delete(string token)
    {
        var entitySearch = await _instanceRepository.ByToken(token);

        if (entitySearch.IsFailure)
            return true;

        var deleteResult = await _instanceRepository.DeleteInstance(entitySearch.Value);

        if (deleteResult.IsSuccess)
            return true;

        return false;
    }

    public async Task<string> InstanceSettings(string token)
    {
        var entitySearch = await _instanceRepository.ByToken(token);

        if (entitySearch.IsFailure)
            return string.Empty;

        var settings = await JsonHelpers.SerializeAsync(entitySearch.Value.Settings);

        if (!string.IsNullOrEmpty(entitySearch.Value.SecretKey))
        {
            settings = SecretString.EncryptData(settings, entitySearch.Value.SecretKey);
        }

        return settings;
    }

    public async Task<Result> SettingsUploaded(string token)
    {
        var entitySearch = await _instanceRepository.ByToken(token);

        if (entitySearch.IsFailure)
            return Result.Failure($"Узел с id {token} не найден");

        entitySearch.Value.SettingsModified = false;

        var updateResult = await _instanceRepository.Update(entitySearch.Value);

        return updateResult.IsSuccess ? Result.Success() : Result.Failure(updateResult.Error);
    }

    public async Task<Result<Stream>> FmuApiUpdate(string token)
    {
        var entitySearch = await _instanceRepository.ByToken(token);

        if (entitySearch.IsFailure)
            return Result.Failure<Stream>(entitySearch.Error);

        var entity = entitySearch.Value;

        var (needUpdate, _) = await _softwareVersionsManager.Value.NeedUpdate(entity.NodeInformation.Os,
            entity.NodeInformation.Architecture,
            entity.Settings.Version,
            entity.Settings.Assembly);

        if (!needUpdate)
            return Result.Failure<Stream>($"Для узла с id {token} не требуется обновление");

        var updateStream = await _softwareVersionsManager.Value.FmuApiUpdateData(entity.NodeInformation.Os,
            entity.NodeInformation.Architecture,
            entity.Settings.Version,
            entity.Settings.Assembly);

        return updateStream.IsSuccess ? Result.Success(updateStream.Value) : Result.Failure<Stream>(updateStream.Error);
    }

    public async Task<Result<List<InstanceMonitoringInformation>>> OfflineInstance(DateTime toDate)
    {
        var offLineInstances = await _instanceRepository.OfflineInstances(toDate);
        
        if (offLineInstances.IsFailure)
            return Result.Failure<List<InstanceMonitoringInformation>>(offLineInstances.Error);
        
        List<InstanceMonitoringInformation> content = [];

        foreach (var entity in offLineInstances.Value)
        {
            InstanceMonitoringInformation record = new()
            {
                Name = entity.Name,
                Token = entity.Id,
                Version = $"{entity.Settings.Version}.{entity.Settings.Assembly}",
                LastUpdated = entity.UpdatedAt,
                LocalModules = entity.LocalModules,
            };

            content.Add(record);
        }
        
        return Result.Success(content);
    }

    public async Task<Result<List<InstanceMonitoringInformation>>> All()
    {
        var allEntities = await _instanceRepository.All();

        if (allEntities.IsFailure)
            return Result.Failure<List<InstanceMonitoringInformation>>(allEntities.Error);
        
        List<InstanceMonitoringInformation> content = [];
        
        foreach (var entity in allEntities.Value)
        {
            InstanceMonitoringInformation record = new()
            {
                Name = entity.Name,
                Token = entity.Id,
                Version = $"{entity.Settings.Version}.{entity.Settings.Assembly}",
                LastUpdated = entity.UpdatedAt,
                LocalModules = entity.LocalModules,
            };

            content.Add(record);
        }
        
        return Result.Success(content);
    }
}