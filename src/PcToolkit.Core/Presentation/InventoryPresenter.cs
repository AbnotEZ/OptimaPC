using System.Globalization;
using PcToolkit.Core.Localization;
using PcToolkit.Core.Model;

namespace PcToolkit.Core.Presentation;

/// <param name="Label">Etiqueta localizada.</param>
/// <param name="Value">Valor formateado o estado ("No disponible", "No compatible"…).</param>
/// <param name="Source">Origen técnico del dato (se muestra en "Detalles técnicos").</param>
/// <param name="Detail">Información técnica adicional opcional.</param>
/// <param name="IsMissing">El dato no está disponible.</param>
public sealed record InfoRow(string Label, string Value, string Source, string? Detail, bool IsMissing)
{
    public static InfoRow From<T>(string labelKey, Reading<T> reading, Func<T, string>? format = null) =>
        new(Loc.T(labelKey), Fmt.Value(reading, format), reading.Source, reading.Detail, !reading.HasValue);

    public static InfoRow Text(string label, string value, string source = "", string? detail = null) =>
        new(label, value, source, detail, false);
}

public sealed record InfoGroup(string Title, IReadOnlyList<InfoRow> Rows, string? Note = null);

public sealed record SummaryLine(string Label, string Value);

/// <summary>Convierte el inventario en grupos de filas listas para mostrar o exportar como texto.</summary>
public static class InventoryPresenter
{
    public static IReadOnlyList<SummaryLine> Summary(SystemInventory inv)
    {
        var lines = new List<SummaryLine>
        {
            new(Loc.T("Inv.Cpu"), Fmt.Value(inv.Cpu.Name)),
        };

        if (inv.Gpus.HasValue && inv.Gpus.Value!.Count > 0)
        {
            foreach (var gpu in inv.Gpus.Value.Where(g => !(g.Kind.HasValue && g.Kind.Value == GpuKind.Software)))
            {
                var kind = gpu.Kind.HasValue ? $" ({Fmt.Kind(gpu.Kind.Value)})" : "";
                lines.Add(new(Loc.T("Inv.Gpu"), Fmt.Value(gpu.Name) + kind));
            }
        }
        else
        {
            lines.Add(new(Loc.T("Inv.Gpu"), Fmt.Status(inv.Gpus.Status)));
        }

        lines.Add(new(Loc.T("Inv.Ram"), Fmt.Value(inv.Memory.InstalledBytes.HasValue ? inv.Memory.InstalledBytes : inv.Memory.UsableBytes, Fmt.Bytes)));
        lines.Add(new(Loc.T("Inv.Windows"), $"{Fmt.Value(inv.Os.Name)} {Fmt.Value(inv.Os.DisplayVersion)}".Trim()));
        lines.Add(new(Loc.T("Inv.FormFactor"), Fmt.Value(inv.Machine.FormFactor, f => Loc.T($"FormFactor.{f}"))));
        return lines;
    }

    public static IReadOnlyList<InfoGroup> Groups(SystemInventory inv, bool includeStartup = true)
    {
        var groups = new List<InfoGroup>
        {
            Os(inv.Os),
            Machine(inv.Machine, inv.Power),
            Cpu(inv.Cpu),
        };
        groups.AddRange(Gpus(inv.Gpus));
        groups.Add(Memory(inv.Memory));
        groups.AddRange(Storage(inv.Storage));
        groups.Add(Power(inv.Power));
        groups.Add(Displays(inv.Displays));
        if (includeStartup)
        {
            groups.Add(Startup(inv.Startup));
        }
        if (inv.Issues.Count > 0)
        {
            groups.Add(new InfoGroup(Loc.T("Inv.Issues"),
                inv.Issues.Select(i => InfoRow.Text(i.Area, Fmt.Status(i.Status), "collector", i.Detail)).ToList()));
        }
        return groups;
    }

