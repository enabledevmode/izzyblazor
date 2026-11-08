using System.Globalization;
using System.Text;

namespace IzzyBlazor;

/// <summary>A radar's grid: rings that join the spokes with straight lines, or circles.</summary>
public enum RadarGrid
{
    /// <summary>Rings as polygons, a corner on each spoke, as the outlines are drawn.</summary>
    Polygon,

    /// <summary>Rings as circles.</summary>
    Circle,
}

/// <summary>One series as the radar draws it.</summary>
internal sealed record RadarSeriesData(string Name, double?[] Values)
{
    public string? Color { get; init; }

    public bool Fill { get; init; }

    public bool Hidden { get; init; }

    public ChartLabels Labels { get; init; }

    public string? LabelFormat { get; init; }
}

internal sealed record RadarInput(string[] Spokes, IReadOnlyList<RadarSeriesData> Series)
{
    public double? Min { get; init; }

    public double? Max { get; init; }

    public double StartAngle { get; init; }

    public string? ValueFormat { get; init; }
}

/// <summary>A spoke's name, where it sits (percent of the plot's box) and which way it reads from there.</summary>
internal sealed record RadarSpoke(string Text, double X, double Y, int Side, int Level);

/// <summary>A ring's value, its radius (0 to 1) and its label.</summary>
internal sealed record RadarRing(double Value, double R, string Text);

/// <summary>A value's marker, in percent of the plot's box.</summary>
internal sealed record RadarMark(int Spoke, double X, double Y, string Text);

/// <summary>A series' shape: its outline (one closed path, or open runs around gaps), its fill, its marks.</summary>
internal sealed record RadarShape(int Index, int Slot, RadarSeriesData Data, string Outline, string? Area, IReadOnlyList<RadarMark> Marks, string?[] Texts);

internal sealed record RadarModel(
    double Min,
    double Max,
    IReadOnlyList<RadarSpoke> Spokes,
    IReadOnlyList<RadarRing> Rings,
    IReadOnlyList<RadarShape> Series,
    double Start);

