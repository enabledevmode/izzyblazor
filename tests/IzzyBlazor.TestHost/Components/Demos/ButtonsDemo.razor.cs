namespace IzzyBlazor.TestHost.Components.Demos;

// The custom-color values and code sample, kept in plain C# beside the markup.
public partial class ButtonsDemo
{
    private static readonly Accent Teal = Accent.Of("#0f766e");
    private static readonly Accent Amber = Accent.Of("#fbbf24");

    // Single-quoted attributes, so the color string can use double quotes.
    private const string CustomCode =
        """
        <Button Accent='Accent.Of("#0f766e")'>Approve</Button>
        <Button Accent='Accent.Token("brand-teal")' Variant="ButtonVariant.Soft">Publish</Button>
        """;
}
