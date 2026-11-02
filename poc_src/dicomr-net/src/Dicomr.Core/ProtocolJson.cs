using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dicomr.Core;

public static class ProtocolJson
{
    public static string Serialize(StudyProtocol protocol, bool indented = true) =>
        JsonSerializer.Serialize(protocol, Options(indented));

    public static StudyProtocol Deserialize(string json)
    {
        var protocol = JsonSerializer.Deserialize<StudyProtocol>(json, Options(indented: false));
        return protocol ?? throw new JsonException("Protocol JSON was empty.");
    }

    public static JsonSerializerOptions Options(bool indented)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = indented,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new FieldValueJsonConverter());
        return options;
    }
}

public sealed class FieldValueJsonConverter : JsonConverter<FieldValue>
{
    public override FieldValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var keyword = RequiredString(root, "keyword");
        var value = ReadScalar(root);
        return new FieldValue
        {
            Keyword = keyword,
            Tag = OptionalString(root, "tag"),
            Vr = OptionalString(root, "vr"),
            Unit = OptionalString(root, "unit"),
            Source = ReadSource(root),
            SourceDetail = OptionalString(root, "sourceDetail"),
            Value = value,
            Varying = root.TryGetProperty("varying", out var varying) && varying.ValueKind == JsonValueKind.True,
        };
    }

    public override void Write(Utf8JsonWriter writer, FieldValue value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("keyword", value.Keyword);
        WriteOptional(writer, "tag", value.Tag);
        WriteOptional(writer, "vr", value.Vr);
        WriteOptional(writer, "unit", value.Unit);
        writer.WriteString("source", SourceName(value.Source));
        WriteOptional(writer, "sourceDetail", value.SourceDetail);
        switch (value.Value)
        {
            case FieldScalar.Text text:
                writer.WriteString("text", text.TextValue);
                break;
            case FieldScalar.Number number:
                writer.WriteNumber("number", number.NumberValue);
                break;
            case FieldScalar.Integer integer:
                writer.WriteNumber("integer", integer.IntegerValue);
                break;
            case FieldScalar.TextList texts:
                writer.WritePropertyName("texts");
                JsonSerializer.Serialize(writer, texts.Items, options);
                break;
            case FieldScalar.NumberList numbers:
                writer.WritePropertyName("numbers");
                JsonSerializer.Serialize(writer, numbers.Items, options);
                break;
            default:
                throw new JsonException($"Unsupported field value for {value.Keyword}.");
        }

        if (value.Varying)
        {
            writer.WriteBoolean("varying", true);
        }

        writer.WriteEndObject();
    }

    private static FieldScalar ReadScalar(JsonElement root)
    {
        var present = 0;
        FieldScalar? scalar = null;
        if (root.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
        {
            present++;
            scalar = new FieldScalar.Text(text.GetString() ?? "");
        }

        if (root.TryGetProperty("number", out var number) && number.ValueKind == JsonValueKind.Number)
        {
            present++;
            scalar = new FieldScalar.Number(number.GetDouble());
        }

        if (root.TryGetProperty("integer", out var integer) && integer.ValueKind == JsonValueKind.Number)
        {
            present++;
            scalar = new FieldScalar.Integer(integer.GetInt64());
        }

        if (root.TryGetProperty("texts", out var texts) && texts.ValueKind == JsonValueKind.Array)
        {
            present++;
            scalar = new FieldScalar.TextList(texts.EnumerateArray().Select(item => item.GetString() ?? "").ToArray());
        }

        if (root.TryGetProperty("numbers", out var numbers) && numbers.ValueKind == JsonValueKind.Array)
        {
            present++;
            scalar = new FieldScalar.NumberList(numbers.EnumerateArray().Select(item => item.GetDouble()).ToArray());
        }

        if (present != 1 || scalar is null)
        {
            throw new JsonException("A field value needs exactly one of text, number, integer, texts, or numbers.");
        }

        return scalar;
    }

    private static FieldSource ReadSource(JsonElement root)
    {
        if (!root.TryGetProperty("source", out var source) || source.ValueKind != JsonValueKind.String)
        {
            return FieldSource.Standard;
        }

        return source.GetString() switch
        {
            "enhancedMacro" => FieldSource.EnhancedMacro,
            "private" => FieldSource.Private,
            "inferred" => FieldSource.Inferred,
            "derived" => FieldSource.Derived,
            _ => FieldSource.Standard,
        };
    }

    private static string SourceName(FieldSource source) => source switch
    {
        FieldSource.EnhancedMacro => "enhancedMacro",
        FieldSource.Private => "private",
        FieldSource.Inferred => "inferred",
        FieldSource.Derived => "derived",
        _ => "standard",
    };

    private static string RequiredString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new JsonException($"Field JSON is missing {name}.");
        }

        return property.GetString() ?? throw new JsonException($"Field JSON is missing {name}.");
    }

    private static string? OptionalString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static void WriteOptional(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(name, value);
        }
    }
}
