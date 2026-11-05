namespace IzzyBlazor;

/// <summary>
/// The text IzzyBlazor's components show or announce when you don't set it on the component:
/// "Close", "Loading", "Next page" and so on. English by default. Most are also a parameter on
/// the components that use it, which wins over this.
/// </summary>
/// <remarks>
/// For one language, register an instance with the strings changed:
/// <code>
/// builder.Services.AddSingleton(new IzzyText { Close = "Schließen", Loading = "Wird geladen" });
/// </code>
/// For several, subclass it and override the getters to read your resources for the current UI
/// culture (they're read on every render, so a per-request culture works):
/// <code>
/// public sealed class AppText(IStringLocalizer&lt;AppText&gt; l) : IzzyText
/// {
///     public override string Close =&gt; l["Close"];
/// }
/// builder.Services.AddSingleton&lt;IzzyText, AppText&gt;();
/// </code>
/// Strings with <c>{0}</c> are format strings.
/// </remarks>
public class IzzyText
{
    /// <summary>A new copy of the built-in English text, to start from or compare with.</summary>
    public static IzzyText English => new();

    // The fallback components use when the app registers none. Internal, so no app code can change
    // it for everyone in the process.
    private static readonly IzzyText Fallback = new();

    /// <summary>The app's registered text, or English.</summary>
    internal static IzzyText From(IServiceProvider services) =>
        services.GetService(typeof(IzzyText)) as IzzyText ?? Fallback;

    // Shared

    /// <summary>A close button. Dialog, Popover.</summary>
    public virtual string Close { get; set; } = "Close";

    /// <summary>A dismiss button. Alert, toasts.</summary>
    public virtual string Dismiss { get; set; } = "Dismiss";

    /// <summary>Table's page size menu: "Rows per page".</summary>
    public virtual string RowsPerPage { get; set; } = "Rows per page";

    /// <summary>A DataGrid header's place in a sort of several columns, ascending: "Sort 2, ascending".</summary>
    public virtual string SortOrderAscending { get; set; } = "Sort {0}, ascending";

    /// <summary>A DataGrid header's place in a sort of several columns, descending: "Sort 2, descending".</summary>
    public virtual string SortOrderDescending { get; set; } = "Sort {0}, descending";

    /// <summary>A DataGrid header's menu button: "Options for Customer". {0} is the column's title.</summary>
    public virtual string ColumnOptions { get; set; } = "Options for {0}";

    /// <summary>A DataGrid column menu: moves the column one place toward the start.</summary>
    public virtual string ColumnMoveStart { get; set; } = "Move left";

    /// <summary>A DataGrid column menu: moves the column one place toward the end.</summary>
    public virtual string ColumnMoveEnd { get; set; } = "Move right";

    /// <summary>A DataGrid column menu: widens the column a step.</summary>
    public virtual string ColumnWider { get; set; } = "Wider";

    /// <summary>A DataGrid column menu: narrows the column a step.</summary>
    public virtual string ColumnNarrower { get; set; } = "Narrower";

    /// <summary>A DataGrid column menu: back to the column's declared width.</summary>
    public virtual string ColumnResetWidth { get; set; } = "Reset width";

    /// <summary>A DataGrid column menu: freezes the column at the grid's start.</summary>
    public virtual string ColumnFreezeStart { get; set; } = "Freeze at start";

    /// <summary>A DataGrid column menu: freezes the column at the grid's end.</summary>
    public virtual string ColumnFreezeEnd { get; set; } = "Freeze at end";

    /// <summary>A DataGrid column menu: unfreezes the column.</summary>
    public virtual string ColumnUnfreeze { get; set; } = "Unfreeze";

    /// <summary>Said when a DataGrid column is resized: "Customer: 180 pixels". {0} is its title, {1} its width.</summary>
    public virtual string ColumnWidth { get; set; } = "{0}: {1} pixels";

    /// <summary>The name of a DataGrid's column of row menus, for screen readers.</summary>
    public virtual string RowActions { get; set; } = "Actions";

    /// <summary>The name of a DataGrid's toolbar, after its caption: "Orders: Tools".</summary>
    public virtual string GridToolbar { get; set; } = "Tools";

    /// <summary>A DataGrid's menu of export links.</summary>
    public virtual string Export { get; set; } = "Export";

    /// <summary>A DataGrid's link to its rows as CSV.</summary>
    public virtual string ExportCsv { get; set; } = "Export CSV";

    /// <summary>A DataGrid's link to its rows as an Excel workbook.</summary>
    public virtual string ExportExcel { get; set; } = "Export Excel";

    /// <summary>A DataGrid's column picker button.</summary>
    public virtual string ColumnPicker { get; set; } = "Columns";

    /// <summary>A DataGrid's column picker: back to the columns as declared.</summary>
    public virtual string ColumnsReset { get; set; } = "Reset layout";

    /// <summary>A Table filter's accessible name: "Filter Customer".</summary>
    public virtual string FilterColumn { get; set; } = "Filter {0}";

    /// <summary>A Table filter menu with several values chosen: "2 selected".</summary>
    public virtual string FilterSelected { get; set; } = "{0} selected";

    /// <summary>A column filter's operator, for screen readers.</summary>
    public virtual string FilterOperatorLabel { get; set; } = "Operator";

    /// <summary>A column filter's one value, for screen readers.</summary>
    public virtual string FilterValueLabel { get; set; } = "Value";

    /// <summary>A text filter's operator.</summary>
    public virtual string FilterContains { get; set; } = "Contains";

    /// <summary>A text filter's operator.</summary>
    public virtual string FilterDoesNotContain { get; set; } = "Does not contain";

    /// <summary>A text or number filter's operator.</summary>
    public virtual string FilterEquals { get; set; } = "Equals";

    /// <summary>A text or number filter's operator.</summary>
    public virtual string FilterDoesNotEqual { get; set; } = "Does not equal";

    /// <summary>A text filter's operator.</summary>
    public virtual string FilterStartsWith { get; set; } = "Starts with";

    /// <summary>A text filter's operator.</summary>
    public virtual string FilterEndsWith { get; set; } = "Ends with";

    /// <summary>A number filter's operator.</summary>
    public virtual string FilterGreaterThan { get; set; } = "Greater than";

    /// <summary>A number filter's operator.</summary>
    public virtual string FilterGreaterOrEqual { get; set; } = "Greater or equal";

    /// <summary>A number filter's operator.</summary>
    public virtual string FilterLessThan { get; set; } = "Less than";

    /// <summary>A number filter's operator.</summary>
    public virtual string FilterLessOrEqual { get; set; } = "Less or equal";

    /// <summary>A date filter's operator.</summary>
    public virtual string FilterOn { get; set; } = "On";

    /// <summary>A date filter's operator.</summary>
    public virtual string FilterNotOn { get; set; } = "Not on";

