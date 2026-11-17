using System.Text.RegularExpressions;

namespace PcToolkit.Core.Interpretation;

/// <summary>
/// Agrega las instancias del contador "\GPU Engine(*)\Utilization Percentage".
/// Cada instancia tiene la forma "pid_1234_luid_0x00000000_0x0000ABCD_phys_0_eng_3_engtype_3D".
/// Método (el mismo criterio que muestra el Administrador de tareas): se suma la utilización de todos
/// los procesos por motor físico y se toma el motor más ocupado de cada adaptador.
/// </summary>
public static partial class GpuCounterAggregator
{
    public const string Formula = "max over engines (sum over processes of \\GPU Engine(*)\\Utilization Percentage)";

    [GeneratedRegex(@"luid_(0x[0-9a-fA-F]+_0x[0-9a-fA-F]+)_phys_(\d+)_eng_(\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex EngineInstance();

    [GeneratedRegex(@"luid_(0x[0-9a-fA-F]+_0x[0-9a-fA-F]+)", RegexOptions.CultureInvariant)]
    private static partial Regex LuidInstance();

    /// <summary>Utilización por adaptador (clave = LUID normalizado en minúsculas, por ejemplo "0x00000000_0x0000abcd").</summary>
    public static IReadOnlyDictionary<string, double> UtilizationByAdapter(IEnumerable<KeyValuePair<string, double>> instances)
    {
        var perEngine = new Dictionary<(string Luid, string Engine), double>();
        foreach (var (name, value) in instances)
        {
            if (!double.IsFinite(value) || value < 0)
            {
                continue;
            }
            var m = EngineInstance().Match(name);
            if (!m.Success)
            {
                continue;
            }
            var key = (m.Groups[1].Value.ToLowerInvariant(), $"{m.Groups[2].Value}_{m.Groups[3].Value}");
            perEngine[key] = perEngine.GetValueOrDefault(key) + value;
        }

        return perEngine
            .GroupBy(kv => kv.Key.Luid)
            .ToDictionary(g => g.Key, g => Math.Min(100.0, g.Max(kv => kv.Value)));
    }

    /// <summary>Suma de un contador "\GPU Adapter Memory(*)\..." por adaptador.</summary>
    public static IReadOnlyDictionary<string, double> SumByAdapter(IEnumerable<KeyValuePair<string, double>> instances)
    {
        var result = new Dictionary<string, double>();
        foreach (var (name, value) in instances)
        {
            if (!double.IsFinite(value) || value < 0)
            {
                continue;
            }
            var m = LuidInstance().Match(name);
            if (m.Success)
            {
                var luid = m.Groups[1].Value.ToLowerInvariant();
                result[luid] = result.GetValueOrDefault(luid) + value;
            }
        }
        return result;
    }

    /// <summary>Formatea un LUID (HighPart, LowPart) con el mismo formato que usan las instancias de los contadores.</summary>
    public static string FormatLuid(int highPart, uint lowPart) =>
        $"0x{unchecked((uint)highPart):x8}_0x{lowPart:x8}";
}
