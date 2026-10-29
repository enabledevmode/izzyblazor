using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IzzyBlazor;

/// <summary>
/// Writes a filter value as JSON by what it is: text, a number, true or false, null, a list, and
/// dates, times, Guids and enums as text (an enum by name). Reads them back as text, numbers, true
/// or false, null or a list; the field's type parses them when the filter is applied.
/// </summary>
internal sealed class FilterValueConverter : JsonConverter<object?>
{
    public override bool HandleNull => true;

    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return FilterValues.Unwrap(document.RootElement.Clone());
    }

    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        switch (FilterValues.Unwrap(value))
        {
            case null:
                writer.WriteNullValue();
                break;
            case string s:
                writer.WriteStringValue(s);
                break;
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case Enum e:
                writer.WriteStringValue(e.ToString());
                break;
            case DateOnly d:
                writer.WriteStringValue(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                break;
            case DateTime dt:
                writer.WriteStringValue(dt.ToString("O", CultureInfo.InvariantCulture));
                break;
            case DateTimeOffset dto:
                writer.WriteStringValue(dto.ToString("O", CultureInfo.InvariantCulture));
                break;
            case TimeOnly t:
                writer.WriteStringValue(t.ToString("O", CultureInfo.InvariantCulture));
                break;
            case TimeSpan span:
                writer.WriteStringValue(span.ToString("c", CultureInfo.InvariantCulture));
                break;
            case Guid g:
                writer.WriteStringValue(g.ToString("D"));
                break;
            case decimal m:
                writer.WriteNumberValue(m);
                break;
            case double d:
                writer.WriteNumberValue(d);
                break;
            case float f:
                writer.WriteNumberValue(f);
                break;
            case ulong u:
                writer.WriteNumberValue(u);
                break;
            case var n when FilterValues.IsNumeric(n.GetType()):
                writer.WriteNumberValue(Convert.ToInt64(n, CultureInfo.InvariantCulture));
                break;
            case System.Collections.IEnumerable list:
                writer.WriteStartArray();
                foreach (var item in list)
                {
                    Write(writer, item, options);
                }

                writer.WriteEndArray();
                break;
            case var other:
                writer.WriteStringValue(Convert.ToString(other, CultureInfo.InvariantCulture));
                break;
        }
    }
}
