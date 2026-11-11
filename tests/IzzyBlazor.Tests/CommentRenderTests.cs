namespace IzzyBlazor.Tests;

public class CommentRenderTests : BunitContext
{
    [Fact]
    public void A_comment_is_an_article_named_by_its_author_with_its_replies_inside()
    {
        var posted = new DateTimeOffset(2026, 11, 4, 14, 20, 0, TimeSpan.Zero);
        var cut = Render<Comment>(p => p
            .Add(c => c.Author, "Marcus Webb")
            .Add(c => c.Initials, "MW")
            .Add(c => c.Time, posted)
            .Add(c => c.Text, "Does the retry back off?")
            .Add(c => c.Replies, b =>
            {
                b.OpenComponent<Comment>(0);
                b.AddAttribute(1, nameof(Comment.Author), "Lena Okafor");
                b.AddAttribute(2, nameof(Comment.Time), DateTimeOffset.UtcNow.AddHours(-3));
                b.AddAttribute(3, nameof(Comment.RelativeTime), true);
                b.AddAttribute(4, nameof(Comment.Text), "It does.");
                b.CloseComponent();
            }));

        var article = cut.Find("article.ib-comment");
        var author = cut.Find("#" + article.GetAttribute("aria-labelledby"));
        Assert.Equal("Marcus Webb", author.TextContent);
        Assert.Equal(posted.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
            article.QuerySelector(":scope > .ib-comment-body > .ib-comment-meta > time")!.GetAttribute("datetime"));

        // The reply is an article of its own, inside the comment it answers, its time relative.
        var reply = article.QuerySelector(".ib-comment-replies > article.ib-comment")!;
        Assert.Contains("It does.", reply.TextContent);
        Assert.Equal("3 hours ago", reply.QuerySelector("time")!.TextContent);
    }
}
