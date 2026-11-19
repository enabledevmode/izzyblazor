using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace IzzyBlazor;

/// <summary>What a static post means for a form's text properties.</summary>
internal static class PostedText
{
    /// <summary>
    /// Makes an optional text property (<c>string?</c>) that posted empty null. A field left empty posts "",
    /// which is no value: <c>[Phone]</c>, <c>[Url]</c> or <c>[EmailAddress]</c> would refuse it. A required
    /// one (<c>string</c>) stays "" for <c>[Required]</c> to refuse.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Only a static post gets here, which runs on a server, not in a trimmed WebAssembly app.")]
    public static void EmptyIsNoValue(object? model)
    {
        if (model is null)
        {
            return;
        }

        NullabilityInfoContext? nullability = null;
        foreach (var property in model.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.PropertyType == typeof(string) && property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0
                && property.GetValue(model) is "" && (nullability ??= new()).Create(property).WriteState == NullabilityState.Nullable)
            {
                property.SetValue(model, null);
            }
        }
    }
}
