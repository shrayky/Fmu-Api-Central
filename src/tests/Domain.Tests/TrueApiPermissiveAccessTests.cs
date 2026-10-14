using System.Text.Json;
using Domain.TrueApiIntegration;

namespace Domain.Tests;

public class TrueApiPermissiveAccessTests
{
    /// <summary>
    /// Срок кэша — локальное время: AppState сравнивает LiveUntil с DateTime.Now по тикам.
    /// </summary>
    [Fact]
    public void Parse_кладёт_ключ_и_локальный_срок_из_секунд()
    {
        var now = new DateTime(2026, 10, 14, 10, 0, 0, DateTimeKind.Local);

        var result = TrueApiPermissiveAccess.Parse("cdn-key", 3600, now);

        Assert.True(result.IsSuccess);
        Assert.Equal("cdn-key", result.Value.Token);
        Assert.Equal(now.AddSeconds(3600), result.Value.LiveUntil);
        Assert.Equal(DateTimeKind.Local, result.Value.LiveUntil.Kind);
    }

    [Fact]
    public void Parse_без_ключа_отказ()
    {
        var now = new DateTime(2026, 10, 14, 10, 0, 0, DateTimeKind.Local);

        var result = TrueApiPermissiveAccess.Parse("  ", 3600, now);

        Assert.True(result.IsFailure);
        Assert.Equal("True API не вернул x-api-key", result.Error);
    }

    [Fact]
    public void Parse_отрицательный_срок_отказ()
    {
        var now = new DateTime(2026, 10, 14, 10, 0, 0, DateTimeKind.Local);

        var result = TrueApiPermissiveAccess.Parse("cdn-key", -1, now);

        Assert.True(result.IsFailure);
        Assert.Equal("True API вернул некорректный срок действия x-api-key", result.Error);
    }

    [Fact]
    public void Body_отправляет_только_подпись()
    {
        var json = JsonSerializer.Serialize(TrueApiPermissiveAccess.Body("c2ln"));

        Assert.Equal("{\"data\":\"c2ln\"}", json);
    }
}
