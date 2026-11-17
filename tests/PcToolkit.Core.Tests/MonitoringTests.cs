using PcToolkit.Core.Inventory;
using PcToolkit.Core.Model;
using PcToolkit.Core.Monitoring;
using PcToolkit.Core.Ports;

namespace PcToolkit.Core.Tests;

public sealed class MonitoringSessionTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly SamplerContext Ctx = new(3600, []);

    [Fact]
    public async Task Takes_one_sample_per_interval_with_timestamps()
    {
        var sampler = new FakeSampler();
        var session = new MonitoringSession(sampler, new FakeProcesses(), new FakeClock(Start));

        var result = await session.RunAsync(new SessionOptions(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1), SessionContext.Idle), Ctx);

        Assert.False(result.Cancelled);
        Assert.Equal(10, sampler.Calls);
        var cpu = result.Series.Single(s => s.Metric.Id == "cpu.utility");
        Assert.Equal(10, cpu.Samples.Count);
        Assert.Equal(Start.AddSeconds(1), cpu.Samples[0].Timestamp);
        Assert.Equal(Start.AddSeconds(10), cpu.Samples[^1].Timestamp);
        Assert.True(sampler.Disposed);
        Assert.Equal(SessionContext.Idle, result.Options.Context);
        Assert.Single(result.Unsupported);
    }

    [Fact]
    public async Task Cancellation_keeps_collected_samples_and_is_flagged()
    {
        var sampler = new FakeSampler();
        var clock = new FakeClock(Start) { CancelAfterDelays = 3 };
        var result = await new MonitoringSession(sampler, new FakeProcesses(), clock)
            .RunAsync(new SessionOptions(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1), SessionContext.Unspecified), Ctx);

        Assert.True(result.Cancelled);
        Assert.Equal(3, result.Series[0].Samples.Count);
    }

    [Fact]
    public async Task Missing_metric_in_a_sample_is_recorded_as_missing_not_zero()
    {
        var sampler = new FakeSampler
        {
            OnCollect = (call, ts) => call == 2
                ? new Dictionary<string, Sample> { ["cpu.utility"] = Sample.Valid(ts, 5) }
                : new Dictionary<string, Sample> { ["cpu.utility"] = Sample.Valid(ts, 5), ["mem.available"] = Sample.Valid(ts, 100) },
        };
        var result = await new MonitoringSession(sampler, new FakeProcesses(), new FakeClock(Start))
            .RunAsync(new SessionOptions(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1), SessionContext.Unspecified), Ctx);

        var mem = result.Series.Single(s => s.Metric.Id == "mem.available");
        Assert.Equal(SampleStatus.Missing, mem.Samples[1].Status);
        Assert.Null(mem.Samples[1].Value);
        Assert.Equal(1, mem.Statistics.MissingCount);
        Assert.Equal(100, mem.Statistics.Min);
    }

    [Fact]
    public async Task Collector_exception_marks_samples_invalid_and_continues()
    {
        var sampler = new FakeSampler
        {
            OnCollect = (call, ts) => call == 1 ? throw new InvalidOperationException("pdh") : new Dictionary<string, Sample>
            {
                ["cpu.utility"] = Sample.Valid(ts, 1),
                ["mem.available"] = Sample.Valid(ts, 1),
            },
        };
        var result = await new MonitoringSession(sampler, new FakeProcesses(), new FakeClock(Start))
            .RunAsync(new SessionOptions(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1), SessionContext.Unspecified), Ctx);

        Assert.Equal(SampleStatus.Invalid, result.Series[0].Samples[0].Status);
        Assert.Equal(SampleStatus.Valid, result.Series[0].Samples[1].Status);
    }

    [Fact]
    public async Task Process_capture_failure_is_explicit()
    {
        var result = await new MonitoringSession(new FakeSampler(), new FakeProcesses { Throw = true }, new FakeClock(Start))
            .RunAsync(new SessionOptions(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), SessionContext.Unspecified), Ctx);
        Assert.Equal(ValueStatus.Failed, result.Processes.Status);
    }

    [Theory]
    [InlineData(100, 10)]      // intervalo menor que el mínimo
    [InlineData(1000, 0)]      // duración cero
    [InlineData(5000, 2)]      // intervalo mayor que la duración
    [InlineData(1000, 3600)]   // más del máximo
    public void Invalid_options_are_rejected(int intervalMs, int durationS) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SessionOptions(TimeSpan.FromSeconds(durationS), TimeSpan.FromMilliseconds(intervalMs), SessionContext.Unspecified).Validate());
}

