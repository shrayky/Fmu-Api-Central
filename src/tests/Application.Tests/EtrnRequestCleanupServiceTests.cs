using Application.Etrn;
using Application.Tests.Fakes;
using Domain.Etrn;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.Tests;

public class EtrnRequestCleanupServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Clean_удаляет_выполненные_старше_суток_и_оставляет_свежие()
    {
        var world = new World();
        world.Add("old", completed: true, age: TimeSpan.FromHours(25));
        world.Add("fresh", completed: true, age: TimeSpan.FromHours(23));

        var deleted = await world.Sut.CleanAsync(CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.Equal(["fresh"], world.Requests.Store.Keys);
    }

    [Fact]
    public async Task Clean_запросы_с_ошибкой_живут_неделю()
    {
        var world = new World();
        world.Add("failed-6d", completed: false, age: TimeSpan.FromDays(6));
        world.Add("failed-8d", completed: false, age: TimeSpan.FromDays(8));

        await world.Sut.CleanAsync(CancellationToken.None);

        Assert.Equal(["failed-6d"], world.Requests.Store.Keys);
    }

    [Fact]
    public async Task Clean_берёт_сроки_из_настроек_сервера()
    {
        var world = new World();
        world.Parameters.Value.EtrnSettings.RequestRetentionDays = 3;
        world.Parameters.Value.EtrnSettings.FailedRequestRetentionDays = 10;
        world.Add("ok-2d", completed: true, age: TimeSpan.FromDays(2));
        world.Add("ok-4d", completed: true, age: TimeSpan.FromDays(4));
        world.Add("bad-8d", completed: false, age: TimeSpan.FromDays(8));
        world.Add("bad-11d", completed: false, age: TimeSpan.FromDays(11));

        await world.Sut.CleanAsync(CancellationToken.None);

        Assert.Equal(["bad-8d", "ok-2d"], world.Requests.Store.Keys.OrderBy(item => item));
    }

    [Fact]
    public async Task Clean_работает_при_выключенной_ЭТрН()
    {
        var world = new World();
        world.Parameters.Value.EtrnSettings.Enable = false;
        world.Add("old", completed: true, age: TimeSpan.FromHours(25));
        world.Add("fresh", completed: true, age: TimeSpan.FromHours(23));

        var deleted = await world.Sut.CleanAsync(CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.Equal(["fresh"], world.Requests.Store.Keys);
    }

    [Fact]
    public async Task Clean_ровно_заданный_срок_ещё_хранит()
    {
        var world = new World();
        world.Parameters.Value.EtrnSettings.RequestRetentionDays = 3;
        world.Add("edge", completed: true, age: TimeSpan.FromDays(3));
        world.Add("over", completed: true, age: TimeSpan.FromDays(3).Add(TimeSpan.FromSeconds(1)));

        await world.Sut.CleanAsync(CancellationToken.None);

        Assert.Equal(["edge"], world.Requests.Store.Keys);
    }

    private sealed class World
    {
        public World()
        {
            Parameters = new MemoryEtrnParameters();
            Sut = new EtrnRequestCleanupService(
                Requests,
                Parameters,
                NullLogger<EtrnRequestCleanupService>.Instance,
                new FixedClock(Now));
        }

        public MemoryEtrnParameters Parameters { get; }

        public MemoryEtrnRequests Requests { get; } = new();

        public EtrnRequestCleanupService Sut { get; }

        public void Add(string id, bool completed, TimeSpan age)
        {
            Requests.Store[id] = new EtrnRequestEntity
            {
                Id = id,
                Completed = completed,
                CreatedAt = (Now - age).ToUnixTimeSeconds()
            };
        }
    }
}