    /// <summary>A date filter's operator.</summary>
    public virtual string FilterAfter { get; set; } = "After";

    /// <summary>A date filter's operator.</summary>
    public virtual string FilterOnOrAfter { get; set; } = "On or after";

    /// <summary>A date filter's operator.</summary>
    public virtual string FilterBefore { get; set; } = "Before";

    /// <summary>A date filter's operator.</summary>
    public virtual string FilterOnOrBefore { get; set; } = "On or before";

    /// <summary>A number or date filter's operator.</summary>
    public virtual string FilterIsBetween { get; set; } = "Between";

    /// <summary>A column filter's operator.</summary>
    public virtual string FilterIsEmpty { get; set; } = "Is empty";

    /// <summary>A column filter's operator.</summary>
    public virtual string FilterIsNotEmpty { get; set; } = "Is not empty";

    /// <summary>A date filter's lower end: "From".</summary>
    public virtual string FilterFrom { get; set; } = "From";

    /// <summary>A date filter's upper end: "To".</summary>
    public virtual string FilterTo { get; set; } = "To";

    /// <summary>A Table number filter's lower end: "Min".</summary>
    public virtual string FilterMin { get; set; } = "Min";

    /// <summary>A Table number filter's upper end: "Max".</summary>
    public virtual string FilterMax { get; set; } = "Max";

    /// <summary>A Table number filter with only a lower end: "100 or more".</summary>
    public virtual string FilterAtLeast { get; set; } = "{0} or more";

    /// <summary>A Table number filter with only an upper end: "500 or less".</summary>
    public virtual string FilterAtMost { get; set; } = "{0} or less";

    /// <summary>A Table number filter with both ends: "100–500".</summary>
    public virtual string FilterBetween { get; set; } = "{0}–{1}";

    /// <summary>A table's FilterPanel with nothing filtered.</summary>
    public virtual string FiltersNone { get; set; } = "No filters";

    /// <summary>The way back from a table with no rows while filters are active.</summary>
    public virtual string ClearFilters { get; set; } = "Clear filters";

    /// <summary>A list filter's row for no value.</summary>
    public virtual string FilterBlank { get; set; } = "(Blank)";

    /// <summary>What a list filter with nothing ticked is set to: "Status: none".</summary>
    public virtual string FilterNone { get; set; } = "none";

    /// <summary>The empty message of a table with a list filter that has nothing ticked; {0} is the column's title.</summary>
    public virtual string FilterNoneEmpty { get; set; } = "Nothing is selected under {0}.";

    /// <summary>A filter row's button that clears a column's filter; {0} is the column's title.</summary>
    public virtual string FilterClearColumn { get; set; } = "Clear the {0} filter";

    /// <summary>A list filter's box for narrowing its rows.</summary>
    public virtual string FilterSearchValues { get; set; } = "Search values";

    /// <summary>A list filter's Select all while a search narrows it; {0} is how many rows it found.</summary>
    public virtual string FilterSelectAllResults { get; set; } = "Select all {0} results";

    /// <summary>What a list filter with some rows unticked is set to; {0} is the value, or how many: "All but Ada", "All but 3".</summary>
    public virtual string FilterAllBut { get; set; } = "All but {0}";

    /// <summary>FilterBuilder's name for screen readers.</summary>
    public virtual string FilterBuilderLabel { get; set; } = "Filter builder";

    /// <summary>The row of filters' button that opens a FilterBuilder.</summary>
    public virtual string FilterEdit { get; set; } = "Edit";

    /// <summary>A tag for a filter in the address that can't be read, which shows no rows.</summary>
    public virtual string FilterUnreadable { get; set; } = "A filter in the address can't be read";

    /// <summary>A FilterBuilder group's name.</summary>
    public virtual string FilterGroupLabel { get; set; } = "Group";

    /// <summary>A FilterBuilder rule's field.</summary>
    public virtual string FilterFieldLabel { get; set; } = "Field";

    /// <summary>What names a FilterBuilder group's And and Or.</summary>
    public virtual string FilterMatchLabel { get; set; } = "Match";

    /// <summary>A FilterBuilder group matching every rule.</summary>
    public virtual string FilterAnd { get; set; } = "And";

    /// <summary>A FilterBuilder group matching any rule.</summary>
    public virtual string FilterOr { get; set; } = "Or";

    /// <summary>FilterBuilder's button that adds a rule.</summary>
    public virtual string FilterAddRule { get; set; } = "Add rule";

    /// <summary>FilterBuilder's button that adds a group.</summary>
    public virtual string FilterAddGroup { get; set; } = "Add group";

    /// <summary>FilterBuilder's remove button; {0} is what it removes.</summary>
    public virtual string FilterRemove { get; set; } = "Remove {0}";

    /// <summary>A FilterBuilder operator for a list of values.</summary>
    public virtual string FilterIsAnyOf { get; set; } = "Is any of";

    /// <summary>A FilterBuilder operator for a list of values left out.</summary>
    public virtual string FilterIsNoneOf { get; set; } = "Is none of";

    /// <summary>A filter row's list with nothing unticked.</summary>
    public virtual string FilterAll { get; set; } = "All";



    /// <summary>A list filter whose search found nothing.</summary>
    public virtual string FilterNoValues { get; set; } = "No values found";

    /// <summary>A list filter with more rows than it will show or select.</summary>
    public virtual string FilterKeepTyping { get; set; } = "Keep typing to see more";

    /// <summary>A list filter whose values failed to load, before a Retry button.</summary>
    public virtual string FilterValuesFailed { get; set; } = "Couldn't load the values.";

    /// <summary>A list filter holding as many values as it can; {0} is how many.</summary>
    public virtual string FilterValuesLimit { get; set; } = "A filter can hold {0} values at most.";

    /// <summary>A Table filter option for true.</summary>
    public virtual string Yes { get; set; } = "Yes";

    /// <summary>A Table filter option for false.</summary>
    public virtual string No { get; set; } = "No";

    /// <summary>A Banner's region name.</summary>
    public virtual string Announcement { get; set; } = "Announcement";

    /// <summary>The confirm button in DialogService.ConfirmAsync.</summary>
    public virtual string Ok { get; set; } = "OK";

    /// <summary>ConfirmButton's confirm button.</summary>
    public virtual string Confirm { get; set; } = "Confirm";

    /// <summary>Cancel in ConfirmButton's dialog and DialogService.ConfirmAsync.</summary>
    public virtual string Cancel { get; set; } = "Cancel";

    /// <summary>A DataGrid editor's title, before it knows its row.</summary>
    public virtual string Edit { get; set; } = "Edit";

    /// <summary>A DataGrid row's edit button and editor. {0} is the row's label.</summary>
    public virtual string EditItem { get; set; } = "Edit {0}";

    /// <summary>A DataGrid's Add button, and its editor's title for a new row.</summary>
    public virtual string Add { get; set; } = "Add";