/// <summary>
/// A radar's geometry, apart from rendering so it can be tested: spokes clockwise from the top
/// (turned by StartAngle), one shared scale from Min (0 by default) to a round Max, rings at its
/// round ticks, and each series' outline, fill and markers. Paths are in a 1000-unit box centered
/// at 500, the plot's own; markers and names in percent of it.
/// </summary>
internal static class RadarLayout
{
    private static string Num(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    public static RadarModel Build(RadarInput input)
    {
        var n = input.Spokes.Length;
        var values = input.Series.SelectMany(s => s.Values).OfType<double>().ToList();
        var min = input.Min ?? Math.Min(0, values.DefaultIfEmpty(0).Min());
        var top = input.Max ?? Math.Max(min, values.DefaultIfEmpty(min).Max());
        double max;
        if (input.Max is { } fixedMax)
        {
            max = fixedMax;
        }
        else
        {
            // The round tick at or above the largest value, from Min.
            max = ChartScale.Nice(min, top > min ? top : min + 1, 4).Max;
        }

        var start = (input.StartAngle % 360 + 360) % 360 / 360;
        double Turn(int k) => (start + (double)k / Math.Max(1, n)) * 2 * Math.PI;
        double Radius(double v) => Math.Clamp((v - min) / (max - min), 0, 1);

        // Rings at the scale's round ticks above Min; Min is the center.
        var spec = ChartScale.TickSpec(min, max, 4);
        var decimals = ChartScale.Decimals(spec.Step);
        var rings = new List<RadarRing>();
        for (var i = spec.I1; i <= spec.I2; i++)
        {
            var v = spec.Value(i);
            if (v <= min) continue;
            rings.Add(new RadarRing(v, Radius(v), Format(v, input.ValueFormat, decimals)));
        }

        if (rings.Count == 0 || rings[^1].Value < max)
        {
            rings.Add(new RadarRing(max, 1, Format(max, input.ValueFormat, decimals)));
        }

        // Names just past the outer ring: Side -1 reads toward the start (left half), 1 toward
        // the end, 0 centered (top and bottom); Level -1 sits above its point, 1 below, 0 level.
        var spokes = new List<RadarSpoke>();
        for (var k = 0; k < n; k++)
        {
            var (sin, cos) = Math.SinCos(Turn(k));
            var side = sin > 0.2 ? 1 : sin < -0.2 ? -1 : 0;
            var level = cos > 0.5 ? -1 : cos < -0.5 ? 1 : 0;
            spokes.Add(new RadarSpoke(input.Spokes[k], 50 + 53 * sin, 50 - 53 * cos, side, level));
        }

        var shapes = new List<RadarShape>();
        for (var s = 0; s < input.Series.Count; s++)
        {
            var data = input.Series[s];
            var points = new (double X, double Y)?[n];
            var marks = new List<RadarMark>();
            var texts = new string?[n];
            for (var k = 0; k < n; k++)
            {
                if (data.Values[k] is not { } v) continue;
                var (sin, cos) = Math.SinCos(Turn(k));
                var r = Radius(v);
                points[k] = (500 + 500 * r * sin, 500 - 500 * r * cos);
                texts[k] = Format(v, data.LabelFormat ?? input.ValueFormat, null);
                marks.Add(new RadarMark(k, 50 + 50 * r * sin, 50 - 50 * r * cos, Format(v, input.ValueFormat, null)));
            }

            var complete = n > 0 && points.All(p => p is not null);
            string outline;
            if (complete)
            {
                outline = Polygon(points.Select(p => p!.Value));
            }
            else
            {
                // Runs of neighbors that both have a value, going round: a gap leaves out the two
                // segments to its spoke, never drawing to the center (that would read as zero).
                var path = new StringBuilder();
                for (var k = 0; k < n; k++)
                {
                    var next = (k + 1) % n;
                    if (n > 1 && points[k] is { } a && points[next] is { } b)
                    {
                        path.Append(CultureInfo.InvariantCulture, $"M{Num(a.X)},{Num(a.Y)}L{Num(b.X)},{Num(b.Y)}");
                    }
                }

                outline = path.ToString();
            }

            // A fill only round a complete outline: with a gap there's no closed shape to fill.
            shapes.Add(new RadarShape(s, s < 8 ? s + 1 : 9, data, outline, data.Fill && complete ? outline : null, marks, texts));
        }

        return new RadarModel(min, max, spokes, rings, shapes, start);
    }

    /// <summary>A ring at radius <paramref name="r"/> (0 to 1) through every spoke.</summary>
    public static string Ring(int spokes, double start, double r)
    {
        var points = Enumerable.Range(0, spokes).Select(k =>
        {
            var (sin, cos) = Math.SinCos((start + (double)k / spokes) * 2 * Math.PI);
            return (500 + 500 * r * sin, 500 - 500 * r * cos);
        });
        return Polygon(points);
    }

    /// <summary>A spoke's line from the center to the outer ring.</summary>
    public static (string X, string Y) SpokeEnd(int spokes, double start, int k)
    {
        var (sin, cos) = Math.SinCos((start + (double)k / spokes) * 2 * Math.PI);
        return (Num(500 + 500 * sin), Num(500 - 500 * cos));
    }

    private static string Polygon(IEnumerable<(double X, double Y)> points)
    {
        var path = new StringBuilder();
        foreach (var (x, y) in points)
        {
            path.Append(path.Length == 0 ? 'M' : 'L').Append(Num(x)).Append(',').Append(Num(y));
        }

        return path.Length == 0 ? "" : path.Append('Z').ToString();
    }

    private static string Format(double value, string? format, int? decimals) =>
        value.ToString(format ?? (decimals is > 0 ? "#,##0." + new string('0', decimals.Value) : "#,##0.##"), CultureInfo.CurrentCulture);
}
