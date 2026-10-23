using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace IzzyBlazor.Site.Services;

/// <summary>
/// frame-heights.json: each example's preview height by frame width, in each mode, measured in a
/// browser by <c>build/measure-frames.mjs</c>. The example viewer sizes its frames from it with
/// container queries, so a frame starts at its content's height, with JavaScript off too, and the
/// page doesn't move as examples load. An example missing from it keeps its components.json
/// <c>height</c> until measured; site.js corrects either once the frame has loaded.
/// </summary>
public sealed class FrameHeights
{
    // example id → mode → [(from width, height)], widths ascending, the first from 0.
    private readonly Dictionary<string, Dictionary<string, (int Width, int Height)[]>> _heights;
    private readonly ConcurrentDictionary<string, string?> _css = new(StringComparer.Ordinal);

    public FrameHeights(Dictionary<string, Dictionary<string, int[][]>> file)
    {
        _heights = file.ToDictionary(
            e => e.Key,
            e => e.Value.ToDictionary(m => m.Key, m => m.Value.Select(s => (s[0], s[1])).ToArray()),
            StringComparer.Ordinal);
    }

    public static FrameHeights Load()
    {
        using var stream = typeof(FrameHeights).Assembly.GetManifestResourceStream("frame-heights.json");
        return stream is null
            ? new FrameHeights([])
            : new FrameHeights(JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int[][]>>>(stream)!);
    }

    /// <summary>Whether the example has been measured.</summary>
    public bool Has(string exampleId) => _heights.ContainsKey(exampleId);

    /// <summary>The heights for a mode: its own, else Static's (a mode measured the same is left out of the file).</summary>
    public (int Width, int Height)[]? For(string exampleId, string mode) =>
        _heights.TryGetValue(exampleId, out var modes)
            ? modes.GetValueOrDefault(mode) ?? modes.GetValueOrDefault("static") ?? modes.Values.FirstOrDefault()
            : null;

    /// <summary>
    /// The CSS that sizes an example's frames: per mode, a height for its narrowest frames and one
    /// container query per width the height changes at. Null when the example isn't measured.
    /// </summary>
    public string? Css(string exampleId, IEnumerable<string> modes) =>
        _css.GetOrAdd(exampleId + "|" + string.Join(",", modes), _ =>
        {
            if (!Has(exampleId))
            {
                return null;
            }

            var css = new StringBuilder();
            foreach (var mode in modes)
            {
                if (For(exampleId, mode) is not { Length: > 0 } steps)
                {
                    continue;
                }

                var target = $".ex[data-ex=\"{exampleId}\"] .ex-mode[data-mode=\"{mode}\"] > :is(.ex-frame, .ex-needs-js)";
                foreach (var (width, height) in steps)
                {
                    var rule = $"{target}{{height:{height.ToString(CultureInfo.InvariantCulture)}px}}";
                    css.Append(width <= 0 ? rule : $"@container (width >= {width.ToString(CultureInfo.InvariantCulture)}px){{{rule}}}");
                }
            }

            return css.ToString();
        });
}
