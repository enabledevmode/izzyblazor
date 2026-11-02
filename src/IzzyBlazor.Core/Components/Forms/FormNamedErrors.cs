using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Forms.Mapping;

namespace IzzyBlazor;

/// <summary>
/// Reports the values of a static post that couldn't be read into a model named with
/// <c>[SupplyParameterFromForm(FormName = "...")]</c>. Blazor files those under the form's name, where
/// its own validator doesn't look, so a number too big for an <c>int</c> would pass as valid and save
/// as 0. A model with no <c>FormName</c> doesn't need this, as Blazor reports its errors itself.
/// </summary>
internal sealed class FormNamedErrors : ComponentBase, IDisposable
{
    private EditContext? _context;
    private ValidationMessageStore? _messages;

    [Parameter, EditorRequired] public string FormName { get; set; } = "";

    [CascadingParameter] private EditContext? EditContext { get; set; }

    [CascadingParameter] private FormMappingContext? Mapping { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_context, EditContext))
        {
            return;
        }

        Dispose();
        _context = EditContext;
        if (_context is not null)
        {
            _messages = new ValidationMessageStore(_context);
            _context.OnValidationRequested += Check;
        }
    }

    private void Check(object? sender, ValidationRequestedEventArgs e)
    {
        _messages!.Clear();
        foreach (var error in Mapping?.GetAllErrors(FormName) ?? [])
        {
            if (error.Container is { } container)
            {
                _messages.Add(new FieldIdentifier(container, error.Name), error.ErrorMessages.Select(m => m.ToString()));
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_context is not null)
        {
            _context.OnValidationRequested -= Check;
            _messages?.Clear();
        }

        (_context, _messages) = (null, null);
    }
}
