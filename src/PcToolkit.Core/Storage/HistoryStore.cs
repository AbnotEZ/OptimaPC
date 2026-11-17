using System.Globalization;
using System.Text.Json;
using PcToolkit.Core.Model;

namespace PcToolkit.Core.Storage;

public sealed record AnalysisRecord
{
    public const int CurrentSchemaVersion = 1;

    public required string Id { get; init; }
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string AppVersion { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public SystemInventory? Inventory { get; init; }
    public SessionResult? Session { get; init; }
}

public sealed record AnalysisSummary(string Id, DateTimeOffset CreatedAt, TimeSpan? InventoryDuration, TimeSpan? SessionDuration, SessionContext? Context, bool Cancelled);

public sealed record HistoryListing(IReadOnlyList<AnalysisSummary> Items, int UnreadableFiles);

/// <summary>
/// Historial de análisis en archivos JSON. Límites: <see cref="MaxRecords"/> registros y
/// <see cref="MaxTotalBytes"/> en total; se eliminan primero los más antiguos.
/// Este historial NO contiene respaldos de restauración y puede borrarse sin riesgo.
/// </summary>
public sealed class HistoryStore(string directory, int maxRecords = 30, long maxTotalBytes = 25L * 1024 * 1024)
{
    private const string Prefix = "analysis-";
    private const string Extension = ".json";
    private readonly object _lock = new();

    public int MaxRecords { get; } = maxRecords;
    public long MaxTotalBytes { get; } = maxTotalBytes;

    public static string NewId(DateTimeOffset when) =>
        when.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);

    public void Save(AnalysisRecord record)
    {
        ValidateId(record.Id);
        lock (_lock)
        {
            AtomicFile.WriteAllText(PathFor(record.Id), JsonSerializer.Serialize(record, JsonDefaults.Options));
            ApplyRetention();
        }
    }

    public HistoryListing List()
    {
        lock (_lock)
        {
            var items = new List<AnalysisSummary>();
            var unreadable = 0;
            foreach (var file in Files())
            {
                var record = TryRead(file.FullName);
                if (record is null)
                {
                    unreadable++;
                    continue;
                }
                items.Add(new AnalysisSummary(
                    record.Id, record.CreatedAt, record.Inventory?.Duration, record.Session?.Elapsed,
                    record.Session?.Options.Context, record.Session?.Cancelled ?? false));
            }
            return new HistoryListing(items.OrderByDescending(i => i.CreatedAt).ToList(), unreadable);
        }
    }

    public AnalysisRecord? Load(string id)
    {
        ValidateId(id);
        lock (_lock)
        {
            var path = PathFor(id);
            return File.Exists(path) ? TryRead(path) : null;
        }
    }

    public AnalysisRecord? LoadLatest()
    {
        var latest = List().Items.FirstOrDefault();
        return latest is null ? null : Load(latest.Id);
    }

    public bool Delete(string id)
    {
        ValidateId(id);
        lock (_lock)
        {
            var path = PathFor(id);
            if (!File.Exists(path))
            {
                return false;
            }
            File.Delete(path);
            return true;
        }
    }

    /// <summary>Elimina todo el historial (incluidos archivos ilegibles). Devuelve el número de archivos borrados.</summary>
    public int DeleteAll()
    {
        lock (_lock)
        {
            var count = 0;
            foreach (var file in Files())
            {
                file.Delete();
                count++;
            }
            return count;
        }
    }

    private void ApplyRetention()
    {
        var files = Files().OrderByDescending(f => f.Name, StringComparer.Ordinal).ToList();
        long total = 0;
        for (var i = 0; i < files.Count; i++)
        {
            total += files[i].Length;
            // El registro más reciente se conserva siempre, aunque supere el límite de tamaño por sí solo.
            if (i > 0 && (i >= MaxRecords || total > MaxTotalBytes))
            {
                files[i].Delete();
            }
        }
    }

    private IEnumerable<FileInfo> Files()
    {
        var dir = new DirectoryInfo(directory);
        if (!dir.Exists)
        {
            return [];
        }
        // Solo archivos regulares de este formato; se ignoran enlaces y puntos de reanálisis.
        return dir.EnumerateFiles(Prefix + "*" + Extension, SearchOption.TopDirectoryOnly)
            .Where(f => (f.Attributes & FileAttributes.ReparsePoint) == 0)
            .ToList();
    }

    private static AnalysisRecord? TryRead(string path)
    {
        try
        {
            var record = JsonSerializer.Deserialize<AnalysisRecord>(File.ReadAllText(path), JsonDefaults.Options);
            return record is { SchemaVersion: AnalysisRecord.CurrentSchemaVersion } ? record : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    private string PathFor(string id) => Path.Combine(directory, Prefix + id + Extension);

    private static void ValidateId(string id)
    {
        // Evita rutas arbitrarias: solo el formato generado por NewId.
        if (string.IsNullOrEmpty(id) || id.Length > 32 || !id.All(c => char.IsAsciiLetterOrDigit(c)))
        {
            throw new ArgumentException("Identificador de análisis no válido.", nameof(id));
        }
    }
}
