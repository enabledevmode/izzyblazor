using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// Hands a value from a prerender to the interactive render that follows, through
/// <see cref="PersistentComponentState"/>. The value is written with source-generated JSON and
/// handed over as a string: the state's own JSON methods find a type's members by reflection, which
/// a trimmed WebAssembly app may have removed, but a string needs only the built-in converter.
/// </summary>
internal static class PersistedState
{
    private const string StringOnly = "Only strings are handed over, which System.Text.Json writes and reads with its built-in converter.";

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = StringOnly)]
    public static void Persist<T>(PersistentComponentState state, string key, T value, JsonTypeInfo<T> type) =>
        state.PersistAsJson(key, JsonSerializer.Serialize(value, type));

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = StringOnly)]
    public static bool TryTake<T>(PersistentComponentState state, string key, JsonTypeInfo<T> type, out T? value)
    {
        if (state.TryTakeFromJson<string>(key, out var json) && !string.IsNullOrEmpty(json))
        {
            value = JsonSerializer.Deserialize(json, type);
            return true;
        }

        value = default;
        return false;
    }
}

/// <summary>The types IzzyBlazor hands over through <see cref="PersistedState"/>.</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(bool?))]
[JsonSerializable(typeof(ColorScheme))]
[JsonSerializable(typeof(List<ToastMessage>))]
[JsonSerializable(typeof(string[]))]
internal sealed partial class PersistedStateJson : JsonSerializerContext;
