using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms.Mapping;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace IzzyBlazor.DocsGen;

/// <summary>
/// What marks a component's outermost element: its tag when it is one of ours (<c>&lt;ib-dialog&gt;</c>) and
/// its <c>ib-</c> classes. <see cref="Root"/> are those it has whatever its options; <see cref="Own"/> are
/// those of them no other component draws on its outermost element, so an element with one is that component's.
/// </summary>
/// <param name="Root">The marks its outermost elements always have.</param>
/// <param name="Own">Those of them that are this component's alone.</param>
/// <param name="Inside">The marks of other components that it draws inside itself (a field's popover, a grid's pager buttons): such an element is a part of it.</param>
/// <param name="Like">The components that draw the same element as it does with some of its options set (Row and Column for Stack): only the source tells them from it.</param>
public sealed record RootMarks(List<string> Root, List<string> Own, List<string> Inside, List<string> Like);

/// <summary>
/// Finds each component's <see cref="RootMarks"/> by drawing it: once as it is, then once for each value of
/// each option that is a bool, an enum or one of the library's named values (<c>Color.Primary</c>). A class
/// that is there every time is the component's; one that comes and goes is an option's. Nothing is read from
/// the source, where a class may come from a base or be put together from parts. One that takes items is
/// drawn with rows too, since what it draws for them (a grid's pager) is a part of it. One that says it needs
/// another inside it (a RadarChart its RadarSeries) is drawn with that one. A component that can't be drawn
/// alone (a part that needs its parent, a toolbar button that needs its grid) has no marks: an element inside
/// it is traced to the component around it.
/// </summary>
public static partial class RootClasses
{
    public static Dictionary<string, RootMarks> Read(IEnumerable<Assembly> assemblies)
    {
        var library = assemblies.Distinct().ToList();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIzzyBlazor();
        services.AddSingleton<NavigationManager, Navigation>();
        services.AddSingleton<IJSRuntime, NoScript>();
        services.AddSingleton<IErrorBoundaryLogger, NoErrors>();
        // A form inside a component (a board's move buttons) asks for it.
        services.AddSingleton<IFormValueMapper, NoPosts>();
        using var provider = services.BuildServiceProvider();

        var drawn = new SortedDictionary<string, (List<string> Always, HashSet<string> Ever, HashSet<string> Inside)>(StringComparer.Ordinal);
        var definitions = library.SelectMany(a => a.GetExportedTypes()).Where(t => typeof(IComponent).IsAssignableFrom(t) && !t.IsAbstract)
            .OrderBy(t => t.Name, StringComparer.Ordinal).DistinctBy(t => t.Name.Split('`')[0]).ToList();
        var alone = new List<Type>();
        var said = new Dictionary<Type, HashSet<string>>();
        foreach (var definition in definitions)
        {
            // The first way it draws: a type for a generic one, and as few made-up options as it takes.
            var basis = (from type in Closed(definition)
                         let parameters = Parameters(type)
                         from given in Fills(parameters, library)
                         let marks = Draw(provider, type, given, said)
                         where marks is not null
                         select (type, parameters, given, marks: marks.Value)).FirstOrDefault();
            if (basis.type is null) alone.Add(definition);
            else drawn[definition.Name.Split('`')[0]] = Vary(provider, library, basis.type, basis.parameters, basis.given, basis.marks);
        }

        // One that didn't draw and named another that didn't (a RadarChart needs a RadarSeries): with that one
        // inside it, of the same type and filled in the same ways.
        foreach (var definition in alone.Where(d => d.GetProperty("ChildContent")?.PropertyType == typeof(RenderFragment)))
        {
            var basis = (from part in alone
                         where part != definition && said.GetValueOrDefault(definition) is { } needs && needs.Any(need => Regex.IsMatch(need, $@"\b{Regex.Escape(part.Name.Split('`')[0])}\b"))
                         from type in Closed(definition)
                         let inner = Close(part, type)
                         where inner is not null
                         let parameters = Parameters(type)
                         from given in Fills(parameters, library)
                         from within in Fills(Parameters(inner), library)
                         let whole = new Dictionary<string, object?>(given) { ["ChildContent"] = (RenderFragment)(builder =>
                         {
                             builder.OpenComponent(0, inner);
                             builder.AddMultipleAttributes(1, within!);
                             builder.CloseComponent();
                         }) }
                         let marks = Draw(provider, type, whole, null)
                         where marks is not null
                         select (type, parameters, given: whole, marks: marks.Value)).FirstOrDefault();
            if (basis.type is not null) drawn[definition.Name.Split('`')[0]] = Vary(provider, library, basis.type, basis.parameters, basis.given, basis.marks);
        }

        // One with two shapes (Code inline and as a block) has no class in every drawing: its marks are those
        // that no other component draws anywhere.
        foreach (var (name, marks) in drawn.Where(c => c.Value.Always.Count == 0).ToList())
            marks.Always.AddRange(marks.Ever.Order(StringComparer.Ordinal).Where(mark => !drawn.Any(other => other.Key != name && (other.Value.Ever.Contains(mark) || other.Value.Inside.Contains(mark)))));

        // A component that is another with some of its options set (a chart's reset button is a Button, neutral,
        // outlined and small; a Row is a Stack) can't be told from it in a page: an element like that is traced
        // to the other, which lists it, and the source says which was written.
        var like = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (name, marks) in drawn.ToList())
            if (drawn.FirstOrDefault(other => other.Key != name && other.Value.Always.Count > 0 && other.Value.Always.Count < marks.Always.Count
                && other.Value.Always.All(marks.Always.Contains) && marks.Always.All(other.Value.Ever.Contains)) is { Key: not null } drawer)
            {
                drawn.Remove(name);
                if (!like.TryGetValue(drawer.Key, out var names)) like[drawer.Key] = names = [];
                names.Add(name);
            }

