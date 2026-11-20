using Microsoft.AspNetCore.Components.Forms;

namespace {{RootNamespace}}.Components;

/// <summary>
/// What every record form does as it saves; Components/_Imports.razor brings these in by name
/// (<c>Fail(form, nameof(Input.Email), "…")</c>).
/// </summary>
public static class FormHelpers
{
    /// <summary>
    /// Puts <paramref name="message"/> under <paramref name="field"/>, for what only the database can tell:
    /// another contact with the email, a record deleted while its form was open.
    /// </summary>
    public static void Fail(EditContext form, string field, string message)
    {
        new ValidationMessageStore(form).Add(form.Field(field), message);
        form.NotifyValidationStateChanged();
    }

    /// <summary>A field's text trimmed, or null when nothing was typed: an empty field is stored as no value.</summary>
    public static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