public sealed class ProcessUsageCalculatorTests
{
    private static readonly DateTime Boot = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Cpu_percent_is_relative_to_total_capacity()
    {
        var start = new List<ProcessSample> { new(10, "game", Boot, TimeSpan.FromSeconds(1), 100, 50) };
        var end = new List<ProcessSample> { new(10, "game", Boot, TimeSpan.FromSeconds(5), 200, 60) };

        // 4 s de CPU en 10 s reales con 4 procesadores lógicos = 10 % de la capacidad total.
        var p = Assert.Single(ProcessUsageCalculator.Compute(start, end, TimeSpan.FromSeconds(10), 4));
        Assert.Equal(10, p.AverageCpuPercent.Value, 6);
        Assert.False(p.Partial);
        Assert.Equal(200UL, p.WorkingSetBytes.Value);
    }

    [Fact]
    public void Reused_pid_is_treated_as_new_process()
    {
        var start = new List<ProcessSample> { new(10, "a", Boot, TimeSpan.FromSeconds(1), 1, 1) };
        var end = new List<ProcessSample> { new(10, "a", Boot.AddSeconds(5), TimeSpan.FromSeconds(1), 1, 1) };
        var p = Assert.Single(ProcessUsageCalculator.Compute(start, end, TimeSpan.FromSeconds(10), 4));
        Assert.True(p.Partial);
        Assert.False(p.AverageCpuPercent.HasValue);
    }

    [Fact]
    public void Protected_process_reports_access_denied_and_idle_is_excluded()
    {
        var start = new List<ProcessSample> { new(0, "Idle", null, null, null, null), new(4, "System", null, null, 10, null) };
        var end = new List<ProcessSample> { new(0, "Idle", null, null, null, null), new(4, "System", null, null, 10, null) };
        var p = Assert.Single(ProcessUsageCalculator.Compute(start, end, TimeSpan.FromSeconds(10), 4));
        Assert.Equal("System", p.Name);
        Assert.Equal(ValueStatus.AccessDenied, p.AverageCpuPercent.Status);
        Assert.Equal(ValueStatus.AccessDenied, p.PrivateBytes.Status);
    }
}

public sealed class InventoryServiceTests
{
    private sealed class Sources(Action? throwOnGpu = null) : IInventorySources
    {
        private readonly SystemInventory _d = TestData.SampleInventory();
        public OsInfo ReadOs() => _d.Os;
        public MachineInfo ReadMachine() => _d.Machine;
        public CpuInfo ReadCpu() => _d.Cpu;
        public Reading<IReadOnlyList<GpuInfo>> ReadGpus() { throwOnGpu?.Invoke(); return _d.Gpus; }
        public MemoryInfo ReadMemory() => _d.Memory;
        public StorageInfo ReadStorage() => _d.Storage;
        public PowerInfo ReadPower() => _d.Power;
        public Reading<IReadOnlyList<DisplayInfo>> ReadDisplays() => _d.Displays;
        public Reading<IReadOnlyList<StartupEntry>> ReadStartup() => _d.Startup;
    }

    [Fact]
    public async Task One_failing_area_does_not_block_the_rest()
    {
        var inv = await new InventoryService(new Sources(() => throw new InvalidOperationException("driver"))).CollectAsync();
        Assert.Equal(ValueStatus.Failed, inv.Gpus.Status);
        var issue = Assert.Single(inv.Issues);
        Assert.Equal("gpu", issue.Area);
        Assert.Contains("driver", issue.Detail);
        Assert.True(inv.Cpu.Name.HasValue);
        Assert.True(inv.Memory.UsableBytes.HasValue);
    }

    [Fact]
    public async Task Healthy_sources_produce_no_issues()
    {
        var inv = await new InventoryService(new Sources()).CollectAsync();
        Assert.Empty(inv.Issues);
    }
}
