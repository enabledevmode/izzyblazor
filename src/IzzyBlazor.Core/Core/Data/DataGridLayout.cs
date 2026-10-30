using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace IzzyBlazor;

/// <summary>
/// How one person has arranged a <c>DataGrid</c>'s columns: only what differs from the columns as
/// declared. Serializable, to keep per user in a database. Sort, filter and page aren't part of it:
/// they're in the address, so one link shows the same rows to everyone.
/// </summary>
public sealed partial record DataGridLayout
{
    private const int MaxColumns = 200;

    /// <summary>The columns that differ from their declaration.</summary>
    [JsonPropertyName("c")]
    public IReadOnlyList<DataGridColumnLayout> Columns { get; init; } = [];

    /// <summary>A column's entry, by its key (its <c>SortKey</c> or property, else its title), or null.</summary>
    public DataGridColumnLayout? Column(string key) =>
        Columns.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>The layout as JSON, as a cookie holds it.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, PersistedStateJson.Default.DataGridLayout);

    /// <summary>
    /// A layout from <see cref="ToJson"/>, or null if it isn't one. It may come from a cookie, so
    /// whatever doesn't hold up is left out: a width that isn't plain pixels or rems, an unknown
    /// frozen side, a column past the limit.
    /// </summary>
    public static DataGridLayout? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var layout = JsonSerializer.Deserialize(json, PersistedStateJson.Default.DataGridLayout);
            return layout is null ? null : new DataGridLayout
            {
                Columns = layout.Columns.Where(c => !string.IsNullOrEmpty(c.Key)).Take(MaxColumns).Select(Clean).ToList(),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DataGridColumnLayout Clean(DataGridColumnLayout column) => column with
    {
        Width = column.Width is { } width && SafeWidth().IsMatch(width) && double.Parse(SafeWidth().Match(width).Groups[1].Value, CultureInfo.InvariantCulture) > 0 ? width : null,
        Frozen = column.Frozen is { } frozen && Enum.IsDefined(frozen) ? frozen : null,
    };

    [GeneratedRegex(@"^(\d{1,5}(?:\.\d{1,3})?)(?:px|rem)$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeWidth();
}

/// <summary>One column's place in a <see cref="DataGridLayout"/>: null leaves it as declared.</summary>
public sealed record DataGridColumnLayout
{
    /// <summary>The column's key: its <c>SortKey</c> or property, else its title.</summary>
    [JsonPropertyName("k")]
    public string Key { get; init; } = "";

    /// <summary>Its width in pixels or rems, such as <c>"180px"</c>.</summary>
    [JsonPropertyName("w"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Width { get; init; }

    /// <summary>Its place among the columns, from 0.</summary>
    [JsonPropertyName("p"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Position { get; init; }

    /// <summary>Whether it's hidden.</summary>
    [JsonPropertyName("h"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Hidden { get; init; }

    /// <summary>Which edge it's frozen at; <see cref="ColumnFrozen.None"/> unfreezes a declared one.</summary>
    [JsonPropertyName("f"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ColumnFrozen? Frozen { get; init; }
}
