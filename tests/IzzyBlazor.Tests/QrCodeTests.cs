using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Tests;

// The expected modules come from another encoder (the qrcode npm package, 1.5.4) with the same
// version, level and mask; the encoder matched it on every mask for versions 1 to 25 when written.
public class QrCodeTests : BunitContext
{
    public QrCodeTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    private static string[] Rows(QrMatrix q) =>
        [.. Enumerable.Range(0, q.Size).Select(y => string.Concat(Enumerable.Range(0, q.Size).Select(x => q[x, y] ? '#' : '.')))];

    [Fact]
    public void Alphanumeric_text_matches_a_known_code()
    {
        var q = QrEncoder.Encode("HELLO WORLD", QrErrorCorrection.Quartile);

        Assert.Equal((1, 0), (q.Version, q.Mask));
        Assert.Equal(
        [
            "#######.##....#######",
            "#.....#.#..#..#.....#",
            "#.###.#.#..##.#.###.#",
            "#.###.#.#.....#.###.#",
            "#.###.#.#.#...#.###.#",
            "#.....#...#...#.....#",
            "#######.#.#.#.#######",
            "........#............",
            ".##.#.##....#.#.#####",
            ".#......####....#...#",
            "..##.###.##...#.##...",
            ".##.##.#..##.#.#.###.",
            "#...#.#.#.###.###.#.#",
            "........##.#..#...#.#",
            "#######.#.#....#.##..",
            "#.....#..#.##.##.#...",
            "#.###.#.#.#...#######",
            "#.###.#..#.#.#.#...#.",
            "#.###.#.#..#.###.#..#",
            "#.....#.#.####...#.##",
            "#######....#.###....#",
        ], Rows(q));
    }

    [Fact]
    public void A_url_takes_the_higher_level_that_fits_and_matches_a_known_code()
    {
        var q = QrEncoder.Encode("https://izzyblazor.dev/docs", QrErrorCorrection.Medium, mask: 6);

        Assert.Equal((3, QrErrorCorrection.Quartile), (q.Version, q.Level));
        Assert.Equal(
        [
            "#######..#.######.###.#######",
            "#.....#.#######.####..#.....#",
            "#.###.#....####.#.#...#.###.#",
            "#.###.#.#####.#..#....#.###.#",
            "#.###.#.###.#.#.#..#..#.###.#",
            "#.....#..#.#.#.##.....#.....#",
            "#######.#.#.#.#.#.#.#.#######",
            "........##...###.##.#........",
            ".#.####.##.####.....###.##.#.",
            "#.#.#...###.##...###....#.##.",
            ".##.#.#.#.##..#.###..###.....",
            "###.##.#.#.####..#.#....#...#",
            "....#.#.#..###.##..#.##..#.#.",
            ".####...#.####.#..#...#.#.###",
            "###...#..########.###..#.#..#",
            "..##...##.#..##.....##..####.",
            "..#.#.##.##....#.####....#..#",
            "#...#..##....##..#.##...###..",
            "##....#.##..#...#.##....##..#",
            "###.#.......##.##.#..####.###",
            "#######.##.####..#..########.",
            "........#..##..#.##.#...###..",
            "#######..#.##..##..##.#.#.#..",
            "#.....#.##.##.#.#.###...##...",
            "#.###.#.####.###.#.######..#.",
            "#.###.#.#.###.#..##....#..##.",
            "#.###.#..#.#.#.#....##.###.##",
            "#.....#.#.#.#...#.#..##.###.#",
            "#######....##.##...###.##....",
        ], Rows(q));
    }

    [Fact]
    public void Renders_an_svg_image_named_by_its_label_with_a_quiet_zone()
    {
        var svg = Render<QrCode>(p => p.Add(c => c.Value, "HELLO WORLD").Add(c => c.Label, "QR code for the demo")).Find("svg");

        Assert.Equal("img", svg.GetAttribute("role"));
        Assert.Equal("QR code for the demo", svg.GetAttribute("aria-label"));
        Assert.Equal("0 0 29 29", svg.GetAttribute("viewBox"));
        // The top-left finder pattern's first row: seven dark modules, after the 4-module margin.
        Assert.StartsWith("M4 4h7v1h-7z", svg.QuerySelector("path")!.GetAttribute("d"));
        Assert.DoesNotContain("HELLO", svg.OuterHtml);
    }

    [Fact]
    public void Needs_a_label_unless_decorative()
    {
        Assert.Throws<InvalidOperationException>(() => Render<QrCode>(p => p.Add(c => c.Value, "x")));
        Assert.Equal("true", Render<QrCode>(p => p.Add(c => c.Value, "x").Add(c => c.Decorative, true)).Find("svg").GetAttribute("aria-hidden"));
    }
}
