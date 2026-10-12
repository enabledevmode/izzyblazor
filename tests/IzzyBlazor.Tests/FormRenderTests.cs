using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace IzzyBlazor.Tests;

public class FormRenderTests : BunitContext
{
    public enum Plan
    {
        Free,
        [Display(Name = "Pro plan")] Pro,
    }

    public class Model
    {
        [Required, Display(Name = "Full name"), StringLength(80, MinimumLength = 2)]
        public string? Name { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        [DataType(DataType.Password)]
        public string? Password { get; set; }

        public string? FavoriteColor { get; set; }

        public bool Accept { get; set; }

        public Plan Plan { get; set; } = Plan.Pro;

        public List<Plan> Plans { get; set; } = [Plan.Pro];

        public string[] Tags { get; set; } = ["b"];
    }

    private readonly Model _model = new();
    private readonly EditContext _context;

    public FormRenderTests()
    {
        _context = new EditContext(_model);
        _context.EnableDataAnnotationsValidation(Services);
    }

    private IRenderedComponent<TextField> TextFieldFor(System.Linq.Expressions.Expression<Func<string?>> field, Action<ComponentParameterCollectionBuilder<TextField>>? more = null) =>
        Render<TextField>(p =>
        {
            p.AddCascadingValue(_context);
            p.Add(f => f.ValueExpression, field);
            more?.Invoke(p);
        });

    [Fact]
    public void Label_required_and_lengths_come_from_annotations()
    {
        var cut = TextFieldFor(() => _model.Name);
        var input = cut.Find("input");
        var label = cut.Find("label");

        Assert.Equal("Full name *", label.TextContent);
        Assert.Equal(input.Id, label.GetAttribute("for"));
        Assert.True(input.HasAttribute("required"));
        Assert.Equal("80", input.GetAttribute("maxlength"));
        Assert.Equal("2", input.GetAttribute("minlength"));
        Assert.EndsWith("Name", input.GetAttribute("name"));
    }

    [Fact]
    public void Property_name_is_humanized_when_there_is_no_display_name()
    {
        var cut = TextFieldFor(() => _model.FavoriteColor);

        Assert.Equal("Favorite color", cut.Find("label").TextContent);
    }

    [Fact]
    public void Input_type_comes_from_annotations()
    {
        Assert.Equal("email", TextFieldFor(() => _model.Email).Find("input").GetAttribute("type"));
        Assert.Equal("password", TextFieldFor(() => _model.Password).Find("input").GetAttribute("type"));
        Assert.Equal("text", TextFieldFor(() => _model.FavoriteColor).Find("input").GetAttribute("type"));
    }

    [Fact]
    public void Password_gets_a_hidden_reveal_button()
    {
        var button = TextFieldFor(() => _model.Password).Find("[data-ib-reveal]");

        Assert.True(button.HasAttribute("hidden"));
        Assert.Equal("Show password", button.GetAttribute("aria-label"));
    }

    [Fact]
    public void Hint_is_linked_to_the_input()
    {
        var cut = TextFieldFor(() => _model.Name, p => p.Add(f => f.Hint, "As on your ID."));
        var input = cut.Find("input");

        Assert.Equal(input.Id + "-hint", input.GetAttribute("aria-describedby"));
        Assert.Equal("As on your ID.", cut.Find(".ib-field-hint").TextContent);
    }

    [Fact]
    public void Validation_errors_mark_the_field_invalid()
    {
        var cut = TextFieldFor(() => _model.Name);

        cut.InvokeAsync(() => _context.Validate());
        var input = cut.Find("input");

        Assert.Equal("true", input.GetAttribute("aria-invalid"));
        Assert.Contains(input.Id + "-error", input.GetAttribute("aria-describedby"));
        Assert.Contains("ib-field-invalid", cut.Find(".ib-field").ClassName);
        Assert.Contains("required", cut.Find(".ib-field-error").TextContent);
    }

    [Fact]
    public void Checkbox_posts_true_and_renders_its_label_beside_it()
    {
        var cut = Render<Checkbox>(p => p
            .AddCascadingValue(_context)
            .Add(c => c.ValueExpression, () => _model.Accept)
            .Add(c => c.Label, "I agree"));
        var input = cut.Find("input");

        Assert.Equal("checkbox", input.GetAttribute("type"));
        Assert.Equal("true", input.GetAttribute("value"));
        Assert.EndsWith("Accept", input.GetAttribute("name"));
        Assert.Equal("I agree", cut.Find(".ib-check-label").TextContent.Trim());
    }

    [Fact]
    public void Switch_has_the_switch_role()
    {
        var cut = Render<Switch>(p => p
            .AddCascadingValue(_context)
            .Add(c => c.ValueExpression, () => _model.Accept));

        Assert.Equal("switch", cut.Find("input").GetAttribute("role"));
    }

    [Fact]
    public void Select_lists_enum_values_with_display_names_and_marks_the_current_one()
    {
        var cut = Render<Select<Plan>>(p => p
            .AddCascadingValue(_context)
            .Add(s => s.Value, _model.Plan)
            .Add(s => s.ValueExpression, () => _model.Plan));
        var options = cut.FindAll("option");

        Assert.Equal(new[] { "Free", "Pro plan" }, options.Select(o => o.TextContent));
        Assert.True(options[1].HasAttribute("selected"));
    }

