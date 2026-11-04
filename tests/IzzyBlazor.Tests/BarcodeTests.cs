using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Tests;

// The expected modules come from JsBarcode (3.11.6), given the same symbols; the encoder matched it
// on every Code 128, EAN-13, EAN-8 and UPC-A value tried when written.
public class BarcodeTests : BunitContext
{
    public BarcodeTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    private static string Bits(string value, BarcodeType type) =>
        string.Concat(BarcodeEncoder.Encode(value, type).Modules.Select(m => m ? '1' : '0'));

    [Fact]
    public void Code_128_switches_to_packed_digits_and_matches_a_known_code()
    {
        // B for the letters, C for the run of four digits at the end.
        Assert.Equal([104, 51, 40, 41, 48, 13, 18, 16, 18, 22, 13, 99, 4, 17], BarcodeEncoder.Code128Values("SHIP-2026-0417"));
        Assert.Equal(
            "1101001000011011101000110001010001100010001011101110110100110111001100111001010011101100110011100101100111010010011011100101110111101001000110010011100110110111101101100011101011",
            Bits("SHIP-2026-0417", BarcodeType.Code128));
    }

    [Fact]
    public void Ean_13_adds_its_check_digit_and_matches_a_known_code()
    {
        Assert.Equal("10100011010100111010111101111010001001011001101010100001010000101000010111010010000101100110101", Bits("400638133393", BarcodeType.Ean13));
        Assert.Throws<ArgumentException>(() => BarcodeEncoder.Encode("4006381333932", BarcodeType.Ean13));
    }

    [Fact]
    public void Renders_an_image_named_by_its_value_with_the_text_hidden()
    {
        var svg = Render<Barcode>(p => p.Add(b => b.Value, "400638133393").Add(b => b.Type, BarcodeType.Ean13)).Find("svg");

        Assert.Equal("img", svg.GetAttribute("role"));
        Assert.Equal("Barcode 4006381333931", svg.GetAttribute("aria-label"));
        Assert.Equal("0 0 113 62", svg.GetAttribute("viewBox"));
        Assert.Equal("true", svg.QuerySelector(".ib-barcode-text")!.GetAttribute("aria-hidden"));
        Assert.Equal(13, svg.QuerySelectorAll("text").Length);
    }
}
