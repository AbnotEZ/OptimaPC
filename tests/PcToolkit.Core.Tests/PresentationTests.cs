using System.Text.RegularExpressions;
using PcToolkit.Core.Localization;
using PcToolkit.Core.Model;
using PcToolkit.Core.Presentation;
using static PcToolkit.Core.Tests.TestData;

namespace PcToolkit.Core.Tests;

[Collection("Localization")]
public sealed partial class LocalizationTests
{
    [Fact]
    public void Spanish_and_English_have_identical_keys()
    {
        var es = Loc.Table("es").Keys.ToHashSet();
        var en = Loc.Table("en").Keys.ToHashSet();
        Assert.NotEmpty(es);
        Assert.Empty(es.Except(en));
        Assert.Empty(en.Except(es));
    }

    [GeneratedRegex(@"\{(\d+)\}")]
    private static partial Regex Placeholder();

    [Fact]
    public void Placeholders_match_between_languages()
    {
        foreach (var (key, es) in Loc.Table("es"))
        {
            var en = Loc.Table("en")[key];
            var a = Placeholder().Matches(es).Select(m => m.Value).Distinct().Order();
            var b = Placeholder().Matches(en).Select(m => m.Value).Distinct().Order();
            Assert.True(a.SequenceEqual(b), $"Placeholders differ for {key}");
        }
    }

    public static TheoryData<string> EnumKeys()
    {
        var data = new TheoryData<string>();
        foreach (var v in Enum.GetNames<ValueStatus>().Where(n => n != "Available")) data.Add($"Status.{v}");
        foreach (var v in Enum.GetNames<GpuKind>()) data.Add($"GpuKind.{v}");
        foreach (var v in Enum.GetNames<FormFactor>()) data.Add($"FormFactor.{v}");
        foreach (var v in Enum.GetNames<StorageMedia>()) data.Add($"Media.{v}");
        foreach (var v in Enum.GetNames<PowerSource>()) data.Add($"PowerSource.{v}");
        foreach (var v in Enum.GetNames<StartupState>()) data.Add($"StartupState.{v}");
        foreach (var v in Enum.GetNames<StartupLocation>()) data.Add($"StartupLocation.{v}");
        foreach (var v in Enum.GetNames<MetricKind>()) data.Add($"MetricKind.{v}");
        foreach (var v in Enum.GetNames<SampleStatus>()) data.Add($"SampleStatus.{v}");
        foreach (var v in Enum.GetNames<SessionContext>()) data.Add($"Context.{v}");
        return data;
    }

    [Theory]
    [MemberData(nameof(EnumKeys))]
    public void Every_enum_value_has_a_text(string key) => Assert.True(Loc.Table("es").ContainsKey(key), key);

    [GeneratedRegex(@"(?:Loc\.[TF]|InfoRow\.From(?:<[^>]+>)?|app:Tr)\(?\s*""?([A-Z][A-Za-z0-9]*\.[A-Za-z0-9\.]+)")]
    private static partial Regex KeyUse();

    /// <summary>Busca en el código fuente las claves usadas y comprueba que existen.</summary>
    [Fact]
    public void Every_key_used_in_source_exists()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "OptimaPC.slnx")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        var missing = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(dir.FullName, "src"), "*.*", SearchOption.AllDirectories)
                     .Where(f => (f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".xaml", StringComparison.Ordinal)) && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            foreach (Match m in KeyUse().Matches(File.ReadAllText(file)))
            {
                var key = m.Groups[1].Value.TrimEnd('.');
                if (!Loc.Table("es").ContainsKey(key))
                {
                    missing.Add($"{Path.GetFileName(file)}: {key}");
                }
            }
        }
        Assert.Empty(missing);
    }

    [Fact]
    public void Missing_key_is_visible_not_silent() => Assert.Equal("[[No.Such.Key]]", Loc.T("No.Such.Key"));
}

[Collection("Localization")]
public sealed class PresenterTests
{
    public PresenterTests() => Loc.SetLanguage("es");

