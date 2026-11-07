using System.Text.Json;

namespace IzzyBlazor.Tests;

// A parser is judged by its spec, so every example runs (CONTRIBUTING's threshold allows it here,
// as for the sanitizer's attack cases): CommonMark 0.31.2's and GitHub-flavored Markdown's
// extensions (tables, task lists, strikethrough, extended autolinks).
public class RichTextMarkdownTests
{
    private sealed record Example(string Markdown, string Html, int Number, string Section);

    // CommonMark examples that GitHub-flavored Markdown renders differently, or that the parser
    // leaves out on purpose:
    // 25: HTML5's named entities (&HilbertSpace;): .NET decodes HTML 4's set, so these stay text.
    // 606, 608, 611, 612: bare addresses, which GFM's extended autolinks link, as GitHub does.
    private static readonly HashSet<int> Skipped = [25, 606, 608, 611, 612];

    private static IEnumerable<Example> Load(string file) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Markdown", file))).RootElement.EnumerateArray()
            .Select(e => new Example(e.GetProperty("markdown").GetString()!, e.GetProperty("html").GetString()!, e.GetProperty("example").GetInt32(), e.GetProperty("section").GetString()!));

    [Fact]
    public void Renders_every_CommonMark_and_GitHub_spec_example()
    {
        var failures = new List<string>();
        foreach (var (file, prefix) in new[] { ("commonmark-spec.json", "CommonMark"), ("gfm-extensions.json", "GFM") })
        {
            foreach (var example in Load(file))
            {
                if (prefix == "CommonMark" && Skipped.Contains(example.Number)) continue;
                string actual;
                try
                {
                    actual = RichTextMarkdown.Render(example.Markdown);
                }
                catch (Exception e)
                {
                    actual = e.ToString();
                }

                if (actual != example.Html)
                {
                    failures.Add($"{prefix} {example.Number} ({example.Section})\n--- markdown\n{example.Markdown}--- expected\n{example.Html}--- actual\n{actual}");
                }
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} failed:\n\n" + string.Join("\n", failures.Take(40)));
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("[x](javascript:alert(1))")]
    [InlineData("[x](<javascript:alert(1)>)")]
    [InlineData("""
        [x][a]

        [a]: javascript:alert(1)
        """)]
    [InlineData("![x](javascript:alert(1))")]
    [InlineData("<javascript:alert(1)>")]
    [InlineData("""Text <img src=x onerror=alert(1)> and <a href="javascript:alert(1)">a link</a>""")]
    [InlineData("""
        <div onclick="alert(1)">

        *hi*

        </div>
        """)]
    [InlineData("""<iframe src="https://example.com"></iframe>""")]
    [InlineData("[x](JAVASCRIPT:alert(1)) [y](data:text/html,<script>alert(1)</script>)")]
    public void Drops_scripts_handlers_and_script_links(string markdown)
    {
        var html = RichTextMarkdown.ToHtml(markdown);

        // Text may still say "javascript:"; no tag or attribute may carry it.
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<iframe", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror=", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick=", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"javascript", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("src=\"javascript", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"data:", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Writes_what_the_editor_and_RichText_know()
    {
        var html = RichTextMarkdown.ToHtml(
            """
            # Plan

            | Task | Hours |
            | --- | ---: |
            | Fix | 6 |

            - [x] Draft
            - [ ] ~~Review~~

            ```csharp
            var x = 1;
            ```

            See www.example.com.
            """,
            topHeading: 2);

        Assert.Contains("<h2>Plan</h2>", html);
        Assert.Contains("""<th class="ib-align-end">Hours</th>""", html);
        Assert.Contains("""<td class="ib-align-end">6</td>""", html);
        Assert.Contains("""<li><input type="checkbox" disabled checked> Draft</li>""", html);
        Assert.Contains("""<input type="checkbox" disabled> <s>Review</s>""", html);
        Assert.Contains("""<pre><code class="language-csharp">var x = 1;""", html);
        Assert.Contains("""<a href="http://www.example.com">www.example.com</a>.""", html);
    }
}
