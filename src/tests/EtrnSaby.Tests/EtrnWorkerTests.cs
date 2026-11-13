using Domain.Configuration;
using Domain.Configuration.Interfaces;
using Domain.Etrn;
using EtrnSaby.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EtrnSaby.Tests;

public class EtrnWorkerTests
{
    [Fact]
    public async Task Выключенная_ЭТрН_не_загружает_накладные()
    {
        var loader = new CountingLoader();
        var jobs = new RecordingJobs();
        var scope = new MapScope(
            (typeof(IParametersService), new FixedParameters(enable: false)),
            (typeof(IEtrnIncomingLoader), loader));
        var worker = new EtrnIncomingWorker(
            scope,
            jobs,
            NullLogger<EtrnIncomingWorker>.Instance,
            TimeSpan.Zero);

        await worker.StartAsync(CancellationToken.None);
        var text = await jobs.WaitText("etrnIncoming");
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(0, loader.Calls);
        Assert.Contains("выключены", text);
    }

    [Fact]
    public async Task Выключенная_ЭТрН_чистит_запросы()
    {
        var cleaner = new CountingCleaner();
        var jobs = new RecordingJobs();
        var scope = new MapScope((typeof(IEtrnRequestCleaner), cleaner));
        var worker = new EtrnRequestCleanupWorker(
            scope,
            jobs,
            NullLogger<EtrnRequestCleanupWorker>.Instance,
            TimeSpan.Zero);

        await worker.StartAsync(CancellationToken.None);
        await jobs.WaitCalls(() => cleaner.Calls, 1);
        await worker.StopAsync(CancellationToken.None);

        Assert.True(cleaner.Calls >= 1);
    }

    [Fact]
    public async Task Включённая_ЭТрН_загружает_накладные()
    {
        var loader = new CountingLoader();
        var jobs = new RecordingJobs();
        var scope = new MapScope(
            (typeof(IParametersService), new FixedParameters(enable: true)),
            (typeof(IEtrnIncomingLoader), loader));
        var worker = new EtrnIncomingWorker(
            scope,
            jobs,
            NullLogger<EtrnIncomingWorker>.Instance,
            TimeSpan.Zero);

        await worker.StartAsync(CancellationToken.None);
        await jobs.WaitCalls(() => loader.Calls, 1);
        await worker.StopAsync(CancellationToken.None);

        Assert.True(loader.Calls >= 1);
    }

    private sealed class FixedParameters(bool enable) : IParametersService
    {
        public Task<Parameters> Current() =>
            Task.FromResult(new Parameters { EtrnSettings = new EtrnSettings { Enable = enable } });

        public Task<bool> Update(Parameters parameters) => Task.FromResult(true);
    }

    private sealed class CountingLoader : IEtrnIncomingLoader
    {
        public int Calls { get; private set; }

        public string LastError => "";

        public Task<int> LoadAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(0);
        }

        public Task SyncOpenAsync(string id, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CountingCleaner : IEtrnRequestCleaner
    {
        public int Calls { get; private set; }

        public Task<int> CleanAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(0);
        }
    }

    private sealed class RecordingJobs : IEtrnJobStatusStore
    {
        private readonly List<EtrnJobUpdate> _updates = [];

        public void Report(EtrnJobUpdate update)
        {
            lock (_updates)
                _updates.Add(update);
        }

        public IReadOnlyList<EtrnJobStatusLine> Current() => [];

        public async Task<string> WaitText(string id)
        {
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < deadline)
            {
                EtrnJobUpdate? update;
                lock (_updates)
                    update = _updates.LastOrDefault(item => item.Id == id && item.Phase != EtrnJobPhase.Starting);

                if (update != null)
                    return EtrnJobStatusText.Format(
                        update.Phase,
                        update.CountLabel,
                        update.Count,
                        update.At,
                        update.NextAt,
                        update.Error);

                await Task.Delay(20);
            }

            return "";
        }

        public async Task WaitCalls(Func<int> calls, int count)
        {
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < deadline)
            {
                if (calls() >= count)
                    return;

                await Task.Delay(20);
            }
        }
    }

    private sealed class MapScope : IServiceScopeFactory, IServiceScope, IServiceProvider
    {
        private readonly Dictionary<Type, object> _services;

        public MapScope(params (Type Type, object Service)[] services)
        {
            _services = services.ToDictionary(item => item.Type, item => item.Service);
        }

        public IServiceProvider ServiceProvider => this;

        public IServiceScope CreateScope() => this;

        public void Dispose()
        {
        }

        public object? GetService(Type serviceType) =>
            _services.TryGetValue(serviceType, out var service) ? service : null;
    }
}