    [Fact]
    public void Missing_values_show_status_text_never_zero()
    {
        var inv = SampleInventory() with
        {
            Memory = new MemoryInfo(NA<ulong>(), R(15UL << 30), R(8UL << 30), Reading<IReadOnlyList<MemoryModule>>.Missing("wmi", ValueStatus.AccessDenied)),
        };
        var rows = InventoryPresenter.Groups(inv).Single(g => g.Title == Loc.T("Inv.Group.Memory")).Rows;
        var installed = rows.Single(r => r.Label == Loc.T("Inv.Mem.Installed"));
        Assert.True(installed.IsMissing);
        Assert.Equal("No disponible", installed.Value);
        Assert.Contains(rows, r => r.Value == "Acceso denegado");
    }

    [Fact]
    public void Dedicated_and_shared_gpu_memory_are_separate_rows()
    {
        var groups = InventoryPresenter.Groups(SampleInventory([
            Gpu("iGPU", GpuKind.Integrated, 128UL << 20, 8UL << 30, "0x0_0x1"),
            Gpu("dGPU", GpuKind.Dedicated, 8UL << 30, 8UL << 30, "0x0_0x2"),
        ]));
        var gpuGroups = groups.Where(g => g.Title.StartsWith(Loc.T("Inv.Group.Gpu"), StringComparison.Ordinal)).ToList();
        Assert.Equal(2, gpuGroups.Count);
        foreach (var g in gpuGroups)
        {
            Assert.Contains(g.Rows, r => r.Label == Loc.T("Inv.Gpu.Dedicated"));
            Assert.Contains(g.Rows, r => r.Label == Loc.T("Inv.Gpu.Shared"));
            Assert.NotNull(g.Note);
        }
        Assert.Contains(gpuGroups[0].Rows, r => r.Value == "128 MiB");
    }

    [Fact]
    public void Summary_lists_each_hardware_gpu_and_skips_software_adapter()
    {
        var lines = InventoryPresenter.Summary(SampleInventory([
            Gpu("iGPU", GpuKind.Integrated, 1, 1, "a"),
            Gpu("dGPU", GpuKind.Dedicated, 1, 1, "b"),
            Gpu("Microsoft Basic Render Driver", GpuKind.Software, 0, 1, "c"),
        ]));
        var gpus = lines.Where(l => l.Label == Loc.T("Inv.Gpu")).Select(l => l.Value).ToList();
        Assert.Equal(["iGPU (integrada)", "dGPU (dedicada)"], gpus);
    }

    [Fact]
    public void Laptop_battery_rows_appear_only_with_battery()
    {
        var withBattery = new PowerInfo(R(PowerSource.Battery), R(true), R(64), R(false), NA<PowerPlan>(), NA<IReadOnlyList<PowerPlan>>(), NA<string>());
        var rows = InventoryPresenter.Groups(SampleInventory(power: withBattery, form: FormFactor.Portable)).Single(g => g.Title == Loc.T("Inv.Group.Power")).Rows;
        Assert.Contains(rows, r => r.Value == "64 %");
        var desktop = InventoryPresenter.Groups(SampleInventory()).Single(g => g.Title == Loc.T("Inv.Group.Power")).Rows;
        Assert.DoesNotContain(desktop, r => r.Label == Loc.T("Inv.Power.Battery"));
    }

    [Fact]
    public void Observations_are_facts_without_judgement()
    {
        var t = DateTimeOffset.UnixEpoch;
        var session = new SessionResult
        {
            StartedAt = t,
            EndedAt = t.AddSeconds(2),
            Options = new SessionOptions(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1), SessionContext.Idle),
            Cancelled = false,
            Series = [new MetricSeries(new MetricDescriptor("cpu.utility", "Metric.CpuUtility", "%", "x", MetricKind.Derived), [Sample.Valid(t, 99), Sample.Invalid(t)])],
            Unsupported = [],
            Processes = NA<IReadOnlyList<ProcessUsage>>(),
        };
        var facts = ObservationPresenter.Facts(SampleInventory(), session);
        Assert.Contains(facts, f => f.Contains("99"));
        Assert.Contains(facts, f => f.Contains("1 muestra"));
        string[] forbidden = ["problema", "cuello de botella", "crítico", "peligro", "mal ", "lento"];
        Assert.DoesNotContain(facts, f => forbidden.Any(w => f.Contains(w, StringComparison.OrdinalIgnoreCase)));
    }
}
