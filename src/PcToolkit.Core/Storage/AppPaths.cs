namespace PcToolkit.Core.Storage;

/// <summary>
/// Ubicaciones de datos locales. Por defecto en %LocalAppData%\&lt;producto&gt; (perfil del usuario,
/// no sincronizado con roaming). El historial de diagnóstico está separado de los futuros respaldos
/// de restauración (carpeta "backups", fase 3), para poder borrar uno sin perder el otro.
/// </summary>
public sealed class AppPaths
{
    public AppPaths(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = Path.GetFullPath(root);
    }

    public static AppPaths Default() =>
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppInfo.DataFolderName));

    public string Root { get; }
    public string HistoryDirectory => Path.Combine(Root, "history");
    public string LogsDirectory => Path.Combine(Root, "logs");
    public string SettingsFile => Path.Combine(Root, "settings.json");
}

internal static class AtomicFile
{
    /// <summary>Escribe en un archivo temporal y lo renombra, para no dejar archivos a medias tras un cierre inesperado.</summary>
    public static void WriteAllText(string path, string content)
    {
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        var temp = Path.Combine(dir, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }
}
