using System.Text.Json;
using PcToolkit.Core.Export;
using PcToolkit.Core.Model;
using PcToolkit.Core.Privacy;
using PcToolkit.Core.Storage;
using static PcToolkit.Core.Tests.TestData;

namespace PcToolkit.Core.Tests;

public sealed class PrivacyScrubberTests
{
    private static readonly PrivacyScrubber S = new(new PrivacyIdentity("alice", "ALICE-PC", "CORP", @"C:\Users\alice"));

    [Theory]
    [InlineData(@"C:\Users\alice\AppData\x.exe", @"%USERPROFILE%\AppData\x.exe")]
    [InlineData(@"""C:\\Users\\alice\\AppData\\x.exe""", @"""%USERPROFILE%\\AppData\\x.exe""")]
    [InlineData("c:/users/ALICE/file", "%USERPROFILE%/file")]
    [InlineData(@"D:\Users\bob\game.exe", @"D:\Users\<USER>\game.exe")]
    [InlineData("Host ALICE-PC in domain CORP for alice", "Host <COMPUTER> in domain <DOMAIN> for <USER>")]
    public void Replaces_identifying_data(string input, string expected) => Assert.Equal(expected, S.Scrub(input));

    [Fact]
    public void Matches_whole_words_only()
    {
        var s = new PrivacyScrubber(new PrivacyIdentity("ana", "PC", null, null));
        Assert.Equal("banana <USER>", s.Scrub("banana ana"));
    }
}

public sealed class ReportBuilderTests
{
    private static readonly PrivacyIdentity Id = new("alice", "ALICE-PC", "ALICE-PC", @"C:\Users\alice");
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 0, 0, 0, TimeSpan.Zero);

    private static SessionResult Session() => new()
    {
        StartedAt = Now,
        EndedAt = Now.AddSeconds(2),
        Options = new SessionOptions(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1), SessionContext.Idle),
        Cancelled = false,
        Series = [new MetricSeries(new MetricDescriptor("cpu.utility", "Metric.CpuUtility", "%", "x", MetricKind.Observed), [Sample.Valid(Now, 5)])],
        Unsupported = [],
        Processes = R<IReadOnlyList<ProcessUsage>>([new ProcessUsage("secretproc", 42, R(1.0), R(1UL), R(1UL), false)]),
    };

    [Theory]
    [InlineData(ReportFormat.Json)]
    [InlineData(ReportFormat.Text)]
    public void Default_export_contains_no_personal_identifiers(ReportFormat format)
    {
        var inv = SampleInventory() with
        {
            Startup = R<IReadOnlyList<StartupEntry>>([new StartupEntry("Tool of alice", @"C:\Users\alice\tool.exe", StartupLocation.UserRun, R(StartupState.Enabled))]),
        };
        var text = new ReportBuilder(new PrivacyScrubber(Id)).Build(inv, Session(), new ExportOptions(format, false, true), Now);

        Assert.DoesNotContain("alice", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALICE-PC", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"tool.exe", text, StringComparison.OrdinalIgnoreCase); // comandos excluidos por defecto
    }

    [Fact]
    public void Processes_can_be_excluded()
    {
        var text = new ReportBuilder(new PrivacyScrubber(Id)).Build(SampleInventory(), Session(), new ExportOptions(ReportFormat.Json, false, false), Now);
        Assert.DoesNotContain("secretproc", text);
        Assert.Contains("excluded-by-user", text);
    }

    [Fact]
    public void Json_export_is_valid_json_with_tool_identity()
    {
        var text = new ReportBuilder(new PrivacyScrubber(Id)).Build(SampleInventory(), null, new ExportOptions(ReportFormat.Json, true, true), Now);
        using var doc = JsonDocument.Parse(text);
        Assert.Equal(AppInfo.ProductName, doc.RootElement.GetProperty("tool").GetString());
        Assert.Contains("%USERPROFILE%", text); // con comandos incluidos, la ruta se anonimiza
    }
}

public sealed class HistoryStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "optimapc-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static AnalysisRecord Rec(DateTimeOffset when) => new()
    {
        Id = HistoryStore.NewId(when),
        AppVersion = "test",
        CreatedAt = when,
        Inventory = SampleInventory(),
    };

    [Fact]
    public void Round_trips_records_with_missing_values()
    {
        var store = new HistoryStore(_dir);
        var when = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);
        store.Save(Rec(when));

        var loaded = store.Load(HistoryStore.NewId(when));
        Assert.NotNull(loaded);
        Assert.Equal("Example CPU 8-Core", loaded.Inventory!.Cpu.Name.Value);
        Assert.Equal(ValueStatus.NotAvailable, loaded.Inventory.Power.BatteryPercent.Status);
        Assert.Equal(GpuKind.Integrated, loaded.Inventory.Gpus.Value![0].Kind.Value);
    }

    [Fact]
    public void Retention_keeps_most_recent_records()
    {
        var store = new HistoryStore(_dir, maxRecords: 3);
        var t0 = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 5; i++)
        {
            store.Save(Rec(t0.AddMinutes(i)));
        }
        var items = store.List().Items;
        Assert.Equal(3, items.Count);
        Assert.Equal(t0.AddMinutes(4), items[0].CreatedAt);
    }

    [Fact]
    public void Corrupt_and_incompatible_files_are_reported_not_loaded()
    {
        var store = new HistoryStore(_dir);
        store.Save(Rec(DateTimeOffset.UnixEpoch.AddYears(56)));
        File.WriteAllText(Path.Combine(_dir, "analysis-20200101T000000000Z.json"), "{ not json");
        File.WriteAllText(Path.Combine(_dir, "analysis-20200102T000000000Z.json"),
            """{"id":"20200102T000000000Z","schemaVersion":999,"appVersion":"x","createdAt":"2020-01-02T00:00:00Z"}""");

        var listing = store.List();
        Assert.Single(listing.Items);
        Assert.Equal(2, listing.UnreadableFiles);
        Assert.Null(store.Load("20200101T000000000Z"));
    }

    [Theory]
    [InlineData(@"..\..\evil")]
    [InlineData("a/b")]
    [InlineData("")]
    public void Path_traversal_ids_are_rejected(string id) =>
        Assert.Throws<ArgumentException>(() => new HistoryStore(_dir).Load(id));

    [Fact]
    public void Delete_all_removes_everything_including_unreadable()
    {
        var store = new HistoryStore(_dir);
        store.Save(Rec(DateTimeOffset.UnixEpoch.AddYears(56)));
        File.WriteAllText(Path.Combine(_dir, "analysis-bad.json"), "x");
        Assert.Equal(2, store.DeleteAll());
        Assert.Empty(store.List().Items);
    }
}

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "optimapc-settings-" + Guid.NewGuid().ToString("N") + ".json");

    public void Dispose() => File.Delete(_file);

    [Fact]
    public void Corrupt_settings_fall_back_to_defaults()
    {
        File.WriteAllText(_file, "{{{");
        Assert.Equal(new AppSettings(), new SettingsStore(_file).Load());
    }

    [Fact]
    public void Values_are_normalized()
    {
        var store = new SettingsStore(_file);
        store.Save(new AppSettings { Language = "fr", QuickAnalysisSeconds = 99999, DefaultIntervalMilliseconds = 1 });
        var loaded = store.Load();
        Assert.Null(loaded.Language);
        Assert.Equal(120, loaded.QuickAnalysisSeconds);
        Assert.Equal(500, loaded.DefaultIntervalMilliseconds);
    }
}
