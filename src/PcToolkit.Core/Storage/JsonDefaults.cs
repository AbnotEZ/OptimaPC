using System.Text.Encodings.Web;
using System.Text.Json;

namespace PcToolkit.Core.Storage;

public static class JsonDefaults
{
    /// <summary>Opciones para archivos locales y exportaciones (texto legible, sin escapar acentos).</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
    };
}
