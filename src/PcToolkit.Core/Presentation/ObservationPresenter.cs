using PcToolkit.Core.Localization;
using PcToolkit.Core.Model;

namespace PcToolkit.Core.Presentation;

/// <summary>
/// Hechos observados para la pantalla de inicio. Solo describen datos medidos; NO emiten juicios
/// ("alto", "problema", "cuello de botella"). Las interpretaciones con evidencia llegarán en la fase 2.
/// </summary>
public static class ObservationPresenter
{
    public static IReadOnlyList<string> Facts(SystemInventory? inv, SessionResult? session)
    {
        var facts = new List<string>();

        if (session is not null)
        {
            var seconds = Fmt.Duration(session.Elapsed);
            Add(session, "cpu.utility", st => Loc.F("Obs.Cpu", seconds, Fmt.Number(st.Mean!.Value, 0), Fmt.Number(st.Max!.Value, 0)));
            Add(session, "mem.available", st => Loc.F("Obs.MemAvailable", Fmt.Bytes((ulong)(st.Min!.Value * 1024 * 1024)), Fmt.Bytes((ulong)(st.Mean!.Value * 1024 * 1024))));
            Add(session, "disk.active", st => Loc.F("Obs.Disk", Fmt.Number(st.Mean!.Value, 0), Fmt.Number(st.Max!.Value, 0)));
            foreach (var s in session.Series.Where(s => s.Metric.Id.EndsWith(".util", StringComparison.Ordinal) && s.Metric.Id.StartsWith("gpu.", StringComparison.Ordinal)))
            {
                var st = s.Statistics;
                if (st.ValidCount > 0)
                {
                    facts.Add(Loc.F("Obs.Gpu", s.Metric.Subject ?? "GPU", Fmt.Number(st.Mean!.Value, 0), Fmt.Number(st.Max!.Value, 0)));
                }
            }
            var invalid = session.Series.Sum(s => s.Statistics.InvalidCount + s.Statistics.MissingCount);
            if (invalid > 0)
            {
                facts.Add(Loc.F("Obs.InvalidSamples", invalid));
            }
        }

        if (inv is not null)
        {
            if (inv.Storage.Volumes.HasValue)
            {
                foreach (var v in inv.Storage.Volumes.Value!.Where(v => v.DriveType == "Fixed" && v.FreeBytes.HasValue && v.TotalBytes.HasValue && v.TotalBytes.Value > 0))
                {
                    facts.Add(Loc.F("Obs.Volume", v.Root, Fmt.Bytes(v.FreeBytes.Value), Fmt.Number(100.0 * v.FreeBytes.Value / v.TotalBytes.Value, 0)));
                }
            }
            if (inv.Power.Source.HasValue && inv.Power.ActivePlan.HasValue)
            {
                facts.Add(Loc.F("Obs.Power", Loc.T($"PowerSource.{inv.Power.Source.Value}"), inv.Power.ActivePlan.Value!.Name));
            }
            if (inv.Startup.HasValue)
            {
                var enabled = inv.Startup.Value!.Count(e => e.State.HasValue && e.State.Value == StartupState.Enabled);
                facts.Add(Loc.F("Obs.Startup", enabled, inv.Startup.Value!.Count));
            }
        }
        return facts;

        void Add(SessionResult s, string id, Func<SeriesStatistics, string> describe)
        {
            var series = s.Series.FirstOrDefault(x => x.Metric.Id == id);
            if (series is not null && series.Statistics.ValidCount > 0)
            {
                facts.Add(describe(series.Statistics));
            }
        }
    }
}
