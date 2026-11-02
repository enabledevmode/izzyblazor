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

        public int Count { get; set; }
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
    public void Input_type_comes_from_annotations()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        Assert.Equal("email", TextFieldFor(() => _model.Email).Find("input").GetAttribute("type"));
        Assert.Equal("password", TextFieldFor(() => _model.Password).Find("input").GetAttribute("type"));
        Assert.Equal("text", TextFieldFor(() => _model.FavoriteColor).Find("input").GetAttribute("type"));
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
    public void Checkbox_posts_false_after_the_box_unless_disabled()
    {
        var inputs = Render<Checkbox>(p => p
            .AddCascadingValue(_context)
            .Add(c => c.ValueExpression, () => _model.Accept)).FindAll("input");

        // The box comes first, so a checked box posts "true" before the hidden "false".
        Assert.Equal(2, inputs.Count);
        Assert.Equal("checkbox", inputs[0].GetAttribute("type"));
        Assert.Equal("hidden", inputs[1].GetAttribute("type"));
        Assert.Equal("false", inputs[1].GetAttribute("value"));
        Assert.Equal(inputs[0].GetAttribute("name"), inputs[1].GetAttribute("name"));

        var disabled = Render<Checkbox>(p => p
            .AddCascadingValue(_context)
            .Add(c => c.ValueExpression, () => _model.Accept)
            .Add(c => c.Disabled, true)).FindAll("input");
        Assert.Single(disabled);
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, "secret123")]
    public void A_static_page_does_not_write_a_posted_password_back(bool interactive, string? shown)
    {
        SetRendererInfo(new RendererInfo(interactive ? "Server" : "Static", interactive));
        _model.Password = "secret123";
        _model.Name = "Ada";

        Assert.Equal(shown, TextFieldFor(() => _model.Password, p => p.Add(f => f.Value, _model.Password)).Find("input").GetAttribute("value"));
        Assert.Equal("Ada", TextFieldFor(() => _model.Name, p => p.Add(f => f.Value, _model.Name)).Find("input").GetAttribute("value"));
    }

    [Fact]
    public void FormErrors_takes_focus_on_a_static_page_after_a_failed_submit()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var cut = Render<FormErrors>(p => p.AddCascadingValue(_context));
        Assert.Empty(cut.FindAll(".ib-form-errors"));

        cut.InvokeAsync(() => _context.Validate());

        var summary = cut.Find(".ib-form-errors");
        Assert.Equal("-1", summary.GetAttribute("tabindex"));
        Assert.True(summary.HasAttribute("autofocus"));
        Assert.True(summary.HasAttribute("data-ib-autofocus"));
        Assert.Equal(summary.QuerySelector(".ib-form-errors-title")!.Id, summary.GetAttribute("aria-labelledby"));
        Assert.NotEmpty(summary.QuerySelectorAll("li"));

        // Read on focus, as a group described by its messages: not a live region too, which reads it twice.
        Assert.Equal("group", summary.GetAttribute("role"));
        var description = summary.QuerySelector($"#{summary.GetAttribute("aria-describedby")}")!;
        Assert.True(description.HasAttribute("hidden"));
        Assert.All(summary.QuerySelectorAll("li"), li => Assert.Contains(li.TextContent.Trim(), description.TextContent));
        Assert.Matches(@"[.!?]$", description.TextContent);
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
        // The chosen text is the field's tooltip, for a value cut short.
        Assert.Equal("Pro plan", cut.Find("ib-field-control").GetAttribute("title"));
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
    public void CheckboxGroup_select_all_posts_nothing_and_follows_the_options()
    {
        IRenderedComponent<CheckboxGroup<List<Plan>, Plan>> Group(Plan[] items, bool selectAll = true, FormOptions? options = null) =>
            Render<CheckboxGroup<List<Plan>, Plan>>(p =>
            {
                p.AddCascadingValue(_context);
                if (options is not null) p.AddCascadingValue(options);
                p.Add(c => c.Value, _model.Plans)
                    .Add(c => c.ValueChanged, (List<Plan> v) => _model.Plans = v)
                    .Add(c => c.ValueExpression, () => _model.Plans)
                    .Add(c => c.Items, items)
                    .Add(c => c.SelectAll, selectAll);
            });
        AngleSharp.Dom.IElement? Box(IRenderedComponent<CheckboxGroup<List<Plan>, Plan>> cut) =>
            cut.FindAll("label").FirstOrDefault(l => l.TextContent.Trim() == "Select all")?.QuerySelector("input");

        // One of two checked: not checked, and only the options post.
        var cut = Group(Enum.GetValues<Plan>());
        Assert.False(Box(cut)!.HasAttribute("checked"));
        Assert.Equal(["Plans=Pro"], Posted(cut.FindAll("input")));

        // Both checked: checked.
        cut.FindAll("input[type=checkbox]").Single(o => o.GetAttribute("value") == "Free").Change(true);
        Assert.True(Box(cut)!.HasAttribute("checked"));
        Assert.Equal(["Plans=Free", "Plans=Pro"], Posted(cut.FindAll("input")));

        // A read-only form: it's disabled and posts nothing; the options post from hidden inputs.
        _model.Plans = [Plan.Pro];
        cut = Group(Enum.GetValues<Plan>(), options: new FormOptions(Disabled: false, ReadOnly: true, RequiredMark.Required));
        Assert.True(Box(cut)!.HasAttribute("disabled"));
        Assert.Equal(["Plans=Pro"], Posted(cut.FindAll("input")));

        // No options, or not asked for: no box.
        Assert.Null(Box(Group([])));
        Assert.Null(Box(Group(Enum.GetValues<Plan>(), selectAll: false)));
    }

    [Fact]
    public void MultiSelect_renders_a_posting_multiple_select_and_binds_its_values()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
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
        // On a static page the server draws the chips, as the script would, so the field doesn't
        // change when the script arrives.
        var chip = Assert.Single(cut.Find(".ib-multiselect-chips").Children);
        Assert.Equal("b", chip.GetAttribute("data-value"));
        Assert.Equal("Remove b", chip.QuerySelector(".ib-chip-remove")!.GetAttribute("aria-label"));

        // A multiple select's change carries the chosen values as a string[] (Blazor's
        // ChangeEventArgs reader turns the browser's array into one). Raised directly, since
        // bUnit's Change() helper may not pass an array through as-is.
        select.TriggerEvent("onchange", new ChangeEventArgs { Value = new[] { "c", "a" } });
        Assert.Equal(new[] { "a", "c" }, _model.Tags);
    }

    [Fact]
    public void A_read_only_form_shows_values_and_still_posts_them()
    {
        var readOnly = new FormOptions(Disabled: false, ReadOnly: true, RequiredMark.Required);

        // The Form passes ReadOnly down to its fields.
        var form = Render<Form>(p => p
            .Add(f => f.Model, _model)
            .Add(f => f.FormName, "view")
            .Add(f => f.ReadOnly, true)
            .Add(f => f.ChildContent, b =>
            {
                b.OpenComponent<TextField>(0);
                b.AddAttribute(1, nameof(TextField.ValueExpression), (System.Linq.Expressions.Expression<Func<string?>>)(() => _model.Name));
                b.CloseComponent();
            }));
        Assert.True(form.Find("input.ib-field-input").HasAttribute("readonly"));

        // A select can't be read-only: it shows its text in a read-only field, and posts its value once.
        var select = Render<Select<Plan>>(p => p
            .AddCascadingValue(_context)
            .AddCascadingValue(readOnly)
            .Add(s => s.Value, _model.Plan)
            .Add(s => s.ValueExpression, () => _model.Plan));
        Assert.Equal("Pro plan", select.Find("input[readonly]").GetAttribute("value"));
        Assert.Equal(new[] { "Plan=Pro" }, Posted(select.FindAll("input, select, textarea")));

        // A box can't be read-only: it's disabled, and posts its value once ("true").
        var box = Render<Checkbox>(p => p
            .AddCascadingValue(_context)
            .AddCascadingValue(readOnly)
            .Add(c => c.Value, true)
            .Add(c => c.ValueExpression, () => _model.Accept));
        var input = box.Find("input[type=checkbox]");
        Assert.True(input.HasAttribute("disabled"));
        Assert.False(input.HasAttribute("aria-readonly"));
        Assert.Equal(new[] { "Accept=true" }, Posted(box.FindAll("input")));

        // A slider too.
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var slider = Render<Slider<int>>(p => p
            .AddCascadingValue(_context)
            .AddCascadingValue(readOnly)
            .Add(s => s.Value, 3)
            .Add(s => s.ValueExpression, () => _model.Count));
        Assert.True(slider.Find("input[type=range]").HasAttribute("disabled"));
        Assert.Equal(new[] { "Count=3" }, Posted(slider.FindAll("input")));
    }

    // What a browser would post from these controls, as "Property=value": named controls that are
    // enabled and in the form (no form=""), boxes and radios only when checked.
    private static List<string> Posted(IEnumerable<AngleSharp.Dom.IElement> controls) =>
        controls
            .Where(e => e.HasAttribute("name") && !e.HasAttribute("disabled") && !e.HasAttribute("form"))
            .Where(e => e.GetAttribute("type") is not ("checkbox" or "radio") || e.HasAttribute("checked"))
            .Select(e => $"{e.GetAttribute("name")!.Split('.')[^1]}=" +
                (e.LocalName == "select" ? e.QuerySelector("option[selected]")?.GetAttribute("value") ?? "" : e.GetAttribute("value") ?? ""))
            .ToList();

    [Fact]
    public void A_disabled_form_disables_its_fields_and_posts_none_of_them()
    {
        var disabled = new FormOptions(Disabled: true, ReadOnly: false, RequiredMark.Required);

        Assert.True(TextFieldFor(() => _model.Name, p => p.AddCascadingValue(disabled)).Find("input").HasAttribute("disabled"));
        var box = Render<Checkbox>(p => p
            .AddCascadingValue(_context)
            .AddCascadingValue(disabled)
            .Add(c => c.ValueExpression, () => _model.Accept)).FindAll("input");
        Assert.Single(box);
        Assert.True(box[0].HasAttribute("disabled"));
    }

    [Fact]
    public async Task After_a_failed_submit_the_first_tab_with_errors_opens_and_counts_them()
    {
        RenderFragment Field(System.Linq.Expressions.Expression<Func<string?>> field) => b =>
        {
            b.OpenComponent<TextField>(0);
            b.AddAttribute(1, nameof(TextField.ValueExpression), field);
            b.CloseComponent();
        };
        var cut = Render<Tabs>(p => p.Add(t => t.Label, "Tabs")
            .AddCascadingValue(_context)
            .Add(t => t.ChildContent, b =>
            {
                b.OpenComponent<Tab>(0);
                b.AddAttribute(1, nameof(Tab.Label), "Profile");
                b.AddAttribute(2, nameof(Tab.ChildContent), Field(() => _model.FavoriteColor));
                b.CloseComponent();
                b.OpenComponent<Tab>(3);
                b.AddAttribute(4, nameof(Tab.Label), "Account");
                b.AddAttribute(5, nameof(Tab.ChildContent), Field(() => _model.Name)); // [Required], empty
                b.CloseComponent();
            }));
        Assert.Empty(cut.FindAll(".ib-tab-errors"));

        await cut.InvokeAsync(() => _context.Validate());

        var radios = cut.FindAll("input[type=radio]");
        Assert.True(radios[1].HasAttribute("checked"));
        Assert.Equal("(1 error)", cut.FindAll(".ib-tab")[1].QuerySelector(".ib-tab-errors .ib-visually-hidden")!.TextContent);
        Assert.Single(cut.FindAll(".ib-tab-errors"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Read_only_choices_post_their_values_once_and_nothing_when_also_disabled(bool alsoDisabled)
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var options = new FormOptions(Disabled: alsoDisabled, ReadOnly: true, RequiredMark.Required);
        List<string> Posts<TComponent>(Action<ComponentParameterCollectionBuilder<TComponent>> parameters) where TComponent : IComponent =>
            Posted(Render<TComponent>(p =>
            {
                p.AddCascadingValue(_context);
                p.AddCascadingValue(options);
                parameters(p);
            }).FindAll("input, select, textarea"));
        string[] Expect(params string[] posted) => alsoDisabled ? [] : posted;

        Assert.Equal(Expect("Plan=Pro"), Posts<RadioGroup<Plan>>(p => p.Add(c => c.Value, Plan.Pro).Add(c => c.ValueExpression, () => _model.Plan)));
        Assert.Equal(Expect("Plan=Pro"), Posts<SegmentedControl<Plan>>(p => p.Add(c => c.Value, Plan.Pro).Add(c => c.ValueExpression, () => _model.Plan)));
        Assert.Equal(Expect("Plan=Pro"), Posts<Select<Plan>>(p => p.Add(c => c.Value, Plan.Pro).Add(c => c.ValueExpression, () => _model.Plan)));
        Assert.Equal(Expect("Accept=true"), Posts<Switch>(p => p.Add(c => c.Value, true).Add(c => c.ValueExpression, () => _model.Accept)));
        Assert.Equal(Expect("Plans=Free", "Plans=Pro"), Posts<CheckboxGroup<List<Plan>, Plan>>(p => p
            .Add(c => c.Value, [Plan.Free, Plan.Pro]).Add(c => c.ValueExpression, () => _model.Plans)));
        Assert.Equal(Expect("Tags=b"), Posts<MultiSelect<string[], string>>(p => p
            .Add(c => c.Value, ["b"]).Add(c => c.ValueExpression, () => _model.Tags).Add(c => c.Items, ["a", "b"])));
        Assert.Equal(Expect("FavoriteColor=red"), Posts<Combobox<string?>>(p => p
            .Add(c => c.Value, "red").Add(c => c.ValueExpression, () => _model.FavoriteColor).Add(c => c.Items, ["red", "blue"])));
    }

    private static RenderFragment FieldFor(System.Linq.Expressions.Expression<Func<string?>> field) => b =>
    {
        b.OpenComponent<TextField>(0);
        b.AddAttribute(1, nameof(TextField.ValueExpression), field);
        b.CloseComponent();
    };

    private static RenderFragment TabOf(string label, RenderFragment content) => b =>
    {
        b.OpenComponent<Tab>(0);
        b.AddAttribute(1, nameof(Tab.Label), label);
        b.AddAttribute(2, nameof(Tab.ChildContent), content);
        b.CloseComponent();
    };

    public class MessageModel
    {
        [Required(ErrorMessage = "Enter your {0}."), Display(Name = "nickname")]
        public string? Nickname { get; set; }

        [StringLength(80), MinLength(3, ErrorMessage = "At least 3 letters.")]
        public string? Code { get; set; }

        [StringLength(80)]
        public string? Plain { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        [Url(ErrorMessage = "Enter a link like https://example.com.")]
        public string? Site { get; set; }
    }

    [Fact]
    public void The_model_s_own_messages_reach_the_browser_s_check_and_a_field_can_replace_them()
    {
        var model = new MessageModel();
        var context = new EditContext(model);
        AngleSharp.Dom.IElement Input(System.Linq.Expressions.Expression<Func<string?>> field, string? own = null) =>
            Render<TextField>(p =>
            {
                p.AddCascadingValue(context);
                p.Add(t => t.ValueExpression, field);
                if (own is not null) p.AddUnmatched("data-ib-required", own);
            }).Find("input");

        Assert.Equal("Enter your nickname.", Input(() => model.Nickname).GetAttribute("data-ib-required"));
        Assert.Equal("At least 3 letters.", Input(() => model.Code).GetAttribute("data-ib-length"));
        Assert.False(Input(() => model.Plain).HasAttribute("data-ib-length")); // the browser's own text
        // [EmailAddress] reports .NET's English text as its ErrorMessage; that's not the model's own.
        Assert.False(Input(() => model.Email).HasAttribute("data-ib-type"));
        Assert.Equal("Enter a link like https://example.com.", Input(() => model.Site).GetAttribute("data-ib-type"));
        Assert.Equal("Say who you are.", Input(() => model.Nickname, "Say who you are.").GetAttribute("data-ib-required"));
    }

    [Fact]
    public void A_read_only_form_s_boxes_and_radios_are_disabled_so_never_block_a_post()
    {
        var readOnly = new FormOptions(Disabled: false, ReadOnly: true, RequiredMark.Required);

        var radios = Render<RadioGroup<Plan>>(p => p
            .AddCascadingValue(_context).AddCascadingValue(readOnly)
            .Add(c => c.Value, Plan.Pro).Add(c => c.ValueExpression, () => _model.Plan)
            .Add(c => c.Required, true));
        Assert.All(radios.FindAll("input[type=radio]"), r => Assert.True(r.HasAttribute("disabled")));

        var box = Render<Checkbox>(p => p
            .AddCascadingValue(_context).AddCascadingValue(readOnly)
            .Add(c => c.Value, false).Add(c => c.ValueExpression, () => _model.Accept)
            .Add(c => c.Required, true));
        Assert.True(box.Find("input[type=checkbox]").HasAttribute("disabled"));
    }
}
