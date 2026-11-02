using Application.Tests.Fakes;
using Application.Vetis;
using CSharpFunctionalExtensions;
using Domain.Configuration;
using Domain.Configuration.Interfaces;
using Domain.Vetis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VetisApi.Workers;

namespace Application.Tests;

public class VetisWorkerTests
{
    [Fact]
    public async Task Курсор_автогашения_живёт_между_кругами_и_сбрасывается_после_прохода()
    {
        var calls = new List<VetisQuenchCursor?>();
        var done = new TaskCompletionSource<List<VetisQuenchCursor?>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var mark = new VetisQuenchLaneMark
        {
            OrganizationId = "o1",
            Status = VetisIncomingStatus.NotAccepted,
            Dated = true,
            AfterId = "doc-1"
        };
        var quencher = new ScriptQuencher(cursor =>
        {
            lock (calls)
            {
                calls.Add(cursor);
                if (calls.Count == 1)
                {
                    return new VetisQuenchBatch
                    {
                        Finished = false,
                        Cursor = new VetisQuenchCursor { Lanes = [mark] }
                    };
                }

                if (calls.Count == 2)
                    return new VetisQuenchBatch { Finished = true, Cursor = new VetisQuenchCursor { Lanes = [mark] } };

                done.TrySetResult(calls.ToList());
                return new VetisQuenchBatch { Finished = true };
            }
        });
        var scope = new MapScope(
            (typeof(IParametersService), new MemoryVetisParameters()),
            (typeof(IVetisIncomingAutoQuencher), quencher));
        var worker = new VetisAutoQuenchWorker(
            scope,
            new VetisJobStatusStore(),
            NullLogger<VetisAutoQuenchWorker>.Instance,
            TimeSpan.Zero,
            TimeSpan.Zero);

        await worker.StartAsync(CancellationToken.None);
        var snapshot = await done.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal("doc-1", snapshot[1]!.Lanes.Single().AfterId);
        Assert.Empty(snapshot[2]!.Lanes);
    }

    [Fact]
    public void Хост_создаёт_воркеры_без_тестовых_пауз()
    {
        var scope = new MapScope(
            (typeof(IServiceScopeFactory), new MapScope()),
            (typeof(IVetisJobStatusStore), new VetisJobStatusStore()),
            (typeof(ILogger<VetisAutoQuenchWorker>), NullLogger<VetisAutoQuenchWorker>.Instance),
            (typeof(ILogger<VetisIncomingWorker>), NullLogger<VetisIncomingWorker>.Instance));

        Assert.NotNull(ActivatorUtilities.CreateInstance<VetisAutoQuenchWorker>(scope));
        Assert.NotNull(ActivatorUtilities.CreateInstance<VetisIncomingWorker>(scope));
    }

    [Fact]
    public async Task Пустые_настройки_Ветис_не_роняют_загрузку()
    {
        var jobs = new VetisJobStatusStore();
        var scan = new DoneScan();
        var scope = new MapScope(
            (typeof(IParametersService), new NullVetisParameters()),
            (typeof(IVetisIncomingSortAligner), scan),
            (typeof(IVetisIncomingTransportSweep), scan),
            (typeof(IVetisIncomingLoader), new BoomLoader()));
        var worker = new VetisIncomingWorker(
            scope,
            jobs,
            NullLogger<VetisIncomingWorker>.Instance,
            TimeSpan.Zero);

        await worker.StartAsync(CancellationToken.None);
        var text = await WaitText(jobs, "incoming");
        await worker.StopAsync(CancellationToken.None);

        Assert.Contains("Остановлено", text);
    }

    private static async Task<string> WaitText(VetisJobStatusStore jobs, string id)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            var line = jobs.Current().FirstOrDefault(item => item.Id == id);
            if (line != null && !line.Text.StartsWith("Запускается") && !line.Text.StartsWith("Готовит"))
                return line.Text;

            await Task.Delay(20);
        }

        return jobs.Current().FirstOrDefault(item => item.Id == id)?.Text ?? "";
    }

    private sealed class ScriptQuencher(Func<VetisQuenchCursor?, VetisQuenchBatch> next) : IVetisIncomingAutoQuencher
    {
        public Task<VetisQuenchBatch> QuenchBatchAsync(VetisQuenchCursor? cursor, CancellationToken cancellationToken) =>
            Task.FromResult(next(cursor));
    }

    private sealed class NullVetisParameters : IParametersService
    {
        public Task<Parameters> Current()
        {
            var parameters = new Parameters { VetisSettings = null! };
            return Task.FromResult(parameters);
        }

        public Task<bool> Update(Parameters parameters) => Task.FromResult(true);
    }

    private sealed class DoneScan : IVetisIncomingSortAligner, IVetisIncomingTransportSweep
    {
        public Task<Result<VetisScanStep>> AlignBatchAsync(string afterId, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(new VetisScanStep(true, "")));

        public Task<Result<VetisScanStep>> SweepBatchAsync(string afterId, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(new VetisScanStep(true, "")));
    }

    private sealed class BoomLoader : IVetisIncomingLoader
    {
        public Task<int> LoadAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("загрузка не должна стартовать");
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