    /// <summary>A DataGrid's delete button in its confirmation.</summary>
    public virtual string Delete { get; set; } = "Delete";

    /// <summary>A DataGrid row's delete action. {0} is the row's label.</summary>
    public virtual string DeleteItem { get; set; } = "Delete {0}";

    /// <summary>A DataGrid's delete confirmation's title. {0} is the row's label.</summary>
    public virtual string DeleteConfirm { get; set; } = "Delete {0}?";

    /// <summary>A DataGrid's delete confirmation.</summary>
    public virtual string DeleteWarning { get; set; } = "This can't be undone.";

    /// <summary>A DataGrid editor's save button.</summary>
    public virtual string Save { get; set; } = "Save";

    /// <summary>A DataGrid editor, when a save would change the row's key.</summary>
    public virtual string EditKeyChanged { get; set; } = "A row's key can't be changed.";

    /// <summary>A DataGrid editor, when its row isn't on the page, or can't be edited.</summary>
    public virtual string EditRowGone { get; set; } = "That row isn't available here, so nothing was changed.";

    /// <summary>A Toolbar's "…" button, which holds what doesn't fit.</summary>
    public virtual string MoreActions { get; set; } = "More actions";

    /// <summary>A SplitButton's arrow, and its submenu in a Toolbar's "…" menu. {0} is its Label.</summary>
    public virtual string SplitButtonMenu { get; set; } = "{0}: more options";

    /// <summary>Announced while busy. Button Loading, Spinner.</summary>
    public virtual string Loading { get; set; } = "Loading";

    /// <summary>Before an item's name on its remove button: "Remove Blazor". Tag, MultiSelect chips.</summary>
    public virtual string Remove { get; set; } = "Remove";

    // Forms

    /// <summary>FormErrors' heading.</summary>
    public virtual string FormErrorsTitle { get; set; } = "Please fix the following";

    /// <summary>For screen readers, MultiSelect's chosen options, read after its name: "Chosen: {0}." {0} is the options' text, joined with commas.</summary>
    public virtual string Chosen { get; set; } = "Chosen: {0}.";

    /// <summary>Combobox and MultiSelect when nothing matches.</summary>
    public virtual string NoMatches { get; set; } = "No matches";

    /// <summary>The box above a CheckboxGroup's options that checks or clears them all (<c>SelectAll</c>).</summary>
    public virtual string SelectAll { get; set; } = "Select all";

    /// <summary>A Rating's first star, for screen readers.</summary>
    public virtual string RatingOneStar { get; set; } = "1 star";

    /// <summary>A Rating's other stars, for screen readers: "3 stars". {0} is the number.</summary>
    public virtual string RatingStars { get; set; } = "{0} stars";

    /// <summary>A Rating's empty choice, for screen readers, on a field that can be empty.</summary>
    public virtual string RatingNone { get; set; } = "No rating";

    /// <summary>A read-only Rating, for screen readers: "4.5 out of 5 stars". {0} is the value, {1} the number of stars.</summary>
    public virtual string RatingOutOf { get; set; } = "{0} out of {1} stars";

    /// <summary>Combobox while searching on the server.</summary>
    public virtual string Searching { get; set; } = "Searching…";

    /// <summary>After the label of a field that isn't required, in a Form with <c>RequiredMark.Optional</c>.</summary>
    public virtual string Optional { get; set; } = "(optional)";

    /// <summary>
    /// For screen readers, after the label of a required group whose boxes can't be marked
    /// required (a CheckboxGroup): its asterisk is hidden from them.
    /// </summary>
    public virtual string Required { get; set; } = "(required)";

    /// <summary>For screen readers, after a tab's label when one field in it has an error.</summary>
    public virtual string OneError { get; set; } = "1 error";

    /// <summary>For screen readers, after a tab's label when fields in it have errors: "{0} errors".</summary>
    public virtual string ErrorCount { get; set; } = "{0} errors";

    /// <summary>A field's message for a value it can't read as its type. {0} is the field's label.</summary>
    public virtual string ValueInvalid { get; set; } = "The {0} field is not valid.";

    /// <summary>NumberField's and Slider's message for a value that isn't a number. {0} is the field's label.</summary>
    public virtual string NumberInvalid { get; set; } = "The {0} field must be a number.";

    /// <summary>DateField's message for a value that isn't a date. {0} is the field's label.</summary>
    public virtual string DateInvalid { get; set; } = "The {0} field must be a date.";

    /// <summary>PinField's message for a code that isn't all digits or isn't the right length. {0} is the field's label, {1} the length.</summary>
    public virtual string PinDigits { get; set; } = "The {0} field must be {1} digits.";

    /// <summary>PinField's message, with Letters, for a code that isn't letters and digits or isn't the right length. {0} is the field's label, {1} the length.</summary>
    public virtual string PinCharacters { get; set; } = "The {0} field must be {1} letters or digits.";

    /// <summary>TimeField's message for a value that isn't a time. {0} is the field's label.</summary>
    public virtual string TimeInvalid { get; set; } = "The {0} field must be a time.";

    /// <summary>DateRangeField's caption for its first date.</summary>
    public virtual string DateRangeStart { get; set; } = "Start";

    /// <summary>DateRangeField's caption for its second date.</summary>
    public virtual string DateRangeEnd { get; set; } = "End";

    /// <summary>A required DateRangeField with one end; {0} is its label.</summary>
    public virtual string DateRangeIncomplete { get; set; } = "{0} needs a start and an end.";

    /// <summary>A DateRangeField that ends before it starts; {0} is its label.</summary>
    public virtual string DateRangeReversed { get; set; } = "{0} can't end before it starts.";

    /// <summary>A range under a DateRangeField's MinDays; {0} is its label and {1} the days.</summary>
    public virtual string DateRangeTooShort { get; set; } = "{0} must span at least {1} days.";

    /// <summary>A range over a DateRangeField's MaxDays; {0} is its label and {1} the days.</summary>
    public virtual string DateRangeTooLong { get; set; } = "{0} can span at most {1} days.";

    /// <summary>A range with a day its DateDisabled turns off inside it; {0} is its label and {1} the date.</summary>
    public virtual string DateRangeHasUnavailable { get; set; } = "{0} can't include {1}.";

    /// <summary>A date or time a field's DateDisabled or TimeDisabled turns off; {0} is its label and {1} the value.</summary>
    public virtual string DateUnavailable { get; set; } = "{0} can't be {1}.";

    /// <summary>A calendar's button to the month before.</summary>
    public virtual string CalendarPrevious { get; set; } = "Previous month";

    /// <summary>A calendar's button to the month after.</summary>
    public virtual string CalendarNext { get; set; } = "Next month";

    /// <summary>A calendar's select of months.</summary>
    public virtual string CalendarMonth { get; set; } = "Month";

    /// <summary>A calendar's select of years.</summary>
    public virtual string CalendarYear { get; set; } = "Year";

