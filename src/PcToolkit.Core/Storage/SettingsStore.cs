using System.Text.Json;

namespace PcToolkit.Core.Storage;

public sealed record AppSettings
{
    /// <summary>"es", "en" o null para seguir el idioma de Windows.</summary>
    public string? Language { get; init; }
    public int QuickAnalysisSeconds { get; init; } = 20;
    public int DefaultSessionSeconds { get; init; } = 60;
    public int DefaultIntervalMilliseconds { get; init; } = 1000;

    public static readonly IReadOnlyList<string> SupportedLanguages = ["es", "en"];

    public AppSettings Normalized() => this with
    {
        Language = Language is not null && SupportedLanguages.Contains(Language) ? Language : null,
        QuickAnalysisSeconds = Math.Clamp(QuickAnalysisSeconds, 5, 120),
        DefaultSessionSeconds = Math.Clamp(DefaultSessionSeconds, 5, 1800),
        DefaultIntervalMilliseconds = Math.Clamp(DefaultIntervalMilliseconds, 500, 10_000),
    };
}

public sealed class SettingsStore(string path)
{
    /// <summary>Carga la configuración; si el archivo no existe o está dañado, usa los valores por defecto.</summary>
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(path))
            {
                return new AppSettings();
            }
            return (JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonDefaults.Options) ?? new AppSettings()).Normalized();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings) =>
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(settings.Normalized(), JsonDefaults.Options));
}
