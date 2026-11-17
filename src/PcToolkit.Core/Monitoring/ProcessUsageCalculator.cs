using PcToolkit.Core.Model;
using PcToolkit.Core.Ports;

namespace PcToolkit.Core.Monitoring;

/// <summary>
/// Calcula el uso medio de CPU por proceso entre dos instantáneas:
/// %CPU = Δ tiempo de procesador / (Δ tiempo real × procesadores lógicos) × 100.
/// Es el porcentaje sobre la capacidad TOTAL del equipo (como la pestaña Detalles del Administrador de tareas).
/// </summary>
public static class ProcessUsageCalculator
{
    public const string Source = "System.Diagnostics.Process (TotalProcessorTime, WorkingSet64, PrivateMemorySize64)";

    public static IReadOnlyList<ProcessUsage> Compute(
        IReadOnlyList<ProcessSample> start,
        IReadOnlyList<ProcessSample> end,
        TimeSpan elapsed,
        int logicalProcessors,
        int top = 15)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(logicalProcessors, 0);
        var startById = new Dictionary<int, ProcessSample>();
        foreach (var s in start)
        {
            startById.TryAdd(s.ProcessId, s);
        }

        var result = new List<ProcessUsage>();
        foreach (var e in end)
        {
            if (e.ProcessId == 0)
            {
                continue; // "Idle": no es un proceso real.
            }

            // Un PID puede reutilizarse: si la hora de inicio cambió, es otro proceso.
            var hasStart = startById.TryGetValue(e.ProcessId, out var s)
                           && string.Equals(s.Name, e.Name, StringComparison.OrdinalIgnoreCase)
                           && s.StartTimeUtc == e.StartTimeUtc;

            Reading<double> cpu;
            if (!hasStart)
            {
                cpu = Reading<double>.Missing(Source, ValueStatus.NotAvailable, "started-during-session");
            }
            else if (s!.TotalProcessorTime is null || e.TotalProcessorTime is null)
            {
                cpu = Reading<double>.Missing(Source, ValueStatus.AccessDenied);
            }
            else if (elapsed <= TimeSpan.Zero)
            {
                cpu = Reading<double>.Missing(Source, ValueStatus.NotAvailable, "zero-interval");
            }
            else
            {
                var delta = (e.TotalProcessorTime.Value - s.TotalProcessorTime.Value).TotalMilliseconds;
                var pct = Math.Clamp(delta / (elapsed.TotalMilliseconds * logicalProcessors) * 100.0, 0, 100);
                cpu = Reading<double>.Of(pct, Source);
            }

            result.Add(new ProcessUsage(
                e.Name,
                e.ProcessId,
                cpu,
                e.WorkingSetBytes is ulong ws ? Reading<ulong>.Of(ws, Source) : Reading<ulong>.Missing(Source, ValueStatus.AccessDenied),
                e.PrivateBytes is ulong pb ? Reading<ulong>.Of(pb, Source) : Reading<ulong>.Missing(Source, ValueStatus.AccessDenied),
                Partial: !hasStart));
        }

        // Top por CPU y top por memoria, sin duplicados.
        var byCpu = result.Where(p => p.AverageCpuPercent.HasValue).OrderByDescending(p => p.AverageCpuPercent.Value).Take(top);
        var byMem = result.Where(p => p.WorkingSetBytes.HasValue).OrderByDescending(p => p.WorkingSetBytes.Value).Take(top);
        return byCpu.Concat(byMem)
            .DistinctBy(p => p.ProcessId)
            .OrderByDescending(p => p.AverageCpuPercent.HasValue ? p.AverageCpuPercent.Value : -1)
            .ThenByDescending(p => p.WorkingSetBytes.HasValue ? p.WorkingSetBytes.Value : 0)
            .ToList();
    }
}