    /// <summary>The heading of a calendar's week numbers, for screen readers.</summary>
    public virtual string CalendarWeek { get; set; } = "Week";

    /// <summary>The heading shown over a calendar's week numbers, in a day's width.</summary>
    public virtual string CalendarWeekText { get; set; } = "Wk";

    /// <summary>A date field's button that opens its calendar.</summary>
    public virtual string CalendarChoose { get; set; } = "Choose date";

    /// <summary>That button once there's a date; {0} is the date in full.</summary>
    public virtual string CalendarChange { get; set; } = "Change date, {0}";

    /// <summary>A calendar's box for the hour.</summary>
    public virtual string CalendarHour { get; set; } = "Hour";

    /// <summary>A calendar's box for the minute.</summary>
    public virtual string CalendarMinute { get; set; } = "Minute";

    /// <summary>A calendar's box for the second.</summary>
    public virtual string CalendarSecond { get; set; } = "Second";

    /// <summary>A calendar's choice of AM or PM.</summary>
    public virtual string CalendarDayPart { get; set; } = "AM or PM";

    /// <summary>The name of a calendar's group of time boxes.</summary>
    public virtual string CalendarTime { get; set; } = "Time";

    /// <summary>The name of a date range field's group of presets.</summary>
    public virtual string CalendarPresets { get; set; } = "Ranges";

    /// <summary>A preset range from <see cref="DateRangePreset.Common"/>.</summary>
    public virtual string PresetToday { get; set; } = "Today";

    /// <summary>A preset range from <see cref="DateRangePreset.Common"/>.</summary>
    public virtual string PresetLast7Days { get; set; } = "Last 7 days";

    /// <summary>A preset range from <see cref="DateRangePreset.Common"/>.</summary>
    public virtual string PresetLast30Days { get; set; } = "Last 30 days";

    /// <summary>A preset range from <see cref="DateRangePreset.Common"/>.</summary>
    public virtual string PresetThisMonth { get; set; } = "This month";

    /// <summary>A preset range from <see cref="DateRangePreset.Common"/>.</summary>
    public virtual string PresetLastMonth { get; set; } = "Last month";

    /// <summary>A preset range from <see cref="DateRangePreset.Common"/>.</summary>
    public virtual string PresetThisYear { get; set; } = "This year";

    /// <summary>A time field's button that opens its list.</summary>
    public virtual string TimeChoose { get; set; } = "Choose time";

    /// <summary>That button once there's a time; {0} is the time.</summary>
    public virtual string TimeChange { get; set; } = "Change time, {0}";

    /// <summary>The name of a time field's list.</summary>
    public virtual string TimeList { get; set; } = "Times";

    /// <summary>A date range field's button that opens its months.</summary>
    public virtual string CalendarChooseRange { get; set; } = "Choose dates";

    /// <summary>That button once both days are set; {0} is both in full.</summary>
    public virtual string CalendarChangeRange { get; set; } = "Change dates, {0}";

    /// <summary>Announced as a day joins a MultiDateField's dates, with {0} the day.</summary>
    public virtual string CalendarAdded { get; set; } = "{0} added";

    /// <summary>Announced as a day leaves a MultiDateField's dates, with {0} the day.</summary>
    public virtual string CalendarRemoved { get; set; } = "{0} removed";

    /// <summary>The message for a required month or year with none, with {0} its label.</summary>
    public virtual string ChoiceRequired { get; set; } = "{0} needs a choice.";

    /// <summary>The message for a required MultiDateField with no dates, with {0} its label.</summary>
    public virtual string DatesRequired { get; set; } = "{0} needs at least one date.";

    /// <summary>The name of a month picker's button, with no month yet, and what it shows.</summary>
    public virtual string CalendarChooseMonth { get; set; } = "Choose month";

    /// <summary>The name of a month picker's button, with {0} the month.</summary>
    public virtual string CalendarChangeMonth { get; set; } = "Change month, {0}";

    /// <summary>The name of a year picker's button, with no year yet.</summary>
    public virtual string CalendarChooseYear { get; set; } = "Choose year";

    /// <summary>The name of a year picker's button, with {0} the year.</summary>
    public virtual string CalendarChangeYear { get; set; } = "Change year, {0}";

    /// <summary>The button for the page of months a year before.</summary>
    public virtual string CalendarPreviousYear { get; set; } = "Previous year";

    /// <summary>The button for the page of months a year after.</summary>
    public virtual string CalendarNextYear { get; set; } = "Next year";

    /// <summary>The button for the page of years before.</summary>
    public virtual string CalendarEarlierYears { get; set; } = "Earlier years";

    /// <summary>The button for the page of years after.</summary>
    public virtual string CalendarLaterYears { get; set; } = "Later years";

    /// <summary>A date range's length, under its months; {0} is how many days.</summary>
    public virtual string CalendarDays { get; set; } = "{0} days";

    /// <summary>A date range of one day, under its months.</summary>
    public virtual string CalendarOneDay { get; set; } = "1 day";

    /// <summary>Under a date range's months, while its first day waits for its second.</summary>
    public virtual string CalendarPickEnd { get; set; } = "Choose the last day";

    /// <summary>A date or time before the field's Min; {0} is its label and {1} the limit.</summary>
    public virtual string TooEarly { get; set; } = "{0} must be {1} or later.";

    /// <summary>A date or time after the field's Max; {0} is its label and {1} the limit.</summary>
    public virtual string TooLate { get; set; } = "{0} must be {1} or earlier.";

    /// <summary>A time outside a window across midnight; {0} is the field's label, {1} its Min and {2} its Max.</summary>
    public virtual string TimeOutsideWindow { get; set; } = "{0} must be between {1} and {2}.";

    /// <summary>DurationField's message for a value that isn't a length of time. {0} is the field's label.</summary>
    public virtual string DurationInvalid { get; set; } = "The {0} field must be a length of time.";

    /// <summary>A length under the field's Min; {0} is its label and {1} the limit.</summary>
    public virtual string DurationTooShort { get; set; } = "{0} must be at least {1}.";

    /// <summary>A length over the field's Max; {0} is its label and {1} the limit.</summary>
    public virtual string DurationTooLong { get; set; } = "{0} must be at most {1}.";

    /// <summary>How to type a length, where DurationField's boxes can't show.</summary>
    public virtual string DurationFormat { get; set; } = "As hh:mm:ss, or d.hh:mm:ss with days.";

    /// <summary>DurationField's box for days.</summary>
    public virtual string DurationDays { get; set; } = "Days";

    /// <summary>DurationField's box for hours.</summary>
    public virtual string DurationHours { get; set; } = "Hours";

    /// <summary>DurationField's box for minutes.</summary>
    public virtual string DurationMinutes { get; set; } = "Minutes";

    /// <summary>DurationField's box for seconds.</summary>
    public virtual string DurationSeconds { get; set; } = "Seconds";