    private static InfoGroup Os(OsInfo os) => new(Loc.T("Inv.Group.Os"),
    [
        InfoRow.From("Inv.Os.Name", os.Name),
        InfoRow.From("Inv.Os.Edition", os.Edition),
        InfoRow.From("Inv.Os.Version", os.DisplayVersion),
        new(Loc.T("Inv.Os.Build"),
            os.Build.HasValue ? (os.Revision.HasValue ? $"{os.Build.Value}.{os.Revision.Value}" : os.Build.Value.ToString(CultureInfo.InvariantCulture)) : Fmt.Status(os.Build.Status),
            os.Build.Source, os.Build.Detail, !os.Build.HasValue),
        InfoRow.From("Inv.Os.Arch", os.Architecture),
    ]);

    private static InfoGroup Machine(MachineInfo m, PowerInfo p) => new(Loc.T("Inv.Group.Machine"),
    [
        InfoRow.From("Inv.Machine.Manufacturer", m.Manufacturer),
        InfoRow.From("Inv.Machine.Model", m.Model),
        InfoRow.From("Inv.FormFactor", m.FormFactor, f => Loc.T($"FormFactor.{f}")),
        InfoRow.From("Inv.Power.HasBattery", p.HasBattery, b => Loc.T(b ? "Common.Yes" : "Common.No")),
    ]);

    private static InfoGroup Cpu(CpuInfo c)
    {
        var rows = new List<InfoRow>
        {
            InfoRow.From("Inv.Cpu.Name", c.Name),
            InfoRow.From("Inv.Cpu.Manufacturer", c.Manufacturer),
            InfoRow.From("Inv.Cpu.Cores", c.PhysicalCores, v => Fmt.Int(v)),
            InfoRow.From("Inv.Cpu.Logical", c.LogicalProcessors, v => Fmt.Int(v)),
            InfoRow.From("Inv.Cpu.MaxClock", c.MaxClockMHz, v => $"{Fmt.Int(v)} MHz"),
        };
        if (c.CoreClasses.HasValue && c.CoreClasses.Value!.Count > 1)
        {
            var ordered = c.CoreClasses.Value.OrderByDescending(x => x.EfficiencyClass).ToList();
            rows.Add(new InfoRow(Loc.T("Inv.Cpu.Hybrid"),
                Loc.F("Inv.Cpu.HybridValue", ordered[0].Cores, ordered.Skip(1).Sum(x => x.Cores)),
                c.CoreClasses.Source, string.Join("; ", ordered.Select(x => $"EfficiencyClass {x.EfficiencyClass}: {x.Cores}")), false));
        }
        else
        {
            rows.Add(InfoRow.From("Inv.Cpu.Hybrid", c.CoreClasses, _ => Loc.T("Inv.Cpu.Homogeneous")));
        }
        return new InfoGroup(Loc.T("Inv.Group.Cpu"), rows);
    }

    private static IEnumerable<InfoGroup> Gpus(Reading<IReadOnlyList<GpuInfo>> gpus)
    {
        if (!gpus.HasValue || gpus.Value!.Count == 0)
        {
            yield return new InfoGroup(Loc.T("Inv.Group.Gpu"),
                [new InfoRow(Loc.T("Inv.Gpu"), gpus.HasValue ? Loc.T("Inv.Gpu.None") : Fmt.Status(gpus.Status), gpus.Source, gpus.Detail, true)]);
            yield break;
        }
        var i = 1;
        foreach (var g in gpus.Value)
        {
            yield return new InfoGroup(
                gpus.Value.Count > 1 ? Loc.F("Inv.Group.GpuN", i++) : Loc.T("Inv.Group.Gpu"),
                [
                    InfoRow.From("Inv.Gpu.Name", g.Name),
                    InfoRow.From("Inv.Gpu.Vendor", g.Vendor),
                    InfoRow.From("Inv.Gpu.Kind", g.Kind, Fmt.Kind),
                    InfoRow.From("Inv.Gpu.Dedicated", g.DedicatedVideoMemoryBytes, Fmt.Bytes),
                    InfoRow.From("Inv.Gpu.Shared", g.SharedSystemMemoryBytes, Fmt.Bytes),
                    InfoRow.From("Inv.Gpu.Driver", g.DriverVersion),
                    InfoRow.From("Inv.Gpu.DriverDate", g.DriverDate, d => d.ToString("d", CultureInfo.CurrentCulture)),
                ],
                Loc.T("Inv.Gpu.MemoryNote"));
        }
    }

