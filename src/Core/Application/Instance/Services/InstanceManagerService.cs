using Application.SoftwareUpdates.Interfaces;
using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Configuration.Interfaces;
using Domain.Configuration.Options;
using Domain.Dto.FmuApiExchangeData;
using Domain.Dto.FmuApiExchangeData.Answer;
using Domain.Dto.FmuApiExchangeData.DataPacket;
using Domain.Dto.FmuApiExchangeData.DataPacket.FmuApiState;
using Domain.Dto.FmuApiExchangeData.Request;
using Domain.Dto.Responces;
using Domain.Entitys.Instance;
using Domain.Entitys.Instance.Dto;
using Domain.Entitys.Instance.Interfaces;
using Domain.Entitys.Interfaces;
using Domain.Entitys.SoftwareUpdateFiles;
using Domain.Entitys.MarkCheckStatistics.Interfaces;
using Domain.Entitys.MarksCheckStatistic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Json;
using Shared.Strings;
using System.Text.Json;

namespace Application.Instance.Services;

[AutoRegisterService()]
public class InstanceManagerService : IInstanceManagerService
{
    private readonly ILogger<IInstanceManagerService> _logger;
    private readonly IInstanceRepository _instanceRepository;
    private readonly Lazy<ISoftwareUpdatesManagerService> _softwareVersionsManager;
    private readonly IMarksCheckStatisticRepository _marksCheckStatisticRepository;
    private readonly IParametersService _parametersService;

    public InstanceManagerService(ILogger<IInstanceManagerService> logger, IInstanceRepository instanceRepository,
        IServiceProvider serviceProvider, IMarksCheckStatisticRepository marksCheckStatisticRepository,
        IParametersService parametersService)
    {
        _logger = logger;
        _instanceRepository = instanceRepository;
        
        _softwareVersionsManager =
            new Lazy<ISoftwareUpdatesManagerService>(serviceProvider
                .GetRequiredService<ISoftwareUpdatesManagerService>);

        _marksCheckStatisticRepository = marksCheckStatisticRepository;
        _parametersService = parametersService;
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
        instanceEntity.TsPiots = fmuApiState.TsPiotsInforamtion;
        instanceEntity.NodeInformation = fmuApiState.NodeInformation;

        var loadStatisticResult = await UpdateNodeStatistics(instanceEntity, fmuApiState.CheckMarkStatisticInformation);

        if (loadStatisticResult.IsFailure)
            return Result.Failure<FmuApiCentralResponse>(loadStatisticResult.Error);

        if (!instanceEntity.SettingsModified)
            instanceEntity.Settings = fmuApiState.FmuApiSetting;

        var updateResult = await _instanceRepository.Update(instanceEntity);

        var (needUpdate, updateHash) = await ResolveAvailableUpdate(instanceEntity, fmuApiState.FmuApiSetting);

        var softwareUpdateSettings = (await _parametersService.Current()).SoftwareUpdateSettings;

        if (needUpdate && !softwareUpdateSettings.IsDownloadAllowedNow())
        {
            _logger.LogInformation(
                "Обновление для узла {Token} скрыто: текущее время вне разрешённых интервалов загрузки",
                instanceEntity.Id);
            needUpdate = false;
            updateHash = string.Empty;
        }

        var answer = new FmuApiCentralResponse()
        {
            SettingsUpdateAvailable = instanceEntity.SettingsModified,
            SoftwareUpdateAvailable = needUpdate,
            UpdateHash = updateHash,
            Success = true,
            CentralServerProperties = CreateCentralServerProperties(softwareUpdateSettings),
        };

        return updateResult.IsSuccess
            ? Result.Success(answer)
            : Result.Failure<FmuApiCentralResponse>(updateResult.Error);
    }

