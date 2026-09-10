using CSharpFunctionalExtensions;

namespace Application.MarkingCalendar.Interfaces;

public interface IMarkingCalendarService
{
    /// <summary>
    /// Проксирует JSON календаря с сайта Честный знак — браузер блокирует прямой запрос из‑за CORS.
    /// </summary>
    Task<Result<string>> GetSchedule(CancellationToken cancellationToken);
}