    /// <summary>DurationField's button that makes the length negative.</summary>
    public virtual string DurationNegative { get; set; } = "Negative";

    /// <summary>The button that opens a Combobox or MultiSelect list.</summary>
    public virtual string ShowOptions { get; set; } = "Show options";

    /// <summary>NumberField's increase button.</summary>
    public virtual string Increase { get; set; } = "Increase";

    /// <summary>NumberField's decrease button.</summary>
    public virtual string Decrease { get; set; } = "Decrease";

    /// <summary>A password field's reveal button (a toggle, pressed while the password shows).</summary>
    public virtual string ShowPassword { get; set; } = "Show password";

    /// <summary>A barcode's accessible name. {0} is its value.</summary>
    public virtual string BarcodeLabel { get; set; } = "Barcode {0}";

    /// <summary>Under a password field while Caps Lock is on.</summary>
    public virtual string CapsLockOn { get; set; } = "Caps Lock is on.";

    /// <summary>A password requirement: the fewest characters. {0} is the number.</summary>
    public virtual string PasswordLength { get; set; } = "At least {0} characters";

    /// <summary>A password requirement: a lowercase letter.</summary>
    public virtual string PasswordLowercase { get; set; } = "A lowercase letter";

    /// <summary>A password requirement: an uppercase letter.</summary>
    public virtual string PasswordUppercase { get; set; } = "An uppercase letter";

    /// <summary>A password requirement: a digit.</summary>
    public virtual string PasswordDigit { get; set; } = "A number";

    /// <summary>A password requirement: a character that's neither a letter nor a digit.</summary>
    public virtual string PasswordSymbol { get; set; } = "A symbol, such as ! or #";

    /// <summary>A password requirement: the fewest different characters. {0} is the number.</summary>
    public virtual string PasswordUnique { get; set; } = "At least {0} different characters";

    /// <summary>For screen readers, after a password requirement the text meets.</summary>
    public virtual string RuleMet { get; set; } = "met";

    /// <summary>For screen readers, after a password requirement the text doesn't meet yet.</summary>
    public virtual string RuleNotMet { get; set; } = "not yet";

    /// <summary>Announced once, when a password meets the last of its requirements.</summary>
    public virtual string PasswordRulesMet { get; set; } = "All requirements met.";

    /// <summary>The message for a password that misses a requirement. {0} is the field's label.</summary>
    public virtual string PasswordRulesUnmet { get; set; } = "The {0} field doesn't meet every requirement.";

    /// <summary>FileUpload's link that opens the picker, for one file.</summary>
    public virtual string ChooseFile { get; set; } = "Choose a file";

    /// <summary>FileUpload's link that opens the picker, with Multiple.</summary>
    public virtual string ChooseFiles { get; set; } = "Choose files";

    /// <summary>FileUpload's text after the link, for one file (hidden on touch screens).</summary>
    public virtual string DragFileHere { get; set; } = "or drag it here";

    /// <summary>FileUpload's text after the link, with Multiple.</summary>
    public virtual string DragFilesHere { get; set; } = "or drag them here";

    /// <summary>FileUpload's hint, the size limit: {0} is the limit ("2 MB").</summary>
    public virtual string FileSizeLimit { get; set; } = "up to {0}";

    /// <summary>The last joining word in a list of choices: "PNG, JPG or GIF".</summary>
    public virtual string ListOr { get; set; } = "or";

    /// <summary>FileUpload, several files chosen: {0} is how many.</summary>
    public virtual string FilesChosen { get; set; } = "{0} files";

    /// <summary>FileUpload, a file over the size limit: {0} is its name, {1} the limit.</summary>
    public virtual string FileTooLarge { get; set; } = "{0} is larger than {1}.";

    /// <summary>FileUpload, a file of the wrong type: {0} is its name.</summary>
    public virtual string FileWrongType { get; set; } = "{0} isn't an accepted file type.";

    /// <summary>FilterForm's name, and that of a table's active filters.</summary>
    public virtual string Filters { get; set; } = "Filters";

    /// <summary>FilterSearch's hidden label.</summary>
    public virtual string Search { get; set; } = "Search";

    /// <summary>FilterForm's apply button.</summary>
    public virtual string Apply { get; set; } = "Apply";

    /// <summary>FilterForm's clear link, and a header filter's.</summary>
    public virtual string Clear { get; set; } = "Clear";

    // Data and feedback

    /// <summary>A DataGrid header menu's item that groups by the column.</summary>
    public virtual string GroupByColumn { get; set; } = "Group by this column";

    /// <summary>A DataGrid header menu's item that stops grouping by the column.</summary>
    public virtual string GroupRemove { get; set; } = "Stop grouping by this column";

    /// <summary>The name of what a DataGrid is grouped by, its chips.</summary>
    public virtual string GroupedBy { get; set; } = "Grouped by";

    /// <summary>A DataGrid's bar of what's grouped, with nothing grouped yet.</summary>
    public virtual string GroupDropHint { get; set; } = "Drag a column header here to group by it";

    /// <summary>Opens all of a DataGrid's groups.</summary>
    public virtual string GroupExpandAll { get; set; } = "Expand all groups";

    /// <summary>Closes all of a DataGrid's groups.</summary>
    public virtual string GroupCollapseAll { get; set; } = "Collapse all groups";

    /// <summary>A DataGrid row's button that opens its detail: {0} is the row's name.</summary>
    public virtual string DetailShow { get; set; } = "Show details for {0}";

    /// <summary>A DataGrid row's button that closes its detail: {0} is the row's name.</summary>
    public virtual string DetailHide { get; set; } = "Hide details for {0}";

    /// <summary>Opens every row's detail in a DataGrid.</summary>
    public virtual string DetailExpandAll { get; set; } = "Show all details";

    /// <summary>Closes every row's detail in a DataGrid.</summary>
    public virtual string DetailCollapseAll { get; set; } = "Hide all details";

    /// <summary>The name of a DataGrid's column of detail buttons, when it has no button of its own.</summary>
    public virtual string DetailColumn { get; set; } = "Details";

    /// <summary>A DataGrid group's label: {0} is the column's title, {1} its value.</summary>
    public virtual string GroupLabel { get; set; } = "{0}: {1}";

    /// <summary>After a DataGrid group's label: {0} is how many rows it has.</summary>
    public virtual string GroupCount { get; set; } = "({0:N0})";

    /// <summary>A DataGrid aggregate: {0} is its word, {1} its value.</summary>
    public virtual string AggregateText { get; set; } = "{0}: {1}";

    /// <summary>The checkbox on a DataGrid's open group: {0} is its value.</summary>
    public virtual string GroupSelect { get; set; } = "Select the rows of {0}";

    /// <summary>A DataGrid group with no value.</summary>
    public virtual string GroupNone { get; set; } = "(None)";

