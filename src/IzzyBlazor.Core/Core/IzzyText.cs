namespace IzzyBlazor;

/// <summary>
/// The text IzzyBlazor's components show or announce when you don't set it on the component:
/// "Close", "Loading", "Next page" and so on. English by default. Each one is also a parameter on
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

    /// <summary>A Table filter's accessible name: "Filter Customer".</summary>
    public virtual string FilterColumn { get; set; } = "Filter {0}";

    /// <summary>The first option of a Table filter select, meaning no filter.</summary>
    public virtual string All { get; set; } = "All";

    /// <summary>A Table filter menu with several values chosen: "2 selected".</summary>
    public virtual string FilterSelected { get; set; } = "{0} selected";

    /// <summary>A Table filter option for true.</summary>
    public virtual string Yes { get; set; } = "Yes";

    /// <summary>A Table filter option for false.</summary>
    public virtual string No { get; set; } = "No";

    /// <summary>A Banner's region name.</summary>
    public virtual string Announcement { get; set; } = "Announcement";

    /// <summary>The confirm button in DialogService.ConfirmAsync.</summary>
    public virtual string Ok { get; set; } = "OK";

    /// <summary>Cancel in ConfirmButton's dialog and DialogService.ConfirmAsync.</summary>
    public virtual string Cancel { get; set; } = "Cancel";

    /// <summary>Announced while busy. Button Loading, Spinner.</summary>
    public virtual string Loading { get; set; } = "Loading";

    /// <summary>Before an item's name on its remove button: "Remove Blazor". Tag, MultiSelect chips.</summary>
    public virtual string Remove { get; set; } = "Remove";

    // Forms

    /// <summary>FormErrors' heading.</summary>
    public virtual string FormErrorsTitle { get; set; } = "Please fix the following";

    /// <summary>Combobox and MultiSelect when nothing matches.</summary>
    public virtual string NoMatches { get; set; } = "No matches";

    /// <summary>Combobox while searching on the server.</summary>
    public virtual string Searching { get; set; } = "Searching…";

    /// <summary>After the label of a field that isn't required, in a Form with <c>RequiredMark.Optional</c>.</summary>
    public virtual string Optional { get; set; } = "(optional)";

    /// <summary>For screen readers, after a tab's label when one field in it has an error.</summary>
    public virtual string OneError { get; set; } = "1 error";

    /// <summary>For screen readers, after a tab's label when fields in it have errors: "{0} errors".</summary>
    public virtual string ErrorCount { get; set; } = "{0} errors";

    /// <summary>The button that opens a Combobox or MultiSelect list.</summary>
    public virtual string ShowOptions { get; set; } = "Show options";

    /// <summary>NumberField's increase button.</summary>
    public virtual string Increase { get; set; } = "Increase";

    /// <summary>NumberField's decrease button.</summary>
    public virtual string Decrease { get; set; } = "Decrease";

    /// <summary>A password field's reveal button (a toggle, pressed while the password shows).</summary>
    public virtual string ShowPassword { get; set; } = "Show password";

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

    /// <summary>FilterForm's name.</summary>
    public virtual string Filters { get; set; } = "Filters";

    /// <summary>FilterForm's apply button.</summary>
    public virtual string Apply { get; set; } = "Apply";

    /// <summary>FilterForm's clear link.</summary>
    public virtual string Clear { get; set; } = "Clear";

    // Data and feedback

    /// <summary>Table with no rows.</summary>
    public virtual string NoItems { get; set; } = "No items";

    /// <summary>Table's select-all checkbox.</summary>
    public virtual string SelectAllRows { get; set; } = "Select all rows";

    /// <summary>Table's row checkboxes, before the row's name.</summary>
    public virtual string SelectRow { get; set; } = "Select";

    /// <summary>Table's scroll region name when it has no Caption.</summary>
    public virtual string TableName { get; set; } = "Table";

    /// <summary>Table's pagination name: {0} is the caption (or <see cref="TableName"/>).</summary>
    public virtual string TablePages { get; set; } = "{0} pages";

    /// <summary>Table's row range under the rows: {0} first, {1} last, {2} total ("1–10 of 42").</summary>
    public virtual string TableRange { get; set; } = "{0:N0}–{1:N0} of {2:N0}";

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

    // Navigation and layout

    /// <summary>Breadcrumb's name.</summary>
    public virtual string Breadcrumb { get; set; } = "Breadcrumb";

    /// <summary>Pagination's name.</summary>
    public virtual string Pagination { get; set; } = "Pagination";

    /// <summary>Pagination's previous button.</summary>
    public virtual string PreviousPage { get; set; } = "Previous page";

    /// <summary>Pagination's next button.</summary>
    public virtual string NextPage { get; set; } = "Next page";

    /// <summary>Before a page number: "Page 3".</summary>
    public virtual string Page { get; set; } = "Page";

    /// <summary>Stepper's name.</summary>
    public virtual string Progress { get; set; } = "Progress";

    /// <summary>Read after a finished step's label.</summary>
    public virtual string Completed { get; set; } = "(completed)";

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

    /// <summary>OnThisPage's heading, which also names its navigation landmark.</summary>
    public virtual string OnThisPage { get; set; } = "On this page";

    // Typography

    /// <summary>A code block's copy button.</summary>
    public virtual string CopyCode { get; set; } = "Copy code";

    /// <summary>CopyButton's name when it shows no text.</summary>
    public virtual string Copy { get; set; } = "Copy";

    /// <summary>Announced after copying.</summary>
    public virtual string Copied { get; set; } = "Copied";

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
