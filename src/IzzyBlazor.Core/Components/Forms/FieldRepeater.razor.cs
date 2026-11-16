using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components.Forms;

namespace IzzyBlazor;

/// <summary>Rows of the same fields that people add and remove, such as invoice lines; see the component's overview.</summary>
/// <typeparam name="TItem">One row's item, such as an invoice line. It keeps its members when trimmed, which the rows' validation reads.</typeparam>
public partial class FieldRepeater<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>
{
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "TItem is annotated to keep all its members, which the validator reads.")]
    private void Validate(object? sender, ValidationRequestedEventArgs e)
    {
        _store!.Clear();
        if (Items is null || ReadOnly)
        {
            return;
        }

        foreach (var item in Items)
        {
            if (item is null)
            {
                continue;
            }

            var results = new List<ValidationResult>();
            Validator.TryValidateObject(item, new ValidationContext(item, item.GetType().Name, Services, null), results, validateAllProperties: true);
            foreach (var result in results)
            {
                foreach (var member in result.MemberNames)
                {
                    var field = new FieldIdentifier(item, member);
                    // An app that validates nested lists itself (AddValidation) has said it already.
                    if (result.ErrorMessage is { } message && !_context!.GetValidationMessages(field).Contains(message))
                    {
                        _store.Add(field, message);
                    }
                }
            }
        }

        if (Items.Count < MinItems)
        {
            _store.Add(TooFewField, string.Format(Culture, TextDefaults.RepeaterTooFew, MinItems == 1 ? TextDefaults.RepeaterOneItem : string.Format(Culture, TextDefaults.RepeaterItems, MinItems)));
        }
    }

}
