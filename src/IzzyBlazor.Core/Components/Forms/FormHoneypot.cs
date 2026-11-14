using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A Form's honeypot: a text field moved off the screen, out of the Tab order and away from screen
/// readers and autofill, which only a bot fills. It reports whether the post filled it.
/// </summary>
internal sealed class FormHoneypot : ComponentBase
{
    internal const string FieldName = "ib-hp";

    [SupplyParameterFromForm(Name = FieldName)]
    public string? Value { get; set; }

    [Parameter] public string Label { get; set; } = "";

    [Parameter] public Action<bool>? Report { get; set; }

    protected override void OnParametersSet() => Report?.Invoke(!string.IsNullOrEmpty(Value));

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "ib-honeypot");
        builder.AddAttribute(2, "aria-hidden", "true");
        builder.OpenElement(3, "label");
        builder.AddContent(4, Label);
        builder.OpenElement(5, "input");
        builder.AddAttribute(6, "type", "text");
        builder.AddAttribute(7, "name", FieldName);
        builder.AddAttribute(8, "value", "");
        builder.AddAttribute(9, "tabindex", "-1");
        builder.AddAttribute(10, "autocomplete", "off");
        builder.CloseElement();
        builder.CloseElement();
        builder.CloseElement();
    }
}
