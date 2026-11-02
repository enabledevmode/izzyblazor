using IzzyBlazor.Units;

namespace IzzyBlazor.Tests;

public class StyleValuesTests
{
    [Theory]
    [InlineData("red; background: blue")]
    [InlineData("1px}body{color:red")]
    [InlineData("url(javascript:alert(1))")]
    [InlineData("10px /* comment */")]
    public void Unsafe_raw_values_are_rejected(string css)
    {
        Assert.Throws<FormatException>(() => Length.Of(css));
        Assert.Throws<FormatException>(() => Color.Of(css));
    }
}
