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

/// <summary>A select for several choices, bound to a collection.</summary>
public partial class MultiSelect<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>;

/// <summary>Checkboxes for several choices, bound to a collection.</summary>
public partial class CheckboxGroup<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>;
