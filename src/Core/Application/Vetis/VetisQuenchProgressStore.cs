using System.Collections.Concurrent;
using Domain.Attributes;
using Domain.Vetis;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Vetis;

/// <summary>
/// Живёт в памяти процесса: опрос стадии и само гашение — разные запросы, scoped-сервис их не свяжет.
/// Замок тоже здесь: два гашения одного документа пишут факт друг поверх друга.
/// Семафоры не удаляются — снятие в гонке со вторым входом отпустило бы чужой, поэтому словарь растёт до остановки процесса.
/// </summary>
[AutoRegisterService(ServiceLifetime.Singleton)]
public sealed class VetisQuenchProgressStore
{
    private readonly ConcurrentDictionary<string, VetisQuenchProgress> _current = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);

    public void Set(string documentId, VetisQuenchProgress progress) =>
        _current[documentId] = progress;

    public VetisQuenchProgress? Find(string documentId) =>
        _current.TryGetValue(documentId, out var progress) ? progress : null;

    public void Clear(string documentId) =>
        _current.TryRemove(documentId, out _);

    public Task<bool> TryEnter(string documentId, CancellationToken cancellationToken) =>
        _gates.GetOrAdd(documentId, _ => new SemaphoreSlim(1, 1)).WaitAsync(0, cancellationToken);

    public void Exit(string documentId)
    {
        if (_gates.TryGetValue(documentId, out var gate))
            gate.Release();
    }
}
