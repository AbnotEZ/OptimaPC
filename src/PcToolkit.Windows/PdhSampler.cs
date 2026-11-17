using System.Runtime.InteropServices;
using PcToolkit.Core.Interpretation;
using PcToolkit.Core.Model;
using PcToolkit.Core.Ports;
using static PcToolkit.Windows.Native.NativeMethods;

namespace PcToolkit.Windows;

public sealed class PdhSamplerFactory : IPerformanceSamplerFactory
{
    public IPerformanceSampler Create(SamplerContext context) => new PdhSampler(context);
}

/// <summary>
/// Muestreo mediante contadores de rendimiento (PDH) con nombres en inglés, válidos en cualquier idioma
/// de Windows. Una sola consulta PDH por sesión; cada llamada a Collect hace una única recolección.
/// No requiere privilegios de administrador.
/// </summary>
internal sealed class PdhSampler : IPerformanceSampler
{
    private sealed record Single(MetricDescriptor Metric, IntPtr Counter, Func<double, double?> Transform);

    private sealed record GpuArray(IntPtr Counter, Func<IEnumerable<KeyValuePair<string, double>>, IReadOnlyDictionary<string, double>> Aggregate,
        Func<double, double> Transform, IReadOnlyList<(MetricDescriptor Metric, string Luid)> Targets);

    private IntPtr _query;
    private readonly List<Single> _singles = [];
    private readonly List<GpuArray> _arrays = [];
    private readonly List<MetricDescriptor> _metrics = [];
    private readonly List<UnsupportedMetric> _unsupported = [];

    public IReadOnlyList<MetricDescriptor> Metrics => _metrics;
    public IReadOnlyList<UnsupportedMetric> Unsupported => _unsupported;

    public PdhSampler(SamplerContext context)
    {
        var rc = PdhOpenQuery(null, IntPtr.Zero, out _query);
        if (rc != 0)
        {
            throw new InvalidOperationException($"PdhOpenQuery failed: 0x{rc:X8}");
        }

        const string cpuUtil = @"\Processor Information(_Total)\% Processor Utility";
        AddSingle(new MetricDescriptor("cpu.utility", "Metric.CpuUtility", "%", "PDH " + cpuUtil, MetricKind.Derived,
            Formula: "min(100, % Processor Utility)"), cpuUtil, v => Math.Clamp(v, 0, 100));

        const string cpuPerf = @"\Processor Information(_Total)\% Processor Performance";
        if (context.MaxClockMHz is int mhz and > 0)
        {
            AddSingle(new MetricDescriptor("cpu.frequency", "Metric.CpuFrequency", "MHz", "PDH " + cpuPerf + " × WMI MaxClockSpeed", MetricKind.Derived,
                Formula: $"% Processor Performance / 100 × {mhz} MHz"), cpuPerf, v => v / 100.0 * mhz);
        }
        else
        {
            _unsupported.Add(new UnsupportedMetric("Metric.CpuFrequency", "Unsupported.NoMaxClock"));
        }

        const string memAvail = @"\Memory\Available MBytes";
        AddSingle(new MetricDescriptor("mem.available", "Metric.MemAvailable", "MiB", "PDH " + memAvail, MetricKind.Observed), memAvail, v => v);

        const string memCommit = @"\Memory\% Committed Bytes In Use";
        AddSingle(new MetricDescriptor("mem.committed", "Metric.MemCommitted", "%", "PDH " + memCommit, MetricKind.Observed), memCommit, v => v);

        const string diskIdle = @"\PhysicalDisk(_Total)\% Idle Time";
        AddSingle(new MetricDescriptor("disk.active", "Metric.DiskActive", "%", "PDH " + diskIdle, MetricKind.Derived,
            Formula: "100 − % Idle Time (_Total, average across disks)"), diskIdle, v => Math.Clamp(100 - v, 0, 100));

        const string diskBytes = @"\PhysicalDisk(_Total)\Disk Bytes/sec";
        AddSingle(new MetricDescriptor("disk.throughput", "Metric.DiskThroughput", "MB/s", "PDH " + diskBytes, MetricKind.Observed,
            Formula: "bytes/s ÷ 1 000 000"), diskBytes, v => v / 1_000_000.0);

        AddGpuCounters(context.Gpus);

        _unsupported.Add(new UnsupportedMetric("Metric.CpuTemperature", "Unsupported.NoSensorSource"));
        _unsupported.Add(new UnsupportedMetric("Metric.GpuTemperature", "Unsupported.NoSensorSource"));

        // Primera recolección: los contadores de tasa necesitan dos lecturas para dar un valor.
        PdhCollectQueryData(_query);
    }

    private void AddSingle(MetricDescriptor metric, string path, Func<double, double?> transform)
    {
        var rc = PdhAddEnglishCounter(_query, path, IntPtr.Zero, out var counter);
        if (rc != 0)
        {
            _unsupported.Add(new UnsupportedMetric(metric.NameKey, $"PDH 0x{rc:X8}"));
            return;
        }
        _singles.Add(new Single(metric, counter, transform));
        _metrics.Add(metric);
    }

