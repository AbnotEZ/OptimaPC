using System.Text.RegularExpressions;

namespace PcToolkit.Core.Privacy;

/// <summary>Datos identificativos del entorno que nunca deben aparecer en informes compartibles.</summary>
public sealed record PrivacyIdentity(string? UserName, string? MachineName, string? DomainName, string? UserProfilePath)
{
    public static PrivacyIdentity FromEnvironment()
    {
        string? Safe(Func<string?> f)
        {
            try { return f(); } catch (InvalidOperationException) { return null; } catch (PlatformNotSupportedException) { return null; }
        }
        return new PrivacyIdentity(
            Safe(() => Environment.UserName),
            Safe(() => Environment.MachineName),
            Safe(() => Environment.UserDomainName),
            Safe(() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
    }
}

/// <summary>
/// Sustituye usuario, nombre del equipo, dominio y rutas del perfil por marcadores.
/// Se aplica al texto final del informe (también tolera barras invertidas escapadas en JSON).
/// </summary>
public sealed partial class PrivacyScrubber
{
    public const string UserToken = "<USER>";
    public const string ComputerToken = "<COMPUTER>";
    public const string DomainToken = "<DOMAIN>";
    public const string ProfileToken = "%USERPROFILE%";

    private readonly List<(Regex Pattern, string Replacement)> _rules = [];

    // C:\Users\<nombre>\ (también con \\ o /) → C:\Users\<USER>\
    [GeneratedRegex(@"(?i)([a-z]:(?:\\\\|\\|/)users(?:\\\\|\\|/))(?!<USER>)[^\\/""'\s:*?<>|]+", RegexOptions.CultureInvariant)]
    private static partial Regex UsersFolder();

    public PrivacyScrubber(PrivacyIdentity identity)
    {
        if (!string.IsNullOrWhiteSpace(identity.UserProfilePath) && identity.UserProfilePath.Length > 3)
        {
            var p = identity.UserProfilePath.TrimEnd('\\', '/');
            foreach (var variant in new[] { p, p.Replace("\\", "\\\\", StringComparison.Ordinal), p.Replace('\\', '/') }.Distinct())
            {
                _rules.Add((new Regex(Regex.Escape(variant), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), ProfileToken));
            }
        }

        AddWord(identity.UserName, UserToken);
        AddWord(identity.MachineName, ComputerToken);
        if (!string.Equals(identity.DomainName, identity.MachineName, StringComparison.OrdinalIgnoreCase))
        {
            AddWord(identity.DomainName, DomainToken);
        }
    }

    private void AddWord(string? value, string token)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 2)
        {
            return;
        }
        // Coincidencia como palabra completa para no alterar otras cadenas que la contengan.
        var pattern = $@"(?<![\p{{L}}\p{{N}}_]){Regex.Escape(value)}(?![\p{{L}}\p{{N}}_])";
        _rules.Add((new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), token));
    }

    public string Scrub(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        foreach (var (pattern, replacement) in _rules)
        {
            text = pattern.Replace(text, replacement);
        }
        return UsersFolder().Replace(text, "$1" + UserToken);
    }
}
