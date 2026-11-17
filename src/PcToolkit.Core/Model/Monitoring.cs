using System.Text.Json.Serialization;

namespace PcToolkit.Core.Model;

[JsonConverter(typeof(JsonStringEnumConverter<MetricKind>))]
public enum MetricKind
{
    /// <summary>Valor leído directamente de la fuente.</summary>
    Observed,
    /// <summary>Valor calculado a partir de valores observados con una fórmula documentada.</summary>
    Derived,
}

/// <param name="Id">Identificador estable (por ejemplo, "cpu.utility" o "gpu.0x0000_0x1234.util").</param>
/// <param name="NameKey">Clave de texto localizable.</param>
/// <param name="Unit">Unidad (%, MiB, MB/s, MHz).</param>
/// <param name="Source">Origen técnico exacto (contador, API).</param>
/// <param name="Subject">Elemento medido cuando hay varios (por ejemplo, el nombre de una GPU).</param>
/// <param name="Formula">Cálculo aplicado si el valor es derivado.</param>
public sealed record MetricDescriptor(
    string Id,
    string NameKey,
    string Unit,
    string Source,
    MetricKind Kind,
    string? Subject = null,
    string? Formula = null);

[JsonConverter(typeof(JsonStringEnumConverter<SampleStatus>))]
public enum SampleStatus
{
    Valid,
    /// <summary>La fuente no devolvió muestra en este instante.</summary>
    Missing,
    /// <summary>La fuente devolvió un valor marcado como no válido o fuera de rango.</summary>
    Invalid,
}

public readonly record struct Sample(DateTimeOffset Timestamp, double? Value, SampleStatus Status)
{
    public static Sample Valid(DateTimeOffset ts, double value) => new(ts, value, SampleStatus.Valid);
    public static Sample Missing(DateTimeOffset ts) => new(ts, null, SampleStatus.Missing);
    public static Sample Invalid(DateTimeOffset ts) => new(ts, null, SampleStatus.Invalid);
}

public sealed record SeriesStatistics(
    int ValidCount,
    int MissingCount,
    int InvalidCount,
    double? Min,
    double? Mean,
    double? P95,
    double? Max);

public sealed record MetricSeries(MetricDescriptor Metric, IReadOnlyList<Sample> Samples)
{
    public SeriesStatistics Statistics => SeriesMath.Compute(Samples);
}

/// <summary>Métrica sin fuente compatible en este equipo; se muestra explícitamente como no compatible.</summary>
public sealed record UnsupportedMetric(string NameKey, string Reason, string? Subject = null);

[JsonConverter(typeof(JsonStringEnumConverter<SessionContext>))]
public enum SessionContext
{
    Unspecified,
    /// <summary>El usuario indica que el equipo está en reposo.</summary>
    Idle,
    /// <summary>El usuario indica que hay un juego u otra carga en marcha.</summary>
    UnderLoad,
}

public sealed record SessionOptions(TimeSpan Duration, TimeSpan Interval, SessionContext Context)
{
    public static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan MaxDuration = TimeSpan.FromMinutes(30);

    public void Validate()
    {
        if (Interval < MinInterval)
        {
            throw new ArgumentOutOfRangeException(nameof(Interval), $"El intervalo mínimo es {MinInterval.TotalMilliseconds} ms.");
        }
        if (Duration <= TimeSpan.Zero || Duration > MaxDuration)
        {
            throw new ArgumentOutOfRangeException(nameof(Duration), $"La duración debe estar entre 0 y {MaxDuration.TotalMinutes} minutos.");
        }
        if (Interval > Duration)
        {
            throw new ArgumentOutOfRangeException(nameof(Interval), "El intervalo no puede superar la duración.");
        }
    }
}

public sealed record ProcessUsage(
    string Name,
    int ProcessId,
    Reading<double> AverageCpuPercent,
    Reading<ulong> WorkingSetBytes,
    Reading<ulong> PrivateBytes,
    // Partial: el proceso empezó o terminó durante la sesión; su media cubre solo parte del intervalo.
    bool Partial);

public sealed record SessionResult
{
    public required DateTimeOffset StartedAt { get; init; }
    public required DateTimeOffset EndedAt { get; init; }
    public required SessionOptions Options { get; init; }
    public required bool Cancelled { get; init; }
    public required IReadOnlyList<MetricSeries> Series { get; init; }
    public required IReadOnlyList<UnsupportedMetric> Unsupported { get; init; }
    public required Reading<IReadOnlyList<ProcessUsage>> Processes { get; init; }

    [JsonIgnore]
    public TimeSpan Elapsed => EndedAt - StartedAt;
}
