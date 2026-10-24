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
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        Assert.Equal("email", TextFieldFor(() => _model.Email).Find("input").GetAttribute("type"));
        Assert.Equal("password", TextFieldFor(() => _model.Password).Find("input").GetAttribute("type"));
        Assert.Equal("text", TextFieldFor(() => _model.FavoriteColor).Find("input").GetAttribute("type"));
    }

    [Fact]
    public void Password_gets_a_hidden_reveal_button()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
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
    public void Field_without_addons_has_no_group()
    {
        var cut = TextFieldFor(() => _model.FavoriteColor);

        Assert.Empty(cut.FindAll(".ib-field-group"));
    }

    [Fact]
    public void Addons_wrap_the_control_in_order()
    {
        var cut = TextFieldFor(() => _model.FavoriteColor, p => p
            .Add(f => f.StartAddon, "https://")
            .Add(f => f.EndAddon, ".com"));

        var group = cut.Find(".ib-field > .ib-field-group");
        var parts = group.Children.Select(c => c.ClassName).ToArray();

        Assert.Equal(new[] { "ib-field-addon ib-field-addon-start", "ib-field-control", "ib-field-addon ib-field-addon-end" }, parts);
        Assert.Equal("https://", group.Children[0].TextContent);
        Assert.Equal(".com", group.Children[2].TextContent);
        Assert.NotNull(group.Children[1].QuerySelector("input"));
    }

    [Fact]
    public void Addon_can_hold_a_button()
    {
        var cut = TextFieldFor(() => _model.FavoriteColor, p => p.Add(f => f.EndAddon, b =>
        {
            b.OpenComponent<Button>(0);
            b.AddAttribute(1, nameof(Button.Submit), true);
            b.AddAttribute(2, nameof(Button.ChildContent), (RenderFragment)(c => c.AddContent(0, "Go")));
            b.CloseComponent();
        }));

        var button = cut.Find(".ib-field-addon-end > button");

        Assert.Equal("submit", button.GetAttribute("type"));
        Assert.Empty(cut.FindAll(".ib-field-addon-start"));
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
    }

    [Fact]
    public async Task FormErrors_on_an_interactive_page_is_focused_from_csharp_after_a_submit()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var cut = Render<FormErrors>(p => p.AddCascadingValue(_context));
        await cut.InvokeAsync(() => _context.Validate());

        var summary = cut.Find(".ib-form-errors");
        Assert.False(summary.HasAttribute("autofocus"));
        Assert.False(summary.HasAttribute("data-ib-autofocus"));
        Assert.Equal("-1", summary.GetAttribute("tabindex"));

        // FocusAsync runs in OnAfterRenderAsync, after the render that a render-driven wait would
        // check on, so poll for it instead (that wait failed now and then when it lost the race).
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!JSInterop.Invocations.Any(i => i.Identifier == "Blazor._internal.domWrapper.focus") && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        JSInterop.VerifyFocusAsyncInvoke();
    }

    [Fact]
    public void FormErrors_doesnt_take_focus_while_a_field_is_edited()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var cut = Render<FormErrors>(p => p.AddCascadingValue(_context));

        // A field edit validates that field (and may show messages) but isn't a submit.
        cut.InvokeAsync(() => _context.NotifyFieldChanged(_context.Field(nameof(Model.Name))));
        cut.Render();

        JSInterop.VerifyNotInvoke("Blazor._internal.domWrapper.focus");
    }

    [Fact]
    public void Indeterminate_checkbox_is_marked_for_css_and_script()
    {
        var input = Render<Checkbox>(p => p
            .AddCascadingValue(_context)
            .Add(c => c.ValueExpression, () => _model.Accept)
            .Add(c => c.Indeterminate, true)).Find("input");

        Assert.True(input.HasAttribute("data-indeterminate"));
        Assert.False(input.HasAttribute("checked"));
        Assert.Equal("ib-check-input", input.ClassName);
    }

    [Fact]
    public void Plain_checkbox_and_switch_have_no_indeterminate_mark()
    {
        Assert.False(Render<Checkbox>(p => p.AddCascadingValue(_context).Add(c => c.ValueExpression, () => _model.Accept)).Find("input").HasAttribute("data-indeterminate"));
        Assert.False(Render<Switch>(p => p.AddCascadingValue(_context).Add(c => c.ValueExpression, () => _model.Accept).Add(c => c.Indeterminate, true)).Find("input").HasAttribute("data-indeterminate"));
    }

    [Fact]
    public void Row_and_section_icons_are_decorative()
    {
        var row = Render<Switch>(p => p
            .AddCascadingValue(_context)
            .Add(c => c.ValueExpression, () => _model.Accept)
            .Add(c => c.Label, "Sign-in alerts")
            .Add(c => c.Icon, Lucide.KeyRound)
            .Add(c => c.ControlPosition, ControlPosition.End));
        Assert.Equal("true", row.Find("label.ib-check > .ib-check-icon").GetAttribute("aria-hidden"));
        Assert.Equal("Sign-in alerts", row.Find(".ib-check-label").TextContent.Trim());

        var section = Render<Fieldset>(p => p.Add(f => f.Label, "Notifications").Add(f => f.Icon, Lucide.Bell));
        Assert.Equal("true", section.Find("fieldset > .ib-fieldset-icon").GetAttribute("aria-hidden"));
        Assert.Equal("Notifications", section.Find("fieldset > legend").TextContent);
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
        // The chosen text is the field's tooltip, for a value cut short.
        Assert.Equal("Pro plan", cut.Find("ib-field-control").GetAttribute("title"));
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
    public void CheckboxGroup_select_all_posts_nothing_and_follows_the_options()
    {
        var cut = Render<CheckboxGroup<List<Plan>, Plan>>(p => p
            .AddCascadingValue(_context)
            .Add(c => c.Value, _model.Plans)
            .Add(c => c.ValueChanged, (List<Plan> v) => _model.Plans = v)
            .Add(c => c.ValueExpression, () => _model.Plans)
            .Add(c => c.Items, Enum.GetValues<Plan>())
            .Add(c => c.SelectAll, "All plans"));

        // One of two checked: a dash, not checked, and only the options post.
        var all = cut.Find("input[data-ib-select-all]");
        Assert.Equal("All plans", all.ParentElement!.TextContent.Trim());
        Assert.False(all.HasAttribute("checked"));
        Assert.True(all.HasAttribute("data-indeterminate"));
        Assert.Equal(["Plans=Pro"], Posted(cut.FindAll("input")));
        var options = cut.FindAll(".ib-radio-options input");
        Assert.Equal(string.Join(" ", options.Select(o => o.Id)), all.GetAttribute("aria-controls"));

        // Both checked: checked, no dash.
        options[0].Change(true);
        all = cut.Find("input[data-ib-select-all]");
        Assert.True(all.HasAttribute("checked"));
        Assert.False(all.HasAttribute("data-indeterminate"));
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
    public void The_prerender_of_an_interactive_MultiSelect_draws_its_chips()
    {
        // The interactive render replaces the prerender's DOM, so drawing them is safe, and the
        // field is its final size before the script and the circuit arrive.
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var cut = Render<MultiSelect<string[], string>>(p => p
            .SetAssignedRenderMode(Microsoft.AspNetCore.Components.Web.RenderMode.InteractiveServer)
            .AddCascadingValue(_context)
            .Add(c => c.Value, _model.Tags)
            .Add(c => c.ValueExpression, () => _model.Tags)
            .Add(c => c.Items, new[] { "a", "b" }));

        Assert.Equal("b", Assert.Single(cut.Find(".ib-multiselect-chips").Children).GetAttribute("data-value"));
    }

    [Fact]
    public void An_interactive_MultiSelect_leaves_its_chips_to_the_script()
    {
        // A re-render would undo the script's chips: Blazor leaves them alone.
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var cut = Render<MultiSelect<string[], string>>(p => p
            .AddCascadingValue(_context)
            .Add(c => c.Value, _model.Tags)
            .Add(c => c.ValueExpression, () => _model.Tags)
            .Add(c => c.Items, new[] { "a", "b" }));

        Assert.Empty(cut.Find(".ib-multiselect-chips").Children);
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

        // A read-only box says it's read-only, and arrives disabled so it can't change without
        // script (the script enables it, keeping it from changing); it posts its value once ("true").
        var box = Render<Checkbox>(p => p
            .AddCascadingValue(_context)
            .AddCascadingValue(readOnly)
            .Add(c => c.Value, true)
            .Add(c => c.ValueExpression, () => _model.Accept));
        var input = box.Find("input[type=checkbox]");
        Assert.True(input.HasAttribute("disabled"));
        Assert.Equal("true", input.GetAttribute("aria-readonly"));
        Assert.Equal(new[] { "Accept=true" }, Posted(box.FindAll("input")));
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

    [Theory]
    [InlineData(RequiredMark.Required, "Full name *", "Favorite color")]
    [InlineData(RequiredMark.Optional, "Full name", "Favorite color (optional)")]
    [InlineData(RequiredMark.None, "Full name", "Favorite color")]
    public void The_form_chooses_which_fields_are_marked(RequiredMark mark, string required, string optional)
    {
        var options = new FormOptions(Disabled: false, ReadOnly: false, mark);
        string Label(System.Linq.Expressions.Expression<Func<string?>> field) =>
            TextFieldFor(field, p => p.AddCascadingValue(options)).Find("label").TextContent;

        Assert.Equal(required, Label(() => _model.Name));
        Assert.Equal(optional, Label(() => _model.FavoriteColor));
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
        var cut = Render<Tabs>(p => p
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
        Assert.Contains("1 error", cut.FindAll(".ib-tab")[1].TextContent);
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

    [Fact]
    public async Task The_open_tab_stays_when_it_has_errors_and_typing_never_switches_tabs()
    {
        _model.Email = "not an address";
        var cut = Render<Tabs>(p => p
            .AddCascadingValue(_context)
            .Add(t => t.Selected, "account")
            .Add(t => t.ChildContent, b =>
            {
                b.AddContent(0, TabOf("Profile", FieldFor(() => _model.Name)));  // [Required], empty
                b.AddContent(1, TabOf("Account", FieldFor(() => _model.Email))); // [EmailAddress]
            }));
        bool Open(int index) => cut.FindAll("input[type=radio]")[index].HasAttribute("checked");

        await cut.InvokeAsync(() => _context.Validate());
        Assert.True(Open(1)); // Account has errors too, so it stays open.

        _model.Email = "izzy@example.com";
        await cut.InvokeAsync(() => _context.NotifyFieldChanged(_context.Field(nameof(Model.Email))));
        Assert.True(Open(1)); // Profile still has errors, but only a submit switches tabs.
        Assert.Single(cut.FindAll(".ib-tab-errors"));
    }

    [Fact]
    public async Task A_field_in_nested_tabs_counts_toward_the_outer_tab()
    {
        RenderFragment inner = b =>
        {
            b.OpenComponent<Tabs>(0);
            b.AddAttribute(1, nameof(Tabs.ChildContent), TabOf("Limits", FieldFor(() => _model.Name)));
            b.CloseComponent();
        };
        var cut = Render<Tabs>(p => p
            .AddCascadingValue(_context)
            .Add(t => t.ChildContent, b =>
            {
                b.AddContent(0, TabOf("General", FieldFor(() => _model.FavoriteColor)));
                b.AddContent(1, TabOf("Advanced", inner));
            }));

        await cut.InvokeAsync(() => _context.Validate());

        var outer = cut.FindAll(".ib-tabs-list")[0];
        Assert.True(outer.QuerySelectorAll("input[type=radio]")[1].HasAttribute("checked"));
        Assert.Contains("1 error", outer.QuerySelectorAll(".ib-tab")[1].TextContent);
    }

    public class MessageModel
    {
        [Required(ErrorMessage = "Enter your {0}."), Display(Name = "nickname")]
        public string? Nickname { get; set; }

        [StringLength(80), MinLength(3, ErrorMessage = "At least 3 letters.")]
        public string? Code { get; set; }

        [StringLength(80)]
        public string? Plain { get; set; }
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
        Assert.Equal("Say who you are.", Input(() => model.Nickname, "Say who you are.").GetAttribute("data-ib-required"));
    }

    [Fact]
    public void Fields_take_ColSpan_and_NewRow()
    {
        var field = TextFieldFor(() => _model.Name, p => p.Add(t => t.ColSpan, (Responsive<int>)2).Add(t => t.NewRow, true)).Find(".ib-field");

        Assert.Contains("--ib-colspan:1 / span min(2,", field.GetAttribute("style"));
    }

    [Fact]
    public async Task An_edit_after_a_valid_submit_never_switches_tabs()
    {
        _model.Name = "Izzy";
        _model.Email = "izzy@example.com";
        var cut = Render<Tabs>(p => p
            .AddCascadingValue(_context)
            .Add(t => t.Selected, "account")
            .Add(t => t.ChildContent, b =>
            {
                b.AddContent(0, TabOf("Profile", FieldFor(() => _model.Name)));
                b.AddContent(1, TabOf("Account", FieldFor(() => _model.Email)));
            }));

        await cut.InvokeAsync(() => _context.Validate()); // valid: nothing to open
        _model.Name = null;
        await cut.InvokeAsync(() => _context.NotifyFieldChanged(_context.Field(nameof(Model.Name))));

        Assert.True(cut.FindAll("input[type=radio]")[1].HasAttribute("checked")); // still Account
        Assert.Single(cut.FindAll(".ib-tab-errors")); // Profile counts its new error
    }

    [Fact]
    public void Read_only_boxes_and_radios_never_block_a_post()
    {
        var readOnly = new FormOptions(Disabled: false, ReadOnly: true, RequiredMark.Required);

        var radios = Render<RadioGroup<Plan>>(p => p
            .AddCascadingValue(_context).AddCascadingValue(readOnly)
            .Add(c => c.Value, Plan.Pro).Add(c => c.ValueExpression, () => _model.Plan)
            .Add(c => c.Required, true));
        Assert.All(radios.FindAll("input[type=radio]"), r => Assert.False(r.HasAttribute("required")));

        var box = Render<Checkbox>(p => p
            .AddCascadingValue(_context).AddCascadingValue(readOnly)
            .Add(c => c.Value, false).Add(c => c.ValueExpression, () => _model.Accept)
            .Add(c => c.Required, true));
        Assert.False(box.Find("input[type=checkbox]").HasAttribute("required"));
    }
}
