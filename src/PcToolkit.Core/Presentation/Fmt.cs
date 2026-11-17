using System.Globalization;
using PcToolkit.Core.Localization;
using PcToolkit.Core.Model;

namespace PcToolkit.Core.Presentation;

/// <summary>Formato de valores para la interfaz. Unidades binarias explícitas (GiB = 1024³ bytes).</summary>
public static class Fmt
{
    private static CultureInfo C => CultureInfo.CurrentCulture;

    public static string Status(ValueStatus status) => status switch
    {
        ValueStatus.Available => "",
        ValueStatus.NotAvailable => Loc.T("Status.NotAvailable"),
        ValueStatus.NotSupported => Loc.T("Status.NotSupported"),
        ValueStatus.AccessDenied => Loc.T("Status.AccessDenied"),
        _ => Loc.T("Status.Failed"),
    };

    public static string Value<T>(Reading<T> reading, Func<T, string>? format = null) =>
        reading.HasValue
            ? (format is null ? Convert.ToString(reading.Value, C) ?? "" : format(reading.Value!))
            : Status(reading.Status);

    public static string Bytes(ulong bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        double v = bytes;
        var i = 0;
        while (v >= 1024 && i < units.Length - 1)
        {
            v /= 1024;
            i++;
        }
        return i == 0 ? $"{bytes.ToString("N0", C)} B" : $"{v.ToString(v >= 100 ? "N0" : "N1", C)} {units[i]}";
    }

    public static string Number(double value, int decimals = 1) => value.ToString("N" + decimals, C);

    public static string Int(long value) => value.ToString("N0", C);

    public static string WithUnit(double? value, string unit, int decimals = 1) =>
        value is double v ? $"{Number(v, decimals)} {unit}" : "—";

    public static string Duration(TimeSpan span)
    {
        if (span.TotalSeconds < 1)
        {
            return $"{Int((long)span.TotalMilliseconds)} ms";
        }
        if (span.TotalMinutes < 1)
        {
            return $"{Number(span.TotalSeconds, 1)} s";
        }
        return $"{(int)span.TotalMinutes} min {span.Seconds} s";
    }

    public static string DateTime(DateTimeOffset when) => when.ToLocalTime().ToString("g", C);

    public static string Time(DateTimeOffset when) => when.ToLocalTime().ToString("T", C);

    public static string Kind(GpuKind kind) => Loc.T($"GpuKind.{kind}");

    public static string Context(SessionContext context) => Loc.T($"Context.{context}");
}
