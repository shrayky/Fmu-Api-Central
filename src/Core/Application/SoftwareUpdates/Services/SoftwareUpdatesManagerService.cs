using System.Text.Json;
using Application.SoftwareUpdates.Interfaces;
using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Dto.Interfaces;
using Domain.Dto.Responces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Extensions;

namespace Application.SoftwareUpdates.Services;

[AutoRegisterService(ServiceLifetime.Scoped)]
public class SoftwareUpdatesManagerService : ISoftwareUpdatesManagerService
{
    private readonly ILogger<SoftwareUpdatesManagerService> _logger;
    private readonly ISoftwareUpdatesRepository  _repository;

    public SoftwareUpdatesManagerService(ILogger<SoftwareUpdatesManagerService> logger, ISoftwareUpdatesRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    public async Task<Result<PaginatedResponse<SoftwareUpdateFilesEntity>>> List(int pageNumber, int pageSize) => await _repository.List(pageNumber, pageSize);
    
    public async Task<Result<string>> Create(string dataPacket)
    {
        try
        {
            using var stream = new MemoryStream();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(dataPacket);
            await writer.FlushAsync();

            stream.Position = 0;

            var data = await JsonSerializer.DeserializeAsync<SoftwareUpdateFilesEntity>(stream);

            if (data == null)
                return Result.Failure<string>("Не удалось десериализовать пакет информации об обновлении fmu-api");

            data.CreatedAt = DateTime.Now;
            data.Id = data.UniqId();

            var creationResult = await _repository.Create(data);

            if (creationResult.IsSuccess)
                return Result.Success(creationResult.Value.AsJson());

            return Result.Failure<string>($"Создание новой записи в БД об обновлении fmu-api завершилось неудачей: {creationResult.Error}");

        }
        catch (Exception ex)
        {
            return Result.Failure<string>(ex.Message);
        }
    }

    public async Task<Result<bool>> AttachFile(string id, IFormFile file)
    {
        var entitySearch = await _repository.ById(id);
        
        if (entitySearch.IsFailure)
            return Result.Failure<bool>(entitySearch.Error);

        if (!await file.VerifyHashAsync(entitySearch.Value.Sha256))
        {
            return Result.Failure<bool>($"Не совпал хэш файла для загрузки с id {id}");
        }
        
        var tempFilePath = await file.SaveToTempAsync("fmu-central-uploads");
        
        var attachResult = await _repository.AttachFile(id, tempFilePath, file.ContentType);

        if (File.Exists(tempFilePath))
            File.Delete(tempFilePath);

        if (attachResult.IsSuccess)
            return Result.Success(true);

        await _repository.Delete(id);

        return Result.Failure<bool>($"Не удалось прикрепить файл обнавления к записи с id {id}");
    }

    public async Task<bool> NeedUpdate(string os, string architecture, int version, int assembly)
    {
        var softwareUpdate = await _repository.MaxUpdateEntity(os, architecture, version, assembly);

        if (softwareUpdate.IsFailure)
            return false;
        
        return softwareUpdate.Value.Version >= version && softwareUpdate.Value.Assembly > assembly;
    }

    public async Task<Result<bool>> Delete(string id)
    {
        var deleteResult = await _repository.Delete(id);

        return deleteResult.IsSuccess ? Result.Success(true) : Result.Failure<bool>(deleteResult.Error);
    }
}