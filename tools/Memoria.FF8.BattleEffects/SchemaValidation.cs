using System.Text.Json;
using System.Text.RegularExpressions;

namespace Memoria.FF8.BattleEffects;

/// <summary>Validates the deliberately small JSON Schema vocabulary used by this tool's version 1 records.</summary>
public static class SchemaValidation
{
    private static readonly HashSet<string> Keywords = ["$schema", "$id", "$ref", "$defs", "title", "description", "type", "properties", "required", "additionalProperties", "items", "minItems", "uniqueItems", "enum", "const", "pattern", "minimum", "exclusiveMinimum", "maximum", "minLength"];

    public static void ValidateFile(string recordPath, string schemaName)
    {
        ValidateBytes(File.ReadAllBytes(recordPath), schemaName);
    }

    public static void ValidateBytes(byte[] bytes, string schemaName)
    {
        EvidenceStore.Id(schemaName);
        string schemaPath = Path.Combine(AppContext.BaseDirectory, "schemas", schemaName + ".v1.schema.json");
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllBytes(schemaPath));
        using JsonDocument record = JsonDocument.Parse(bytes);
        Validate(record.RootElement, schema.RootElement, schema.RootElement, "$", 0);
    }

    public static void ValidateRecord<T>(T record, string schemaName)
    {
        EvidenceStore.Id(schemaName);
        string schemaPath = Path.Combine(AppContext.BaseDirectory, "schemas", schemaName + ".v1.schema.json");
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllBytes(schemaPath));
        JsonElement value = JsonSerializer.SerializeToElement(record, RecordJson.Options);
        Validate(value, schema.RootElement, schema.RootElement, "$", 0);
    }

    private static void Validate(JsonElement value, JsonElement schema, JsonElement root, string path, int depth)
    {
        if (depth > 64)
        {
            throw new InvalidDataException("Schema recursion limit exceeded.");
        }

        foreach (JsonProperty keyword in schema.EnumerateObject())
        {
            if (!Keywords.Contains(keyword.Name))
            {
                throw new InvalidDataException($"Unsupported schema keyword: {keyword.Name}");
            }
        }

        if (schema.TryGetProperty("$ref", out JsonElement reference))
        {
            string name = reference.GetString()!;
            if (!name.StartsWith("#/$defs/", StringComparison.Ordinal))
            {
                throw new InvalidDataException("Only local schema definitions are supported.");
            }

            Validate(value, root.GetProperty("$defs").GetProperty(name[8..]), root, path, depth + 1);
            return;
        }

        if (schema.TryGetProperty("type", out JsonElement type))
        {
            string[] types = type.ValueKind == JsonValueKind.Array ? type.EnumerateArray().Select(t => t.GetString()!).ToArray() : [type.GetString()!];
            bool matches = types.Any(t => t switch
            {
                "object" => value.ValueKind == JsonValueKind.Object,
                "array" => value.ValueKind == JsonValueKind.Array,
                "string" => value.ValueKind == JsonValueKind.String,
                "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
                "number" => value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double n) && double.IsFinite(n),
                "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "null" => value.ValueKind == JsonValueKind.Null,
                _ => throw new InvalidDataException($"Unsupported schema type: {t}")
            });
            Require(matches, path, "wrong JSON type");
        }

        if (schema.TryGetProperty("const", out JsonElement constant))
        {
            Require(JsonElement.DeepEquals(value, constant), path, "unexpected constant");
        }

        if (schema.TryGetProperty("enum", out JsonElement choices))
        {
            Require(choices.EnumerateArray().Any(choice => JsonElement.DeepEquals(value, choice)), path, "not in enum");
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = [];
            foreach (JsonProperty property in value.EnumerateObject())
            {
                Require(names.Add(property.Name), path, "duplicate property");
                if (schema.TryGetProperty("properties", out JsonElement properties) && properties.TryGetProperty(property.Name, out JsonElement propertySchema))
                {
                    Validate(property.Value, propertySchema, root, path + "." + property.Name, depth + 1);
                }
                else if (schema.TryGetProperty("additionalProperties", out JsonElement additional) && additional.ValueKind == JsonValueKind.False)
                {
                    throw new InvalidDataException($"{path}: unknown property {property.Name}");
                }
            }

            if (schema.TryGetProperty("required", out JsonElement required))
            {
                foreach (JsonElement name in required.EnumerateArray())
                {
                    Require(names.Contains(name.GetString()!), path, "missing " + name.GetString());
                }
            }
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            if (schema.TryGetProperty("minItems", out JsonElement min))
            {
                Require(value.GetArrayLength() >= min.GetInt32(), path, "too few items");
            }

            JsonElement[] items = value.EnumerateArray().ToArray();
            if (schema.TryGetProperty("uniqueItems", out JsonElement unique) && unique.GetBoolean())
            {
                for (int i = 0; i < items.Length; i++)
                {
                    Require(!items.Skip(i + 1).Any(other => JsonElement.DeepEquals(items[i], other)), path, "duplicate array item");
                }
            }

            if (schema.TryGetProperty("items", out JsonElement itemSchema))
            {
                for (int i = 0; i < items.Length; i++)
                {
                    Validate(items[i], itemSchema, root, $"{path}[{i}]", depth + 1);
                }
            }
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            if (schema.TryGetProperty("minLength", out JsonElement length))
            {
                Require(value.GetString()!.Length >= length.GetInt32(), path, "string is too short");
            }

            if (schema.TryGetProperty("pattern", out JsonElement pattern))
            {
                Require(Regex.IsMatch(value.GetString()!, pattern.GetString()!, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)), path, "pattern mismatch");
            }
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            double number = value.GetDouble();
            if (schema.TryGetProperty("minimum", out JsonElement minimum))
            {
                Require(number >= minimum.GetDouble(), path, "below minimum");
            }

            if (schema.TryGetProperty("exclusiveMinimum", out JsonElement exclusive))
            {
                Require(number > exclusive.GetDouble(), path, "below exclusive minimum");
            }

            if (schema.TryGetProperty("maximum", out JsonElement maximum))
            {
                Require(number <= maximum.GetDouble(), path, "above maximum");
            }
        }
    }

    private static void Require(bool condition, string path, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException($"{path}: {message}");
        }
    }
}
