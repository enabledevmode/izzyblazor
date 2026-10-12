namespace IzzyBlazor.TestHost.Components.Demos;

// The custom-color values and code sample, kept in plain C# beside the markup.
public partial class ButtonsDemo
{
    private static readonly Color Teal = Color.Of("#0f766e");
    private static readonly Color Amber = Color.Of("#fbbf24");

    // Single-quoted attributes, so the color string can use double quotes.
    private const string CustomCode =
        """
        <Button Color='Color.Of("#0f766e")'>Approve</Button>
        <Button Color='Color.Token("brand-teal")' Variant="ButtonVariant.Soft">Publish</Button>
        """;
}
