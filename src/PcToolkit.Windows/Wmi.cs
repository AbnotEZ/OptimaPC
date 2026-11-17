using System.Globalization;
using System.Management;
using PcToolkit.Core.Model;

namespace PcToolkit.Windows;

/// <summary>Consultas WMI/CIM de solo lectura con tiempo límite.</summary>
internal static class Wmi
{
    public const string Cimv2 = @"root\CIMV2";
    public const string Storage = @"root\Microsoft\Windows\Storage";

    public static List<Dictionary<string, object?>> Query(string scope, string className, params string[] properties)
    {
        var wql = $"SELECT {string.Join(",", properties)} FROM {className}";
        var options = new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(15), ReturnImmediately = true, Rewindable = false };
        using var searcher = new ManagementObjectSearcher(new ManagementScope(scope), new ObjectQuery(wql), options);
        var result = new List<Dictionary<string, object?>>();
        using var collection = searcher.Get();
        foreach (var obj in collection)
        {
            using (obj)
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in properties)
                {
                    try
                    {
                        row[p] = obj[p];
                    }
                    catch (ManagementException)
                    {
                        row[p] = null; // Propiedad inexistente en esta versión de Windows.
                    }
                }
                result.Add(row);
            }
        }
        return result;
    }

    public static string Source(string className, string property) => $"WMI {className}.{property}";

    public static string? Str(this Dictionary<string, object?> row, string key)
    {
        var s = row.GetValueOrDefault(key) switch
        {
            null => null,
            char c => c.ToString(),
            var v => Convert.ToString(v, CultureInfo.InvariantCulture),
        };
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    public static long? Long(this Dictionary<string, object?> row, string key)
    {
        var v = row.GetValueOrDefault(key);
        if (v is null)
        {
            return null;
        }
        try
        {
            return Convert.ToInt64(v, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return null;
        }
    }

    public static IReadOnlyList<int>? IntArray(this Dictionary<string, object?> row, string key) =>
        row.GetValueOrDefault(key) is Array a
            ? a.Cast<object>().Select(o => Convert.ToInt32(o, CultureInfo.InvariantCulture)).ToList()
            : null;

    /// <summary>Clasifica una excepción de consulta como estado del dato.</summary>
    public static ValueStatus StatusOf(Exception ex) => ex switch
    {
        UnauthorizedAccessException => ValueStatus.AccessDenied,
        ManagementException { ErrorCode: ManagementStatus.AccessDenied } => ValueStatus.AccessDenied,
        ManagementException { ErrorCode: ManagementStatus.InvalidNamespace or ManagementStatus.InvalidClass or ManagementStatus.NotSupported } => ValueStatus.NotSupported,
        _ => ValueStatus.Failed,
    };
}
