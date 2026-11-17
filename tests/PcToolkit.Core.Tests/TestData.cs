using PcToolkit.Core.Model;
using PcToolkit.Core.Ports;

namespace PcToolkit.Core.Tests;

/// <summary>Datos de prueba sintéticos. No corresponden a ningún equipo real.</summary>
internal static class TestData
{
    public const string Src = "test";

    public static Reading<T> R<T>(T v) => Reading<T>.Of(v, Src);
    public static Reading<T> NA<T>() => Reading<T>.Missing(Src);

    public static GpuInfo Gpu(string name, GpuKind kind, ulong dedicated, ulong shared, string luid) => new()
    {
        Name = R(name),
        Vendor = R("Vendor"),
        Kind = R(kind),
        DedicatedVideoMemoryBytes = R(dedicated),
        SharedSystemMemoryBytes = R(shared),
        DriverVersion = R("31.0.101.5000"),
        DriverDate = R(new DateOnly(2026, 1, 15)),
        Luid = luid,
    };

    public static SystemInventory SampleInventory(
        IReadOnlyList<GpuInfo>? gpus = null,
        PowerInfo? power = null,
        IReadOnlyList<StartupEntry>? startup = null,
        FormFactor form = FormFactor.Desktop) => new()
    {
        CapturedAt = new DateTimeOffset(2026, 11, 16, 20, 0, 0, TimeSpan.Zero),
        Duration = TimeSpan.FromMilliseconds(1500),
        Os = new OsInfo(R("Microsoft Windows 11 Pro"), R("Professional"), R("25H2"), R(26200), R(1000), R("X64")),
        Machine = new MachineInfo(R("Contoso"), R("Model X"), R(form)),
        Cpu = new CpuInfo(R("Example CPU 8-Core"), R("ExampleVendor"), R(8), R(16), R(3600), R<IReadOnlyList<CoreClassCount>>([new CoreClassCount(0, 8)])),
        Gpus = R(gpus ?? [Gpu("Example iGPU", GpuKind.Integrated, 128UL << 20, 8UL << 30, "0x00000000_0x0000aaaa")]),
        Memory = new MemoryInfo(R(16UL << 30), R(15UL << 30), R(8UL << 30), R<IReadOnlyList<MemoryModule>>([
            new MemoryModule(R("DIMM 1"), R(8UL << 30), R("DDR5"), R(5600), R(4800), NA<string>(), NA<string>()),
        ])),
        Storage = new StorageInfo(
            R<IReadOnlyList<PhysicalDiskInfo>>([new PhysicalDiskInfo(0, R("Example NVMe"), R(StorageMedia.Ssd), R("NVMe"), R(1UL << 40))]),
            R<IReadOnlyList<VolumeInfo>>([new VolumeInfo(@"C:\", "Fixed", R("NTFS"), R(500UL << 30), R(100UL << 30), R(0))])),
        Power = power ?? new PowerInfo(R(PowerSource.AC), R(false), NA<int>(), NA<bool>(),
            R(new PowerPlan(Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e"), "Balanced")),
            R<IReadOnlyList<PowerPlan>>([new PowerPlan(Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e"), "Balanced")]),
            R("PowerMode.Balanced")),
        Displays = R<IReadOnlyList<DisplayInfo>>([new DisplayInfo(@"\\.\DISPLAY1", true, R("Generic Monitor"), R(2560), R(1440), R(144), R(32))]),
        Startup = R(startup ?? [new StartupEntry("Example", @"C:\Users\alice\AppData\Local\Example\example.exe --tray", StartupLocation.UserRun, R(StartupState.Enabled))]),
    };
}

internal sealed class FakeClock(DateTimeOffset start) : ISessionClock
{
    public DateTimeOffset Now { get; private set; } = start;
    public int CancelAfterDelays { get; init; } = int.MaxValue;
    private int _delays;

    public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (++_delays > CancelAfterDelays)
        {
            throw new OperationCanceledException();
        }
        if (delay > TimeSpan.Zero)
        {
            Now += delay;
        }
        return Task.CompletedTask;
    }
}

internal sealed class FakeSampler : IPerformanceSampler, IPerformanceSamplerFactory
{
    public List<MetricDescriptor> MetricList { get; } =
    [
        new("cpu.utility", "Metric.CpuUtility", "%", Src, MetricKind.Derived),
        new("mem.available", "Metric.MemAvailable", "MiB", Src, MetricKind.Observed),
    ];

    public Func<int, DateTimeOffset, IReadOnlyDictionary<string, Sample>>? OnCollect { get; set; }
    public int Calls { get; private set; }
    public bool Disposed { get; private set; }

    private const string Src = "fake";

    public IReadOnlyList<MetricDescriptor> Metrics => MetricList;
    public IReadOnlyList<UnsupportedMetric> Unsupported { get; } = [new("Metric.CpuTemperature", "Unsupported.NoSensorSource")];

    public IReadOnlyDictionary<string, Sample> Collect(DateTimeOffset timestamp)
    {
        Calls++;
        return OnCollect?.Invoke(Calls, timestamp) ?? new Dictionary<string, Sample>
        {
            ["cpu.utility"] = Sample.Valid(timestamp, 10 * Calls),
            ["mem.available"] = Sample.Valid(timestamp, 4096),
        };
    }

    public IPerformanceSampler Create(SamplerContext context) => this;
    public void Dispose() => Disposed = true;
}

internal sealed class FakeProcesses : IProcessSnapshotSource
{
    public Queue<IReadOnlyList<ProcessSample>> Snapshots { get; } = new();
    public int LogicalProcessorCount { get; init; } = 4;
    public bool Throw { get; init; }

    public IReadOnlyList<ProcessSample> Capture() =>
        Throw ? throw new InvalidOperationException("boom") : (Snapshots.Count > 0 ? Snapshots.Dequeue() : []);
}
