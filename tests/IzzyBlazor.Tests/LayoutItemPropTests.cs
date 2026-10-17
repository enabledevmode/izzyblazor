using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace IzzyBlazor.Tests;

// Item props (margins, flex sizing, widths, AlignSelf) and Style on the element a parent lays out.
public class LayoutItemPropTests : BunitContext
{
    public class Model
    {
        public string? Query { get; set; }

        public bool Accept { get; set; }
    }

    private readonly Model _model = new();
    private readonly EditContext _context;

    public LayoutItemPropTests()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        _context = new EditContext(_model);
    }

    private IRenderedComponent<TextField> TextField(Action<ComponentParameterCollectionBuilder<TextField>> more) =>
        Render<TextField>(p =>
        {
            p.AddCascadingValue(_context);
            p.Add(f => f.ValueExpression, () => _model.Query);
            more(p);
        });

    [Fact]
    public void FileUpload_keeps_Style_on_its_wrapper()
    {
        var cut = Render<FileUpload>(p => p.Add(f => f.Name, "profileImage").Add(f => f.Style, "flex-grow: 1"));

        Assert.Contains("flex-grow: 1", cut.Find(".ib-field-file").GetAttribute("style"));
        Assert.False(cut.Find("input[type=file]").HasAttribute("style"));
    }

    [Fact]
    public void FileUpload_grows_in_a_row()
    {
        var wrapper = Render<FileUpload>(p => p.Add(f => f.Name, "profileImage").Add(f => f.Grow, true)).Find(".ib-field-file");

        Assert.Contains("ib-grow", wrapper.ClassName);
        Assert.Contains("--ib-grow:1", wrapper.GetAttribute("style"));
    }

    [Fact]
    public void A_fields_Style_goes_on_the_wrapper_not_the_input()
    {
        var cut = TextField(p => p.Add(f => f.Style, "flex-grow: 1"));

        Assert.Contains("flex-grow: 1", cut.Find(".ib-field").GetAttribute("style"));
        Assert.False(cut.Find("input").HasAttribute("style"));
    }

    [Fact]
    public void A_field_with_no_style_props_has_no_style_attribute()
    {
        var wrapper = TextField(_ => { }).Find(".ib-field");

        Assert.False(wrapper.HasAttribute("style"));
        Assert.DoesNotContain("ib-grow", wrapper.ClassName);
        Assert.DoesNotContain("ib-shrink", wrapper.ClassName);
    }

    [Fact]
    public void Fields_take_the_item_props_on_the_wrapper()
    {
        var wrapper = TextField(p => p
            .Add(f => f.Grow, true)
            .Add(f => f.Width, new Responsive<Length> { Base = Length.Percent(100), Md = Length.Rem(20) })
            .Add(f => f.MarginTop, 4)
            .Add(f => f.AlignSelf, Align.End)).Find(".ib-field");
        var style = wrapper.GetAttribute("style")!;

        Assert.Contains("ib-grow", wrapper.ClassName);
        Assert.Contains("ib-w-md", wrapper.ClassName);
        Assert.Contains("ib-mt", wrapper.ClassName);
        Assert.Contains("ib-self", wrapper.ClassName);
        Assert.Contains("--ib-w-md:20rem", style);
        Assert.Contains("--ib-self:flex-end", style);
        Assert.DoesNotContain("ib-grow", TextField(p => p.Add(f => f.Grow, true)).Find("input").ClassName ?? "");
    }

    [Fact]
    public void Fields_take_HiddenFrom_and_VisibleFrom()
    {
        var wrapper = TextField(p => p.Add(f => f.HiddenFrom, Breakpoint.Md)).Find(".ib-field");
        Assert.Contains("ib-hidden-from-md", wrapper.ClassName);

        var checkbox = Render<Checkbox>(p => p
            .AddCascadingValue(_context)
            .Add(c => c.ValueExpression, () => _model.Accept)
            .Add(c => c.VisibleFrom, Breakpoint.Lg)).Find(".ib-field");
        Assert.Contains("ib-visible-from-lg", checkbox.ClassName);
    }

    [Fact]
    public void User_classes_come_after_the_item_props()
    {
        var wrapper = TextField(p => p.Add(f => f.Grow, true).Add(f => f.Class, "mine")).Find(".ib-field");

        Assert.EndsWith("ib-grow mine", wrapper.ClassName);
    }

    [Fact]
    public void Buttons_grow_and_align_themselves()
    {
        var button = Render<Button>(p => p
            .Add(b => b.Grow, true)
            .Add(b => b.AlignSelf, Align.End)
            .AddChildContent("Search")).Find("button");

        Assert.Equal("ib-btn ib-grow ib-self ib-btn-primary ib-btn-solid", button.ClassName);
        Assert.Contains("--ib-self:flex-end", button.GetAttribute("style"));
    }

    [Fact]
    public void Shrink_false_keeps_the_size()
    {
        var progress = Render<Progress>(p => p.Add(x => x.Shrink, false)).Find("div");

        Assert.Contains("ib-shrink", progress.ClassName);
        Assert.Contains("--ib-shrink:0", progress.GetAttribute("style"));
    }

    [Fact]
    public void Fieldset_takes_the_surface_props()
    {
        var fieldset = Render<Fieldset>(p => p.Add(f => f.Padding, 4).Add(f => f.Border, 1)).Find("fieldset");

        Assert.Contains("ib-px", fieldset.ClassName);
        Assert.Contains("ib-bx", fieldset.ClassName);
    }

    [Fact]
    public void Box_writes_the_props_in_the_same_order_as_before()
    {
        var box = Render<Box>(p => p
            .Add(b => b.Padding, 2)
            .Add(b => b.Margin, 1)
            .Add(b => b.Width, Length.Rem(10))
            .Add(b => b.MinHeight, Length.Rem(4))
            .Add(b => b.MinWidth, Length.Rem(2))
            .Add(b => b.Grow, true)
            .Add(b => b.AlignSelf, Align.Center)).Find("div");

        Assert.Equal("ib-box ib-px ib-py ib-mx ib-my ib-w ib-minh ib-minw ib-grow ib-self", box.ClassName);
    }

    // A StyledComponentBase whose Class goes inside, as a menu's does: surface props stay on the
    // inner element and only the item props move out.
    public sealed class InnerPanel : StyledComponentBase
    {
        protected override bool VisibilityOnRoot => false;

        protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
        {
            classes.Add("inner");
            base.BuildAttributes(classes, styles);
        }

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            var (outerClass, outerStyle) = BuildOuterAttributes("outer");
            var (cssClass, style) = BuildRootAttributes();
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", outerClass);
            builder.AddAttribute(2, "style", outerStyle);
            builder.OpenElement(3, "div");
            builder.AddAttribute(4, "class", cssClass);
            builder.AddAttribute(5, "style", style);
            builder.CloseElement();
            builder.CloseElement();
        }
    }

    [Fact]
    public void Surface_props_stay_on_the_inner_element_when_Class_goes_inside()
    {
        var cut = Render<InnerPanel>(p => p
            .Add(x => x.Padding, 4)
            .Add(x => x.Bg, Color.Primary)
            .Add(x => x.Grow, true)
            .Add(x => x.HiddenFrom, Breakpoint.Md));

        Assert.Equal("outer ib-grow ib-hidden-from-md", cut.Find(".outer").ClassName);
        Assert.Equal("inner ib-px ib-py ib-bg ib-fg", cut.Find(".inner").ClassName);
    }

    public static TheoryData<string> ItemComponents =>
    [
        nameof(Button), nameof(Badge), nameof(Spinner), nameof(Tag), nameof(Kbd),
        nameof(Avatar), nameof(Progress), nameof(Stat), nameof(Fieldset),
    ];

    // The props go on the outermost element, the one the parent lays out.
    [Theory]
    [MemberData(nameof(ItemComponents))]
    public void Style_and_item_props_reach_the_root(string component)
    {
        var root = component switch
        {
            nameof(Button) => Root(Render<Button>(p => p.Add(c => c.Style, "--probe: 1").Add(c => c.MarginStart, 2).AddChildContent("Go"))),
            nameof(Badge) => Root(Render<Badge>(p => p.Add(c => c.Style, "--probe: 1").Add(c => c.MarginStart, 2).AddChildContent("New"))),
            nameof(Spinner) => Root(Render<Spinner>(p => p.Add(c => c.Style, "--probe: 1").Add(c => c.MarginStart, 2))),
            nameof(Tag) => Root(Render<Tag>(p => p.Add(c => c.Style, "--probe: 1").Add(c => c.MarginStart, 2).AddChildContent("Tag"))),
            nameof(Kbd) => Root(Render<Kbd>(p => p.Add(c => c.Style, "--probe: 1").Add(c => c.MarginStart, 2).AddChildContent("Esc"))),
            nameof(Avatar) => Root(Render<Avatar>(p => p.Add(c => c.Style, "--probe: 1").Add(c => c.MarginStart, 2).Add(c => c.Name, "Ada Lovelace"))),
            nameof(Progress) => Root(Render<Progress>(p => p.Add(c => c.Style, "--probe: 1").Add(c => c.MarginStart, 2))),
            nameof(Stat) => Root(Render<Stat>(p => p.Add(c => c.Style, "--probe: 1").Add(c => c.MarginStart, 2).Add(c => c.Label, "Revenue"))),
            nameof(Fieldset) => Root(Render<Fieldset>(p => p.Add(c => c.Style, "--probe: 1").Add(c => c.MarginStart, 2))),
            _ => throw new ArgumentOutOfRangeException(nameof(component)),
        };

        Assert.Contains("--probe: 1", root.GetAttribute("style"));
        Assert.Contains("--ib-ms:", root.GetAttribute("style"));
        Assert.Contains("ib-ms", root.ClassName);
    }

    private static IElement Root<T>(IRenderedComponent<T> cut) where T : IComponent =>
        cut.Nodes.OfType<IElement>().First();
}
