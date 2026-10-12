namespace IzzyBlazor;

/// <summary>What an <see cref="Accordion"/> shares with its items.</summary>
/// <param name="Name">The shared <c>name</c> attribute when the accordion is exclusive, else null.</param>
/// <param name="Variant">How the items are drawn.</param>
/// <param name="ContentPadding">Content padding for every item, unless an item sets its own.</param>
/// <param name="ContentGap">Space between the content's children for every item, unless an item sets its own.</param>
public sealed record AccordionContext(
    string? Name,
    AccordionVariant Variant,
    Responsive<int> ContentPadding = default,
    Responsive<int> ContentGap = default);
