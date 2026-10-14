namespace IzzyBlazor;

/// <summary>What a piece of highlighted code is, which decides its color.</summary>
public enum CodeTokenKind
{
    /// <summary>Plain text: identifiers, operators, whitespace.</summary>
    Text,

    /// <summary>A comment.</summary>
    Comment,

    /// <summary>A language keyword, such as <c>if</c> or <c>public</c>.</summary>
    Keyword,

    /// <summary>A string or character literal.</summary>
    String,

    /// <summary>A number, or a constant such as a CSS color.</summary>
    Number,

    /// <summary>A type name.</summary>
    Type,

    /// <summary>A function or method name, or a shell command.</summary>
    Function,

    /// <summary>A markup tag or component name, or a CSS selector.</summary>
    Tag,

    /// <summary>An attribute or parameter name, a CSS property, a JSON key or a command-line flag.</summary>
    Attribute,

    /// <summary>Directives and transitions: Razor's <c>@</c>, C# preprocessor lines, CSS at-rules, shell variables.</summary>
    Meta,
}

/// <summary>One run of highlighted code.</summary>
/// <param name="Kind">What it is.</param>
/// <param name="Text">The code, exactly as written.</param>
public readonly record struct CodeToken(CodeTokenKind Kind, string Text);

/// <summary>
/// Splits code into <see cref="CodeToken"/>s for <see cref="Code"/>. Register your own as a service
/// to replace the built-in <see cref="IzzyCodeHighlighter"/>, for instance to add languages.
/// </summary>
public interface ICodeHighlighter
{
    /// <summary>
    /// Splits <paramref name="code"/> into tokens. Returns false when <paramref name="language"/>
    /// isn't supported; the code is then shown as plain text.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="language">The language name as given to <see cref="Code"/>, such as "razor".</param>
    /// <param name="tokens">The tokens, which together spell out the code exactly.</param>
    bool TryHighlight(string code, string language, out IReadOnlyList<CodeToken> tokens);
}
