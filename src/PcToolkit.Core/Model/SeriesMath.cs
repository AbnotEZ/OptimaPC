namespace PcToolkit.Core.Model;

/// <summary>Estadísticas descriptivas. Las muestras ausentes o inválidas se cuentan, nunca se interpolan.</summary>
public static class SeriesMath
{
    public static SeriesStatistics Compute(IReadOnlyList<Sample> samples)
    {
        var values = new List<double>(samples.Count);
        int missing = 0, invalid = 0;
        foreach (var s in samples)
        {
            switch (s.Status)
            {
                case SampleStatus.Valid when s.Value is double v && double.IsFinite(v):
                    values.Add(v);
                    break;
                case SampleStatus.Missing:
                    missing++;
                    break;
                default:
                    invalid++;
                    break;
            }
        }

        if (values.Count == 0)
        {
            return new SeriesStatistics(0, missing, invalid, null, null, null, null);
        }

        values.Sort();
        return new SeriesStatistics(
            values.Count, missing, invalid,
            values[0], values.Average(), Percentile(values, 0.95), values[^1]);
    }

    /// <summary>
    /// Percentil por el método del rango más cercano (nearest-rank) sobre valores ordenados:
    /// el menor valor tal que al menos p·N muestras son menores o iguales.
    /// </summary>
    public static double Percentile(IReadOnlyList<double> sortedValues, double p)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(p, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(p, 1);
        if (sortedValues.Count == 0)
        {
            throw new ArgumentException("Sin valores.", nameof(sortedValues));
        }
        var rank = (int)Math.Ceiling(p * sortedValues.Count);
        return sortedValues[Math.Clamp(rank, 1, sortedValues.Count) - 1];
    }
}
