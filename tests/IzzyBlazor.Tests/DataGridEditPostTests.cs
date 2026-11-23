namespace IzzyBlazor.Tests;

public class DataGridEditPostTests
{
    private sealed class Person
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Phone { get; set; }
        public Guid Key { get; } = Guid.NewGuid();
        public List<string> Tags { get; } = [];
    }

    [Fact]
    public void An_optional_text_field_posted_empty_saves_as_no_value()
    {
        var editable = new[] { typeof(Person).GetProperty(nameof(Person.Name))!, typeof(Person).GetProperty(nameof(Person.Phone))! };

        var loaded = new Person { Id = 7, Name = "Nora", Phone = "555 0100", Tags = { "vip" } };

        var row = DataGridEditCopy.Restore(new Person { Name = "", Phone = "" }, loaded, editable);

        // What isn't editable comes from the row; a required text stays as posted, for its validator to refuse.
        Assert.Equal((7, "", null), (row.Id, row.Name, row.Phone));
        // A property with no setter too: the posted object made a key and a list of its own.
        Assert.Equal((loaded.Key, "vip"), (row.Key, Assert.Single(row.Tags)));
    }
}
