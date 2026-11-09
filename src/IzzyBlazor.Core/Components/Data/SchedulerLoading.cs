using System.Linq.Expressions;

namespace IzzyBlazor;

/// <summary>
/// What a <see cref="Scheduler{TItem}"/>'s <c>ItemsProvider</c> is asked for: the appointments
/// that touch the days shown, from <see cref="Start"/> up to <see cref="End"/>, leaving out the
/// hidden categories. It's asked again for each page drawn: another day, week or view, and after a
/// save, a delete or a move.
/// </summary>
/// <param name="Start">The first day shown, at midnight.</param>
/// <param name="End">The day after the last shown, at midnight: an appointment touches the days shown when it starts before this and ends after <see cref="Start"/>.</param>
/// <param name="View">The view shown.</param>
/// <param name="HiddenCategories">The categories hidden from the Categories menu: their appointments needn't come.</param>
/// <param name="CancellationToken">Canceled when another page is asked for first, or the Scheduler goes away.</param>
public sealed record SchedulerRequest(DateTime Start, DateTime End, SchedulerView View, IReadOnlyCollection<string> HiddenCategories, CancellationToken CancellationToken);

/// <summary>The appointments of a Scheduler's <c>IQueryable</c> that touch the days shown, as a query the database runs.</summary>
internal static class SchedulerQuery
{
    public static IQueryable<TItem> InRange<TItem>(IQueryable<TItem> query, Expression<Func<TItem, DateTime>> start, Expression<Func<TItem, DateTime>> end,
        Expression<Func<TItem, string?>>? category, DateTime from, DateTime to, IReadOnlyCollection<string> hidden)
    {
        var x = Expression.Parameter(typeof(TItem), "x");
        var s = Rebind(start, x);
        var e = Rebind(end, x);
        // Starts before the last day ends, and ends after the first begins (or is a moment, at or after it).
        Expression touches = Expression.AndAlso(
            Expression.LessThan(s, Expression.Constant(to)),
            Expression.OrElse(Expression.GreaterThan(e, Expression.Constant(from)), Expression.GreaterThanOrEqual(s, Expression.Constant(from))));
        if (category is not null && hidden.Count > 0)
        {
            var isHidden = Expression.Call(Expression.Constant(hidden.ToList()), typeof(List<string>).GetMethod(nameof(List<string>.Contains), [typeof(string)])!, Rebind(category, x));
            touches = Expression.AndAlso(touches, Expression.Not(isHidden));
        }

        return query.Where(Expression.Lambda<Func<TItem, bool>>(touches, x));
    }

    private static Expression Rebind(LambdaExpression lambda, ParameterExpression x) => new Rebinder(lambda.Parameters[0], x).Visit(lambda.Body);

    private sealed class Rebinder(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
