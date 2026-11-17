using System.Text;
using System.Text.Json;
using PcToolkit.Core.Localization;
using PcToolkit.Core.Model;
using PcToolkit.Core.Presentation;
using PcToolkit.Core.Privacy;
using PcToolkit.Core.Storage;

namespace PcToolkit.Core.Export;

public enum ReportFormat { Json, Text }

public sealed record ExportOptions(ReportFormat Format, bool IncludeStartupCommands, bool IncludeProcesses);

/// <summary>Documento exportado. Se genera siempre localmente; la aplicación nunca lo envía.</summary>
public sealed record ReportDocument
{
    public required string Tool { get; init; }
    public required string ToolVersion { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public required string PrivacyNote { get; init; }
    public SystemInventory? Inventory { get; init; }
    public SessionResult? Session { get; init; }
}

/// <summary>
/// Construye el informe exportable. Pasos: 1) se eliminan las partes que el usuario no quiere incluir;
/// 2) se serializa; 3) se aplica <see cref="PrivacyScrubber"/> al texto final. El resultado es
/// exactamente lo que se muestra en la vista previa y lo que se guarda.
/// </summary>
public sealed class ReportBuilder(PrivacyScrubber scrubber)
{
    public const string OmittedToken = "<omitted>";

    public string Build(SystemInventory? inventory, SessionResult? session, ExportOptions options, DateTimeOffset now)
    {
        inventory = inventory is null ? null : Filter(inventory, options);
        session = session is null ? null : Filter(session, options);

        var text = options.Format switch
        {
            ReportFormat.Json => JsonSerializer.Serialize(new ReportDocument
            {
                Tool = AppInfo.ProductName,
                ToolVersion = AppInfo.Version,
                GeneratedAt = now,
                PrivacyNote = "User name, computer name, domain and profile paths have been replaced by placeholders.",
                Inventory = inventory,
                Session = session,
            }, JsonDefaults.Options),
            _ => BuildText(inventory, session, now),
        };
        return scrubber.Scrub(text);
    }

    private static SystemInventory Filter(SystemInventory inv, ExportOptions o)
    {
        if (o.IncludeStartupCommands || !inv.Startup.HasValue)
        {
            return inv;
        }
        var startup = inv.Startup.Value!.Select(e => e with { Command = OmittedToken }).ToList();
        return inv with { Startup = Reading<IReadOnlyList<StartupEntry>>.Of(startup, inv.Startup.Source, inv.Startup.Detail) };
    }

    private static SessionResult Filter(SessionResult s, ExportOptions o) =>
        o.IncludeProcesses
            ? s
            : s with { Processes = Reading<IReadOnlyList<ProcessUsage>>.Missing(s.Processes.Source, ValueStatus.NotAvailable, "excluded-by-user") };

    private static string BuildText(SystemInventory? inv, SessionResult? session, DateTimeOffset now)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{AppInfo.ProductName} {AppInfo.Version}");
        sb.AppendLine(Loc.F("Export.GeneratedAt", Fmt.DateTime(now)));
        sb.AppendLine(Loc.T("Export.PrivacyNote"));
        sb.AppendLine();

        if (inv is not null)
        {
            sb.AppendLine($"== {Loc.T("Nav.MyPc")} ({Fmt.DateTime(inv.CapturedAt)}) ==");
            foreach (var group in InventoryPresenter.Groups(inv))
            {
                sb.AppendLine();
                sb.AppendLine($"[{group.Title}]");
                foreach (var row in group.Rows)
                {
                    sb.Append("  ").Append(row.Label).Append(": ").Append(row.Value);
                    if (!string.IsNullOrEmpty(row.Source))
                    {
                        sb.Append("   {").Append(row.Source).Append('}');
                    }
                    sb.AppendLine();
                    if (!string.IsNullOrEmpty(row.Detail))
                    {
                        sb.Append("      ").AppendLine(row.Detail);
                    }
                }
                if (group.Note is not null)
                {
                    sb.Append("  * ").AppendLine(group.Note);
                }
            }
            sb.AppendLine();
        }

        if (session is not null)
        {
            sb.AppendLine($"== {Loc.T("Nav.Diagnostics")} ==");
            sb.AppendLine(SessionPresenter.Description(session));
            sb.AppendLine();
            sb.AppendLine(Loc.T("Export.MetricHeader"));
            foreach (var m in SessionPresenter.Metrics(session))
            {
                sb.AppendLine($"  {m.Name} [{m.Unit}] | {m.Samples} | {m.Min} | {m.Mean} | {m.P95} | {m.Max} | {m.KindText} {{{m.Source}}}");
                if (m.Formula is not null)
                {
                    sb.AppendLine($"      = {m.Formula}");
                }
            }
            foreach (var u in SessionPresenter.Unsupported(session))
            {
                sb.AppendLine($"  {u}");
            }
            var procs = SessionPresenter.Processes(session);
            if (procs.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine(Loc.T("Export.ProcessHeader"));
                foreach (var p in procs)
                {
                    sb.AppendLine($"  {p.Name} (PID {p.ProcessId}) | CPU {p.Cpu} | {p.WorkingSet} | {p.PrivateBytes} {p.Note}".TrimEnd());
                }
            }
        }
        return sb.ToString();
    }
}
