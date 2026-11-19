using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace {{RootNamespace}}.Data;

/// <summary>An option as the app writes it: its <c>[Display]</c> name ("Design &amp; technology"), or its own name without one ("Retail").</summary>
public static class Names
{
    public static string Of(Enum value) =>
        value.GetType().GetField(value.ToString())?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? value.ToString();
}
