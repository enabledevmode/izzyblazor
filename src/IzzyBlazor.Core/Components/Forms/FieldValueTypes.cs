using System.Diagnostics.CodeAnalysis;

namespace IzzyBlazor;

// Blazor's BindConverter formats and parses a field's value, and it can fall back on the value
// type's TypeConverter, so a trimmed app must keep that type's members. FieldBase asks for them
// on TValue, and every generic field passes the request on (the multi-choice fields for TItem too,
// whose values are formatted the same way). A .razor file can't put an attribute
// on its type parameter, so these partial declarations add it (C# merges them with the
// component's own).

/// <summary>A native select, with options from Items or an enum.</summary>
public partial class Select<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>;

/// <summary>Radio buttons for one choice, from Items or an enum.</summary>
public partial class RadioGroup<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>;

/// <summary>A row of segments for one choice, from Items or an enum.</summary>
public partial class SegmentedControl<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>;

/// <summary>A select you can type in to filter the options.</summary>
public partial class Combobox<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>;

/// <summary>A number field with optional stepper buttons.</summary>
public partial class NumberField<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>;

/// <summary>A range slider.</summary>
public partial class Slider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>;

/// <summary>A star rating: a radio for each star, or a read-only display of any value.</summary>
public partial class Rating<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>;

/// <summary>A select for several choices, bound to a collection.</summary>
public partial class MultiSelect<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>;

/// <summary>Checkboxes for several choices, bound to a collection.</summary>
public partial class CheckboxGroup<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>;

/// <summary>
/// Lets the user type a date, or a date and time, or choose it from a calendar. Bind it to
/// <c>DateOnly</c>, <c>DateTime</c> or <c>DateTimeOffset</c>.
/// </summary>
public partial class DateField<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>;

/// <inheritdoc cref="DateField{TValue}"/>
public partial class MultiDateField<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>;

/// <summary>
/// Lets the user type a time of day in the browser's own time input, or choose one from a list.
/// Bind it to <c>TimeOnly</c> or <c>TimeOnly?</c>.
/// </summary>
public partial class TimeField<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>;

/// <summary>
/// Shows a month on the page for the user to choose a day from, with the browser's own date input
/// posting the day. Bind it to <c>DateOnly</c> or <c>DateOnly?</c>.
/// </summary>
public partial class Calendar<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>;

/// <summary>
/// Lets the user enter a length of time, such as a timeout, with a number box for each unit. Bind it
/// to <c>TimeSpan</c> or <c>TimeSpan?</c>.
/// </summary>
public partial class DurationField<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>;
