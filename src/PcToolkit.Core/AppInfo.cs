using System.Reflection;

namespace PcToolkit.Core;

/// <summary>
/// Identidad del producto. El nombre se define solo en Directory.Build.props (&lt;Product&gt;).
/// </summary>
public static class AppInfo
{
    private static readonly Assembly Self = typeof(AppInfo).Assembly;

    public static string ProductName { get; } =
        Self.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "OptimaPC";

    public static string Version { get; } =
        (Self.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0")
            .Split('+')[0];

    /// <summary>Nombre de carpeta para datos locales, derivado del nombre del producto.</summary>
    public static string DataFolderName { get; } =
        new string(ProductName.Where(char.IsLetterOrDigit).ToArray());
}
