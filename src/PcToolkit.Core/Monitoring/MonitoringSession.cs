using PcToolkit.Core.Model;
using PcToolkit.Core.Ports;

namespace PcToolkit.Core.Monitoring;

public sealed record SessionProgress(TimeSpan Elapsed, TimeSpan Total, IReadOnlyDictionary<string, Sample> Latest, IReadOnlyList<MetricDescriptor> Metrics);

/// <summary>
/// Sesión de muestreo durante un intervalo configurable. Solo lectura.
/// Las muestras se toman en instantes fijos (inicio + k·intervalo) para no acumular deriva.
/// La cancelación devuelve las muestras obtenidas hasta ese momento, marcadas como sesión cancelada.
/// </summary>
public sealed class MonitoringSession(
    IPerformanceSamplerFactory samplerFactory,
    IProcessSnapshotSource processes,
    ISessionClock? clock = null)
{
    private readonly ISessionClock _clock = clock ?? SystemSessionClock.Instance;

    public async Task<SessionResult> RunAsync(
        SessionOptions options,
        SamplerContext context,
        IProgress<SessionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options.Validate();

        using var sampler = samplerFactory.Create(context);
        var series = sampler.Metrics.ToDictionary(m => m.Id, _ => new List<Sample>());

        IReadOnlyList<ProcessSample>? startProcs = null;
        Reading<IReadOnlyList<ProcessUsage>>? procError = null;
        try
        {
            startProcs = processes.Capture();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            procError = Reading<IReadOnlyList<ProcessUsage>>.Missing(ProcessUsageCalculator.Source, ValueStatus.Failed, ex.Message);
        }

        var startedAt = _clock.Now;
        var cancelled = false;
        var tick = 1;
        while (true)
        {
            var due = startedAt + options.Interval * tick;
            if (due - startedAt > options.Duration)
            {
                break;
            }
            try
            {
                await _clock.Delay(due - _clock.Now, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                break;
            }

            var now = _clock.Now;
            IReadOnlyDictionary<string, Sample> collected;
            try
            {
                collected = sampler.Collect(now);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Si falla la recolección completa, la muestra se registra como inválida en todas las métricas.
                _ = ex;
                collected = series.Keys.ToDictionary(k => k, _ => Sample.Invalid(now));
            }

            foreach (var (id, list) in series)
            {
                list.Add(collected.TryGetValue(id, out var sample) ? sample : Sample.Missing(now));
            }
            progress?.Report(new SessionProgress(now - startedAt, options.Duration, collected, sampler.Metrics));
            tick++;
        }

        var endedAt = _clock.Now;
        Reading<IReadOnlyList<ProcessUsage>> procResult;
        if (procError is not null)
        {
            procResult = procError;
        }
        else
        {
            try
            {
                var endProcs = processes.Capture();
                procResult = Reading<IReadOnlyList<ProcessUsage>>.Of(
                    ProcessUsageCalculator.Compute(startProcs!, endProcs, endedAt - startedAt, processes.LogicalProcessorCount),
                    ProcessUsageCalculator.Source);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                procResult = Reading<IReadOnlyList<ProcessUsage>>.Missing(ProcessUsageCalculator.Source, ValueStatus.Failed, ex.Message);
            }
        }

        return new SessionResult
        {
            StartedAt = startedAt,
            EndedAt = endedAt,
            Options = options,
            Cancelled = cancelled,
            Series = sampler.Metrics.Select(m => new MetricSeries(m, series[m.Id])).ToList(),
            Unsupported = sampler.Unsupported,
            Processes = procResult,
        };
    }
}
