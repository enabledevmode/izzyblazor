using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>How an <see cref="Image"/> fills a box with a different shape (<see cref="Image.Ratio"/>).</summary>
public enum ImageFit
{
    /// <summary>Fills the box, cropping the edges (the default).</summary>
    Cover,

    /// <summary>Shows the whole image, leaving bands.</summary>
    Contain,
}

/// <summary>
/// An image that loads lazily and keeps its space while loading: <c>width</c> and <c>height</c> or a
/// <see cref="Ratio"/> reserve the box, so the page doesn't jump. <see cref="Caption"/> wraps it in a
/// <c>figure</c>.
/// </summary>
/// <example>
/// <code>&lt;Image Src="/shots/console.png" Alt="The rules list" Ratio="16 / 9.0" Radius="Radius.Lg" /&gt;</code>
/// </example>
[ModeSupport]
public class Image : StyledComponentBase
{
    /// <summary>The image URL.</summary>
    [Parameter, EditorRequired] public string Src { get; set; } = "";

    /// <summary>
    /// What the image shows, for screen readers and when it fails to load. Use "" for a purely
    /// decorative image.
    /// </summary>
    [Parameter, EditorRequired] public string Alt { get; set; } = "";

    /// <summary>The image's own width in pixels, so the browser reserves its space before it loads.</summary>
    [Parameter] public int? IntrinsicWidth { get; set; }

    /// <summary>The image's own height in pixels. See <see cref="IntrinsicWidth"/>.</summary>
    [Parameter] public int? IntrinsicHeight { get; set; }

    /// <summary>A shape to show it in, width over height, such as <c>16 / 9.0</c> or <c>1</c>; the image fills it per <see cref="Fit"/>.</summary>
    [Parameter] public double? Ratio { get; set; }

    /// <summary>How it fills a <see cref="Ratio"/> of a different shape. Defaults to <see cref="ImageFit.Cover"/>.</summary>
    [Parameter] public ImageFit Fit { get; set; } = ImageFit.Cover;

    /// <summary>Loads only when the image nears the screen. Defaults to true; set false for the first image on a page.</summary>
    [Parameter] public bool Lazy { get; set; } = true;

    /// <summary>A caption under the image; wraps it in a <c>figure</c>.</summary>
    [Parameter] public string? Caption { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-image");
        base.BuildAttributes(classes, styles);
        if (Ratio is { } ratio && ratio > 0)
        {
            classes.Add("ib-image-ratio");
            styles.AddVar("ratio", ratio.ToString("0.####", CultureInfo.InvariantCulture));
            classes.Add("ib-image-contain", Fit == ImageFit.Contain);
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        if (Caption is not null)
        {
            builder.OpenElement(0, "figure");
            builder.AddAttribute(1, "class", "ib-figure");
        }

        builder.OpenElement(2, "img");
        builder.AddMultipleAttributes(3, AdditionalAttributes);
        builder.AddAttribute(4, "src", Src);
        builder.AddAttribute(5, "alt", Alt);
        if (IntrinsicWidth is { } w)
        {
            builder.AddAttribute(6, "width", w);
        }

        if (IntrinsicHeight is { } h)
        {
            builder.AddAttribute(7, "height", h);
        }

        if (Lazy)
        {
            builder.AddAttribute(8, "loading", "lazy");
        }

        builder.AddAttribute(9, "decoding", "async");
        builder.AddAttribute(10, "class", cssClass);
        builder.AddAttribute(11, "style", style);
        builder.CloseElement();

        if (Caption is not null)
        {
            builder.OpenElement(12, "figcaption");
            builder.AddAttribute(13, "class", "ib-figure-caption");
            builder.AddContent(14, Caption);
            builder.CloseElement();
            builder.CloseElement();
        }
    }
}

/// <summary>
/// A YouTube video that loads only when played: a poster image with a play button, which is a link
/// to the video on YouTube. With script, clicking swaps in the player (from youtube-nocookie.com)
/// in place, so pages don't load YouTube's player, and its cookies, until someone wants it.
/// </summary>
/// <example><code>&lt;VideoEmbed YouTubeId="dQw4w9WgXcQ" Label="Building a form in IzzyBlazor" /&gt;</code></example>
[ModeSupport(Off = ModeLevel.Partial, Notes = "With JavaScript off, the play button opens the video on YouTube instead of playing it in place.")]
public partial class VideoEmbed : StyledComponentBase
{
    /// <summary>
    /// The video's id, the 11 characters after <c>v=</c> in its URL. Unset for a video that isn't
    /// out yet: the box keeps its place with <see cref="Poster"/> (if set) and
    /// <see cref="EmptyContent"/> or <see cref="EmptyText"/>, and nothing to play.
    /// </summary>
    [Parameter] public string? YouTubeId { get; set; }

    /// <summary>The video's name, usually its title: the player's accessible name and the play button's.</summary>
    [Parameter, EditorRequired] public string Label { get; set; } = "";

