using System.Text.Json;
using PcToolkit.Core.Inventory;
using PcToolkit.Core.Model;
using PcToolkit.Core.Monitoring;
using PcToolkit.Core.Ports;
using PcToolkit.Core.Storage;
using Xunit.Abstractions;

namespace PcToolkit.Windows.IntegrationTests;

/// <summary>
/// Ejecuta el inventario y el muestreo reales. Son de SOLO LECTURA. Las aserciones comprueban
/// coherencia (no valores concretos de un equipo), porque deben pasar en cualquier PC y en CI.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ReadOnlyInventoryTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Inventory_completes_and_reports_explicit_states()
    {
        var inv = await new InventoryService(new WindowsInventorySources()).CollectAsync();

        output.WriteLine(JsonSerializer.Serialize(new { inv.Cpu, inv.Gpus, inv.Memory.InstalledBytes, inv.Memory.UsableBytes, inv.Storage, inv.Power, inv.Displays, inv.Os, inv.Machine.FormFactor, inv.Issues, Startup = inv.Startup.Value?.Count }, JsonDefaults.Options));

        Assert.Empty(inv.Issues);
        Assert.True(inv.Os.Build.HasValue);
        Assert.True(inv.Os.Build.Value >= 10240);
        Assert.True(inv.Cpu.LogicalProcessors.HasValue);
        Assert.Equal(Environment.ProcessorCount, inv.Cpu.LogicalProcessors.Value);
        Assert.True(inv.Memory.UsableBytes.HasValue && inv.Memory.UsableBytes.Value > 0);
        Assert.True(inv.Memory.AvailableBytes.Value <= inv.Memory.UsableBytes.Value);
        if (inv.Memory.InstalledBytes.HasValue)
        {
            Assert.True(inv.Memory.InstalledBytes.Value >= inv.Memory.UsableBytes.Value);
        }
        Assert.True(inv.Storage.Volumes.HasValue);
        Assert.Contains(inv.Storage.Volumes.Value!, v => v.Root.StartsWith(Path.GetPathRoot(Environment.SystemDirectory)![..1], StringComparison.OrdinalIgnoreCase));
        Assert.True(inv.Gpus.Status is ValueStatus.Available or ValueStatus.Failed);
        if (inv.Gpus.HasValue)
        {
            foreach (var gpu in inv.Gpus.Value!)
            {
                // Integrada o dedicada solo si la fuente lo declara; nunca se adivina.
                Assert.True(gpu.Kind.HasValue || gpu.Kind.Status == ValueStatus.NotSupported);
            }
        }
    }

    [Fact]
    public async Task Sampling_session_produces_timestamped_samples()
    {
        var inv = await new InventoryService(new WindowsInventorySources()).CollectAsync();
        var session = new MonitoringSession(new PdhSamplerFactory(), new ProcessSnapshotSource());
        var ctx = new SamplerContext(inv.Cpu.MaxClockMHz.HasValue ? inv.Cpu.MaxClockMHz.Value : null, inv.Gpus.Value ?? []);

        var result = await session.RunAsync(new SessionOptions(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1), SessionContext.Unspecified), ctx);

        foreach (var s in result.Series)
        {
            output.WriteLine($"{s.Metric.Id} [{s.Metric.Unit}] {JsonSerializer.Serialize(s.Statistics)}");
        }
        foreach (var u in result.Unsupported)
        {
            output.WriteLine($"unsupported: {u.NameKey} {u.Subject} {u.Reason}");
        }
        output.WriteLine($"processes: {result.Processes.Status} {result.Processes.Value?.Count}");

        var cpu = Assert.Single(result.Series, s => s.Metric.Id == "cpu.utility");
        Assert.Equal(3, cpu.Samples.Count);
        Assert.True(cpu.Statistics.ValidCount >= 2);
        Assert.All(cpu.Samples.Where(x => x.Status == SampleStatus.Valid), x => Assert.InRange(x.Value!.Value, 0, 100));
        var mem = Assert.Single(result.Series, s => s.Metric.Id == "mem.available");
        Assert.True(mem.Statistics.ValidCount >= 2);
        Assert.Contains(result.Unsupported, u => u.NameKey == "Metric.CpuTemperature");
        Assert.True(result.Processes.HasValue);
        Assert.NotEmpty(result.Processes.Value!);
    }
}
