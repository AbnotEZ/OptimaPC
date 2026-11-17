using System.Globalization;
using PcToolkit.Core.Privacy;

namespace PcToolkit.Core.Diagnostics;

/// <summary>
/// Registro local para resolver errores. Pasa por <see cref="PrivacyScrubber"/> antes de escribir,
/// rota a 1 MiB y conserva 3 archivos. No se envía a ningún sitio.
/// </summary>
public static class AppLog
{
    private const long MaxBytes = 1024 * 1024;
    private const int Keep = 3;
    private static readonly object Lock = new();
    private static string? _directory;
    private static PrivacyScrubber? _scrubber;

    public static void Initialize(string directory, PrivacyScrubber scrubber)
    {
        lock (Lock)
        {
            _directory = directory;
            _scrubber = scrubber;
        }
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message} | {ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}");

    private static void Write(string level, string message)
    {
        lock (Lock)
        {
            if (_directory is null || _scrubber is null)
            {
                return;
            }
            try
            {
                Directory.CreateDirectory(_directory);
                var path = Path.Combine(_directory, "app.log");
                Rotate(path);
                var line = string.Create(CultureInfo.InvariantCulture, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {_scrubber.Scrub(message)}{Environment.NewLine}");
                File.AppendAllText(path, line);
            }
            catch (IOException)
            {
                // El registro nunca debe hacer fallar la aplicación.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static void Rotate(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < MaxBytes)
        {
            return;
        }
        for (var i = Keep - 1; i >= 1; i--)
        {
            var src = i == 1 ? path : $"{path}.{i - 1}";
            var dst = $"{path}.{i}";
            if (File.Exists(src))
            {
                File.Move(src, dst, overwrite: true);
            }
        }
    }
}
