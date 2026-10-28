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
    public void Compare_gives_up_past_its_limit()
    {
        var old = string.Join('\n', Enumerable.Range(0, 30).Select(i => $"a{i}"));
        var @new = string.Join('\n', Enumerable.Range(0, 30).Select(i => $"b{i}"));

        Assert.Null(TextDiff.Compare(old, @new, maxChanges: 59));
        Assert.Equal(60, TextDiff.Compare(old, @new, maxChanges: 60)!.Count);
        Assert.Null(TextDiff.Compare("", old, maxChanges: 10));
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

    [Theory]
    [InlineData("a\nb", 2)] // two unchanged lines between the changes: folded with no context
    [InlineData("a", 0)] // one: shown
    public void Context_zero_folds_every_run_but_a_single_line(string middle, int folded)
    {
        var old = $"x\n{middle}\ny\n";
        var @new = old.Replace("x\n", "X\n").Replace("y\n", "Y\n");
        var cut = Render<Diff>(p => p.Add(d => d.OldText, old).Add(d => d.NewText, @new).Add(d => d.Context, 0));

        Assert.Equal(folded, cut.FindAll("details .ib-diff-line").Count);
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

    [Fact]
    public void Split_draws_unchanged_lines_on_both_sides_and_hides_one_from_screen_readers()
    {
        var unified = Render<Diff>(p => p.Add(d => d.OldText, Text("a", "b")).Add(d => d.NewText, Text("a", "c")));
        var split = Render<Diff>(p => p.Add(d => d.OldText, Text("a", "b")).Add(d => d.NewText, Text("a", "c")).Add(d => d.Layout, DiffLayout.Split));

        Assert.Empty(unified.FindAll(".ib-diff-same > .ib-diff-old"));
        Assert.Equal("true", split.Find(".ib-diff-same > .ib-diff-old").GetAttribute("aria-hidden"));
        Assert.Null(split.Find(".ib-diff-change > .ib-diff-old").GetAttribute("aria-hidden"));
        Assert.Equal(Shown(unified), Shown(split));
    }

    [Fact]
    public void Marks_the_changed_words_of_paired_lines_unless_the_line_was_rewritten()
    {
        var cut = Render<Diff>(p => p
            .Add(d => d.OldText, Text("<c op=\"eq\" value=\"25\"/>", "keep", "the quick brown fox"))
            .Add(d => d.NewText, Text("<c op=\"in\" value=\"20\"/>", "keep", "a lazy dog sleeps"))
            .Add(d => d.Language, "xml"));

        Assert.Equal(["eq", "25"], cut.FindAll(".ib-diff-removed .ib-diff-word").Select(e => e.TextContent));
        Assert.Equal(["in", "20"], cut.FindAll(".ib-diff-added .ib-diff-word").Select(e => e.TextContent));

        // A mark inside a string keeps the string's color: the token is cut, not the mark.
        Assert.All(cut.FindAll(".ib-diff-word"), mark => Assert.NotNull(mark.QuerySelector(".ib-tok-string")));
        Assert.Equal("<c op=\"eq\" value=\"25\"/>", cut.Find(".ib-diff-removed .ib-diff-text").TextContent);

        // The rewritten line shares nothing: no marks on it.
        Assert.Equal(2, cut.FindAll(".ib-diff-removed").Count);
        Assert.Empty(cut.FindAll(".ib-diff-removed")[1].QuerySelectorAll(".ib-diff-word"));

        // An emoji is one character: marked whole, never half a surrogate pair.
        var emoji = Render<Diff>(p => p.Add(d => d.OldText, "status: 😀 done").Add(d => d.NewText, "status: 😁 done"));
        Assert.Equal("😀", emoji.Find(".ib-diff-removed .ib-diff-word").TextContent);
        Assert.Equal("😁", emoji.Find(".ib-diff-added .ib-diff-word").TextContent);

        var plain = Render<Diff>(p => p.Add(d => d.OldText, "a b").Add(d => d.NewText, "a c").Add(d => d.MarkWords, false));
        Assert.Empty(plain.FindAll(".ib-diff-word"));
    }

    [Fact]
    public void Colors_each_text_whole_so_a_removed_comment_across_lines_stays_a_comment()
    {
        var cut = Render<Diff>(p => p
            .Add(d => d.OldText, Text("<!-- one", "two -->", "<a/>"))
            .Add(d => d.NewText, Text("<a/>"))
            .Add(d => d.Language, "xml"));

        Assert.Equal(["<!-- one", "two -->"], cut.FindAll(".ib-diff-removed .ib-tok-comment").Select(e => e.TextContent));
        Assert.Equal("a", cut.Find(".ib-diff-same .ib-tok-tag").TextContent);

        // Lines computed elsewhere are colored the same way.
        var lines = Render<Diff>(p => p.Add(d => d.Lines, TextDiff.Compare("<!-- one\ntwo -->\n<a/>", "<a/>")).Add(d => d.Language, "xml"));
        Assert.Equal(2, lines.FindAll(".ib-diff-removed .ib-tok-comment").Count);
    }

    [Fact]
    public void Notes_for_the_same_text_and_for_too_many_changes()
    {
        var same = Render<Diff>(p => p.Add(d => d.OldText, "a\nb").Add(d => d.NewText, "a\r\nb\n"));
        Assert.Equal("The two texts are the same.", same.Find(".ib-diff-note").TextContent);
        Assert.Empty(same.FindAll(".ib-diff-body"));

        var many = Render<Diff>(p => p.Add(d => d.OldText, "a\nb").Add(d => d.NewText, "c\nd").Add(d => d.MaxChanges, 3));
        Assert.Equal("The texts differ by more than 3 lines, too many to show line by line.", many.Find(".ib-diff-note").TextContent);

        var own = Render<Diff>(p => p.Add(d => d.OldText, "a").Add(d => d.NewText, "a").Add(d => d.SameText, "No changes."));
        Assert.Equal("No changes.", own.Find(".ib-diff-note").TextContent);
    }

    [Fact]
    public void Lines_and_texts_together_throw()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Render<Diff>(p => p.Add(d => d.Lines, []).Add(d => d.OldText, "a")));
        Assert.Contains("Lines", ex.Message);
    }
}
