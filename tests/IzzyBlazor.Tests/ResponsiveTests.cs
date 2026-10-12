namespace IzzyBlazor.Tests;

public class ResponsiveTests
{
    [Fact]
    public void Default_is_empty()
    {
        Responsive<int> value = default;

        Assert.True(value.IsEmpty);
        Assert.Empty(value.Entries);
    }

    [Fact]
    public void Single_value_converts_to_a_base_value()
    {
        Responsive<int> value = 4;

        var entry = Assert.Single(value.Entries);
        Assert.Equal((null, 4), entry);
    }

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

    [Fact]
    public void Breakpoint_without_base()
    {
        Responsive<Direction> value = new() { Md = Direction.Row };

        Assert.Equal((Breakpoint.Md, Direction.Row), Assert.Single(value.Entries));
    }

    [Fact]
    public void ToString_reads_like_the_map()
    {
        Responsive<int> value = new() { Base = 2, Lg = 6 };

        Assert.Equal("2 lg:6", value.ToString());
    }
}
