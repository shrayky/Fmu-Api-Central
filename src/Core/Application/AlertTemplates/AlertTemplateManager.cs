using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Dto.Responces;
using Domain.Entitys.AlertTemplates;
using Domain.Entitys.AlertTemplates.Dto;
using Domain.Entitys.AlertTemplates.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.AlertTemplates;

[AutoRegisterService]
public class AlertTemplateManager : IAlertTemplateManager
{
    private readonly ILogger<AlertTemplateManager> _logger;
    private readonly IAlertTemplateRepository _repository;
    private static readonly Regex TimeFormat =
        new(@"^([01]\d|2[0-3]):([0-5]\d):([0-5]\d)$", RegexOptions.Compiled);

    public AlertTemplateManager(ILogger<AlertTemplateManager> logger, IAlertTemplateRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    public async Task<Result> Create(AlertTemplateView data)
    {
        var validation = Validate(data);
        if (validation.IsFailure)
            return validation;

        var entityExist = await _repository.GetById(data.Id);
        if (entityExist.IsFailure)
            return await _repository.Create(ToEntity(data));

        return await _repository.Update(ToEntity(data));
    }

    public async Task<Result> Update(AlertTemplateView data)
    {
        var validation = Validate(data);
        if (validation.IsFailure)
            return validation;

        var entityExist = await _repository.GetById(data.Id);
        if (entityExist.IsFailure)
            return Result.Failure(entityExist.Error);

        return await _repository.Update(ToEntity(data));
    }

    public async Task<Result> Delete(string id)
    {
        _logger.LogWarning("Удаляю шаблон оповещения с id {id}", id);

        var entityExist = await _repository.GetById(id);
        if (entityExist.IsFailure)
            return Result.Failure(entityExist.Error);

        return await _repository.Delete(id);
    }

    public async Task<PaginatedResponse<AlertTemplateView>> List(int pageNumber, int pageSize)
    {
        await EnsureDefaults();

        var entityList = await _repository.List(pageNumber, pageSize);

        return new PaginatedResponse<AlertTemplateView>
        {
            Description = entityList.Description,
            ListEnabled = entityList.ListEnabled,
            TotalCount = entityList.TotalCount,
            PageSize = entityList.PageSize,
            CurrentPage = entityList.CurrentPage,
            Content = entityList.Content.Select(ToView).ToList()
        };
    }

    public async Task<Result> EnsureDefaults()
    {
        var existing = await _repository.All();
        if (existing.Count > 0)
            return Result.Success();

        foreach (var template in AlertTemplateDefaults.All())
        {
            var createResult = await _repository.Create(template);
            if (createResult.IsFailure)
                return createResult;
        }

        return Result.Success();
    }

    private static Result Validate(AlertTemplateView data)
    {
        if (string.IsNullOrWhiteSpace(data.Name))
            return Result.Failure("Укажите имя шаблона");

        if (string.IsNullOrWhiteSpace(data.Script))
            return Result.Failure("Укажите JS-скрипт набора данных");

        var scheduler = data.Scheduler ?? [];
        if (data.Enabled && scheduler.Count == 0)
            return Result.Failure("Укажите хотя бы одно время запуска");

        var invalid = scheduler.Find(slot => !TimeFormat.IsMatch(slot.Time?.Trim() ?? string.Empty));
        if (invalid != null)
            return Result.Failure($"Некорректное время в строке №{invalid.Id}");

        return Result.Success();
    }

    private static AlertTemplateView ToView(AlertTemplateEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Script = entity.Script,
        Enabled = entity.Enabled,
        Scheduler = CopyScheduler(entity.Scheduler)
    };

    private static AlertTemplateEntity ToEntity(AlertTemplateView data) => new()
    {
        Id = data.Id,
        Name = data.Name,
        Script = data.Script,
        Enabled = data.Enabled,
        Scheduler = CopyScheduler(data.Scheduler)
    };

    private static List<AlertTemplateScheduleSlot> CopyScheduler(List<AlertTemplateScheduleSlot>? source) =>
        (source ?? [])
        .Select(slot => new AlertTemplateScheduleSlot
        {
            Id = slot.Id,
            Time = slot.Time?.Trim() ?? string.Empty
        })
        .ToList();
}