    [Fact]
    public void Select_ItemTemplate_renders_markup_with_a_text_label_and_selectedcontent()
    {
        var cut = Render<Select<Plan>>(p => p
            .AddCascadingValue(_context)
            .Add(s => s.Value, _model.Plan)
            .Add(s => s.ValueExpression, () => _model.Plan)
            .Add(s => s.ItemTemplate, plan => $"<b>{plan}</b><span class=\"ib-option-hint\">hint</span>"));

        Assert.NotNull(cut.Find("select > button.ib-select-button > selectedcontent"));
        var options = cut.FindAll("option");
        Assert.Equal("Pro plan", options[1].GetAttribute("data-ib-label"));
        Assert.Equal("Pro", options[1].QuerySelector("b")!.TextContent);
        Assert.Null(options[1].GetAttribute("label"));
        Assert.True(options[1].HasAttribute("selected"));
    }

    [Fact]
    public void Select_without_ItemTemplate_stays_plain()
    {
        var cut = Render<Select<Plan>>(p => p
            .AddCascadingValue(_context)
            .Add(s => s.Value, _model.Plan)
            .Add(s => s.ValueExpression, () => _model.Plan));

        Assert.Empty(cut.FindAll("selectedcontent"));
        Assert.Null(cut.Find("option").GetAttribute("data-ib-label"));
    }

    [Fact]
    public void Form_keeps_its_EditContext_across_renders()
    {
        // Passing EditForm a null EditContext on every render replaced its context, which made it
        // rebuild everything inside the form (losing focus on interactive pages).
        var cut = Render<Form>(p => p.Add(f => f.Model, _model).Add(f => f.FormName, "keep"));
        var before = cut.FindComponent<EditForm>().Instance.EditContext;

        cut.Render(p => p.Add(f => f.Model, _model).Add(f => f.FormName, "keep"));

        Assert.Same(before, cut.FindComponent<EditForm>().Instance.EditContext);
    }

    [Fact]
    public void Form_uses_a_given_EditContext()
    {
        var context = new EditContext(_model);
        var cut = Render<Form>(p => p.Add(f => f.EditContext, context).Add(f => f.FormName, "given"));

        Assert.Same(context, cut.FindComponent<EditForm>().Instance.EditContext);
    }

    [Fact]
    public void CheckboxGroup_posts_each_checked_value_and_toggles_in_option_order()
    {
        var cut = Render<CheckboxGroup<List<Plan>, Plan>>(p => p
            .AddCascadingValue(_context)
            .Add(c => c.Value, _model.Plans)
            .Add(c => c.ValueChanged, (List<Plan> v) => _model.Plans = v)
            .Add(c => c.ValueExpression, () => _model.Plans)
            .Add(c => c.Items, Enum.GetValues<Plan>()));

        var boxes = cut.FindAll("input[type=checkbox]");
        Assert.Equal(new[] { "Free", "Pro" }, boxes.Select(b => b.GetAttribute("value")));
        Assert.All(boxes, b => Assert.Equal(boxes[0].GetAttribute("name"), b.GetAttribute("name")));
        Assert.Equal(new[] { false, true }, boxes.Select(b => b.HasAttribute("checked")));
        Assert.Equal("Pro plan", cut.FindAll(".ib-check-label")[1].TextContent.Trim());
        Assert.NotNull(cut.Find("fieldset legend"));

        cut.FindAll("input[type=checkbox]")[0].Change(true);
        Assert.Equal(new[] { Plan.Free, Plan.Pro }, _model.Plans);
    }

    [Fact]
    public void MultiSelect_renders_a_posting_multiple_select_and_binds_its_values()
    {
        string[] all = ["a", "b", "c"];
        var cut = Render<MultiSelect<string[], string>>(p => p
            .AddCascadingValue(_context)
            .Add(c => c.Value, _model.Tags)
            .Add(c => c.ValueChanged, (string[] v) => _model.Tags = v)
            .Add(c => c.ValueExpression, () => _model.Tags)
            .Add(c => c.Items, all));

        var select = cut.Find("select");
        Assert.True(select.HasAttribute("multiple"));
        Assert.Equal(new[] { false, true, false }, cut.FindAll("select option").Select(o => o.HasAttribute("selected")));
        Assert.Equal("true", cut.Find("[role=listbox]").GetAttribute("aria-multiselectable"));
        Assert.Empty(cut.Find(".ib-multiselect-chips").Children);

        // A multiple select's change carries the chosen values as a string[] (Blazor's
        // ChangeEventArgs reader turns the browser's array into one). Raised directly, since
        // bUnit's Change() helper may not pass an array through as-is.
        select.TriggerEvent("onchange", new ChangeEventArgs { Value = new[] { "c", "a" } });
        Assert.Equal(new[] { "a", "c" }, _model.Tags);
    }

    [Fact]
    public void RadioGroup_is_a_fieldset_of_radios()
    {
        var cut = Render<RadioGroup<Plan>>(p => p
            .AddCascadingValue(_context)
            .Add(r => r.Value, _model.Plan)
            .Add(r => r.ValueExpression, () => _model.Plan));

        Assert.Equal("Plan", cut.Find("legend").TextContent);
        var radios = cut.FindAll("input[type=radio]");
        Assert.Equal(2, radios.Count);
        Assert.All(radios, r => Assert.EndsWith("Plan", r.GetAttribute("name")));
        Assert.True(radios[1].HasAttribute("checked"));
    }
}