    /// <summary>A DataGrid group too long to draw: how many are drawn, how many it has.</summary>
    public virtual string GroupShowing { get; set; } = "Showing {0:N0} of {1:N0}.";

    /// <summary>The link from a group too long to draw to the grid with only its rows.</summary>
    public virtual string GroupShowOnly { get; set; } = "Show only this group";

    /// <summary>The range of a page of groups: first, last, how many in all.</summary>
    public virtual string GroupRange { get; set; } = "{0:N0}–{1:N0} of {2:N0} groups";

    /// <summary>The word before a DataGrid column's Sum.</summary>
    public virtual string AggregateSum { get; set; } = "Sum";

    /// <summary>The word before a DataGrid column's Avg.</summary>
    public virtual string AggregateAvg { get; set; } = "Avg";

    /// <summary>The word before a DataGrid column's Min.</summary>
    public virtual string AggregateMin { get; set; } = "Min";

    /// <summary>The word before a DataGrid column's Max.</summary>
    public virtual string AggregateMax { get; set; } = "Max";

    /// <summary>The word before a DataGrid column's Count.</summary>
    public virtual string AggregateCount { get; set; } = "Count";

    /// <summary>Table with no rows.</summary>
    public virtual string NoItems { get; set; } = "No items";

    /// <summary>Table's select-all checkbox.</summary>
    public virtual string SelectAllRows { get; set; } = "Select all rows";

    /// <summary>Table's row checkboxes, before the row's name.</summary>
    public virtual string SelectRow { get; set; } = "Select";

    /// <summary>The name of a Table's or ListView's selection bar, for screen readers, when the view has no name.</summary>
    public virtual string SelectionBar { get; set; } = "Selection";

    /// <summary>The name of the selection bar of a named view (a Table's Caption, a ListView's AriaLabel): {0} is that name ("Orders selection").</summary>
    public virtual string SelectionBarOf { get; set; } = "{0} selection";

    /// <summary>The selection's count in a Table or ListView: {0} is how many are selected ("3 selected").</summary>
    public virtual string SelectionCount { get; set; } = "{0:N0} selected";

    /// <summary>The selection's count when every matching item is selected: {0} is how many ("All 1,234 selected").</summary>
    public virtual string SelectionAllCount { get; set; } = "All {0:N0} selected";

    /// <summary>The selection's count when every matching item is selected and the total isn't known (cursor paging).</summary>
    public virtual string SelectionAllOpen { get; set; } = "All selected";

    /// <summary>The button beside the selection's count that clears it.</summary>
    public virtual string ClearSelection { get; set; } = "Clear selection";

    /// <summary>Table's scroll region name when it has no Caption.</summary>
    public virtual string TableName { get; set; } = "Table";

    /// <summary>Table's pagination name: {0} is the caption (or <see cref="TableName"/>).</summary>
    public virtual string TablePages { get; set; } = "{0} pages";

    /// <summary>Table's row range under the rows: {0} first, {1} last, {2} total ("1–10 of 42").</summary>
    public virtual string TableRange { get; set; } = "{0:N0}–{1:N0} of {2:N0}";

    /// <summary>Table's row range when the total isn't known (cursor paging): {0} first, {1} last ("201–400").</summary>
    public virtual string TableRangeOpen { get; set; } = "{0:N0}–{1:N0}";

    /// <summary>The button that loads the next page after the items: a ListView's Load more, and the keyboard's way on when a Table or ListView loads on scroll.</summary>
    public virtual string LoadMore { get; set; } = "Load more";

    /// <summary>Table's message when its rows failed to load, before a Retry button.</summary>
    public virtual string LoadFailed { get; set; } = "Couldn't load the rows.";

    /// <summary>ReconnectDialog's title.</summary>
    public virtual string ReconnectTitle { get; set; } = "Connection lost";

    /// <summary>ReconnectDialog while it tries to rejoin the server.</summary>
    public virtual string ReconnectRejoining { get; set; } = "Rejoining the server…";

    /// <summary>ReconnectDialog between attempts: {0} is the seconds until the next one.</summary>
    public virtual string ReconnectRetrying { get; set; } = "Couldn't rejoin. Trying again in {0} seconds.";

    /// <summary>ReconnectDialog between attempts, with one second to go.</summary>
    public virtual string ReconnectRetryingOne { get; set; } = "Couldn't rejoin. Trying again in 1 second.";

    /// <summary>ReconnectDialog when every attempt failed, above its Retry button.</summary>
    public virtual string ReconnectFailed { get; set; } = "Couldn't rejoin the server. Retry, or reload the page.";

    /// <summary>ReconnectDialog when the server paused the session, above its Resume button.</summary>
    public virtual string ReconnectPaused { get; set; } = "The server paused this session.";

    /// <summary>ReconnectDialog when the session couldn't resume, above its Reload button.</summary>
    public virtual string ReconnectResumeFailed { get; set; } = "Couldn't resume the session. Reload the page to go on.";

    /// <summary>ReconnectDialog's button after it failed to rejoin, and Table's after its rows failed to load.</summary>
    public virtual string Retry { get; set; } = "Retry";

    /// <summary>ReconnectDialog's button for a session the server paused.</summary>
    public virtual string Resume { get; set; } = "Resume";

    /// <summary>ReconnectDialog's button when the session can't come back.</summary>
    public virtual string Reload { get; set; } = "Reload";

    /// <summary>Alert's and Banner's status, read before the message (Info).</summary>
    public virtual string StatusInfo { get; set; } = "Info";

    /// <summary>Alert's and Banner's status, read before the message (Success).</summary>
    public virtual string StatusSuccess { get; set; } = "Success";

    /// <summary>Alert's and Banner's status, read before the message (Warning).</summary>
    public virtual string StatusWarning { get; set; } = "Warning";

    /// <summary>Alert's and Banner's status, read before the message (Danger).</summary>
    public virtual string StatusError { get; set; } = "Error";

    /// <summary>AvatarGroup's "+N" avatar: {0} is how many more.</summary>
    public virtual string MoreAvatars { get; set; } = "{0} more";

    /// <summary>The toast region's name.</summary>
    public virtual string Notifications { get; set; } = "Notifications";

    /// <summary>Stat, a figure that went up.</summary>
    public virtual string TrendUp { get; set; } = "Up";

    /// <summary>Stat, a figure that went down.</summary>
    public virtual string TrendDown { get; set; } = "Down";

    /// <summary>Stat, a figure that didn't change.</summary>
    public virtual string TrendFlat { get; set; } = "No change";

    /// <summary>A ListView's name for screen readers in its pagination, when it has no AriaLabel.</summary>
    public virtual string ListName { get; set; } = "List";

    /// <summary>A ListView whose items failed to load, with Retry after it.</summary>
    public virtual string ListLoadFailed { get; set; } = "Couldn't load the items.";

    /// <summary>PickList's list of items not chosen.</summary>
    public virtual string PickAvailable { get; set; } = "Available";

