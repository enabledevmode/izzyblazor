using AngleSharp.Dom;

namespace IzzyBlazor.Tests;

public class DiffTests : BunitContext
{
    private static string Text(params string[] lines) => string.Join("\n", lines) + "\n";

    // What a reader sees of each line: its sign, numbers and text, as "-3 |text" ("-" removed, "+" added, " " same).
    private static List<string> Shown(IRenderedComponent<Diff> cut, string side = "") =>
        cut.FindAll($".ib-diff-block{side} .ib-diff-new .ib-diff-line, .ib-diff-change .ib-diff-old .ib-diff-line")
            .Select(Describe).ToList();

    private static string Describe(IElement line)
    {
        var sign = line.ClassList.Contains("ib-diff-removed") ? "-" : line.ClassList.Contains("ib-diff-added") ? "+" : " ";
        var num = line.QuerySelector(".ib-diff-num");
        return $"{sign}{num?.GetAttribute("data-old")}/{num?.GetAttribute("data-new")} {line.QuerySelector(".ib-diff-text")!.TextContent}";
    }

    [Fact]
    public void Compare_gives_every_line_in_order_with_its_numbers()
    {
        var lines = TextDiff.Compare("a\r\nb\rc\nd", "a\nB\nc\nd\ne\n")!;

        Assert.Equal(
            [
                new DiffLine(DiffKind.Same, 1, 1, "a"),
                new DiffLine(DiffKind.Removed, 2, null, "b"),
                new DiffLine(DiffKind.Added, null, 2, "B"),
                new DiffLine(DiffKind.Same, 3, 3, "c"),
                new DiffLine(DiffKind.Same, 4, 4, "d"),
                new DiffLine(DiffKind.Added, null, 5, "e"),
            ],
            lines);
        Assert.Empty(TextDiff.Compare("", "")!);
        Assert.Equal([new DiffLine(DiffKind.Added, null, 1, "")], TextDiff.Compare("", "\n"));
        Assert.Equal(["x", ""], TextDiff.Lines("x\n\n"));
    }

    [Fact]
    public void Compare_finds_the_fewest_changes_removed_before_added()
    {
        var a = string.Join('\n', "abcabba".ToCharArray());
        var b = string.Join('\n', "cbabac".ToCharArray());
        var lines = TextDiff.Compare(a, b)!;

        // Myers's example: 5 edits, the longest common subsequence of 4.
        Assert.Equal(5, lines.Count(line => line.Kind != DiffKind.Same));
        Assert.Equal(b.Replace("\n", ""), string.Concat(lines.Where(l => l.Kind != DiffKind.Removed).Select(l => l.Text)));
        Assert.Equal(a.Replace("\n", ""), string.Concat(lines.Where(l => l.Kind != DiffKind.Added).Select(l => l.Text)));
        for (var i = 1; i < lines.Count; i++)
        {
            Assert.False(lines[i - 1].Kind == DiffKind.Added && lines[i].Kind == DiffKind.Removed, "an added line came before a removed one");
        }
    }

    [Fact]
    public void Folds_unchanged_runs_away_from_the_changes_but_never_one_line()
    {
        var old = Text(Enumerable.Range(1, 20).Select(i => $"line {i}").ToArray());
        var @new = old.Replace("line 10\n", "line ten\n");
        var cut = Render<Diff>(p => p.Add(d => d.OldText, old).Add(d => d.NewText, @new));

        var folds = cut.FindAll("details.ib-diff-fold");
        Assert.Equal(2, folds.Count);
        Assert.Equal("6 unchanged lines", folds[0].QuerySelector("summary")!.TextContent);
        Assert.Equal("7 unchanged lines", folds[1].QuerySelector("summary")!.TextContent);
        Assert.Equal(
            [" 7/7 line 7", " 8/8 line 8", " 9/9 line 9", "-10/ line 10", "+/10 line ten", " 11/11 line 11", " 12/12 line 12", " 13/13 line 13"],
            cut.FindAll(".ib-diff-lines > .ib-diff-block .ib-diff-line").Select(Describe));

        // One line short of a fold at the start (line 1 is 4 from the change): shown, not folded.
        var near = Render<Diff>(p => p.Add(d => d.OldText, Text("1", "2", "3", "4", "5")).Add(d => d.NewText, Text("1", "2", "3", "4", "five")));
        Assert.Empty(near.FindAll("details"));

        var all = Render<Diff>(p => p.Add(d => d.OldText, old).Add(d => d.NewText, @new).Add(d => d.Fold, false));
        Assert.Empty(all.FindAll("details"));
        Assert.Equal(21, all.FindAll(".ib-diff-line").Count);
    }

    [Fact]
    public void Screen_readers_hear_what_each_changed_line_is_and_the_counts()
    {
        var cut = Render<Diff>(p => p.Add(d => d.OldText, Text("a", "b")).Add(d => d.NewText, Text("a", "c", "d")));

        Assert.Equal(["Removed: ", "Added: ", "Added: "], cut.FindAll(".ib-diff-sr").Select(e => e.TextContent));
        var body = cut.Find(".ib-diff-body");
        Assert.Equal("group", body.GetAttribute("role"));
        Assert.Equal("0", body.GetAttribute("tabindex"));
        Assert.Equal("Changes", body.GetAttribute("aria-label"));
        Assert.Equal("2 added, 1 removed", cut.Find($"#{body.GetAttribute("aria-describedby")}").TextContent);
        Assert.All(cut.FindAll(".ib-diff-num, .ib-diff-sign"), e => Assert.Equal("true", e.GetAttribute("aria-hidden")));
        Assert.Equal("del", cut.Find(".ib-diff-removed").LocalName);
        Assert.Equal("ins", cut.Find(".ib-diff-added").LocalName);

        var titled = Render<Diff>(p => p.Add(d => d.OldText, "a").Add(d => d.NewText, "b").Add(d => d.Title, "rules.xml"));
        var named = titled.Find(".ib-diff-body");
        Assert.Null(named.GetAttribute("aria-label"));
        Assert.Equal("rules.xml", titled.Find($"#{named.GetAttribute("aria-labelledby")}").TextContent);
    }
}
