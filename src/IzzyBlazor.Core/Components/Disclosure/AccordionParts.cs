using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// Heading content for an <see cref="AccordionItem"/> in place of its <c>Title</c>, such as a title
/// with a badge. Keep it to inline content: it's drawn in the item's <c>summary</c>. Write it
/// directly in the item.
/// </summary>
/// <example><code>&lt;AccordionHeader&gt;Billing &lt;Badge&gt;2&lt;/Badge&gt;&lt;/AccordionHeader&gt;</code></example>
public sealed class AccordionHeader : ComponentPart
{
    internal override Type HostType => typeof(AccordionItem);
}