    /// <summary>The poster image. Defaults to YouTube's own thumbnail; set your own to load nothing from YouTube before play.</summary>
    [Parameter] public string? Poster { get; set; }

    /// <summary>Width over height. Defaults to 16:9.</summary>
    [Parameter] public double Ratio { get; set; } = 16 / 9.0;

    /// <summary>The play button's name, with {0} for the title. Defaults to "Play video: {0}".</summary>
    [Parameter] public string? PlayLabel { get; set; }

    /// <summary>Shown over the poster while there's no <see cref="YouTubeId"/>, in place of <see cref="EmptyText"/>.</summary>
    [Parameter] public RenderFragment? EmptyContent { get; set; }

    /// <summary>Shown while there's no <see cref="YouTubeId"/>. Defaults to "Coming soon".</summary>
    [Parameter] public string? EmptyText { get; set; }

    private bool Upcoming => string.IsNullOrWhiteSpace(YouTubeId);

    [Inject] private IServiceProvider Services { get; set; } = default!;

    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex IdPattern();

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!Upcoming && !IdPattern().IsMatch(YouTubeId!))
        {
            throw new ArgumentException($"\"{YouTubeId}\" isn't a YouTube video id (11 letters, digits, - or _).", nameof(YouTubeId));
        }
    }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-video");
        classes.Add("ib-video-upcoming", Upcoming);
        base.BuildAttributes(classes, styles);
        styles.AddVar("ratio", Ratio.ToString("0.####", CultureInfo.InvariantCulture));
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        if (Upcoming)
        {
            BuildUpcoming(builder, cssClass, style);
            return;
        }

        var label = string.Format(CultureInfo.CurrentCulture, PlayLabel ?? IzzyText.From(Services).PlayVideo, Label);
        builder.OpenElement(0, "ib-video");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "class", cssClass);
        builder.AddAttribute(3, "style", style);
        builder.AddAttribute(4, "data-title", Label);

        builder.OpenElement(5, "a");
        builder.AddAttribute(6, "class", "ib-video-poster");
        builder.AddAttribute(7, "href", $"https://www.youtube.com/watch?v={YouTubeId}");
        builder.AddAttribute(8, "data-ib-video-src", $"https://www.youtube-nocookie.com/embed/{YouTubeId}?autoplay=1");
        builder.AddAttribute(9, "aria-label", label);
        builder.OpenElement(10, "img");
        builder.AddAttribute(11, "src", Poster ?? $"https://i.ytimg.com/vi/{YouTubeId}/hqdefault.jpg");
        builder.AddAttribute(12, "alt", "");
        builder.AddAttribute(13, "loading", "lazy");
        builder.AddAttribute(14, "decoding", "async");
        builder.CloseElement();
        builder.OpenElement(15, "span");
        builder.AddAttribute(16, "class", "ib-video-play");
        builder.AddAttribute(17, "aria-hidden", "true");
        builder.OpenComponent<Icon>(18);
        builder.AddComponentParameter(19, nameof(Icon.Name), IconSet.Current.Get(IconRole.Play));
        builder.CloseComponent();
        builder.CloseElement();
        builder.CloseElement();

        builder.OpenElement(20, "div");
        builder.AddAttribute(21, "class", "ib-video-frame");
        builder.CloseElement();
        builder.CloseElement();
    }

    // No video yet: the same box, a group named by the title, the poster if there is one, and a
    // note in the middle. Nothing loads from YouTube.
    private void BuildUpcoming(RenderTreeBuilder builder, string? cssClass, string? style)
    {
        builder.OpenElement(30, "div");
        builder.AddMultipleAttributes(31, AdditionalAttributes);
        builder.AddAttribute(32, "class", cssClass);
        builder.AddAttribute(33, "style", style);
        builder.AddAttribute(34, "role", "group");
        builder.AddAttribute(35, "aria-label", Label);
        if (Poster is not null)
        {
            builder.OpenElement(36, "img");
            builder.AddAttribute(37, "src", Poster);
            builder.AddAttribute(38, "alt", "");
            builder.AddAttribute(39, "loading", "lazy");
            builder.AddAttribute(40, "decoding", "async");
            builder.CloseElement();
        }

        builder.OpenElement(41, "div");
        builder.AddAttribute(42, "class", "ib-video-empty");
        if (EmptyContent is not null)
        {
            builder.AddContent(43, EmptyContent);
        }
        else
        {
            builder.OpenComponent<Icon>(44);
            builder.AddComponentParameter(45, nameof(Icon.Role), (IconRole?)IconRole.Clock);
            builder.CloseComponent();
            builder.OpenElement(46, "span");
            builder.AddContent(47, EmptyText ?? IzzyText.From(Services).VideoComingSoon);
            builder.CloseElement();
        }

        builder.CloseElement();
        builder.CloseElement();
    }
}
