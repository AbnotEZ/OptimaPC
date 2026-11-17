using System.Globalization;
using System.Text.Json;

namespace PcToolkit.Core.Localization;

/// <summary>
/// Textos centralizados en Localization/Strings.&lt;idioma&gt;.json (recursos incrustados).
/// Si falta una clave en el idioma activo se usa el español; si tampoco existe, se muestra
/// "[[clave]]" para que el fallo sea visible y no pase desapercibido.
/// </summary>
public static class Loc
{
    public const string FallbackLanguage = "es";
    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> Tables = new(StringComparer.Ordinal);
    private static readonly object Lock = new();

    public static string Language { get; private set; } = FallbackLanguage;

    public static IReadOnlyList<string> Available => ["es", "en"];

    /// <summary>Elige el idioma: el indicado si es compatible; si es null, el de la interfaz de Windows; si no, español.</summary>
    public static void SetLanguage(string? language)
    {
        var lang = language ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        Language = Available.Contains(lang) ? lang : (language is null && lang != "es" ? "en" : FallbackLanguage);
    }

    public static string T(string key)
    {
        if (Table(Language).TryGetValue(key, out var value) || Table(FallbackLanguage).TryGetValue(key, out value))
        {
            return value;
        }
        return $"[[{key}]]";
    }

    public static string F(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(key), args);

    public static bool Has(string key) => Table(Language).ContainsKey(key) || Table(FallbackLanguage).ContainsKey(key);

    /// <summary>Tabla completa de un idioma (para pruebas de cobertura de traducciones).</summary>
    public static IReadOnlyDictionary<string, string> Table(string language)
    {
        lock (Lock)
        {
            if (Tables.TryGetValue(language, out var table))
            {
                return table;
            }
            using var stream = typeof(Loc).Assembly.GetManifestResourceStream($"PcToolkit.Strings.Strings.{language}.json");
            table = stream is null
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new Dictionary<string, string>();
            Tables[language] = table;
            return table;
        }
    }
}
