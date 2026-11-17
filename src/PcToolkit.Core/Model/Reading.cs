using System.Text.Json.Serialization;

namespace PcToolkit.Core.Model;

/// <summary>Estado de un dato. Nunca se sustituye un dato ausente por un valor inventado.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ValueStatus>))]
public enum ValueStatus
{
    /// <summary>Valor obtenido de la fuente indicada.</summary>
    Available,
    /// <summary>La fuente existe pero no devolvió el dato en este equipo.</summary>
    NotAvailable,
    /// <summary>No hay una fuente compatible para este dato en esta versión de la herramienta.</summary>
    NotSupported,
    /// <summary>La fuente denegó el acceso (por ejemplo, proceso protegido).</summary>
    AccessDenied,
    /// <summary>La consulta falló con un error.</summary>
    Failed,
}

/// <summary>
/// Un dato junto con su estado y su origen. <see cref="Detail"/> es información técnica
/// opcional (código de error, limitación conocida), nunca texto para sustituir el valor.
/// </summary>
public sealed record Reading<T>
{
    public T? Value { get; init; }
    public ValueStatus Status { get; init; }
    public string Source { get; init; } = "";
    public string? Detail { get; init; }

    [JsonIgnore]
    public bool HasValue => Status == ValueStatus.Available;

    public static Reading<T> Of(T value, string source, string? detail = null) =>
        new() { Value = value, Status = ValueStatus.Available, Source = source, Detail = detail };

    public static Reading<T> Missing(string source, ValueStatus status = ValueStatus.NotAvailable, string? detail = null)
    {
        if (status == ValueStatus.Available)
        {
            throw new ArgumentException("Un dato ausente no puede marcarse como disponible.", nameof(status));
        }
        return new() { Status = status, Source = source, Detail = detail };
    }

    /// <summary>Crea un dato disponible solo si el valor no es nulo (ni cadena vacía).</summary>
    public static Reading<T> FromNullable(T? value, string source, string? detail = null)
    {
        if (value is null || value is string s && string.IsNullOrWhiteSpace(s))
        {
            return Missing(source, ValueStatus.NotAvailable, detail);
        }
        return Of(value, source, detail);
    }

    public Reading<TOut> Map<TOut>(Func<T, TOut> map) =>
        HasValue
            ? new Reading<TOut> { Value = map(Value!), Status = Status, Source = Source, Detail = Detail }
            : new Reading<TOut> { Status = Status, Source = Source, Detail = Detail };
}

/// <summary>Problema registrado durante la recolección de un área (para depuración y para la interfaz).</summary>
public sealed record CollectorIssue(string Area, ValueStatus Status, string Detail);
