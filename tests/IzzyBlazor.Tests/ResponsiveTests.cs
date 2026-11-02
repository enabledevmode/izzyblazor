namespace IzzyBlazor.Tests;

public class ResponsiveTests
{
    [Fact]
    public void Zero_is_a_real_value_not_empty()
    {
        Responsive<int> value = 0;

        Assert.False(value.IsEmpty);
    }

    [Fact]
    public void Map_entries_come_base_first_then_smallest_breakpoint_first()
    {
        Responsive<int> value = new() { Xl = 16, Base = 4, Md = 10 };

        Assert.Equal(
            new (Breakpoint?, int)[] { (null, 4), (Breakpoint.Md, 10), (Breakpoint.Xl, 16) },
            value.Entries);
    }
}
