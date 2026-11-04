using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>Option lists shared by the choice fields (Select, RadioGroup, Combobox, CheckboxGroup, MultiSelect).</summary>
internal static class ChoiceOptions
{
    /// <summary>The given items, or every value of an enum (or nullable enum) type.</summary>
    public static IEnumerable<T> Of<T>(IEnumerable<T>? items)
    {
        if (items is not null)
        {
            return items;
        }

        var type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return type.IsEnum ? Enum.GetValues(type).Cast<object>().Select(v => (T)v) : [];
    }

    /// <summary>An option's text: <paramref name="itemText"/>, an enum value's <c>[Display(Name)]</c>, or its string form.</summary>
    public static string Text<T>(T item, Func<T, string>? itemText)
    {
        if (itemText is not null)
        {
            return itemText(item);
        }

        return item is Enum e ? EnumText(e) : item?.ToString() ?? "";
    }

    /// <summary>An enum value's <c>[Display(Name)]</c>, or its name.</summary>
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "The trimmer keeps every field of an enum it keeps, and their [Display] attributes when DisplayAttribute is kept, which this method's use of it ensures.")]
    public static string EnumText(Enum e)
    {
        var member = e.GetType().GetMember(e.ToString()).FirstOrDefault();
        var display = member?.GetCustomAttributes(typeof(DisplayAttribute), false).OfType<DisplayAttribute>().FirstOrDefault();
        return display?.GetName() ?? e.ToString();
    }

    /// <summary>An option's group: <paramref name="itemGroup"/>, or an enum value's <c>[Display(GroupName)]</c>. Null for none.</summary>
    public static string? Group<T>(T item, Func<T, string?>? itemGroup) =>
        itemGroup is not null ? itemGroup(item) : item is Enum e ? EnumGroup(e) : null;

    /// <summary>An enum value's <c>[Display(GroupName)]</c>, or null.</summary>
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "As EnumText: the enum's fields and their [Display] attributes are kept.")]
    public static string? EnumGroup(Enum e) =>
        e.GetType().GetMember(e.ToString()).FirstOrDefault()?.GetCustomAttributes(typeof(DisplayAttribute), false).OfType<DisplayAttribute>().FirstOrDefault()?.GetGroupName();

    /// <summary>
    /// The options in groups: those with no group first (under no header), then each group in the order
    /// it first appears, its options in their order. One run of everything when nothing has a group.
    /// </summary>
    public static IReadOnlyList<(string? Group, IReadOnlyList<T> Items)> Grouped<T>(IEnumerable<T> items, Func<T, string?>? itemGroup)
    {
        var groups = new List<(string? Group, List<T> Items)>();
        var ungrouped = new List<T>();
        foreach (var item in items)
        {
            var group = Group(item, itemGroup);
            if (string.IsNullOrEmpty(group))
            {
                ungrouped.Add(item);
                continue;
            }

            var at = groups.FindIndex(g => g.Group == group);
            if (at < 0)
            {
                groups.Add((group, [item]));
            }
            else
            {
                groups[at].Items.Add(item);
            }
        }

        var result = new List<(string?, IReadOnlyList<T>)>();
        if (ungrouped.Count > 0 || groups.Count == 0)
        {
            result.Add((null, ungrouped));
        }

        result.AddRange(groups.Select(g => ((string?)g.Group, (IReadOnlyList<T>)g.Items)));
        return result;
    }

    /// <summary>An option's form value, the way Blazor binds it back (an enum's name, a number in the current culture).</summary>
    public static string Value<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(T item) => BindConverter.FormatValue(item, CultureInfo.CurrentCulture)?.ToString() ?? "";
}

/// <summary>
/// Reads and builds the collection a multi-choice field is bound to, whatever its type: an array,
/// <c>List&lt;T&gt;</c>, <c>HashSet&lt;T&gt;</c>, or an interface either implements.
/// </summary>
internal static class ChoiceCollection<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TValue, TItem>
{
    public static IEnumerable<TItem> Items(TValue? value) => value as IEnumerable<TItem> ?? [];

    public static TValue Create(IEnumerable<TItem> items)
    {
        var list = items.ToList();
        var type = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);
        object result;
        if (type == typeof(TItem[]))
        {
            result = list.ToArray();
        }
        else if (type.IsAssignableFrom(typeof(List<TItem>)))
        {
            result = list;
        }
        else if (type.IsAssignableFrom(typeof(HashSet<TItem>)))
        {
            result = new HashSet<TItem>(list);
        }
        else if (type == typeof(TValue) && !type.IsAbstract && Activator.CreateInstance<TValue>() is ICollection<TItem> collection)
        {
            foreach (var item in list)
            {
                collection.Add(item);
            }

            result = collection;
        }
        else
        {
            throw new NotSupportedException($"A multi-choice field can't create a {type.Name}. Bind it to an array, List<{typeof(TItem).Name}> or HashSet<{typeof(TItem).Name}>.");
        }

        return (TValue)result;
    }
}