    /// <summary>PickList's list of chosen items.</summary>
    public virtual string PickChosen { get; set; } = "Chosen";

    /// <summary>PickList's button that moves the selected available items to the chosen list.</summary>
    public virtual string PickMoveToChosen { get; set; } = "Move to chosen";

    /// <summary>PickList's button that moves every available item (every match, when searching).</summary>
    public virtual string PickMoveAllToChosen { get; set; } = "Move all to chosen";

    /// <summary>PickList's button that moves the selected chosen items back.</summary>
    public virtual string PickMoveToAvailable { get; set; } = "Move to available";

    /// <summary>PickList's button that moves every chosen item back (every match, when searching).</summary>
    public virtual string PickMoveAllToAvailable { get; set; } = "Move all to available";

    /// <summary>Announced when PickList moves items to the chosen list; {0} is their names.</summary>
    public virtual string PickMovedToChosen { get; set; } = "Moved {0} to chosen";

    /// <summary>Announced when PickList moves items back; {0} is their names.</summary>
    public virtual string PickMovedToAvailable { get; set; } = "Moved {0} to available";

    /// <summary>A Splitter bar, named after the pane before it: "Resize Folders". {0} is the pane's label.</summary>
    public virtual string ResizePane { get; set; } = "Resize {0}";

    /// <summary>A Splitter bar whose pane has no label.</summary>
    public virtual string Resize { get; set; } = "Resize";

    /// <summary>A Splitter bar's button that collapses a pane: "Collapse Folders". {0} is the pane's label, or empty.</summary>
    public virtual string CollapsePane { get; set; } = "Collapse {0}";

    /// <summary>A Splitter bar's button that restores a collapsed pane: "Expand Folders". {0} is the pane's label, or empty.</summary>
    public virtual string ExpandPane { get; set; } = "Expand {0}";

    /// <summary>A Carousel's role description, read after its label: "Featured trips, carousel".</summary>
    public virtual string CarouselRole { get; set; } = "carousel";

    /// <summary>A Carousel's track, which holds the slides and scrolls with the arrow keys.</summary>
    public virtual string CarouselSlides { get; set; } = "Slides";

    /// <summary>A Carousel slide's role description.</summary>
    public virtual string SlideRole { get; set; } = "slide";

    /// <summary>A Carousel slide with no label: "3 of 5". {0} is its place, {1} the number of slides.</summary>
    public virtual string SlideOf { get; set; } = "{0} of {1}";

    /// <summary>A Carousel's button that moves back a slide, or a page of them.</summary>
    public virtual string PreviousSlide { get; set; } = "Previous slide";

    /// <summary>A Carousel's button that moves on a slide, or a page of them.</summary>
    public virtual string NextSlide { get; set; } = "Next slide";

    /// <summary>A Carousel's dot: "Slide 3". {0} is the slide's place.</summary>
    public virtual string GoToSlide { get; set; } = "Slide {0}";

    /// <summary>A Carousel's dot when several slides show at once: "Slides 4 to 6".</summary>
    public virtual string SlidesRange { get; set; } = "Slides {0} to {1}";

    /// <summary>A Carousel's button that stops its slides rotating.</summary>
    public virtual string StopRotation { get; set; } = "Stop slide rotation";

    /// <summary>A Carousel's button that starts its slides rotating.</summary>
    public virtual string StartRotation { get; set; } = "Start slide rotation";

    /// <summary>A TreeView branch whose children load with script, when JavaScript is off.</summary>
    public virtual string TreeNeedsScript { get; set; } = "Turn on JavaScript to see these items.";

    /// <summary>A ListView item's ⋯ button, which opens its ItemMenu: "Actions for Ada". {0} is the item's label.</summary>
    public virtual string ItemMenu { get; set; } = "Actions for {0}";

    /// <summary>A ListView item's drag handle, which opens the move menu: "Move Ada". {0} is the item's label.</summary>
    public virtual string MoveItem { get; set; } = "Move {0}";

    /// <summary>The submenu holding the moves in a ListView item's ⋯ menu, beside its own items.</summary>
    public virtual string MoveMenu { get; set; } = "Move";

    /// <summary>A ListView item's move menu: one place earlier.</summary>
    public virtual string MoveUp { get; set; } = "Move up";

    /// <summary>A ListView item's move menu: one place later.</summary>
    public virtual string MoveDown { get; set; } = "Move down";

    /// <summary>A ListView item's move menu: to the first place.</summary>
    public virtual string MoveToTop { get; set; } = "Move to top";

    /// <summary>A ListView item's move menu: to the last place.</summary>
    public virtual string MoveToBottom { get; set; } = "Move to bottom";

    /// <summary>The heading over the other drop zones in a ListView item's move menu, each named by its label.</summary>
    public virtual string MoveToMenu { get; set; } = "Move to";

    /// <summary>Announced after a move: "Moved Ada to position 3 of 5".</summary>
    public virtual string MovedTo { get; set; } = "Moved {0} to position {1} of {2}";

    /// <summary>Announced after a move between zones: "Moved Ada to Done, position 1 of 2".</summary>
    public virtual string MovedToZone { get; set; } = "Moved {0} to {1}, position {2} of {3}";

    /// <summary>Announced when a move is cancelled: "Ada wasn't moved".</summary>
    public virtual string MoveCancelled { get; set; } = "{0} wasn't moved";

    /// <summary>The column of a DataGrid's row handles, named for screen readers.</summary>
    public virtual string MoveColumn { get; set; } = "Move";

    /// <summary>A DataGrid row handle's tooltip while the rows aren't in the grid's own order.</summary>
    public virtual string MoveRowsUnavailable { get; set; } = "Clear the sort, filters and grouping to move rows";

    /// <summary>A DataGrid row handle's tooltip while a row is being edited in place.</summary>
    public virtual string MoveRowsEditing { get; set; } = "Finish editing to move rows";

    /// <summary>An empty drop zone.</summary>
    public virtual string DropHere { get; set; } = "Drop items here";

    // Navigation and layout

    /// <summary>Breadcrumb's name.</summary>
    public virtual string Breadcrumb { get; set; } = "Breadcrumb";

    /// <summary>Pagination's name.</summary>
    public virtual string Pagination { get; set; } = "Pagination";

    /// <summary>Pagination's previous button.</summary>
    public virtual string PreviousPage { get; set; } = "Previous page";

    /// <summary>Pagination's next button.</summary>
    public virtual string NextPage { get; set; } = "Next page";

    /// <summary>A table's link back to its first page, when it pages by cursor.</summary>
    public virtual string FirstPage { get; set; } = "First page";

    /// <summary>Before a page number: "Page 3".</summary>
    public virtual string Page { get; set; } = "Page";

    /// <summary>Stepper's name.</summary>
    public virtual string Progress { get; set; } = "Progress";

    /// <summary>Read after a finished step's label.</summary>
    public virtual string Completed { get; set; } = "(completed)";

