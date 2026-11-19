namespace IzzyBlazor.Tests;

public class PostedTextTests
{
    private sealed class Person
    {
        public string Name { get; set; } = "";
        public string? Phone { get; set; }
        public string? Title { get; set; }
    }

    [Fact]
    public void An_optional_text_field_posted_empty_is_no_value_and_a_required_one_stays_empty()
    {
        var person = new Person { Name = "", Phone = "", Title = "Lead" };

        PostedText.EmptyIsNoValue(person);

        Assert.Equal(("", null, "Lead"), (person.Name, person.Phone, person.Title));
    }
}