        var roots = drawn.Where(c => c.Value.Always.Count > 0).SelectMany(c => c.Value.Always).ToHashSet(StringComparer.Ordinal);
        return drawn.Where(c => c.Value.Always.Count > 0).ToDictionary(c => c.Key,
            c => new RootMarks(c.Value.Always,
                [.. c.Value.Always.Where(mark => !drawn.Any(other => other.Key != c.Key && (other.Value.Ever.Contains(mark) || other.Value.Inside.Contains(mark))))],
                [.. c.Value.Inside.Where(mark => roots.Contains(mark) && !c.Value.Always.Contains(mark)).Order(StringComparer.Ordinal)],
                like.GetValueOrDefault(c.Key) ?? []),
            StringComparer.Ordinal);
    }

    static List<PropertyInfo> Parameters(Type type) => [.. type.GetProperties().Where(p => p.GetCustomAttribute<ParameterAttribute>() is not null && p.CanWrite)];

    // A part for a component of this type: RadarSeries<string> for RadarChart<string>.
    static Type? Close(Type part, Type around)
    {
        if (!part.IsGenericTypeDefinition) return part;
        if (!around.IsGenericType || around.GetGenericArguments().Length != part.GetGenericArguments().Length) return null;
        try { return part.MakeGenericType(around.GetGenericArguments()); }
        catch (ArgumentException) { return null; }
    }

    // What a component's drawings have: from the first one, then one for each value of each option, and the
    // same again with rows when it takes items and drew with none.
    static (List<string> Always, HashSet<string> Ever, HashSet<string> Inside) Vary(IServiceProvider services, List<Assembly> library, Type type, List<PropertyInfo> parameters,
        Dictionary<string, object?> given, (List<string> Outermost, List<string> Inside) first)
    {
        var always = new List<string>(first.Outermost);
        var ever = new HashSet<string>(first.Outermost, StringComparer.Ordinal);
        var inside = new HashSet<string>(first.Inside, StringComparer.Ordinal);
        var ways = new List<Dictionary<string, object?>> { given };
        if (parameters.FirstOrDefault(p => p.Name == "Items") is { } items && Rows(items.PropertyType) is { } rows && (!given.TryGetValue("Items", out var has) || has is System.Collections.ICollection { Count: 0 }))
            ways.Add(new Dictionary<string, object?>(given) { ["Items"] = rows });
        foreach (var way in ways)
        {
            var tries = parameters.SelectMany(p => Values(p.PropertyType, library).Select(value => new Dictionary<string, object?>(way) { [p.Name] = value }));
            foreach (var tried in way == given ? tries : tries.Prepend(way))
            {
                if (Draw(services, type, tried, null) is not var (outermost, within)) continue;
                always.RemoveAll(mark => !outermost.Contains(mark));
                ever.UnionWith(outermost);
                inside.UnionWith(within);
            }
        }
        return (always, ever, inside);
    }

    // Thirty rows for an Items option: more than a page of them, each its own value where the type has them.
    static object? Rows(Type type)
    {
        if (!type.IsGenericType || type.GetGenericArguments() is not [var item] || !type.IsAssignableFrom(typeof(List<>).MakeGenericType(item))) return null;
        var rows = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(item))!;
        for (var i = 1; i <= 30; i++)
        {
            object? row = item == typeof(string) ? $"Item {i}" : item == typeof(int) ? i : item == typeof(DateOnly) ? new DateOnly(2026, 1, 1).AddDays(i)
                : item == typeof(TimeOnly) ? new TimeOnly(0, 0).AddMinutes(i) : item == typeof(TimeSpan) ? TimeSpan.FromMinutes(i) : item.IsValueType ? Activator.CreateInstance(item) : null;
            if (row is null && !item.IsValueType) return null;
            rows.Add(row);
        }
        return rows;
    }

    // Classes any element may carry: they mark no component.
    static readonly string[] Utilities = ["ib-visually-hidden", "ib-visually-hidden-focusable"];

    static readonly HashSet<string> Void = new(StringComparer.OrdinalIgnoreCase)
        { "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "source", "track", "wbr" };

    // The marks of the outermost elements of what the component draws (a sidebar is its backdrop and its nav),
    // and the marks of the elements inside them; null when it draws no element, or can't be drawn. A drawing
    // that doesn't end (a loop on a value it was given) is left behind after five seconds.
    static (List<string> Outermost, List<string> Inside)? Draw(IServiceProvider services, Type type, Dictionary<string, object?> given, Dictionary<Type, HashSet<string>>? said)
    {
        string? html = null;
        var drawing = Task.Run(async () =>
        {
            // The services a page has for itself (toasts, dialogs), new for each drawing.
            await using var scope = services.CreateAsyncScope();
            await using var renderer = new HtmlRenderer(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<ILoggerFactory>());
            html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync(type, ParameterView.FromDictionary(given))).ToHtmlString());
        });
        try
        {
            if (!drawing.Wait(TimeSpan.FromSeconds(5)) && html is null) return null;
        }
        catch (AggregateException failed)
        {
            // It said what it needs (a parent, a Label): the next way of drawing it may give that. One that
            // drew and then failed to close is still a drawing.
            if (html is null)
            {
                if (said is not null)
                {
                    var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
                    if (!said.TryGetValue(definition, out var needs)) said[definition] = needs = new HashSet<string>(StringComparer.Ordinal);
                    needs.Add(failed.GetBaseException().Message);
                }
                return null;
            }
        }
        if (html is null) return null;

        var (outermost, inside, depth) = (new List<string>(), new List<string>(), 0);
        for (var tag = Tag().Match(html); tag.Success; tag = tag.NextMatch())
        {
            var name = tag.Groups[2].Value.ToLowerInvariant();
            if (tag.Groups[1].Length > 0)
            {
                depth = Math.Max(0, depth - 1);
                continue;
            }
            // A stylesheet or script it brings is not its element, and what a script holds is not markup.
            if (name is "script" or "style")
            {
                var close = html.IndexOf($"</{name}", tag.Index + tag.Length, StringComparison.OrdinalIgnoreCase);
                if (close < 0) break;
                tag = Tag().Match(html, close);
                continue;
            }
            if (name != "link")
            {
                var marks = ClassAttribute().Match(tag.Value).Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(c => c.StartsWith("ib-", StringComparison.Ordinal) && !Utilities.Contains(c)).ToList();
                if (name.StartsWith("ib-", StringComparison.Ordinal)) marks.Insert(0, $"<{name}>");
                (depth == 0 ? outermost : inside).AddRange(marks);
            }
            if (!Void.Contains(name) && tag.Groups[3].Length == 0) depth++;
        }
        return outermost.Count == 0 && inside.Count == 0 && !Tag().IsMatch(html) ? null : ([.. outermost.Distinct()], [.. inside.Distinct()]);
    }

    // A generic component with each of the types its value may be, until one draws.
    static IEnumerable<Type> Closed(Type definition)
    {
        if (!definition.IsGenericTypeDefinition) return [definition];
        return new[] { typeof(string), typeof(int), typeof(DateOnly), typeof(TimeOnly), typeof(TimeSpan), typeof(List<DateOnly>), typeof(Dictionary<string, object?>) }.Select(argument =>
        {
            try { return definition.MakeGenericType([.. definition.GetGenericArguments().Select(_ => argument)]); }
            catch (ArgumentException) { return null; }
        }).Where(t => t is not null)!;
    }

    // Options a component refuses to draw without, in the ways tried in turn: none but a field's binding; the
    // names it asks for; those and what it shows or reads; those and what it is for; those and how it reads
    // its items, first the ones it marks as required and then all of them; and last the required ones with
    // one more (a tree's children or its parent key, where it takes one of the two).
    static IEnumerable<Dictionary<string, object?>> Fills(List<PropertyInfo> parameters, List<Assembly> library)
    {
        for (var way = 0; way <= 5; way++) yield return Fill(parameters, way, library);
        var all = Fill(parameters, 5, library);
        var required = Fill(parameters, 4, library);
        foreach (var (name, value) in all.Where(option => !required.ContainsKey(option.Key)))
            yield return new Dictionary<string, object?>(required) { [name] = value };
        // And with rows, for one that draws nothing of none (a chart with no spokes).
        if (parameters.FirstOrDefault(p => p.Name == "Items") is { } items && Rows(items.PropertyType) is { } rows)
            foreach (var filled in new[] { required, all }.Where(filled => filled.ContainsKey("Items")))
                yield return new Dictionary<string, object?>(filled) { ["Items"] = rows };
    }

    static Dictionary<string, object?> Fill(List<PropertyInfo> parameters, int way, List<Assembly> library)
    {
        var given = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var p in parameters)
        {
            var type = p.PropertyType;
            var function = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Expression<>) ? type.GetGenericArguments()[0] : type;
            if (p.Name == "ValueExpression" && type.IsGenericType)
            {
                var holder = Activator.CreateInstance(typeof(Holder<>).MakeGenericType(function.GetGenericArguments()[0]))!;
                given[p.Name] = Expression.Lambda(function, Expression.Field(Expression.Constant(holder), "Value"));
            }
            else if (way == 0) continue;
            else if (type == typeof(string) && p.Name is "Label" or "Title" or "Name" or "Alt") given[p.Name] = p.Name == "Alt" ? "" : "x";
            else if (p.Name is "Icon" or "Name" && Values(type, library).FirstOrDefault() is { } icon) given[p.Name] = icon;
            else if (p.Name == "Model" && type == typeof(object)) given[p.Name] = new object();
            // What it copies is its Text or the element it is For, never both.
            else if (way != 3 && way >= 2 && type == typeof(string) && p.Name is "Text" or "Field" or "Value") given[p.Name] = "x";
            else if (way == 3 && type == typeof(string) && p.Name == "For") given[p.Name] = "x";
            else if (way < 4) continue;
            else if (p.Name == "Items" && type.IsGenericType && type.GetGenericArguments() is [var item] && type.IsAssignableFrom(typeof(List<>).MakeGenericType(item)))
                given[p.Name] = Activator.CreateInstance(typeof(List<>).MakeGenericType(item));
            // A provider stands in place of Items: a component takes one of the two.
            else if (function.IsGenericType && function.Name.StartsWith("Func`", StringComparison.Ordinal) && !p.Name.EndsWith("Provider", StringComparison.Ordinal)
                && (way == 5 || p.Name == "ItemKey" || p.GetCustomAttribute<EditorRequiredAttribute>() is not null))
                given[p.Name] = function == type ? Lambda(function)?.Compile() : Lambda(function);
        }
        return given;
    }

    // x => x where what it gives may be what it takes (a row's key, its spoke: each row its own), else x => default.
    static LambdaExpression? Lambda(Type function)
    {
        try
        {
            var invoke = function.GetMethod("Invoke")!;
            var takes = invoke.GetParameters().Select(a => Expression.Parameter(a.ParameterType)).ToList();
            Expression gives = takes is [var one] && invoke.ReturnType != typeof(void) && invoke.ReturnType.IsAssignableFrom(one.Type) && !typeof(Delegate).IsAssignableFrom(one.Type)
                ? Expression.Convert(one, invoke.ReturnType) : Expression.Default(invoke.ReturnType);
            return Expression.Lambda(function, gives, takes);
        }
        catch (ArgumentException) { return null; }
    }

    // The values an option is tried with: both of a bool, each of an enum, the named values of a library
    // type, a number other than nothing, and for an option that takes a value at each width, a value of its
    // own kind. A class the shared options add (a gap, a margin) so shows as an option's, on every component.
    static IEnumerable<object> Values(Type type, List<Assembly> library)
    {
        var inner = Nullable.GetUnderlyingType(type) ?? type;
        if (inner == typeof(bool)) return [true, false];
        if (inner.IsEnum) return Enum.GetValues(inner).Cast<object>();
        if (inner == typeof(int)) return [2];
        if (inner == typeof(double)) return [2d];
        if (!library.Contains(inner.Assembly)) return [];
        // Responsive<Align> from an Align.
        if (inner.IsGenericType && inner.GetGenericArguments() is [var held] && inner.GetMethod("op_Implicit", [held]) is { } widen)
            return Values(held, library).Take(4).Select(value => widen.Invoke(null, [value])!);
        // Color.Primary on the type itself, Lucide.Pencil on a class of them, or Length.Rem(2).
        var holders = inner.GetProperties(BindingFlags.Public | BindingFlags.Static).Any(p => p.PropertyType == inner) || inner.GetFields(BindingFlags.Public | BindingFlags.Static).Any(f => f.FieldType == inner)
            ? [inner] : library.SelectMany(a => a.GetExportedTypes()).Where(t => t.IsAbstract && t.IsSealed);
        var named = holders.SelectMany(holder => holder.GetProperties(BindingFlags.Public | BindingFlags.Static).Where(p => p.PropertyType == inner).OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => p.GetValue(null))
            .Concat(holder.GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.FieldType == inner).OrderBy(f => f.Name, StringComparer.Ordinal).Select(f => f.GetValue(null))))
            .Where(v => v is not null).Take(16).ToList();
        if (named.Count > 0) return named!;
        return inner.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.ReturnType == inner && !m.IsSpecialName && m.GetParameters() is [{ ParameterType: var number }] && (number == typeof(double) || number == typeof(int)))
            .OrderBy(m => m.Name, StringComparer.Ordinal).Take(1).Select(m => m.Invoke(null, [Convert.ChangeType(2, m.GetParameters()[0].ParameterType)])!);
    }

    public sealed class Holder<T>
    {
        public T Value = default!;
    }

    sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("http://localhost/", "http://localhost/");

        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }

    sealed class NoScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => default;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => default;
    }

    sealed class NoPosts : IFormValueMapper
    {
        public bool CanMap(Type valueType, string scopeName, string? formName) => false;

        public void Map(FormValueMappingContext context) { }
    }

    sealed class NoErrors : IErrorBoundaryLogger
    {
        public ValueTask LogErrorAsync(Exception exception) => default;
    }

    [GeneratedRegex(@"<(/?)([a-zA-Z][a-zA-Z0-9-]*)\b(?:""[^""]*""|'[^']*'|[^>""'])*?(/?)>", RegexOptions.Singleline)]
    private static partial Regex Tag();

    [GeneratedRegex("\\bclass=\"([^\"]*)\"")]
    private static partial Regex ClassAttribute();
}
