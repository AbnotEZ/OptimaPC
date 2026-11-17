using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using PcToolkit.Core.Localization;
using PcToolkit.Core.Model;
using PcToolkit.Core.Presentation;

namespace PcToolkit.App.Controls;

/// <summary>
/// Gráfica de línea de una serie temporal con etiquetas de eje y unidad. Las muestras ausentes o
/// inválidas cortan la línea (no se interpolan) y se marcan en el eje inferior.
/// </summary>
public sealed class SeriesChart : FrameworkElement
{
    public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(
        nameof(Series), typeof(MetricSeries), typeof(SeriesChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSeriesChanged));

    public MetricSeries? Series
    {
        get => (MetricSeries?)GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    private static void OnSeriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SeriesChart chart && e.NewValue is MetricSeries s)
        {
            var st = s.Statistics;
            AutomationProperties.SetName(chart, Loc.F("Chart.A11y", SessionPresenter.MetricName(s.Metric),
                Fmt.WithUnit(st.Min, s.Metric.Unit), Fmt.WithUnit(st.Mean, s.Metric.Unit), Fmt.WithUnit(st.Max, s.Metric.Unit), st.ValidCount, s.Samples.Count));
        }
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width, 120);

    private Brush Res(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;

    protected override void OnRender(DrawingContext dc)
    {
        var series = Series;
        if (series is null || ActualWidth < 80)
        {
            return;
        }
        var text = Res("TextFillColorSecondaryBrush", Brushes.Gainsboro);
        var line = Res("AccentFillColorDefaultBrush", Brushes.DeepSkyBlue);
        var grid = Res("ControlStrokeColorDefaultBrush", new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)));
        var warn = Res("SystemFillColorCautionBrush", Brushes.Orange);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var typeface = new Typeface("Segoe UI");

        FormattedText Label(string s) => new(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11, text, dpi);

        var samples = series.Samples;
        var valid = samples.Where(s => s.Status == SampleStatus.Valid && s.Value.HasValue).Select(s => s.Value!.Value).ToList();
        var unit = series.Metric.Unit;
        double min = 0, max = unit == "%" ? 100 : (valid.Count > 0 ? valid.Max() : 1);
        if (unit != "%" && valid.Count > 0)
        {
            min = Math.Min(0, valid.Min());
            if (max - min < 1e-9)
            {
                max = min + 1;
            }
            max *= 1.05;
        }

        var maxLabel = Label($"{Fmt.Number(max, unit == "%" ? 0 : 1)} {unit}");
        var minLabel = Label($"{Fmt.Number(min, 0)} {unit}");
        var left = Math.Max(maxLabel.Width, minLabel.Width) + 6;
        const double top = 6, bottomPad = 20;
        var plot = new Rect(left, top, Math.Max(10, ActualWidth - left - 4), Math.Max(10, ActualHeight - top - bottomPad));

        var pen = new Pen(grid, 1);
        dc.DrawRectangle(null, pen, plot);
        dc.DrawText(maxLabel, new Point(0, plot.Top - 2));
        dc.DrawText(minLabel, new Point(0, plot.Bottom - minLabel.Height + 2));

        if (samples.Count == 0)
        {
            return;
        }
        var start = samples[0].Timestamp;
        var span = Math.Max(1e-3, (samples[^1].Timestamp - start).TotalSeconds);
        var startLabel = Label("0 s");
        var endLabel = Label($"{Fmt.Number(span, 0)} s");
        dc.DrawText(startLabel, new Point(plot.Left, plot.Bottom + 2));
        dc.DrawText(endLabel, new Point(plot.Right - endLabel.Width, plot.Bottom + 2));

        double X(DateTimeOffset t) => samples.Count == 1 ? plot.Left + plot.Width / 2 : plot.Left + (t - start).TotalSeconds / span * plot.Width;
        double Y(double v) => plot.Bottom - (Math.Clamp(v, min, max) - min) / (max - min) * plot.Height;

        var linePen = new Pen(line, 2) { LineJoin = PenLineJoin.Round };
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var drawing = false;
            foreach (var s in samples)
            {
                if (s.Status != SampleStatus.Valid || s.Value is not double v)
                {
                    drawing = false;
                    dc.DrawRectangle(warn, null, new Rect(X(s.Timestamp) - 1.5, plot.Bottom - 4, 3, 4));
                    continue;
                }
                var p = new Point(X(s.Timestamp), Y(v));
                if (!drawing)
                {
                    ctx.BeginFigure(p, false, false);
                    drawing = true;
                }
                else
                {
                    ctx.LineTo(p, true, true);
                }
            }
        }
        geometry.Freeze();
        dc.DrawGeometry(null, linePen, geometry);
    }
}
