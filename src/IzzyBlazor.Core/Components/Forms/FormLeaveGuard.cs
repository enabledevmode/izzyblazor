using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Routing;

namespace IzzyBlazor;

/// <summary>
/// Form's ConfirmLeave: the dialog that asks before leaving with unsaved changes, and what tells
/// the page's script (elements/leave-guard.ts) whether there are any.
/// </summary>
/// <remarks>
/// The script catches links and other forms' posts in both modes, and reload or close with
/// beforeunload. On interactive pages this also catches NavigateTo, and back and forward when the
/// whole app is interactive, through Blazor's location-changing handlers. The same dialog asks.
///
/// data-ib-unsaved: on a static page, the form shows validation errors (the last post wasn't
/// saved, so its values are unsaved though they're on the page). On an interactive page, the
/// EditContext is modified. The script also compares the form's values with those it had when
/// the user first touched it, which covers typing a value back on static pages and an edit whose
/// change event hasn't reached C# yet on interactive ones.
/// data-ib-epoch: goes up when an interactive form becomes unmodified (a save), so the script
/// forgets the values it compared with.
/// </remarks>
internal sealed class FormLeaveGuard : ComponentBase, IDisposable
{
    private readonly string _messageId = IdGenerator.Next("ib-leave");
    private EditContext? _context;
    private IDisposable? _registration;
    private IzzyText? _text;
    private bool _unsaved;
    private bool _open;
    private bool _leaving;
    private string? _target;
    private int _epoch;

    /// <summary>The form's edit context. A parameter, not only the cascade, so a re-render of the form re-renders this.</summary>
    [Parameter, EditorRequired] public EditContext EditContext { get; set; } = default!;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Inject] private IServiceProvider Services { get; set; } = default!;

    private IzzyText Text => _text ??= IzzyText.From(Services);

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_context, EditContext))
        {
            Unsubscribe();
            _context = EditContext;
            _context.OnFieldChanged += Changed;
            _context.OnValidationStateChanged += Changed;
        }

        var unsaved = RendererInfo.IsInteractive ? EditContext.IsModified() : EditContext.GetValidationMessages().Any();
        if (_unsaved && !unsaved)
        {
            _epoch++;
        }

        _unsaved = unsaved;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "ib-leave-guard");
        builder.AddAttribute(1, "style", "display:contents");
        builder.AddAttribute(2, "data-ib-unsaved", _unsaved ? "" : null);
        builder.AddAttribute(3, "data-ib-interactive", RendererInfo.IsInteractive ? "" : null);
        builder.AddAttribute(4, "data-ib-epoch", _epoch);

        builder.OpenElement(5, "input");
        builder.AddAttribute(6, "type", "hidden");
        builder.AddAttribute(7, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, LeaveTo));
        builder.CloseElement();

        builder.OpenComponent<Dialog>(8);
        builder.AddComponentParameter(9, nameof(Dialog.Title), Text.LeaveTitle);
        builder.AddComponentParameter(10, nameof(Dialog.Open), _open);
        builder.AddComponentParameter(11, nameof(Dialog.OpenChanged), EventCallback.Factory.Create<bool>(this, open => _open = open));
        builder.AddComponentParameter(12, nameof(Dialog.Size), DialogSize.Sm);
        builder.AddComponentParameter(13, nameof(Dialog.InsideForm), true);
        builder.AddComponentParameter(14, nameof(Dialog.QueryKey), null);
        builder.AddComponentParameter(15, nameof(Dialog.ShowCloseButton), false);
        builder.AddComponentParameter(16, nameof(Dialog.CloseOnBackdrop), false);
        builder.AddAttribute(17, "role", "alertdialog");
        builder.AddAttribute(18, "aria-describedby", _messageId);
        builder.AddAttribute(19, nameof(Dialog.ChildContent), (RenderFragment)(content =>
        {
            content.OpenElement(20, "p");
            content.AddAttribute(21, "id", _messageId);
            content.AddAttribute(22, "class", "ib-confirm-message");
            content.AddContent(23, Text.LeaveMessage);
            content.CloseElement();

            content.OpenComponent<DialogFooter>(24);
            content.AddAttribute(25, nameof(DialogFooter.ChildContent), (RenderFragment)(footer =>
            {
                footer.OpenComponent<DialogClose>(26);
                footer.AddAttribute(27, "autofocus", true);
                footer.AddAttribute(28, nameof(DialogClose.ChildContent), (RenderFragment)(b => b.AddContent(29, Text.Stay)));
                footer.CloseComponent();

                footer.OpenComponent<Button>(30);
                footer.AddComponentParameter(31, nameof(Button.Accent), Accent.Danger);
                footer.AddAttribute(32, "data-ib-leave", true);
                footer.AddAttribute(33, nameof(Button.ChildContent), (RenderFragment)(b => b.AddContent(34, Text.Leave)));
                footer.CloseComponent();
            }));
            content.CloseComponent();
        }));
        builder.CloseComponent();

        builder.CloseElement();
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender && RendererInfo.IsInteractive)
        {
            _registration = Navigation.RegisterLocationChangingHandler(OnLocationChanging);
        }
    }

    private void Changed(object? sender, EventArgs e)
    {
        // OnParametersSet works the state out; a field change doesn't set parameters.
        OnParametersSet();
        StateHasChanged();
    }

    private ValueTask OnLocationChanging(LocationChangingContext context)
    {
        if (_leaving)
        {
            _leaving = false;
            return ValueTask.CompletedTask;
        }

        if (!EditContext.IsModified())
        {
            return ValueTask.CompletedTask;
        }

        context.PreventNavigation();
        _target = context.TargetLocation;
        _open = true;
        StateHasChanged();
        return ValueTask.CompletedTask;
    }

    // The script reports Leave here on interactive pages: the link's address, or empty when this
    // asked (back/forward or NavigateTo), to go where that was going.
    private void LeaveTo(ChangeEventArgs e)
    {
        var url = e.Value as string;
        url = string.IsNullOrEmpty(url) ? _target : url;
        _open = false;
        _target = null;
        if (url is null)
        {
            return;
        }

        _leaving = true;
        Navigation.NavigateTo(url);
    }

    private void Unsubscribe()
    {
        if (_context is not null)
        {
            _context.OnFieldChanged -= Changed;
            _context.OnValidationStateChanged -= Changed;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Unsubscribe();
        _registration?.Dispose();
    }
}
