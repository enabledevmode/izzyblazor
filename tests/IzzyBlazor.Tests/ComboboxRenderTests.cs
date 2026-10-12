using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace IzzyBlazor.Tests;

public class ComboboxRenderTests : BunitContext
{
    public class Model
    {
        [Required]
        public string? Country { get; set; } = "CA";

        public int? OwnerId { get; set; }
    }

    private static readonly Dictionary<string, string> Countries = new() { ["CA"] = "Canada", ["FR"] = "France", ["JP"] = "Japan" };
    private static readonly Dictionary<int, string> People = new() { [1] = "Ada Lovelace", [2] = "Grace Hopper", [3] = "Alan Turing" };

    private readonly Model _model = new();
    private readonly EditContext _context;
    private readonly List<string> _searches = [];

    public ComboboxRenderTests()
    {
        _context = new EditContext(_model);
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
    }

    private Task<IEnumerable<int?>> FindPeople(string text, CancellationToken token)
    {
        _searches.Add(text);
        return Task.FromResult(People.Where(p => p.Value.Contains(text, StringComparison.OrdinalIgnoreCase)).Select(p => (int?)p.Key));
    }

    private IRenderedComponent<Combobox<string?>> Countries_() => Render<Combobox<string?>>(p => p
        .AddCascadingValue(_context)
        .Add(c => c.Value, _model.Country)
        .Add(c => c.ValueExpression, () => _model.Country)
        .Add(c => c.Items, Countries.Keys)
        .Add(c => c.ItemText, code => Countries[code!])
        .Add(c => c.Placeholder, "Search countries"));

    private IRenderedComponent<Combobox<int?>> People_() => Render<Combobox<int?>>(p => p
        .AddCascadingValue(_context)
        .Add(c => c.Value, _model.OwnerId)
        .Add(c => c.ValueExpression, () => _model.OwnerId)
        .Add(c => c.Search, FindPeople)
        .Add(c => c.Debounce, 0)
        .Add(c => c.ItemText, id => People[id!.Value]));

    [Fact]
    public void Renders_a_posting_select_and_a_combobox_input()
    {
        var cut = Countries_();
        var select = cut.Find("select");
        var input = cut.Find("input");
        var list = cut.Find("[role=listbox]");
        var label = cut.Find("label");

        // The select posts the value and works without script.
        Assert.False(string.IsNullOrEmpty(select.GetAttribute("name")));
        Assert.True(select.HasAttribute("required"));
        Assert.Equal(new[] { "", "CA", "FR", "JP" }, select.QuerySelectorAll("option").Select(o => o.GetAttribute("value")));

        // The input isn't posted, and is wired to the list and the label.
        Assert.Null(input.GetAttribute("name"));
        Assert.Equal("combobox", input.GetAttribute("role"));
        Assert.Equal("false", input.GetAttribute("aria-expanded"));
        Assert.Equal(list.Id, input.GetAttribute("aria-controls"));
        Assert.Equal(label.Id, input.GetAttribute("aria-labelledby"));
        Assert.Equal(select.Id, label.GetAttribute("for"));
        Assert.Equal("manual", list.GetAttribute("popover"));
    }

    [Fact]
    public void Shows_the_chosen_text_and_marks_its_option()
    {
        var cut = Countries_();

        Assert.Equal("Canada", cut.Find("input").GetAttribute("value"));
        var options = cut.FindAll("[role=option]");
        Assert.Equal(new[] { "true", "false", "false" }, options.Select(o => o.GetAttribute("aria-selected")));
        Assert.Equal("Canada", options[0].GetAttribute("data-text"));
        Assert.Equal("CA", options[0].GetAttribute("data-value"));
    }

    [Fact]
    public void Search_fills_the_first_list_and_filters_in_the_browser_without_a_circuit()
    {
        var cut = People_();

        Assert.Equal(new[] { "" }, _searches);
        Assert.Equal(3, cut.FindAll("[role=option]").Count);
        Assert.Null(cut.Find("ib-combobox").GetAttribute("data-server"));
    }

    [Fact]
    public void Search_runs_on_the_server_on_interactive_pages()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var cut = People_();

        Assert.NotNull(cut.Find("ib-combobox").GetAttribute("data-server"));
        cut.Find("input").Input("grace");

        cut.WaitForAssertion(() => Assert.Equal(new[] { "Grace Hopper" }, cut.FindAll("[role=option]").Select(o => o.GetAttribute("data-text"))));
        Assert.Equal(new[] { "", "grace" }, _searches);
    }

    [Fact]
    public void Chosen_value_stays_in_the_select_when_results_leave_it_out()
    {
        _model.OwnerId = 1;
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var cut = People_();

        cut.Find("input").Input("grace");

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[role=option]")));
        Assert.Contains("1", cut.FindAll("select option").Select(o => o.GetAttribute("value")));
        Assert.Equal("Ada Lovelace", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void Choosing_through_the_select_binds_the_value()
    {
        string? bound = null;
        var cut = Render<Combobox<string?>>(p => p
            .AddCascadingValue(_context)
            .Add(c => c.Value, _model.Country)
            .Add(c => c.ValueChanged, (string? v) => bound = v)
            .Add(c => c.ValueExpression, () => _model.Country)
            .Add(c => c.Items, Countries.Keys)
            .Add(c => c.ItemText, code => Countries[code!]));

        // What the browser element does on choosing: set select.value and fire change.
        cut.Find("select").Change("JP");

        Assert.Equal("JP", bound);
    }
}