    private static InfoGroup Memory(MemoryInfo m)
    {
        var rows = new List<InfoRow>
        {
            InfoRow.From("Inv.Mem.Installed", m.InstalledBytes, Fmt.Bytes),
            InfoRow.From("Inv.Mem.Usable", m.UsableBytes, Fmt.Bytes),
            InfoRow.From("Inv.Mem.Available", m.AvailableBytes, Fmt.Bytes),
        };
        if (m.Modules.HasValue)
        {
            foreach (var mod in m.Modules.Value!)
            {
                var parts = new List<string>
                {
                    Fmt.Value(mod.CapacityBytes, Fmt.Bytes),
                    Fmt.Value(mod.Type),
                    mod.ConfiguredSpeedMTs.HasValue
                        ? Loc.F("Inv.Mem.SpeedValue", Fmt.Int(mod.ConfiguredSpeedMTs.Value), Fmt.Value(mod.RatedSpeedMTs, v => $"{Fmt.Int(v)} MT/s"))
                        : Fmt.Value(mod.RatedSpeedMTs, v => $"{Fmt.Int(v)} MT/s"),
                };
                rows.Add(new InfoRow(Loc.F("Inv.Mem.Module", Fmt.Value(mod.Slot)), string.Join(" · ", parts), mod.CapacityBytes.Source,
                    $"{Fmt.Value(mod.Manufacturer)} {Fmt.Value(mod.PartNumber)}".Trim(), !mod.CapacityBytes.HasValue));
            }
        }
        else
        {
            rows.Add(InfoRow.From<IReadOnlyList<MemoryModule>>("Inv.Mem.Modules", m.Modules, _ => ""));
        }
        return new InfoGroup(Loc.T("Inv.Group.Memory"), rows);
    }

    private static IEnumerable<InfoGroup> Storage(StorageInfo s)
    {
        var disks = new List<InfoRow>();
        if (s.Disks.HasValue)
        {
            foreach (var d in s.Disks.Value!)
            {
                var media = d.Media.HasValue && d.Media.Value != StorageMedia.Unspecified
                    ? Loc.T($"Media.{d.Media.Value}")
                    : Loc.T("Media.Unspecified");
                disks.Add(new InfoRow(
                    Loc.F("Inv.Disk.N", d.Number, Fmt.Value(d.Model)),
                    $"{media} · {Fmt.Value(d.Bus)} · {Fmt.Value(d.SizeBytes, Fmt.Bytes)}",
                    d.Media.Source, d.Media.Detail, false));
            }
        }
        else
        {
            disks.Add(InfoRow.From<IReadOnlyList<PhysicalDiskInfo>>("Inv.Disks", s.Disks, _ => ""));
        }
        yield return new InfoGroup(Loc.T("Inv.Group.Disks"), disks, Loc.T("Inv.Disk.Note"));

        var vols = new List<InfoRow>();
        if (s.Volumes.HasValue)
        {
            foreach (var v in s.Volumes.Value!)
            {
                var free = v.FreeBytes.HasValue && v.TotalBytes.HasValue && v.TotalBytes.Value > 0
                    ? Loc.F("Inv.Vol.FreeOf", Fmt.Bytes(v.FreeBytes.Value), Fmt.Bytes(v.TotalBytes.Value), Fmt.Number(100.0 * v.FreeBytes.Value / v.TotalBytes.Value, 0))
                    : Fmt.Status(v.TotalBytes.Status);
                var disk = v.DiskNumber.HasValue ? Loc.F("Inv.Vol.OnDisk", v.DiskNumber.Value) : Loc.T("Inv.Vol.DiskUnknown");
                vols.Add(new InfoRow(v.Root, $"{free} · {Fmt.Value(v.FileSystem)} · {disk}", v.TotalBytes.Source, v.DriveType, !v.TotalBytes.HasValue));
            }
        }
        else
        {
            vols.Add(InfoRow.From<IReadOnlyList<VolumeInfo>>("Inv.Volumes", s.Volumes, _ => ""));
        }
        yield return new InfoGroup(Loc.T("Inv.Group.Volumes"), vols);
    }

