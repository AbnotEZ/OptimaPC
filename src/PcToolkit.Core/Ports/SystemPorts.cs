using PcToolkit.Core.Model;

namespace PcToolkit.Core.Ports;

/// <summary>
/// Fuentes de inventario. Cada implementación debe devolver estados explícitos
/// (NotAvailable, AccessDenied…) en lugar de lanzar excepciones por datos ausentes.
/// Si aun así lanza, el orquestador lo registra como fallo del área.
/// Todas son de SOLO LECTURA.
/// </summary>
public interface IInventorySources
{
    OsInfo ReadOs();
    MachineInfo ReadMachine();
    CpuInfo ReadCpu();
    Reading<IReadOnlyList<GpuInfo>> ReadGpus();
    MemoryInfo ReadMemory();
    StorageInfo ReadStorage();
    PowerInfo ReadPower();
    Reading<IReadOnlyList<DisplayInfo>> ReadDisplays();
    Reading<IReadOnlyList<StartupEntry>> ReadStartup();
}

/// <summary>Muestreador de contadores. Se crea por sesión y se libera al terminar.</summary>
public interface IPerformanceSampler : IDisposable
{
    IReadOnlyList<MetricDescriptor> Metrics { get; }
    IReadOnlyList<UnsupportedMetric> Unsupported { get; }

    /// <summary>Toma una muestra de todas las métricas. Las ausentes se devuelven como Missing/Invalid.</summary>
    IReadOnlyDictionary<string, Sample> Collect(DateTimeOffset timestamp);
}

/// <summary>Contexto que el muestreador necesita del inventario (por ejemplo, GPU detectadas).</summary>
public sealed record SamplerContext(int? MaxClockMHz, IReadOnlyList<GpuInfo> Gpus);

public interface IPerformanceSamplerFactory
{
    IPerformanceSampler Create(SamplerContext context);
}

/// <summary>Instantánea de tiempos de CPU acumulados por proceso.</summary>
public sealed record ProcessSample(
    int ProcessId,
    string Name,
    DateTime? StartTimeUtc,
    TimeSpan? TotalProcessorTime,
    ulong? WorkingSetBytes,
    ulong? PrivateBytes);

public interface IProcessSnapshotSource
{
    IReadOnlyList<ProcessSample> Capture();
    int LogicalProcessorCount { get; }
}

/// <summary>Reloj inyectable para que las sesiones se puedan probar sin esperar.</summary>
public interface ISessionClock
{
    DateTimeOffset Now { get; }
    Task Delay(TimeSpan delay, CancellationToken cancellationToken);
}

public sealed class SystemSessionClock : ISessionClock
{
    public static readonly SystemSessionClock Instance = new();
    public DateTimeOffset Now => DateTimeOffset.Now;
    public Task Delay(TimeSpan delay, CancellationToken cancellationToken) =>
        delay <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(delay, cancellationToken);
}
