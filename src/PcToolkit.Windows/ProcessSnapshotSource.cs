using System.ComponentModel;
using System.Diagnostics;
using PcToolkit.Core.Ports;

namespace PcToolkit.Windows;

/// <summary>
/// Lee nombre, tiempos de CPU y memoria de los procesos. SOLO LECTURA: nunca cierra ni modifica procesos.
/// No lee rutas ni líneas de comandos (pueden contener datos personales). Los procesos protegidos
/// devuelven acceso denegado y se informan así.
/// </summary>
public sealed class ProcessSnapshotSource : IProcessSnapshotSource
{
    public int LogicalProcessorCount => Environment.ProcessorCount;

    public IReadOnlyList<ProcessSample> Capture()
    {
        var processes = Process.GetProcesses();
        var result = new List<ProcessSample>(processes.Length);
        foreach (var p in processes)
        {
            using (p)
            {
                string name;
                try
                {
                    name = p.ProcessName;
                }
                catch (InvalidOperationException)
                {
                    continue; // El proceso terminó mientras se leía.
                }
                result.Add(new ProcessSample(
                    p.Id,
                    name,
                    Try(() => p.StartTime.ToUniversalTime()),
                    Try(() => p.TotalProcessorTime),
                    Try(() => (ulong)p.WorkingSet64),
                    Try(() => (ulong)p.PrivateMemorySize64)));
            }
        }
        return result;
    }

    private static T? Try<T>(Func<T> read) where T : struct
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }
}