    private static InfoGroup Power(PowerInfo p)
    {
        var rows = new List<InfoRow>
        {
            InfoRow.From("Inv.Power.Source", p.Source, s => Loc.T($"PowerSource.{s}")),
        };
        if (p.HasBattery is { HasValue: true, Value: true })
        {
            rows.Add(InfoRow.From("Inv.Power.Battery", p.BatteryPercent, v => $"{v} %"));
            rows.Add(InfoRow.From("Inv.Power.Charging", p.Charging, b => Loc.T(b ? "Common.Yes" : "Common.No")));
        }
        rows.Add(InfoRow.From("Inv.Power.Plan", p.ActivePlan, plan => plan.Name));
        rows.Add(InfoRow.From("Inv.Power.Plans", p.AvailablePlans, list => string.Join(", ", list.Select(x => x.Name))));
        rows.Add(InfoRow.From("Inv.Power.Mode", p.PowerMode, key => Loc.Has(key) ? Loc.T(key) : key));
        return new InfoGroup(Loc.T("Inv.Group.Power"), rows, Loc.T("Inv.Power.Note"));
    }

    private static InfoGroup Displays(Reading<IReadOnlyList<DisplayInfo>> displays)
    {
        if (!displays.HasValue)
        {
            return new InfoGroup(Loc.T("Inv.Group.Displays"), [InfoRow.From<IReadOnlyList<DisplayInfo>>("Inv.Displays", displays, _ => "")]);
        }
        var rows = displays.Value!.Select((d, i) =>
        {
            var res = d.Width.HasValue && d.Height.HasValue ? $"{d.Width.Value} × {d.Height.Value}" : Fmt.Status(d.Width.Status);
            var hz = Fmt.Value(d.RefreshRateHz, v => $"{v} Hz");
            var primary = d.IsPrimary ? $" · {Loc.T("Inv.Display.Primary")}" : "";
            return new InfoRow(Loc.F("Inv.Display.N", i + 1, Fmt.Value(d.MonitorName)), $"{res} · {hz}{primary}", d.Width.Source, Loc.T("Inv.Display.HzNote"), !d.Width.HasValue);
        }).ToList();
        return new InfoGroup(Loc.T("Inv.Group.Displays"), rows);
    }

    private static InfoGroup Startup(Reading<IReadOnlyList<StartupEntry>> startup)
    {
        if (!startup.HasValue)
        {
            return new InfoGroup(Loc.T("Inv.Group.Startup"), [InfoRow.From<IReadOnlyList<StartupEntry>>("Inv.Startup", startup, _ => "")]);
        }
        var rows = startup.Value!
            .OrderBy(e => e.State.HasValue && e.State.Value == StartupState.Disabled)
            .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(e => new InfoRow(
                e.Name,
                $"{Fmt.Value(e.State, s => Loc.T($"StartupState.{s}"))} · {Loc.T($"StartupLocation.{e.Location}")}",
                e.State.Source,
                e.Command,
                false))
            .ToList();
        if (rows.Count == 0)
        {
            rows.Add(InfoRow.Text(Loc.T("Inv.Startup"), Loc.T("Inv.Startup.None")));
        }
        return new InfoGroup(Loc.F("Inv.Group.StartupN", startup.Value!.Count), rows, Loc.T("Inv.Startup.Note"));
    }
}
