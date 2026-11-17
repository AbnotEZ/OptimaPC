using PcToolkit.Core.Localization;
using PcToolkit.Core.Model;

namespace PcToolkit.Core.Presentation;

public sealed record MetricRow(
    string Name,
    string Unit,
    string Samples,
    string Min,
    string Mean,
    string P95,
    string Max,
    string Source,
    string KindText,
    string? Formula,
    MetricSeries Series);

public sealed record ProcessRow(string Name, int ProcessId, string Cpu, string WorkingSet, string PrivateBytes, string Note);

public static class SessionPresenter
{
    public static string MetricName(MetricDescriptor m) =>
        m.Subject is null ? Loc.T(m.NameKey) : $"{Loc.T(m.NameKey)} — {m.Subject}";

    public static IReadOnlyList<MetricRow> Metrics(SessionResult session) =>
        session.Series.Select(s =>
        {
            var st = s.Statistics;
            var total = s.Samples.Count;
            return new MetricRow(
                MetricName(s.Metric),
                s.Metric.Unit,
                Loc.F("Session.SamplesValue", st.ValidCount, total),
                Fmt.WithUnit(st.Min, s.Metric.Unit),
                Fmt.WithUnit(st.Mean, s.Metric.Unit),
                Fmt.WithUnit(st.P95, s.Metric.Unit),
                Fmt.WithUnit(st.Max, s.Metric.Unit),
                s.Metric.Source,
                Loc.T($"MetricKind.{s.Metric.Kind}"),
                s.Metric.Formula,
                s);
        }).ToList();

    public static IReadOnlyList<string> Unsupported(SessionResult session) =>
        session.Unsupported.Select(u =>
            $"{(u.Subject is null ? Loc.T(u.NameKey) : $"{Loc.T(u.NameKey)} — {u.Subject}")}: {Loc.T("Status.NotSupported")} ({(Loc.Has(u.Reason) ? Loc.T(u.Reason) : u.Reason)})").ToList();

    public static IReadOnlyList<ProcessRow> Processes(SessionResult session)
    {
        if (!session.Processes.HasValue)
        {
            return [];
        }
        return session.Processes.Value!.Select(p => new ProcessRow(
            p.Name,
            p.ProcessId,
            Fmt.Value(p.AverageCpuPercent, v => $"{Fmt.Number(v, 1)} %"),
            Fmt.Value(p.WorkingSetBytes, Fmt.Bytes),
            Fmt.Value(p.PrivateBytes, Fmt.Bytes),
            p.Partial ? Loc.T("Session.Process.Partial") : "")).ToList();
    }

    /// <summary>Descripción objetiva de la sesión (solo hechos, sin interpretaciones).</summary>
    public static string Description(SessionResult session)
    {
        var text = Loc.F("Session.Description",
            Fmt.DateTime(session.StartedAt),
            Fmt.Duration(session.Elapsed),
            Fmt.Duration(session.Options.Interval),
            Fmt.Context(session.Options.Context));
        return session.Cancelled ? $"{text} {Loc.T("Session.CancelledNote")}" : text;
    }
}
