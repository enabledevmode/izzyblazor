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

    /// <summary>The button that opens a Combobox or MultiSelect list.</summary>
    public virtual string ShowOptions { get; set; } = "Show options";

    /// <summary>NumberInput's increase button.</summary>
    public virtual string Increase { get; set; } = "Increase";

    /// <summary>NumberInput's decrease button.</summary>
    public virtual string Decrease { get; set; } = "Decrease";

    /// <summary>A password field's reveal button (a toggle, pressed while the password shows).</summary>
    public virtual string ShowPassword { get; set; } = "Show password";

    /// <summary>FileUpload's zone, before its button, for one file.</summary>
    public virtual string DropFile { get; set; } = "Drag a file here, or";

    /// <summary>FileUpload's zone, before its button, with Multiple.</summary>
    public virtual string DropFiles { get; set; } = "Drag files here, or";

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

    /// <summary>The button that opens the sidebar drawer on phones.</summary>
    public virtual string OpenMenu { get; set; } = "Open menu";

    /// <summary>The sidebar drawer's close button.</summary>
    public virtual string CloseMenu { get; set; } = "Close menu";

    /// <summary>The button that collapses the sidebar to icons.</summary>
    public virtual string CollapseSidebar { get; set; } = "Collapse sidebar";

    // Typography

    /// <summary>A code block's copy button.</summary>
    public virtual string CopyCode { get; set; } = "Copy code";

    /// <summary>Announced after copying.</summary>
    public virtual string Copied { get; set; } = "Copied";

    /// <summary>After a link that opens a new tab.</summary>
    public virtual string OpensInNewTab { get; set; } = "(opens in a new tab)";
}
