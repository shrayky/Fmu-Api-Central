using System.Text.Json;
using Application.Instance.DTO;
using Application.Instance.Interfaces;
using Application.SoftwareUpdates.Interfaces;
using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Dto;
using Domain.Dto.fmuApiAnswer;
using Domain.Dto.Interfaces;
using Domain.Dto.Responces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;

namespace Application.Instance.Services;

[AutoRegisterService(ServiceLifetime.Scoped)]
public class InstanceManagerService : IInstanceManagerService
{
    private readonly ILogger<IInstanceManagerService> _logger;
    private readonly IInstanceRepository _instanceRepository; 
    private readonly Lazy<ISoftwareUpdatesManagerService> _softwareVersionsManager;

    public InstanceManagerService(ILogger<IInstanceManagerService> logger, IInstanceRepository instanceRepository, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _instanceRepository = instanceRepository;
        _softwareVersionsManager = new Lazy<ISoftwareUpdatesManagerService>(serviceProvider.GetRequiredService<ISoftwareUpdatesManagerService>);
    }

    public async Task<Result<FmuApiAnswer>> UpdateFmuApiInstanceInformation(string instanceData)
    {
        _logger.LogInformation("Обрабатываю пакет от fmu-api {InstanceData}", instanceData);
        
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(instanceData));
        var packet = await JsonSerializer.DeserializeAsync<FmuApiPacket>(stream);
        
        if  (packet == null)
            return Result.Failure<FmuApiAnswer>($"Не удалось десериализовать входящий пакет {instanceData}!");

        var entitySearchResult = await _instanceRepository.ByToken(packet.Token);
        
        if (entitySearchResult.IsFailure)
            return Result.Failure<FmuApiAnswer>(entitySearchResult.Error);

        var instanceEntity = entitySearchResult.Value;
        
        instanceEntity.UpdatedAt = DateTime.Now;
        instanceEntity.Name = packet.Name;
        instanceEntity.Cdn = packet.CdnData;
        
        if (!instanceEntity.SettingsModified)
            instanceEntity.Settings = packet.Parameters;
        
        var updateResult = await _instanceRepository.Update(instanceEntity);

        var needUpdate = await _softwareVersionsManager.Value.NeedUpdate(packet.Os, packet.Architecture, packet.Parameters.AppVersion, packet.Parameters.Assembly);
        
        var answer = new FmuApiAnswer()
        {
            ConfigurationUpdateAvailable = instanceEntity.SettingsModified,
            SoftwareUpdateAvailable = needUpdate,
        };
        
        return updateResult.IsSuccess ? Result.Success(answer) : Result.Failure<FmuApiAnswer>(updateResult.Error);
    }

    public async Task<PaginatedResponse<InstanceMonitoringInformation>> InstancesList(int pageNumber, int pageSize, string filter = "")
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
                Version = $"{entity.Settings.AppVersion}.{entity.Settings.Assembly}",
                LastUpdated = entity.UpdatedAt
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
            CreatedAt = DateAndTime.Now,
            UpdatedAt = DateTime.Now
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
}