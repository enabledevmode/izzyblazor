namespace IzzyBlazor;

/// <summary>Font size tokens, from <see cref="Xs"/> (12px) to <see cref="Xl6"/> (60px).</summary>
public enum TextSize
{
    /// <summary>12px.</summary>
    Xs,

    /// <summary>14px.</summary>
    Sm,

    /// <summary>16px, the body size.</summary>
    Md,

    /// <summary>18px.</summary>
    Lg,

    /// <summary>20px.</summary>
    Xl,

    /// <summary>24px.</summary>
    Xl2,

    /// <summary>30px.</summary>
    Xl3,

    /// <summary>36px.</summary>
    Xl4,

    /// <summary>48px.</summary>
    Xl5,

    /// <summary>60px.</summary>
    Xl6,
}

/// <summary>Font weights.</summary>
public enum FontWeight
{
    /// <summary>300.</summary>
    Light,

    /// <summary>400.</summary>
    Normal,

    /// <summary>500.</summary>
    Medium,

    /// <summary>600.</summary>
    Semibold,

    /// <summary>700.</summary>
    Bold,

    /// <summary>800.</summary>
    Extrabold,
}

/// <summary>Font family tokens.</summary>
public enum FontFamily
{
    /// <summary>The theme's sans-serif stack (the default).</summary>
    Sans,

    /// <summary>The theme's serif stack.</summary>
    Serif,

    /// <summary>The theme's monospace stack, for code and figures.</summary>
    Mono,
}

/// <summary>Line height tokens.</summary>
public enum LineHeight
{
    /// <summary>1.</summary>
    None,

    /// <summary>1.25, for headings.</summary>
    Tight,

    /// <summary>1.375.</summary>
    Snug,

    /// <summary>1.5, for body text.</summary>
    Normal,

    /// <summary>1.625.</summary>
    Relaxed,

    /// <summary>2.</summary>
    Loose,
}

/// <summary>Letter spacing tokens.</summary>
public enum LetterSpacing
{
    /// <summary>-0.05em.</summary>
    Tighter,

    /// <summary>-0.025em, for large headings.</summary>
    Tight,

    /// <summary>0.</summary>
    Normal,

    /// <summary>0.025em.</summary>
    Wide,

    /// <summary>0.05em, for small uppercase labels.</summary>
    Wider,

    /// <summary>0.1em.</summary>
    Widest,
}

/// <summary>Horizontal text alignment. Start and End follow the reading direction.</summary>
public enum TextAlign
{
    /// <summary>Left in left-to-right languages.</summary>
    Start,

    /// <summary>Centered.</summary>
    Center,

    /// <summary>Right in left-to-right languages.</summary>
    End,

    /// <summary>Justified to both edges.</summary>
    Justify,
}

/// <summary>Letter case changes.</summary>
public enum TextTransform
{
    /// <summary>As written.</summary>
    None,

    /// <summary>ALL CAPS.</summary>
    Uppercase,

    /// <summary>all lowercase.</summary>
    Lowercase,

    /// <summary>Each Word Capitalized.</summary>
    Capitalize,
}

/// <summary>Text decoration lines.</summary>
public enum TextDecoration
{
    /// <summary>No line.</summary>
    None,

    /// <summary>Underlined.</summary>
    Underline,

    /// <summary>Struck through.</summary>
    LineThrough,
}

/// <summary>How lines break.</summary>
public enum TextWrap
{
    /// <summary>Normal wrapping.</summary>
    Wrap,

    /// <summary>Never wrap.</summary>
    NoWrap,

    /// <summary>Even line lengths; best for headings and short text.</summary>
    Balance,

    /// <summary>Avoids a lone word on the last line; good for paragraphs.</summary>
    Pretty,
}
