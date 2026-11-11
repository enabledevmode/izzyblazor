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

    /// <summary>A close button. Dialog, Popover; before a tab's label on its close button ("Close Billing").</summary>
    public virtual string Close { get; set; } = "Close";

    /// <summary>A dismiss button. Alert, toasts.</summary>
    public virtual string Dismiss { get; set; } = "Dismiss";

    /// <summary>Table's page size menu: "Rows per page".</summary>
    public virtual string RowsPerPage { get; set; } = "Rows per page";

    /// <summary>A DataGrid header's place in a sort of several columns, ascending: "Sort 2, ascending".</summary>
    public virtual string SortOrderAscending { get; set; } = "Sort {0}, ascending";

    /// <summary>A DataGrid header's place in a sort of several columns, descending: "Sort 2, descending".</summary>
    public virtual string SortOrderDescending { get; set; } = "Sort {0}, descending";

    /// <summary>
    /// A column's name with its band's, wherever it's named away from its header (a filter's tag, a
    /// phone's label beside the value): "Q1 Revenue". {0} is the band's title, {1} the column's. For
    /// a band in a band, it's applied once a level, from the inside out, so {1} is then the column's
    /// name with the inner bands' already: "2026 Q1 Revenue".
    /// </summary>
    public virtual string ColumnInBand { get; set; } = "{0} {1}";

    /// <summary>
    /// A DataGrid header's menu button, its hover text and description: "Options for Customer". {0}
    /// is the column's title. Its name is <see cref="ColumnOptionsButton"/>, short, since it's read
    /// as part of the header for every cell in the column.
    /// </summary>
    public virtual string ColumnOptions { get; set; } = "Options for {0}";

    /// <summary>A DataGrid header's menu button's name: "Options" (the column's name is the header's own).</summary>
    public virtual string ColumnOptionsButton { get; set; } = "Options";

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

    /// <summary>A DataGrid column menu: the submenu that freezes the columns on one side of this one, as a sheet's frozen panes.</summary>
    public virtual string ColumnFreeze { get; set; } = "Freeze columns";

    /// <summary>A DataGrid column menu's Freeze columns: every column to this one's left.</summary>
    public virtual string ColumnFreezeLeft { get; set; } = "To the left";

    /// <summary>A DataGrid column menu's Freeze columns: every column to this one's right.</summary>
    public virtual string ColumnFreezeRight { get; set; } = "To the right";

    /// <summary>A DataGrid column menu's Freeze columns: this column's side, or all frozen columns from one that scrolls.</summary>
    public virtual string ColumnUnfreeze { get; set; } = "Unfreeze";

    /// <summary>A DataGrid column menu: sorts by the column, smallest first.</summary>
    public virtual string ColumnSortAscending { get; set; } = "Sort ascending";

    /// <summary>A DataGrid column menu: sorts by the column, largest first.</summary>
    public virtual string ColumnSortDescending { get; set; } = "Sort descending";

    /// <summary>A DataGrid column menu, with another column sorted and not this one: adds the column to the sort, smallest first.</summary>
    public virtual string ColumnSortAddAscending { get; set; } = "Add to sort, ascending";

    /// <summary>A DataGrid column menu, with another column sorted and not this one: adds the column to the sort, largest first.</summary>
    public virtual string ColumnSortAddDescending { get; set; } = "Add to sort, descending";

    /// <summary>A DataGrid column menu: takes the column out of the sort.</summary>
    public virtual string ColumnSortClear { get; set; } = "Clear sort";

    /// <summary>A DataGrid column menu: as wide as its header and the rows shown.</summary>
    public virtual string ColumnFit { get; set; } = "Fit to content";

    /// <summary>A DataGrid column menu: every column that can be resized, fitted to its content.</summary>
    public virtual string ColumnFitAll { get; set; } = "Fit all columns";

    /// <summary>A DataGrid column menu: the submenu of Wider, Narrower and Reset width.</summary>
    public virtual string ColumnSize { get; set; } = "Width";

    /// <summary>A DataGrid column menu: hides the column (the picker shows it again).</summary>
    public virtual string ColumnHide { get; set; } = "Hide column";

    /// <summary>A DataGrid column menu: opens the column picker.</summary>
    public virtual string ColumnChoose { get; set; } = "Choose columns…";

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

    /// <summary>A DataGrid column picker's title.</summary>
    public virtual string ColumnPickerTitle { get; set; } = "Choose columns";

    /// <summary>A DataGrid column picker's box that shows every column, or hides all but one.</summary>
    public virtual string ColumnsShowAll { get; set; } = "Show all columns";

    /// <summary>A DataGrid column picker's box that narrows the list by title.</summary>
    public virtual string ColumnsSearch { get; set; } = "Search columns";

    /// <summary>A DataGrid column picker's count of hidden columns: "3 of 8 hidden". {0} is how many, {1} of how many.</summary>
    public virtual string ColumnsHidden { get; set; } = "{0} of {1} hidden";

    /// <summary>A DataGrid column picker's button that opens every band's list.</summary>
    public virtual string ColumnsExpandAll { get; set; } = "Expand all bands";

    /// <summary>A DataGrid column picker's button that closes every band's list.</summary>
    public virtual string ColumnsCollapseAll { get; set; } = "Collapse all bands";

    /// <summary>A DataGrid column picker's name for a frozen column, for screen readers: "Region, frozen". {0} is its name.</summary>
    public virtual string ColumnFrozenNote { get; set; } = "{0}, frozen";

    /// <summary>A DataGrid's column picker: back to the columns as declared.</summary>
    public virtual string ColumnsReset { get; set; } = "Reset columns";

    /// <summary>A Table filter's panel name and its button's hover text and description: "Filter Customer".</summary>
    public virtual string FilterColumn { get; set; } = "Filter {0}";

    /// <summary>A header's filter button's name: "Filter". Its hover text and description say the column and what it's set to ("Filter Status: 2 selected").</summary>
    public virtual string FilterButton { get; set; } = "Filter";

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

    /// <summary>A list filter's box for narrowing its rows: its name.</summary>
    public virtual string FilterSearchValues { get; set; } = "Search values";

    /// <summary>The hint in a box that narrows a list: a list filter's, a column picker's.</summary>
    public virtual string SearchPlaceholder { get; set; } = "Search…";

    /// <summary>
    /// A list filter's box that ticks every row: in parentheses, as "(Blank)" is, so it reads as an
    /// option, not one of the values.
    /// </summary>
    public virtual string FilterSelectAll { get; set; } = "(Select all)";

    /// <summary>A list filter's Select all while a search narrows it; {0} is how many rows it found.</summary>
    public virtual string FilterSelectAllResults { get; set; } = "(Select all {0} results)";

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

    /// <summary>The question a Form with ConfirmLeave asks before leaving with unsaved changes.</summary>
    public virtual string LeaveTitle { get; set; } = "Leave this page?";

    /// <summary>The line under <see cref="LeaveTitle"/>.</summary>
    public virtual string LeaveMessage { get; set; } = "Your changes haven't been saved.";

    /// <summary>The button that leaves anyway, in a Form's ConfirmLeave dialog.</summary>
    public virtual string Leave { get; set; } = "Leave";

    /// <summary>The button that stays on the page, in a Form's ConfirmLeave dialog.</summary>
    public virtual string Stay { get; set; } = "Stay";

    /// <summary>A DataGrid editor's title, before it knows its row.</summary>
    public virtual string Edit { get; set; } = "Edit";

    /// <summary>A DataGrid row's edit button and editor. {0} is the row's label.</summary>
    public virtual string EditItem { get; set; } = "Edit {0}";

    /// <summary>A DataGrid's Add button, and its editor's title for a new row.</summary>
    public virtual string Add { get; set; } = "Add";

    /// <summary>A TreeList row's Add child action. {0} is the row's name.</summary>
    public virtual string AddUnder { get; set; } = "Add under {0}";

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

    /// <summary>For screen readers, when a MultiSelect with <c>AllowCustom</c> takes a typed value: "Added {0}." {0} is the value.</summary>
    public virtual string Added { get; set; } = "Added {0}.";

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

    /// <summary>The add button after a Tabs' tabs.</summary>
    public virtual string AddTab { get; set; } = "Add tab";

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

    /// <summary>RangeSlider's name for its lower thumb, which screen readers hear after the label.</summary>
    public virtual string RangeSliderStart { get; set; } = "Minimum";

    /// <summary>RangeSlider's name for its upper thumb, which screen readers hear after the label.</summary>
    public virtual string RangeSliderEnd { get; set; } = "Maximum";

    /// <summary>A RangeSlider with an end outside its Min and Max; {0} is its label, {1} and {2} the ends.</summary>
    public virtual string RangeOutside { get; set; } = "{0} must be between {1} and {2}.";

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

    /// <summary>The message for a masked TextField with a slot left empty. {0} is the field's label.</summary>
    public virtual string MaskIncomplete { get; set; } = "The {0} field is incomplete.";

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

    /// <summary>A DataGrid header menu's item that moves the column's grouping level one place left among the grouped-by chips.</summary>
    public virtual string GroupMoveLeft { get; set; } = "Move group left";

    /// <summary>A DataGrid header menu's item that moves the column's grouping level one place right among the grouped-by chips.</summary>
    public virtual string GroupMoveRight { get; set; } = "Move group right";

    /// <summary>A DataGrid grouped-by chip's menu item that moves its level one place left.</summary>
    public virtual string GroupChipMoveLeft { get; set; } = "Move left";

    /// <summary>A DataGrid grouped-by chip's menu item that moves its level one place right.</summary>
    public virtual string GroupChipMoveRight { get; set; } = "Move right";

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

    /// <summary>A TreeList row's chevron that shows the rows under it: {0} is the row's name.</summary>
    public virtual string TreeExpand { get; set; } = "Expand {0}";

    /// <summary>A TreeList row's chevron that hides the rows under it: {0} is the row's name.</summary>
    public virtual string TreeCollapse { get; set; } = "Collapse {0}";

    /// <summary>Opens every row in a TreeList.</summary>
    public virtual string TreeExpandAll { get; set; } = "Expand all rows";

    /// <summary>Closes every row in a TreeList.</summary>
    public virtual string TreeCollapseAll { get; set; } = "Collapse all rows";

    /// <summary>The button after a tree list's first children of a row, when it has more.</summary>
    public virtual string TreeShowMore { get; set; } = "Show more";

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

    /// <summary>The name of the selection bar of a named view (a Table's Caption, a ListView's Label): {0} is that name ("Orders selection").</summary>
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

    /// <summary>ReconnectDialog's title while the session is paused, by the server or by the page.</summary>
    public virtual string ReconnectPausedTitle { get; set; } = "Session paused";

    /// <summary>ReconnectDialog when the server paused the session, above its Resume button.</summary>
    public virtual string ReconnectPaused { get; set; } = "The server paused this session.";

    /// <summary>
    /// ReconnectDialog when the page paused its own session, such as .NET 11's auto-pause while the
    /// tab is out of view, above its Resume button.
    /// </summary>
    public virtual string ReconnectPausedHere { get; set; } = "This session is paused. Resume to go on.";

    /// <summary>ReconnectDialog when the session couldn't resume, above its Reload button.</summary>
    public virtual string ReconnectResumeFailed { get; set; } = "Couldn't resume the session. Reload the page to go on.";

    /// <summary>ReconnectDialog's button after it failed to rejoin, and Table's after its rows failed to load.</summary>
    public virtual string Retry { get; set; } = "Retry";

    /// <summary>ReconnectDialog's button for a paused session.</summary>
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

    /// <summary>A ListView's name for screen readers in its pagination, when it has no Label.</summary>
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

    /// <summary>A Splitter bar's menu: makes the bar's pane wider, side by side.</summary>
    public virtual string PaneWider { get; set; } = "Wider";

    /// <summary>A Splitter bar's menu: makes the bar's pane narrower, side by side.</summary>
    public virtual string PaneNarrower { get; set; } = "Narrower";

    /// <summary>A Splitter bar's menu: makes the bar's pane taller, stacked.</summary>
    public virtual string PaneTaller { get; set; } = "Taller";

    /// <summary>A Splitter bar's menu: makes the bar's pane shorter, stacked.</summary>
    public virtual string PaneShorter { get; set; } = "Shorter";

    /// <summary>A Splitter bar's menu: puts the panes on either side back to their starting sizes.</summary>
    public virtual string PaneResetSize { get; set; } = "Reset size";

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

    /// <summary>A PieChart's value column header, when its Value isn't a property and it has no Title.</summary>
    public virtual string ChartValue { get; set; } = "Value";

    /// <summary>What screen readers call a chart once its script gives it the keyboard.</summary>
    public virtual string ChartRole { get; set; } = "chart";

    /// <summary>How the keyboard moves through a chart, read after its name.</summary>
    public virtual string ChartKeys { get; set; } = "Arrow keys move between values; Escape hides the tooltip.";

    /// <summary>A PieChart's data table and tooltip: each slice's share of the whole.</summary>
    public virtual string ChartShare { get; set; } = "Share";

    /// <summary>A NestedDonutChart's outer ring, named under its title: "Outside: {0}".</summary>
    public virtual string ChartRingOutside { get; set; } = "Outside: {0}";

    /// <summary>A NestedDonutChart's inner ring, named under its title: "Inside: {0}".</summary>
    public virtual string ChartRingInside { get; set; } = "Inside: {0}";

    /// <summary>The ring between the outer and inner ones of three: "Middle: {0}".</summary>
    public virtual string ChartRingMiddle { get; set; } = "Middle: {0}";

    /// <summary>A ring by its place from the outside, with four or more: "Ring {0}: {1}".</summary>
    public virtual string ChartRingNumber { get; set; } = "Ring {0}: {1}";

    /// <summary>How the keyboard moves through a NestedDonutChart, read after its name.</summary>
    public virtual string ChartRingKeys { get; set; } = "Left and Right move along a ring, Up and Down between rings; Escape hides the tooltip.";

    /// <summary>How the keyboard moves through a Chart that zooms, read after its name.</summary>
    public virtual string ChartZoomKeys { get; set; } = "Arrow keys move between values; plus and minus zoom, 0 zooms out; Escape hides the tooltip.";

    /// <summary>How the keyboard zooms a Chart that has no tooltips, read after its name.</summary>
    public virtual string ChartZoomOnlyKeys { get; set; } = "Plus and minus zoom, 0 zooms out, arrows pan.";

    /// <summary>The button that zooms a Chart all the way out.</summary>
    public virtual string ChartResetZoom { get; set; } = "Reset zoom";

    /// <summary>The button that zooms a Chart in, around the middle of what's shown.</summary>
    public virtual string ChartZoomIn { get; set; } = "Zoom in";

    /// <summary>The button that zooms a Chart out a step.</summary>
    public virtual string ChartZoomOut { get; set; } = "Zoom out";

    /// <summary>A Scheduler's form's title for a new appointment, and its toolbar button's name for screen readers.</summary>
    public virtual string SchedulerNew { get; set; } = "New appointment";

    /// <summary>A Scheduler's toolbar button for a new appointment (its name for screen readers is <see cref="SchedulerNew"/>).</summary>
    public virtual string SchedulerNewShort { get; set; } = "New";

    /// <summary>A Scheduler's form for an appointment that's there.</summary>
    public virtual string SchedulerEdit { get; set; } = "Edit appointment";

    /// <summary>A Scheduler's form fields.</summary>
    public virtual string SchedulerTitleField { get; set; } = "Title";

    /// <inheritdoc cref="SchedulerTitleField" />
    public virtual string SchedulerStartDate { get; set; } = "Start date";

    /// <inheritdoc cref="SchedulerTitleField" />
    public virtual string SchedulerStartTime { get; set; } = "Start time";

    /// <inheritdoc cref="SchedulerTitleField" />
    public virtual string SchedulerEndDate { get; set; } = "End date";

    /// <inheritdoc cref="SchedulerTitleField" />
    public virtual string SchedulerEndTime { get; set; } = "End time";

    /// <inheritdoc cref="SchedulerTitleField" />
    public virtual string SchedulerDescription { get; set; } = "Description";

    /// <summary>A Scheduler's form whose end comes before its start.</summary>
    public virtual string SchedulerEndBeforeStart { get; set; } = "The end comes before the start.";

    /// <summary>A Scheduler's form for an appointment that's gone (deleted, or a link made by hand).</summary>
    public virtual string SchedulerGone { get; set; } = "This appointment isn't there any more.";

    /// <summary>A Scheduler's hours, for screen readers, when they scroll with no appointment links in them.</summary>
    public virtual string SchedulerHours { get; set; } = "Hours";

    /// <summary>A Scheduler's question before it deletes an appointment.</summary>
    public virtual string SchedulerDeleteTitle { get; set; } = "Delete this appointment?";

    /// <summary>A Scheduler's edit form: the appointment's category, which sets its color.</summary>
    public virtual string SchedulerCategory { get; set; } = "Category";

    /// <summary>A Scheduler's menu of categories to show or hide.</summary>
    public virtual string SchedulerCategories { get; set; } = "Categories";

    /// <summary>The categories menu's name for screen readers with some hidden: "Categories, {0} of {1} hidden".</summary>
    public virtual string SchedulerCategoriesHidden { get; set; } = "Categories, {0} of {1} hidden";

    /// <summary>A Scheduler's name for screen readers, when it has no Label.</summary>
    public virtual string SchedulerName { get; set; } = "Schedule";

    /// <summary>A Scheduler's calendar, opened from its title, that goes to the date chosen.</summary>
    public virtual string SchedulerGoTo { get; set; } = "Go to a date";

    /// <summary>A Scheduler's button that goes to the date typed, with JavaScript off.</summary>
    public virtual string SchedulerGo { get; set; } = "Go";

    /// <summary>A Scheduler's message when its ItemsProvider fails, before the Retry button.</summary>
    public virtual string SchedulerLoadFailed { get; set; } = "Couldn't load the appointments.";

    /// <summary>A month's day number, a link to that day: "Show {0}" (the day's full date).</summary>
    public virtual string SchedulerShowDay { get; set; } = "Show {0}";

    /// <summary>A Scheduler's link back to today.</summary>
    public virtual string SchedulerToday { get; set; } = "Today";

    /// <summary>A Scheduler's link to the page before: "Previous {0}" (the view's period: day, week, month).</summary>
    public virtual string SchedulerPrevious { get; set; } = "Previous {0}";

    /// <summary>A Scheduler's link to the page after: "Next {0}".</summary>
    public virtual string SchedulerNext { get; set; } = "Next {0}";

    /// <summary>A Scheduler's Day view, in its view menu; also the period in "Previous day" (lowercased).</summary>
    public virtual string SchedulerDay { get; set; } = "Day";

    /// <summary>A Scheduler's Week view, in its view menu.</summary>
    public virtual string SchedulerWeek { get; set; } = "Week";

    /// <summary>A Scheduler's Work week view, in its view menu.</summary>
    public virtual string SchedulerWorkWeek { get; set; } = "Work week";

    /// <summary>A Scheduler's Month view, in its view menu.</summary>
    public virtual string SchedulerMonth { get; set; } = "Month";

    /// <summary>A Scheduler's Agenda view, in its view menu.</summary>
    public virtual string SchedulerAgenda { get; set; } = "Agenda";

    /// <summary>A Scheduler's month cell with more appointments than fit: "+{0} more", a link to the day.</summary>
    public virtual string SchedulerMore { get; set; } = "+{0} more";

    /// <summary>A Scheduler's agenda with nothing in it.</summary>
    public virtual string SchedulerEmpty { get; set; } = "No appointments";

    /// <summary>An all-day appointment's time, and the all-day row's label.</summary>
    public virtual string SchedulerAllDay { get; set; } = "All day";

    /// <summary>An appointment's times for screen readers: "{0} to {1}".</summary>
    public virtual string SchedulerTimeRange { get; set; } = "{0} to {1}";

    /// <summary>A drilled Chart's breadcrumb: the top level.</summary>
    public virtual string ChartDrillTop { get; set; } = "All";

    /// <summary>In a drillable item's tooltip: what selecting it shows, "Select to see by {0}" (the next level's name).</summary>
    public virtual string ChartDrillHint { get; set; } = "Select to see by {0}";

    /// <summary>Added to a drillable chart's keys hint: how the keys open an item's level.</summary>
    public virtual string ChartDrillKeys { get; set; } = "Enter opens the level below.";

    /// <summary>A drilled chart's breadcrumb's name: "{0} levels" (the chart's title), or "Levels" with none.</summary>
    public virtual string ChartDrillCrumbs { get; set; } = "{0} levels";

    /// <summary>Said after drilling into a Chart's item: "{0}, by {1}: {2} items" (the path, the level's name, how many).</summary>
    public virtual string ChartDrilled { get; set; } = "{0}, by {1}: {2} items";

    /// <summary>The toggle button that pauses a live Chart's updates (Refresh, Live).</summary>
    public virtual string ChartPauseUpdates { get; set; } = "Pause updates";

    /// <summary>The button that saves a chart as an image (Download).</summary>
    public virtual string ChartDownload { get; set; } = "Download as PNG";

    /// <summary>Said when a Chart zooms or pans: its first and last arguments in view, "Showing {0} to {1}".</summary>
    public virtual string ChartShowing { get; set; } = "Showing {0} to {1}";

    /// <summary>A RangeSelector's name: the strip that chooses the part of a chart in view.</summary>
    public virtual string RangeSelectorName { get; set; } = "Range in view";

    /// <summary>A RangeSelector's start handle.</summary>
    public virtual string RangeSelectorStart { get; set; } = "Start";

    /// <summary>A RangeSelector's end handle.</summary>
    public virtual string RangeSelectorEnd { get; set; } = "End";

    /// <summary>A RangeSelector's button for the last few months, as shown: "{0}M".</summary>
    public virtual string RangeMonths { get; set; } = "{0}M";

    /// <summary>A RangeSelector's button for the last few months, as read: "Last {0} months".</summary>
    public virtual string RangeMonthsName { get; set; } = "Last {0} months";

    /// <summary>A RangeSelector's button for the last month, as read.</summary>
    public virtual string RangeMonthName { get; set; } = "Last month";

    /// <summary>A RangeSelector's button for the year to date, as shown.</summary>
    public virtual string RangeYearToDate { get; set; } = "YTD";

    /// <summary>A RangeSelector's button for the year to date, as read.</summary>
    public virtual string RangeYearToDateName { get; set; } = "Year to date";

    /// <summary>A RangeSelector's button for the last year, as shown.</summary>
    public virtual string RangeYear { get; set; } = "1Y";

    /// <summary>A RangeSelector's button for the last year, as read.</summary>
    public virtual string RangeYearName { get; set; } = "Last year";

    /// <summary>A RangeSelector's button that shows everything.</summary>
    public virtual string RangeAll { get; set; } = "All";

    /// <summary>A Chart reference line's label for <c>ChartStatistic.Average</c>, with no Label of its own.</summary>
    public virtual string ChartAverage { get; set; } = "Average";

    /// <summary>A Chart reference line's label for <c>ChartStatistic.Median</c>, with no Label of its own.</summary>
    public virtual string ChartMedian { get; set; } = "Median";

    /// <summary>Joins a reference band's two ends in the chart's description, as in "Nov to Dec".</summary>
    public virtual string ChartRangeTo { get; set; } = "to";

    /// <summary>A Chart trend's name, from its series' name: "{0} trend".</summary>
    public virtual string ChartTrendName { get; set; } = "{0} trend";

    /// <summary>A Chart moving average's name, from its series' name and period: "{0}, {1}-point average".</summary>
    public virtual string ChartAverageName { get; set; } = "{0}, {1}-point average";

    /// <summary>A moving average's name over daily dates: "{0}, {1}-day average".</summary>
    public virtual string ChartAverageDays { get; set; } = "{0}, {1}-day average";

    /// <summary>A moving average's name over monthly dates: "{0}, {1}-month average".</summary>
    public virtual string ChartAverageMonths { get; set; } = "{0}, {1}-month average";

    /// <summary>A histogram series' name, with no Name of its own: what its bars are, in the tooltip and data table.</summary>
    public virtual string ChartCount { get; set; } = "Count";

    /// <summary>A bubble's size, in the tooltip and data table, when its Size isn't a property.</summary>
    public virtual string ChartSize { get; set; } = "Size";

    /// <summary>A bar series' target, in the tooltip and the data table's column for it.</summary>
    public virtual string ChartTarget { get; set; } = "Target";

    /// <summary>A Gauge's target, read after its value: "target {0}".</summary>
    public virtual string GaugeTarget { get; set; } = "target {0}";

    /// <summary>A BulletChart's tooltip and data table: the heading over a row's target, when Target isn't a property.</summary>
    public virtual string BulletTarget { get; set; } = "Target";

    /// <summary>A BulletChart's data table: the heading over the range each value is in.</summary>
    public virtual string BulletRange { get; set; } = "Range";

    /// <summary>A BulletChart's keyboard hint, rows across.</summary>
    public virtual string BulletKeys { get; set; } = "Up and down arrow keys move between rows; Escape hides the tooltip.";

    /// <summary>A vertical BulletChart's keyboard hint, rows standing as columns.</summary>
    public virtual string BulletKeysAcross { get; set; } = "Left and right arrow keys move between columns; Escape hides the tooltip.";

    /// <summary>A candlestick's or OHLC bar's prices, in its tooltip and description: open, high, low, close.</summary>
    public virtual string ChartOhlc { get; set; } = "open {0}, high {1}, low {2}, close {3}";

    /// <summary>An OHLC bar's prices with no Open: high, low, close.</summary>
    public virtual string ChartHlc { get; set; } = "high {0}, low {1}, close {2}";

    /// <summary>A box plot's box, in its tooltip and description: the median, the quartiles and the whiskers' ends.</summary>
    public virtual string ChartBox { get; set; } = "median {0}, quartiles {1} – {2}, whiskers {3} – {4}";

    /// <summary>A box plot's tooltip and table: the column for the box itself, from the first quartile to the third.</summary>
    public virtual string ChartBoxMiddle { get; set; } = "Middle half";

    /// <summary>A box plot's tooltip and table: the column for each box's mean (a box plot series' ShowMean).</summary>
    public virtual string ChartMean { get; set; } = "Mean";

    /// <summary>A box plot's table: the column for each box's first quartile.</summary>
    public virtual string ChartBoxQ1 { get; set; } = "Lower quartile";

    /// <summary>A box plot's table: the column for each box's third quartile.</summary>
    public virtual string ChartBoxQ3 { get; set; } = "Upper quartile";

    /// <summary>A box plot's table: the column for where each box's lower whisker ends.</summary>
    public virtual string ChartBoxLow { get; set; } = "Lower whisker";

    /// <summary>A box plot's table: the column for where each box's upper whisker ends.</summary>
    public virtual string ChartBoxHigh { get; set; } = "Upper whisker";

    /// <summary>A box plot's table: the column for how many values make each box.</summary>
    public virtual string ChartBoxCount { get; set; } = "Values";

    /// <summary>A box plot's table: the column for how many of each box's values are outliers.</summary>
    public virtual string ChartBoxOutlierCount { get; set; } = "Outliers";

    /// <summary>A chart's table, with several series in its rows: the column that names each one.</summary>
    public virtual string ChartSeries { get; set; } = "Series";

    /// <summary>A box's mean, after <see cref="ChartBox"/> (a box plot series' ShowMean).</summary>
    public virtual string ChartBoxMean { get; set; } = "mean {0}";

    /// <summary>How many values make a box, when it's one.</summary>
    public virtual string ChartBoxValue { get; set; } = "{0:N0} value";

    /// <summary>How many values make a box, when it's more than one.</summary>
    public virtual string ChartBoxValues { get; set; } = "{0:N0} values";

    /// <summary>How many of a box's values are outliers, past its whiskers, when it's one.</summary>
    public virtual string ChartBoxOutlier { get; set; } = "{0:N0} outlier";

    /// <summary>How many of a box's values are outliers, when it's more than one.</summary>
    public virtual string ChartBoxOutliers { get; set; } = "{0:N0} outliers";

    /// <summary>A waterfall chart's legend: the key for bars that add to the running total.</summary>
    public virtual string ChartIncrease { get; set; } = "Increase";

    /// <summary>A waterfall chart's legend: the key for bars that take from the running total.</summary>
    public virtual string ChartDecrease { get; set; } = "Decrease";

    /// <summary>A waterfall chart's legend: the key for total bars, which show everything so far.</summary>
    public virtual string ChartTotal { get; set; } = "Total";

    /// <summary>A FunnelChart's data table: the column of each stage's share of the first stage.</summary>
    public virtual string FunnelOfFirst { get; set; } = "Of the first";

    /// <summary>A FunnelChart's data table: the column of each stage's share of the stage before it.</summary>
    public virtual string FunnelOfPrevious { get; set; } = "Of the stage before";

    /// <summary>A FunnelChart stage's share of the first, read after its value: "{0} of the first".</summary>
    public virtual string FunnelOfFirstText { get; set; } = "{0} of the first";

    /// <summary>A FunnelChart stage's share of the stage before, read after its value: "{0} of the stage before".</summary>
    public virtual string FunnelOfPreviousText { get; set; } = "{0} of the stage before";

    /// <summary>A HeatmapChart cell with no value: in its tooltip, what screen readers hear, and the data table.</summary>
    public virtual string HeatmapNoData { get; set; } = "No data";

    /// <summary>A HeatmapChart's legend: the name of its color scale, for screen readers.</summary>
    public virtual string HeatmapScale { get; set; } = "Color scale";

    /// <summary>A HeatmapChart's legend: one class of values, "{0} to {1}".</summary>
    public virtual string HeatmapRangeText { get; set; } = "{0} to {1}";

    /// <summary>A diverging heatmap legend's middle value, read after its range: "middle {0}".</summary>
    public virtual string HeatmapMiddleText { get; set; } = "middle {0}";

    /// <summary>A RadarChart's scale, in its description: "Scale {0} at the center to {1} at the edge".</summary>
    public virtual string RadarScaleText { get; set; } = "Scale {0} at the center to {1} at the edge";

    /// <summary>A SankeyChart's description: "{0} flows between {1} nodes, left to right".</summary>
    public virtual string SankeyDescription { get; set; } = "{0} flows between {1} nodes, left to right";

    /// <summary>A SankeyChart node's tooltip: a flow into it, "from {0}".</summary>
    public virtual string SankeyFromText { get; set; } = "from {0}";

    /// <summary>A SankeyChart node's tooltip: a flow out of it, "to {0}".</summary>
    public virtual string SankeyToText { get; set; } = "to {0}";

    /// <summary>A SankeyChart flow's tooltip: its share of what leaves its node, "{0} of {1}" (such as "40% of Salary").</summary>
    public virtual string SankeyOfText { get; set; } = "{0} of {1}";

    /// <summary>A SankeyChart's data table: the heading over where each flow comes from, when From isn't a property.</summary>
    public virtual string SankeyFrom { get; set; } = "From";

    /// <summary>A SankeyChart's data table: the heading over where each flow goes, when To isn't a property.</summary>
    public virtual string SankeyTo { get; set; } = "To";

    /// <summary>A SankeyChart's data table: the heading over each flow's share of its node, "Share of {0}" with the From column's heading.</summary>
    public virtual string SankeyShare { get; set; } = "Share of {0}";

    /// <summary>A SankeyChart's keyboard hint.</summary>
    public virtual string SankeyKeys { get; set; } = "Arrow keys move between nodes; Escape hides the tooltip.";

    /// <summary>A TreemapChart's description with groups: "{0} tiles in {1} groups, the largest {2}".</summary>
    public virtual string TreemapDescription { get; set; } = "{0} tiles in {1} groups, the largest {2}";

    /// <summary>A TreemapChart's description without groups: "{0} tiles, the largest {1}".</summary>
    public virtual string TreemapDescriptionFlat { get; set; } = "{0} tiles, the largest {1}";

    /// <summary>A TreemapChart tile's tooltip: its share of its group, "{0} of {1}" (such as "31% of Asia").</summary>
    public virtual string TreemapOfText { get; set; } = "{0} of {1}";

    /// <summary>A TreemapChart tile's tooltip: its share of the whole, "{0} of all".</summary>
    public virtual string TreemapOfAllText { get; set; } = "{0} of all";

    /// <summary>A TreemapChart's data table: the heading over each tile's group, when Group isn't a property.</summary>
    public virtual string TreemapGroup { get; set; } = "Group";

    /// <summary>A TreemapChart's data table: the heading over each tile's name, when Name isn't a property.</summary>
    public virtual string TreemapName { get; set; } = "Name";

    /// <summary>A TreemapChart's data table: the heading over each tile's share of its group.</summary>
    public virtual string TreemapShareOfGroup { get; set; } = "Share of group";

    /// <summary>A TreemapChart's data table: the heading over each tile's share of the whole.</summary>
    public virtual string TreemapShareOfAll { get; set; } = "Share of all";

    /// <summary>A TreemapChart's keyboard hint.</summary>
    public virtual string TreemapKeys { get; set; } = "Arrow keys move between tiles; Escape hides the tooltip.";

    /// <summary>A CalendarHeatmap's data table: the heading over its column of days of the month.</summary>
    public virtual string CalendarHeatmapDay { get; set; } = "Day";

    /// <summary>A pyramid level's share of the total (FunnelChart Pyramid), read after its value: "{0} of the total".</summary>
    public virtual string FunnelOfTotalText { get; set; } = "{0} of the total";

    /// <summary>
    /// A Sparkline's accessible name, which says what a glance at it shows: "Revenue, last 12 weeks:
    /// from 41 to 48, low 39, high 52". {0} is the label; {1} and {2} the first and last values;
    /// {3} and {4} the lowest and highest.
    /// </summary>
    public virtual string SparklineSummary { get; set; } = "{0}: from {1} to {2}, low {3}, high {4}";

    /// <summary>
    /// A win-loss Sparkline's accessible name: "Games this season: 9 up, 4 down". {0} is the label;
    /// {1} and {2} the counts over and under its threshold. <see cref="SparklineEven"/> follows
    /// when some are at it.
    /// </summary>
    public virtual string SparklineWinLossSummary { get; set; } = "{0}: {1} up, {2} down";

    /// <summary>After <see cref="SparklineWinLossSummary"/>, the count at the threshold: ", 1 even".</summary>
    public virtual string SparklineEven { get; set; } = ", {0} even";

    /// <summary>ColorPicker's hex text box, after the field's label: "Brand color, hex".</summary>
    public virtual string ColorHex { get; set; } = "hex";

    /// <summary>ColorPicker's row of preset swatches.</summary>
    public virtual string ColorPresets { get; set; } = "Preset colors";

    /// <summary>ColorPanel's hex field.</summary>
    public virtual string ColorHexField { get; set; } = "Hex";

    /// <summary>ColorPanel's group of swatches.</summary>
    public virtual string ColorSwatches { get; set; } = "Colors";

    /// <summary>ColorPanel's swatch for no color (with Clearable).</summary>
    public virtual string ColorNone { get; set; } = "No color";

    /// <summary>ColorPanel's saturation and brightness area.</summary>
    public virtual string ColorArea { get; set; } = "Saturation and brightness";

    /// <summary>ColorPanel's area's role, read after its name.</summary>
    public virtual string ColorAreaRole { get; set; } = "2D slider";

    /// <summary>ColorPanel's area's value. {0} is the saturation, {1} the brightness, {2} the color's name.</summary>
    public virtual string ColorAreaValue { get; set; } = "Saturation {0}%, brightness {1}%, {2}";

    /// <summary>ColorPanel's hue slider.</summary>
    public virtual string ColorHue { get; set; } = "Hue";

    /// <summary>ColorPanel's hue slider's value. {0} is the degrees, {1} the hue's name.</summary>
    public virtual string ColorHueValue { get; set; } = "{0}°, {1}";

    /// <summary>ColorPanel's opacity slider and field.</summary>
    public virtual string ColorOpacity { get; set; } = "Opacity";

    /// <summary>ColorPanel's red field.</summary>
    public virtual string ColorRedChannel { get; set; } = "Red";

    /// <summary>ColorPanel's green field.</summary>
    public virtual string ColorGreenChannel { get; set; } = "Green";

    /// <summary>ColorPanel's blue field.</summary>
    public virtual string ColorBlueChannel { get; set; } = "Blue";

    /// <summary>ColorPanel's eyedropper button (in browsers that have one).</summary>
    public virtual string ColorEyedropper { get; set; } = "Pick a color from the screen";

    /// <summary>A color's name, for screen readers.</summary>
    public virtual string ColorBlack { get; set; } = "black";

    /// <summary>A color's name, for screen readers.</summary>
    public virtual string ColorWhite { get; set; } = "white";

    /// <summary>A color's name, for screen readers.</summary>
    public virtual string ColorGray { get; set; } = "gray";

    /// <summary>A color's name, for screen readers.</summary>
    public virtual string ColorRed { get; set; } = "red";

    /// <summary>A color's name, for screen readers.</summary>
    public virtual string ColorOrange { get; set; } = "orange";

    /// <summary>A color's name, for screen readers.</summary>
    public virtual string ColorYellow { get; set; } = "yellow";

    /// <summary>A color's name, for screen readers.</summary>
    public virtual string ColorGreen { get; set; } = "green";

    /// <summary>A color's name, for screen readers.</summary>
    public virtual string ColorCyan { get; set; } = "cyan";

    /// <summary>A color's name, for screen readers.</summary>
    public virtual string ColorBlue { get; set; } = "blue";

    /// <summary>A color's name, for screen readers.</summary>
    public virtual string ColorPurple { get; set; } = "purple";

    /// <summary>A color's name, for screen readers.</summary>
    public virtual string ColorPink { get; set; } = "pink";

    /// <summary>A light color's name, such as light green. {0} is the color.</summary>
    public virtual string ColorLight { get; set; } = "light {0}";

    /// <summary>A dark color's name, such as dark blue. {0} is the color.</summary>
    public virtual string ColorDark { get; set; } = "dark {0}";

    /// <summary>ColorPicker's message for a value that isn't a hex color. {0} is the field's label.</summary>
    public virtual string ColorInvalid { get; set; } = "The {0} field must be a color like #2563eb.";

    /// <summary>ColorSchemeSwitch's toggle, on a light page.</summary>
    public virtual string SchemeToDark { get; set; } = "Switch to dark mode";

    /// <summary>ColorSchemeSwitch's toggle, on a dark page.</summary>
    public virtual string SchemeToLight { get; set; } = "Switch to light mode";

    /// <summary>A RichTextEditor's toolbar.</summary>
    public virtual string RichTextToolbar { get; set; } = "Formatting";

    /// <summary>A RichTextEditor's list of text styles.</summary>
    public virtual string RichTextStyle { get; set; } = "Text style";

    /// <summary>A RichTextEditor's group of text format tools: bold, italic and the rest.</summary>
    public virtual string RichTextFormats { get; set; } = "Text formats";

    /// <summary>A RichTextEditor's group of list tools.</summary>
    public virtual string RichTextLists { get; set; } = "Lists";

    /// <summary>A RichTextEditor's group of undo and redo.</summary>
    public virtual string RichTextHistory { get; set; } = "History";

    /// <summary>A RichTextEditor's text style: normal text, not a heading.</summary>
    public virtual string RichTextParagraph { get; set; } = "Normal text";

    /// <summary>A RichTextEditor's text style: a heading. {0} is its level, 2 to 4.</summary>
    public virtual string RichTextHeading { get; set; } = "Heading {0}";

    /// <summary>A RichTextEditor's quote tool.</summary>
    public virtual string RichTextQuote { get; set; } = "Quote";

    /// <summary>A RichTextEditor's code block tool.</summary>
    public virtual string RichTextCodeBlock { get; set; } = "Code block";

    /// <summary>A RichTextEditor's bold tool.</summary>
    public virtual string RichTextBold { get; set; } = "Bold";

    /// <summary>A RichTextEditor's italic tool.</summary>
    public virtual string RichTextItalic { get; set; } = "Italic";

    /// <summary>A RichTextEditor's underline tool.</summary>
    public virtual string RichTextUnderline { get; set; } = "Underline";

    /// <summary>A RichTextEditor's strikethrough tool.</summary>
    public virtual string RichTextStrikethrough { get; set; } = "Strikethrough";

    /// <summary>A RichTextEditor's inline code tool.</summary>
    public virtual string RichTextInlineCode { get; set; } = "Code";

    /// <summary>A RichTextEditor's link tool, and its dialog.</summary>
    public virtual string RichTextLink { get; set; } = "Link";

    /// <summary>A RichTextEditor's link dialog: the address field.</summary>
    public virtual string RichTextLinkAddress { get; set; } = "Address";

    /// <summary>A RichTextEditor's link dialog: the text field, for a link with no text selected.</summary>
    public virtual string RichTextLinkText { get; set; } = "Text";

    /// <summary>A RichTextEditor's link dialog: the checkbox that opens the link in a new tab.</summary>
    public virtual string RichTextLinkNewTab { get; set; } = "Open in a new tab";

    /// <summary>A RichTextEditor's link dialog: the button that adds or changes the link.</summary>
    public virtual string RichTextLinkApply { get; set; } = "OK";

    /// <summary>A RichTextEditor's link dialog: the button that removes the link, keeping its text.</summary>
    public virtual string RichTextLinkRemove { get; set; } = "Remove link";

    /// <summary>A RichTextEditor's message for text over its MaxLength. {0} is the field's label, {1} the most characters.</summary>
    public virtual string RichTextTooLong { get; set; } = "{0} must be {1} characters or fewer.";

    /// <summary>A RichTextEditor's subscript tool.</summary>
    public virtual string RichTextSubscript { get; set; } = "Subscript";

    /// <summary>A RichTextEditor's superscript tool.</summary>
    public virtual string RichTextSuperscript { get; set; } = "Superscript";

    /// <summary>A RichTextEditor's group of alignment tools.</summary>
    public virtual string RichTextAlignment { get; set; } = "Alignment";

    /// <summary>A RichTextEditor's tool that aligns text to the start of the line (the left, in left-to-right languages).</summary>
    public virtual string RichTextAlignStart { get; set; } = "Align left";

    /// <summary>A RichTextEditor's tool that centers text.</summary>
    public virtual string RichTextAlignCenter { get; set; } = "Center";

    /// <summary>A RichTextEditor's tool that aligns text to the end of the line (the right, in left-to-right languages).</summary>
    public virtual string RichTextAlignEnd { get; set; } = "Align right";

    /// <summary>A RichTextEditor's text color menu.</summary>
    public virtual string RichTextTextColor { get; set; } = "Text color";

    /// <summary>A RichTextEditor's highlight menu.</summary>
    public virtual string RichTextHighlight { get; set; } = "Highlight";

    /// <summary>A RichTextEditor's font menu.</summary>
    public virtual string RichTextFont { get; set; } = "Font";

    /// <summary>A RichTextEditor's text size menu.</summary>
    public virtual string RichTextSize { get; set; } = "Size";

    /// <summary>A RichTextEditor's text size: the smallest step.</summary>
    public virtual string RichTextSizeSmaller { get; set; } = "Smaller";

    /// <summary>A RichTextEditor's text size: small.</summary>
    public virtual string RichTextSizeSmall { get; set; } = "Small";

    /// <summary>A RichTextEditor's text size: the text's own.</summary>
    public virtual string RichTextSizeNormal { get; set; } = "Normal";

    /// <summary>A RichTextEditor's text size: large.</summary>
    public virtual string RichTextSizeLarge { get; set; } = "Large";

    /// <summary>A RichTextEditor's text size: the largest step.</summary>
    public virtual string RichTextSizeLargest { get; set; } = "Largest";

    /// <summary>A RichTextEditor's text size: larger.</summary>
    public virtual string RichTextSizeLarger { get; set; } = "Larger";

    /// <summary>A RichTextEditor's choice of no font or color of its own.</summary>
    public virtual string RichTextDefault { get; set; } = "Default";

    /// <summary>A RichTextEditor's choice of no highlight.</summary>
    public virtual string RichTextNone { get; set; } = "None";

    /// <summary>RichTextEditor's last item in its color menus, with CustomColors.</summary>
    public virtual string RichTextCustomColor { get; set; } = "Custom…";

    /// <summary>The title of RichTextEditor's custom color dialog for the text color.</summary>
    public virtual string RichTextCustomTextColor { get; set; } = "Custom text color";

    /// <summary>The title of RichTextEditor's custom color dialog for the highlight.</summary>
    public virtual string RichTextCustomHighlight { get; set; } = "Custom highlight";

    /// <summary>A RichTextEditor's palette color.</summary>
    public virtual string RichTextRed { get; set; } = "Red";

    /// <summary>A RichTextEditor's palette color.</summary>
    public virtual string RichTextOrange { get; set; } = "Orange";

    /// <summary>A RichTextEditor's palette color.</summary>
    public virtual string RichTextYellow { get; set; } = "Yellow";

    /// <summary>A RichTextEditor's palette color.</summary>
    public virtual string RichTextGreen { get; set; } = "Green";

    /// <summary>A RichTextEditor's palette color.</summary>
    public virtual string RichTextBlue { get; set; } = "Blue";

    /// <summary>A RichTextEditor's palette color.</summary>
    public virtual string RichTextPurple { get; set; } = "Purple";

    /// <summary>A RichTextEditor's palette color.</summary>
    public virtual string RichTextGray { get; set; } = "Gray";

    /// <summary>A RichTextEditor's image tool, and its dialog.</summary>
    public virtual string RichTextImage { get; set; } = "Image";

    /// <summary>A RichTextEditor's image dialog: the image's address.</summary>
    public virtual string RichTextImageAddress { get; set; } = "Image address";

    /// <summary>A RichTextEditor's image dialog: the button that uploads an image from the device.</summary>
    public virtual string RichTextImageUpload { get; set; } = "Upload";

    /// <summary>A RichTextEditor's image dialog: the image's alternative text.</summary>
    public virtual string RichTextImageAlt { get; set; } = "Description";

    /// <summary>A RichTextEditor's image dialog: the hint under the description.</summary>
    public virtual string RichTextImageAltHint { get; set; } = "What the image shows, for people who can't see it.";

    /// <summary>A RichTextEditor's image dialog: the checkbox for an image that adds nothing to the text.</summary>
    public virtual string RichTextImageDecorative { get; set; } = "Decorative: no description";

    /// <summary>A RichTextEditor's image dialog: the button that removes the image.</summary>
    public virtual string RichTextImageRemove { get; set; } = "Remove image";

    /// <summary>A RichTextEditor's image dialog: the message for an image with no description.</summary>
    public virtual string RichTextImageNeedsAlt { get; set; } = "Describe the image, or mark it decorative.";

    /// <summary>A RichTextEditor's image dialog: the message when an upload fails.</summary>
    public virtual string RichTextImageFailed { get; set; } = "The image couldn't be uploaded.";

    /// <summary>A RichTextEditor's image dialog: the message for an image over MaxImageSize.</summary>
    public virtual string RichTextImageTooLarge { get; set; } = "The image is too large.";

    /// <summary>A RichTextEditor's link dialog title, for a new link.</summary>
    public virtual string RichTextLinkAdd { get; set; } = "Add link";

    /// <summary>A RichTextEditor's link dialog title, for a link already there.</summary>
    public virtual string RichTextLinkEdit { get; set; } = "Edit link";

    /// <summary>A RichTextEditor's image dialog title, for a new image.</summary>
    public virtual string RichTextImageInsert { get; set; } = "Insert image";

    /// <summary>A RichTextEditor's image dialog title, for an image already there.</summary>
    public virtual string RichTextImageEdit { get; set; } = "Edit image";

    /// <summary>A RichTextEditor's image dialog: the tabs for where the image comes from.</summary>
    public virtual string RichTextImageSource { get; set; } = "Image source";

    /// <summary>A RichTextEditor's image dialog: the tab for a file from the device.</summary>
    public virtual string RichTextImageFromDevice { get; set; } = "From your device";

    /// <summary>A RichTextEditor's image dialog: the tab for an image's address.</summary>
    public virtual string RichTextImageFromWeb { get; set; } = "From the web";

    /// <summary>A RichTextEditor's image dialog: the button that opens the file picker.</summary>
    public virtual string RichTextImageSelect { get; set; } = "Select a file";

    /// <summary>A RichTextEditor's image dialog: the text beside Select a file.</summary>
    public virtual string RichTextImageDrop { get; set; } = "or drop a file here";

    /// <summary>A RichTextEditor's image dialog: the width field.</summary>
    public virtual string RichTextImageWidth { get; set; } = "Width (px)";

    /// <summary>A RichTextEditor's image dialog: the height field.</summary>
    public virtual string RichTextImageHeight { get; set; } = "Height (px)";

    /// <summary>A RichTextEditor's image dialog: the toggle that keeps width and height in proportion.</summary>
    public virtual string RichTextImageKeepRatio { get; set; } = "Keep proportions";

    /// <summary>A RichTextEditor's table menu.</summary>
    public virtual string RichTextTable { get; set; } = "Table";

    /// <summary>A RichTextEditor's table menu: insert a table; and the title of its dialog.</summary>
    public virtual string RichTextTableInsert { get; set; } = "Insert table";

    /// <summary>A RichTextEditor's insert table dialog: the number of rows.</summary>
    public virtual string RichTextTableRows { get; set; } = "Rows";

    /// <summary>A RichTextEditor's insert table dialog: the number of columns.</summary>
    public virtual string RichTextTableColumns { get; set; } = "Columns";

    /// <summary>A RichTextEditor's table: a first row of column headers.</summary>
    public virtual string RichTextTableHeader { get; set; } = "Header row";

    /// <summary>A RichTextEditor's table menu.</summary>
    public virtual string RichTextTableRowAbove { get; set; } = "Insert row above";

    /// <summary>A RichTextEditor's table menu.</summary>
    public virtual string RichTextTableRowBelow { get; set; } = "Insert row below";

    /// <summary>A RichTextEditor's table menu: a column before the caret's (left, in left-to-right languages).</summary>
    public virtual string RichTextTableColumnBefore { get; set; } = "Insert column left";

    /// <summary>A RichTextEditor's table menu: a column after the caret's (right, in left-to-right languages).</summary>
    public virtual string RichTextTableColumnAfter { get; set; } = "Insert column right";

    /// <summary>A RichTextEditor's table menu: aligns the caret's column to the start (left, in left-to-right languages).</summary>
    public virtual string RichTextTableAlignStart { get; set; } = "Align column left";

    /// <summary>A RichTextEditor's table menu: centers the caret's column.</summary>
    public virtual string RichTextTableAlignCenter { get; set; } = "Center column";

    /// <summary>A RichTextEditor's table menu: aligns the caret's column to the end (right, in left-to-right languages).</summary>
    public virtual string RichTextTableAlignEnd { get; set; } = "Align column right";

    /// <summary>A RichTextEditor's table menu.</summary>
    public virtual string RichTextTableDeleteRow { get; set; } = "Delete row";

    /// <summary>A RichTextEditor's table menu.</summary>
    public virtual string RichTextTableDeleteColumn { get; set; } = "Delete column";

    /// <summary>A RichTextEditor's table menu.</summary>
    public virtual string RichTextTableDelete { get; set; } = "Delete table";

    /// <summary>A RichTextEditor's code block language menu.</summary>
    public virtual string RichTextCodeLanguage { get; set; } = "Code language";

    /// <summary>A RichTextEditor's code block language: none.</summary>
    public virtual string RichTextPlainText { get; set; } = "Plain text";

    /// <summary>A RichTextEditor's bulleted list tool.</summary>
    public virtual string RichTextBulletedList { get; set; } = "Bulleted list";

    /// <summary>A RichTextEditor's numbered list tool.</summary>
    public virtual string RichTextNumberedList { get; set; } = "Numbered list";

    /// <summary>A Chat's message box.</summary>
    public virtual string ChatMessage { get; set; } = "Message";

    /// <summary>A Chat's button that scrolls back to the newest entry.</summary>
    public virtual string ChatJumpToLatest { get; set; } = "Jump to latest";

    /// <summary>A Chat's message box's description, once its keys work.</summary>
    public virtual string ChatKeys { get; set; } = "Enter sends; Shift+Enter adds a line";

    /// <summary>A Chat's button that attaches files.</summary>
    public virtual string ChatAttach { get; set; } = "Attach files";

    /// <summary>The list of files attached to a Chat's next message.</summary>
    public virtual string ChatAttached { get; set; } = "Attached files";

    /// <summary>A Chat's button that removes an attached file: {0} is its name.</summary>
    public virtual string ChatRemoveFile { get; set; } = "Remove {0}";

    /// <summary>A Chat's error for too many files: {0} is the most allowed.</summary>
    public virtual string ChatTooManyFiles { get; set; } = "A message can have up to {0} files.";

    /// <summary>A Chat's send button.</summary>
    public virtual string ChatSend { get; set; } = "Send";

    /// <summary>A Chat's button that stops a reply.</summary>
    public virtual string ChatStop { get; set; } = "Stop";

    /// <summary>A Chat's group of suggested messages.</summary>
    public virtual string ChatSuggestions { get; set; } = "Suggestions";

    /// <summary>Announced when a Chat's reply starts.</summary>
    public virtual string ChatReplying { get; set; } = "Replying";

    /// <summary>Announced when a Chat's reply is done.</summary>
    public virtual string ChatReplied { get; set; } = "Reply complete";

    /// <summary>Announced when a Chat's reply is stopped.</summary>
    public virtual string ChatStopped { get; set; } = "Stopped";

    /// <summary>A Chat's failed reply, announced and shown on it.</summary>
    public virtual string ChatReplyFailed { get; set; } = "Reply failed";

    /// <summary>A ChatEntry of yours that failed to send.</summary>
    public virtual string ChatNotSent { get; set; } = "Not sent";

    /// <summary>A failed ChatEntry's retry button.</summary>
    public virtual string ChatRetry { get; set; } = "Retry";

    /// <summary>A ChatEntry's copy button.</summary>
    public virtual string ChatCopy { get; set; } = "Copy";

    /// <summary>The hidden heading of a ChatEntry of yours.</summary>
    public virtual string ChatYouSaid { get; set; } = "You said";

    /// <summary>The hidden heading of someone else's ChatEntry: {0} is the author.</summary>
    public virtual string ChatSaid { get; set; } = "{0} said";

    /// <summary>The hidden heading of a ChatEntry with no author.</summary>
    public virtual string ChatReply { get; set; } = "Reply";

    /// <summary>A Chat's day separator for today.</summary>
    public virtual string ChatToday { get; set; } = "Today";

    /// <summary>A Chat's day separator for yesterday.</summary>
    public virtual string ChatYesterday { get; set; } = "Yesterday";

    /// <summary>A RichTextEditor's task list tool.</summary>
    public virtual string RichTextTaskList { get; set; } = "Task list";

    /// <summary>A RichTextEditor's tool that indents a list item.</summary>
    public virtual string RichTextIndent { get; set; } = "Indent";

    /// <summary>A RichTextEditor's tool that outdents a list item.</summary>
    public virtual string RichTextOutdent { get; set; } = "Outdent";

    /// <summary>A RichTextEditor's horizontal line tool.</summary>
    public virtual string RichTextHorizontalRule { get; set; } = "Horizontal line";

    /// <summary>A RichTextEditor's tool that removes text formats.</summary>
    public virtual string RichTextClearFormatting { get; set; } = "Clear formatting";

    /// <summary>A RichTextEditor's undo tool.</summary>
    public virtual string RichTextUndo { get; set; } = "Undo";

    /// <summary>A RichTextEditor's redo tool.</summary>
    public virtual string RichTextRedo { get; set; } = "Redo";
}