    private async Task<Result> UpdateNodeStatistics(InstanceEntity instanceEntity, List<CheckMarkStatisticInformation> checkMarkStatistics)
    {
        var statisticsEntity = new List<MarkCheckStatisticsEntity>();

        foreach (var statistic in checkMarkStatistics)
        {
            MarkCheckStatisticsEntity checkStatisticsEntity = new()
            {
                NodeId = instanceEntity.Id,
                Date = statistic.Date,
                Total = statistic.MarkCheckStatistics.Total,
                SuccessfulOnlineChecks = statistic.MarkCheckStatistics.SuccessfulOnlineChecks,
                SuccessfulOfflineChecks = statistic.MarkCheckStatistics.SuccessfulOfflineChecks,
                Id = $"{instanceEntity.Id}_{statistic.Date}",
            };

            statisticsEntity.Add(checkStatisticsEntity);
        }

        return await _marksCheckStatisticRepository.AddRange(statisticsEntity);
    }

    public async Task<PaginatedResponse<InstanceMonitoringInformation>> InstancesList(
        int pageNumber,
        int pageSize,
        InstanceListFilter filter)
    {
        var answer = await _instanceRepository.List(pageNumber, pageSize, filter);

        if (answer.IsFailure)
        {
            return new PaginatedResponse<InstanceMonitoringInformation>()
            {
                ListEnabled = false,
                Description = answer.Error,
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
                Version = $"{entity.Settings.Version}.{entity.Settings.Assembly} {entity.NodeInformation.Architecture} {entity.NodeInformation.Os}",
                LastUpdated = entity.UpdatedAt,
                LocalModules = entity.LocalModules,
                TsPiots = entity.TsPiots,
                Address = entity.Address,
                ForcedUpdateId = entity.ForcedUpdateId,
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
            UpdatedAt = instance.LastUpdated,
            SecretKey = instance.SecretKey,
            Address = instance.Address
        };
        
        var existInstance = await _instanceRepository.ByToken(instance.Token);

        if (existInstance.IsSuccess)
        {
            entity.LocalModules = existInstance.Value.LocalModules;
            entity.NodeInformation = existInstance.Value.NodeInformation;
            entity.Settings = existInstance.Value.Settings;
            entity.Cdn = existInstance.Value.Cdn;
            entity.TsPiots = existInstance.Value.TsPiots;
            entity.SettingsModified = existInstance.Value.SettingsModified;
            entity.ForcedUpdateId = existInstance.Value.ForcedUpdateId;
        }

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

    public async Task<Result<SoftwareUpdateFileDownload>> FmuApiUpdate(string token, long? rangeFrom)
    {
        var entitySearch = await _instanceRepository.ByToken(token);

        if (entitySearch.IsFailure)
            return Result.Failure<SoftwareUpdateFileDownload>(entitySearch.Error);

        var entity = entitySearch.Value;

        var softwareUpdateSettings = (await _parametersService.Current()).SoftwareUpdateSettings;

        if (!softwareUpdateSettings.IsDownloadAllowedNow())
            return Result.Failure<SoftwareUpdateFileDownload>("Загрузка обновления недоступна вне разрешённых интервалов");

        Result<SoftwareUpdateFileDownload> download;

        if (!string.IsNullOrWhiteSpace(entity.ForcedUpdateId))
        {
            download = await _softwareVersionsManager.Value.FmuApiUpdateById(entity.ForcedUpdateId, rangeFrom);

            if (download.IsFailure)
            {
                entity.ForcedUpdateId = string.Empty;
                await _instanceRepository.Update(entity);
            }
            else
            {
                await ClearForcedUpdateIfDownloadCompleted(entity, download.Value);
                return download;
            }
        }

        var (needUpdate, _) = await _softwareVersionsManager.Value.NeedUpdate(entity.NodeInformation.Os,
            entity.NodeInformation.Architecture,
            entity.Settings.Version,
            entity.Settings.Assembly);

        if (!needUpdate)
            return Result.Failure<SoftwareUpdateFileDownload>($"Для узла с id {token} не требуется обновление");

        return await _softwareVersionsManager.Value.FmuApiUpdateData(
            entity.NodeInformation.Os,
            entity.NodeInformation.Architecture,
            entity.Settings.Version,
            entity.Settings.Assembly,
            rangeFrom);
    }

    public async Task<Result<ForceUpdateResult>> AssignForcedUpdate(IReadOnlyList<string> tokens, string updateId)
    {
        try
        {
            if (tokens == null || tokens.Count == 0)
                return Result.Failure<ForceUpdateResult>("Не выбраны инстансы");

            if (string.IsNullOrWhiteSpace(updateId))
                return Result.Failure<ForceUpdateResult>("Не указан идентификатор обновления");

            var updateSearch = await _softwareVersionsManager.Value.ById(updateId);
            if (updateSearch.IsFailure)
                return Result.Failure<ForceUpdateResult>(updateSearch.Error);

            var update = updateSearch.Value;
            var assigned = 0;
            var skipped = 0;

            foreach (var token in tokens.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var instanceSearch = await _instanceRepository.ByToken(token);
                if (instanceSearch.IsFailure || !OsArchMatches(instanceSearch.Value.NodeInformation, update))
                {
                    skipped++;
                    continue;
                }

                var instance = instanceSearch.Value;
                instance.ForcedUpdateId = update.Id;
                var save = await _instanceRepository.Update(instance);
                if (save.IsFailure)
                {
                    skipped++;
                    continue;
                }

                assigned++;
            }

            return Result.Success(new ForceUpdateResult
            {
                Assigned = assigned,
                Skipped = skipped,
                Description = $"Назначено: {assigned}, пропущено: {skipped}"
            });
        }
        catch (Exception ex)
        {
            return Result.Failure<ForceUpdateResult>(ex.Message);
        }
    }

    private async Task<(bool needUpdate, string updateHash)> ResolveAvailableUpdate(
        InstanceEntity instance,
        FmuApiSetting settings)
    {
        if (!string.IsNullOrWhiteSpace(instance.ForcedUpdateId))
        {
            var forced = await _softwareVersionsManager.Value.ById(instance.ForcedUpdateId);
            if (forced.IsSuccess)
                return (true, forced.Value.Sha256);

            instance.ForcedUpdateId = string.Empty;
            await _instanceRepository.Update(instance);
        }

        return await _softwareVersionsManager.Value.NeedUpdate(
            instance.NodeInformation.Os,
            instance.NodeInformation.Architecture,
            settings.Version,
            settings.Assembly);
    }

    private async Task ClearForcedUpdateIfDownloadCompleted(InstanceEntity instance, SoftwareUpdateFileDownload download)
    {
        if (download.TotalLength <= 0 || download.To < download.TotalLength - 1)
            return;

        instance.ForcedUpdateId = string.Empty;
        await _instanceRepository.Update(instance);
    }

    private static bool OsArchMatches(NodeInformation node, SoftwareUpdateFilesEntity update)
    {
        if (string.IsNullOrWhiteSpace(node.Os) || string.IsNullOrWhiteSpace(node.Architecture))
            return false;

        return string.Equals(node.Os, update.Os, StringComparison.OrdinalIgnoreCase)
               && string.Equals(node.Architecture, update.Architecture, StringComparison.OrdinalIgnoreCase);
    }

    private static CentralServerProperties CreateCentralServerProperties(SoftwareUpdateSettings settings) =>
        new()
        {
            ExchangeServerAddresses = settings.ExchangeServerAddresses,
            ExchangeRequestInterval = settings.ExchangeRequestInterval,
            SchedulerUpdateDownload = settings.SchedulerUpdateDownload,
        };

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
                TsPiots = entity.TsPiots,
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
                TsPiots = entity.TsPiots,
            };

            content.Add(record);
        }
        
        return Result.Success(content);
    }
}