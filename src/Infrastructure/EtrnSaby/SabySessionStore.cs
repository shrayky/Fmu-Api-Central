using System.Collections.Concurrent;
using CSharpFunctionalExtensions;

namespace EtrnSaby;

/// <summary>
/// Сессия живёт в памяти процесса. Ключ — организация и серийный номер: два подключения с одним сертификатом делят вход.
/// </summary>
public sealed class SabySessionStore
{
    private sealed class Slot
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);

        public string? SessionId { get; set; }
    }

    private readonly ConcurrentDictionary<string, Slot> _slots = new(StringComparer.Ordinal);

    public static string Key(string organizationId, string certificateNumber) =>
        $"{(organizationId ?? "").Trim()}|{(certificateNumber ?? "").Trim()}";

    public async Task<Result<string>> GetAsync(
        string key,
        Func<CancellationToken, Task<Result<string>>> login,
        CancellationToken cancellationToken)
    {
        var slot = _slots.GetOrAdd(key, _ => new Slot());
        if (!string.IsNullOrWhiteSpace(slot.SessionId))
            return Result.Success(slot.SessionId!);

        await slot.Gate.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(slot.SessionId))
                return Result.Success(slot.SessionId!);

            var logged = await login(cancellationToken);
            if (logged.IsFailure)
                return logged;

            slot.SessionId = logged.Value;
            return logged;
        }
        finally
        {
            slot.Gate.Release();
        }
    }

    /// <summary>
    /// Сбрасывается только тот идентификатор, которым получен отказ: параллельный вход уже мог положить новый.
    /// </summary>
    public void Invalidate(string key, string? sessionId)
    {
        if (!_slots.TryGetValue(key, out var slot))
            return;

        lock (slot)
        {
            if (sessionId == null || slot.SessionId == sessionId)
                slot.SessionId = null;
        }
    }
}