    private void AddGpuCounters(IReadOnlyList<GpuInfo> gpus)
    {
        var targets = gpus
            .Where(g => g.Luid is not null && !(g.Kind.HasValue && g.Kind.Value == GpuKind.Software))
            .ToList();
        if (targets.Count == 0)
        {
            return;
        }
        string Subject(GpuInfo g) => g.Name.HasValue ? g.Name.Value! : g.Luid!;

        void AddArray(string path, string id, string nameKey, string unit, MetricKind kind, string? formula,
            Func<IEnumerable<KeyValuePair<string, double>>, IReadOnlyDictionary<string, double>> aggregate, Func<double, double> transform)
        {
            var rc = PdhAddEnglishCounter(_query, path, IntPtr.Zero, out var counter);
            if (rc != 0)
            {
                foreach (var g in targets)
                {
                    _unsupported.Add(new UnsupportedMetric(nameKey, "Unsupported.CounterUnavailable", Subject(g)));
                }
                return;
            }
            var list = targets.Select(g => (new MetricDescriptor($"gpu.{g.Luid}.{id}", nameKey, unit, "PDH " + path, kind, Subject(g), formula), g.Luid!)).ToList();
            _arrays.Add(new GpuArray(counter, aggregate, transform, list));
            _metrics.AddRange(list.Select(t => t.Item1));
        }

        AddArray(@"\GPU Engine(*)\Utilization Percentage", "util", "Metric.GpuUtil", "%", MetricKind.Derived, GpuCounterAggregator.Formula,
            GpuCounterAggregator.UtilizationByAdapter, v => v);
        AddArray(@"\GPU Adapter Memory(*)\Dedicated Usage", "dedicated", "Metric.GpuDedicatedUsage", "MiB", MetricKind.Observed, null,
            GpuCounterAggregator.SumByAdapter, v => v / (1024.0 * 1024.0));
        AddArray(@"\GPU Adapter Memory(*)\Shared Usage", "shared", "Metric.GpuSharedUsage", "MiB", MetricKind.Observed, null,
            GpuCounterAggregator.SumByAdapter, v => v / (1024.0 * 1024.0));
    }

    public IReadOnlyDictionary<string, Sample> Collect(DateTimeOffset timestamp)
    {
        ObjectDisposedException.ThrowIf(_query == IntPtr.Zero, this);
        var result = new Dictionary<string, Sample>();
        var rc = PdhCollectQueryData(_query);
        if (rc != 0)
        {
            foreach (var m in _metrics)
            {
                result[m.Id] = Sample.Invalid(timestamp);
            }
            return result;
        }

        foreach (var s in _singles)
        {
            var status = PdhGetFormattedCounterValue(s.Counter, PDH_FMT_DOUBLE | PDH_FMT_NOCAP100, out _, out var value);
            result[s.Metric.Id] = status == 0 && IsValid(value.CStatus) && double.IsFinite(value.doubleValue) && s.Transform(value.doubleValue) is double v
                ? Sample.Valid(timestamp, v)
                : Sample.Invalid(timestamp);
        }

        foreach (var a in _arrays)
        {
            var instances = ReadArray(a.Counter);
            if (instances is null)
            {
                foreach (var (metric, _) in a.Targets)
                {
                    result[metric.Id] = Sample.Invalid(timestamp);
                }
                continue;
            }
            var byAdapter = a.Aggregate(instances);
            foreach (var (metric, luid) in a.Targets)
            {
                // Sin instancias para un adaptador: no hay actividad registrada → muestra ausente, no cero.
                result[metric.Id] = byAdapter.TryGetValue(luid, out var v) ? Sample.Valid(timestamp, a.Transform(v)) : Sample.Missing(timestamp);
            }
        }
        return result;
    }

    private static bool IsValid(uint cstatus) => cstatus is PDH_CSTATUS_VALID_DATA or PDH_CSTATUS_NEW_DATA;

    private static List<KeyValuePair<string, double>>? ReadArray(IntPtr counter)
    {
        uint size = 0;
        var rc = PdhGetFormattedCounterArray(counter, PDH_FMT_DOUBLE | PDH_FMT_NOCAP100, ref size, out _, IntPtr.Zero);
        if (rc != PDH_MORE_DATA || size == 0)
        {
            return rc == 0 ? [] : null;
        }
        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            rc = PdhGetFormattedCounterArray(counter, PDH_FMT_DOUBLE | PDH_FMT_NOCAP100, ref size, out var count, buffer);
            if (rc != 0)
            {
                return null;
            }
            var itemSize = Marshal.SizeOf<PDH_FMT_COUNTERVALUE_ITEM>();
            var list = new List<KeyValuePair<string, double>>((int)count);
            for (var i = 0; i < count; i++)
            {
                var item = Marshal.PtrToStructure<PDH_FMT_COUNTERVALUE_ITEM>(buffer + i * itemSize);
                if (IsValid(item.FmtValue.CStatus) && Marshal.PtrToStringUni(item.szName) is string name)
                {
                    list.Add(new(name, item.FmtValue.doubleValue));
                }
            }
            return list;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Dispose()
    {
        if (_query != IntPtr.Zero)
        {
            PdhCloseQuery(_query);
            _query = IntPtr.Zero;
        }
    }
}
