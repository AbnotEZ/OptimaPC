using System.Diagnostics;
using PcToolkit.Core.Model;
using PcToolkit.Core.Ports;

namespace PcToolkit.Core.Inventory;

/// <summary>
/// Ejecuta las fuentes de inventario en paralelo. Un fallo en un área no impide obtener las demás:
/// el área afectada queda marcada como Failed y el detalle se añade a <see cref="SystemInventory.Issues"/>.
/// </summary>
public sealed class InventoryService(IInventorySources sources, ISessionClock? clock = null)
{
    private readonly ISessionClock _clock = clock ?? SystemSessionClock.Instance;

    public async Task<SystemInventory> CollectAsync(CancellationToken cancellationToken = default)
    {
        var started = _clock.Now;
        var watch = Stopwatch.StartNew();
        var issues = new List<CollectorIssue>();
        var issuesLock = new object();

        Task<T> Run<T>(string area, Func<T> read, Func<string, T> onFailure) => Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return read();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var detail = $"{ex.GetType().Name}: {ex.Message}";
                lock (issuesLock)
                {
                    issues.Add(new CollectorIssue(area, ValueStatus.Failed, detail));
                }
                return onFailure(detail);
            }
        }, cancellationToken);

        static Reading<T> F<T>(string d) => Reading<T>.Missing("collector", ValueStatus.Failed, d);

        var os = Run("os", sources.ReadOs, d => new OsInfo(F<string>(d), F<string>(d), F<string>(d), F<int>(d), F<int>(d), F<string>(d)));
        var machine = Run("machine", sources.ReadMachine, d => new MachineInfo(F<string>(d), F<string>(d), F<FormFactor>(d)));
        var cpu = Run("cpu", sources.ReadCpu, d => new CpuInfo(F<string>(d), F<string>(d), F<int>(d), F<int>(d), F<int>(d), F<IReadOnlyList<CoreClassCount>>(d)));
        var gpus = Run("gpu", sources.ReadGpus, F<IReadOnlyList<GpuInfo>>);
        var memory = Run("memory", sources.ReadMemory, d => new MemoryInfo(F<ulong>(d), F<ulong>(d), F<ulong>(d), F<IReadOnlyList<MemoryModule>>(d)));
        var storage = Run("storage", sources.ReadStorage, d => new StorageInfo(F<IReadOnlyList<PhysicalDiskInfo>>(d), F<IReadOnlyList<VolumeInfo>>(d)));
        var power = Run("power", sources.ReadPower, d => new PowerInfo(F<PowerSource>(d), F<bool>(d), F<int>(d), F<bool>(d), F<PowerPlan>(d), F<IReadOnlyList<PowerPlan>>(d), F<string>(d)));
        var displays = Run("displays", sources.ReadDisplays, F<IReadOnlyList<DisplayInfo>>);
        var startup = Run("startup", sources.ReadStartup, F<IReadOnlyList<StartupEntry>>);

        await Task.WhenAll(os, machine, cpu, gpus, memory, storage, power, displays, startup).ConfigureAwait(false);
        watch.Stop();

        return new SystemInventory
        {
            CapturedAt = started,
            Duration = watch.Elapsed,
            Os = os.Result,
            Machine = machine.Result,
            Cpu = cpu.Result,
            Gpus = gpus.Result,
            Memory = memory.Result,
            Storage = storage.Result,
            Power = power.Result,
            Displays = displays.Result,
            Startup = startup.Result,
            Issues = issues.OrderBy(i => i.Area, StringComparer.Ordinal).ToList(),
        };
    }
}
