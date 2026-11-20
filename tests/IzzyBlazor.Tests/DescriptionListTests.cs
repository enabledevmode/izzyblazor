namespace IzzyBlazor.Tests;

public class DescriptionListTests : BunitContext
{
    [Fact]
    public void A_single_Columns_number_starts_at_640px_and_a_map_is_taken_as_written()
    {
        // One number: columns from 640px up, one column on phones.
        var single = Render<DescriptionList>(p => p.Add(d => d.Orientation, Orientation.Vertical).Add(d => d.Columns, 3)).Find("dl");
        Assert.Contains("ib-cols-sm", single.ClassList);
        Assert.DoesNotContain("ib-cols", single.ClassList);

        // A map: each width as given, phones included, even with only Base.
        var map = Render<DescriptionList>(p => p.Add(d => d.Orientation, Orientation.Vertical).Add(d => d.Columns, new Responsive<int> { Base = 2, Md = 3 })).Find("dl");
        Assert.Contains("ib-cols", map.ClassList);
        Assert.Contains("ib-cols-md", map.ClassList);
        Assert.DoesNotContain("ib-cols-sm", map.ClassList);
        var baseOnly = Render<DescriptionList>(p => p.Add(d => d.Orientation, Orientation.Vertical).Add(d => d.Columns, new Responsive<int> { Base = 2 })).Find("dl");
        Assert.Contains("ib-cols", baseOnly.ClassList);

        // Terms beside details have no columns to set.
        var horizontal = Render<DescriptionList>(p => p.Add(d => d.Columns, 3)).Find("dl");
        Assert.DoesNotContain(horizontal.ClassList, c => c.StartsWith("ib-cols"));
    }
}