    /// <summary>AppShell's skip link, the first thing a keyboard reaches.</summary>
    public virtual string SkipToContent { get; set; } = "Skip to content";

    /// <summary>The sidebar navigation's name.</summary>
    public virtual string MainNavigation { get; set; } = "Main";

    /// <summary>SiteNav's name: different from the sidebar's, since both can be on screen.</summary>
    public virtual string SiteNavigation { get; set; } = "Site";

    /// <summary>SiteNav's menu button, which opens and closes its panel (screen readers also hear whether it's open).</summary>
    public virtual string Menu { get; set; } = "Menu";

    /// <summary>The button that opens the sidebar drawer on phones.</summary>
    public virtual string OpenMenu { get; set; } = "Open menu";

    /// <summary>The sidebar drawer's close button.</summary>
    public virtual string CloseMenu { get; set; } = "Close menu";

    /// <summary>The button that collapses the sidebar to icons.</summary>
    public virtual string CollapseSidebar { get; set; } = "Collapse sidebar";

    /// <summary>The button that opens an AppAside drawer below its breakpoint.</summary>
    public virtual string OpenPanel { get; set; } = "Open panel";

    /// <summary>An AppAside drawer's close button.</summary>
    public virtual string ClosePanel { get; set; } = "Close panel";

    /// <summary>OnThisPage's heading, which also names its navigation landmark.</summary>
    public virtual string OnThisPage { get; set; } = "On this page";

    /// <summary>PrevNext's navigation landmark.</summary>
    public virtual string PrevNextNavigation { get; set; } = "More pages";

    /// <summary>PrevNext: the small text above the previous page's title.</summary>
    public virtual string Previous { get; set; } = "Previous";

    /// <summary>PrevNext: the small text above the next page's title.</summary>
    public virtual string Next { get; set; } = "Next";

    // Typography

    /// <summary>A code block's copy button.</summary>
    public virtual string CopyCode { get; set; } = "Copy code";

    /// <summary>A code block's name for screen readers when it has no Title (it's a tab stop, so it can scroll).</summary>
    public virtual string CodeBlock { get; set; } = "Code";

    /// <summary>CodeExample's toggle while its source is hidden.</summary>
    public virtual string ShowCode { get; set; } = "Show code";

    /// <summary>CodeExample's toggle while its source is shown.</summary>
    public virtual string HideCode { get; set; } = "Hide code";

    /// <summary>CodeExample's tabs of source files, for screen readers.</summary>
    public virtual string SourceFiles { get; set; } = "Source files";

    /// <summary>CopyButton's name when it shows no text.</summary>
    public virtual string Copy { get; set; } = "Copy";

    /// <summary>Announced after copying.</summary>
    public virtual string Copied { get; set; } = "Copied";

    /// <summary>A diff's name for screen readers when it has no Title (it's a tab stop, so it can scroll).</summary>
    public virtual string DiffBlock { get; set; } = "Changes";

    /// <summary>For screen readers, before each line a diff adds.</summary>
    public virtual string DiffAdded { get; set; } = "Added:";

    /// <summary>For screen readers, before each line a diff removes.</summary>
    public virtual string DiffRemoved { get; set; } = "Removed:";

    /// <summary>A diff's folded run of unchanged lines. {0} is how many (always 2 or more).</summary>
    public virtual string DiffUnchanged { get; set; } = "{0:N0} unchanged lines";

    /// <summary>For screen readers, a diff's counts. {0} is the lines added, {1} the lines removed.</summary>
    public virtual string DiffSummary { get; set; } = "{0:N0} added, {1:N0} removed";

    /// <summary>A diff's note when the two texts are the same.</summary>
    public virtual string DiffSame { get; set; } = "The two texts are the same.";

    /// <summary>A diff's note when the texts differ by too many lines to compare. {0} is the most it compares.</summary>
    public virtual string DiffTooMany { get; set; } = "The texts differ by more than {0:N0} lines, too many to show line by line.";

    /// <summary>After a link that opens a new tab.</summary>
    public virtual string OpensInNewTab { get; set; } = "(opens in a new tab)";

    // Theming

    /// <summary>ColorSchemeSwitch's name.</summary>
    public virtual string ColorScheme { get; set; } = "Color scheme";

    /// <summary>ColorSchemeSwitch: follow the device.</summary>
    public virtual string SchemeSystem { get; set; } = "System";

    /// <summary>ColorSchemeSwitch: light.</summary>
    public virtual string SchemeLight { get; set; } = "Light";

    /// <summary>ColorSchemeSwitch: dark.</summary>
    public virtual string SchemeDark { get; set; } = "Dark";

    /// <summary>VideoEmbed's play button, with {0} for the video's title.</summary>
    public virtual string PlayVideo { get; set; } = "Play video: {0}";

    /// <summary>VideoEmbed with no video yet.</summary>
    public virtual string VideoComingSoon { get; set; } = "Coming soon";

    /// <summary>A Chart's disclosure that shows its values as a table.</summary>
    public virtual string ChartShowData { get; set; } = "Show data";

    /// <summary>The first column's header in a Chart's data table, when its Argument isn't a property.</summary>
    public virtual string ChartItem { get; set; } = "Item";

    /// <summary>What screen readers call a chart once its script gives it the keyboard.</summary>
    public virtual string ChartRole { get; set; } = "chart";

    /// <summary>How the keyboard moves through a chart, read after its name.</summary>
    public virtual string ChartKeys { get; set; } = "Arrow keys move between values; Escape hides the tooltip.";

    /// <summary>A PieChart's data table and tooltip: each slice's share of the whole.</summary>
    public virtual string ChartShare { get; set; } = "Share";

    /// <summary>
    /// A Sparkline's accessible name, which says what a glance at it shows: "Revenue, last 12 weeks:
    /// from 41 to 48, low 39, high 52". {0} is the label; {1} and {2} the first and last values;
    /// {3} and {4} the lowest and highest.
    /// </summary>
    public virtual string SparklineSummary { get; set; } = "{0}: from {1} to {2}, low {3}, high {4}";

    /// <summary>ColorPicker's hex text box, after the field's label: "Brand color, hex".</summary>
    public virtual string ColorHex { get; set; } = "hex";

    /// <summary>ColorPicker's row of preset swatches.</summary>
    public virtual string ColorPresets { get; set; } = "Preset colors";

    /// <summary>ColorPicker's message for a value that isn't a hex color. {0} is the field's label.</summary>
    public virtual string ColorInvalid { get; set; } = "The {0} field must be a color like #2563eb.";

    /// <summary>ColorSchemeSwitch's toggle, on a light page.</summary>
    public virtual string SchemeToDark { get; set; } = "Switch to dark mode";

    /// <summary>ColorSchemeSwitch's toggle, on a dark page.</summary>
    public virtual string SchemeToLight { get; set; } = "Switch to light mode";
}
