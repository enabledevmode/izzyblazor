using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

// Fixes from the fourth review (Oct 14): one test or more per finding.
public class FourthReviewTests : BunitContext
{
    [Fact]
    public void Derived_brand_colors_meet_their_ratios_as_written_on_every_built_in_gray()
    {
        string[] lightHigh = ["#dfdfe3", "#dce2ea", "#e0ddd5"];
        string[] darkSurface = ["#0d0d0f", "#0f1218", "#0e0f10"];
        var random = new Random(7);
        for (var i = 0; i < 1500; i++)
        {
            var hex = $"#{random.Next(0x1000000):x6}";
            var p = BrandPalette.From(hex);

            Assert.True(Ratio(p.OnFill, p.Fill) >= 4.5, $"{hex}: on-fill");
            Assert.True(Ratio(p.DarkOnFill, p.DarkFill) >= 4.5, $"{hex}: dark on-fill");
            Assert.True(Ratio(p.Text, p.Soft) >= 4.5, $"{hex}: text on soft");
            foreach (var gray in lightHigh)
            {
                Assert.True(Ratio(p.Text, gray) >= 4.5, $"{hex}: text on {gray}");
            }

            foreach (var surface in darkSurface)
            {
                Assert.True(Ratio(p.DarkFill, surface) >= 3, $"{hex}: dark fill on {surface}");
            }
        }
    }

    private static string Read(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "izzyblazor.sln")))
        {
            dir = dir.Parent;
        }

        return File.ReadAllText(Path.Combine([dir!.FullName, "src", "IzzyBlazor.Core", .. parts])).Replace("\r", "");
    }

    private static double Luminance(string hex)
    {
        var v = Convert.ToInt32(hex[1..], 16);
        static double Lin(int c) => c / 255.0 <= 0.04045 ? c / 255.0 / 12.92 : Math.Pow((c / 255.0 + 0.055) / 1.055, 2.4);
        return 0.2126 * Lin((v >> 16) & 0xFF) + 0.7152 * Lin((v >> 8) & 0xFF) + 0.0722 * Lin(v & 0xFF);
    }

    private static double Ratio(string a, string b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }
}
