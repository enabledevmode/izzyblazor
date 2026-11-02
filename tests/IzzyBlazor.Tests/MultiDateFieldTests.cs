using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace IzzyBlazor.Tests;

public class MultiDateFieldTests : BunitContext
{
    public class Model
    {
        public List<DateOnly>? Days { get; set; }

        public DateOnly? Day { get; set; }
    }

    private readonly Model _model = new();
    private readonly EditContext _context;

    public MultiDateFieldTests()
    {
        _context = new EditContext(_model);
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    private IRenderedComponent<MultiDateField<T>> Field<T>(Expression<Func<T>> field, Action<ComponentParameterCollectionBuilder<MultiDateField<T>>>? more = null) =>
        Render<MultiDateField<T>>(p =>
        {
            p.AddCascadingValue(_context);
            p.Add(f => f.ValueExpression, field);
            p.Add(f => f.Value, field.Compile()());
            more?.Invoke(p);
        });

    [Fact]
    public void Each_date_is_checked_on_the_server_whatever_was_posted()
    {
        _model.Days = [new(2027, 3, 1), new(2027, 3, 7)];

        Field(() => _model.Days, p => p.Add(f => f.DateDisabled, day => day.DayOfWeek == DayOfWeek.Sunday));

        Assert.False(_context.Validate());
    }

    [Fact]
    public void It_only_binds_a_collection_of_dates() =>
        Assert.Throws<InvalidOperationException>(() => Field(() => _model.Day));
}
