using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Mods;

/// <summary>An authored positional beam. RGB channels are sRGB values in 0..1; intensity is relative, not calibrated lumens.</summary>
public sealed record FieldSpotLight
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public bool Enabled { get; init; } = true;
    public string Type { get; init; } = "spot";
    public Vector3 Position { get; init; }
    public Vector3 Direction { get; init; } = -Vector3.UnitZ;
    public Vector3 Color { get; init; } = Vector3.One;
    public float Intensity { get; init; } = 1;
    public float Range { get; init; } = 1000;
    public float InnerConeDegrees { get; init; } = 30;
    public float OuterConeDegrees { get; init; } = 60;
}

public sealed record FieldGroundShadowSettings
{
    public float Opacity { get; init; } = 0.5f;
    /// <summary>Shadow-filter radius in texels, bounded to 0..3.</summary>
    public float Softness { get; init; } = 1;
}

/// <summary>The complete lighting for one native field. An empty active scene deliberately illuminates no model surfaces.</summary>
public sealed record FieldLightingScene
{
    public int SchemaVersion { get; init; } = 1;
    public required string FieldName { get; init; }
    public IReadOnlyList<FieldSpotLight> Lights { get; init; } = [];
    public string? PrimaryLightId { get; init; }
    public FieldGroundShadowSettings GroundShadow { get; init; } = new();

    public static FieldLightingScene Empty(string fieldName)
    {
        FieldLightingScene scene = new() { FieldName = fieldName };
        FieldLightingSceneIO.Validate(scene);
        return scene;
    }
}

public static class FieldLightingSceneIO
{
    public const int MaximumEnabledLights = 8;
    public const int MaximumLights = 1024;
    public const int MaximumDocumentBytes = 512 * 1024;
    public const string ComponentPath = "lighting/component.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
        Converters = { new VectorConverter() }
    };

    public static string ScenePath(string fieldName)
    {
        ValidateFieldName(fieldName);
        return $"lighting/fields/{fieldName}.json";
    }

    public static void ValidateFieldName(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName) || fieldName.Length > 64 || fieldName.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
        {
            throw new InvalidDataException("A lighting field selector must contain 1..64 ASCII letters, digits or underscores.");
        }
    }

    public static void Validate(FieldLightingScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ValidateFieldName(scene.FieldName);
        if (scene.SchemaVersion != 1)
        {
            throw new InvalidDataException("Unsupported lighting scene schemaVersion; expected 1.");
        }
        if (scene.Lights is null || scene.Lights.Count > MaximumLights)
        {
            throw new InvalidDataException($"A lighting scene requires a complete lights array with at most {MaximumLights} entries.");
        }
        HashSet<string> ids = new(StringComparer.Ordinal);
        int enabled = 0;
        foreach (FieldSpotLight light in scene.Lights)
        {
            if (light is null || string.IsNullOrWhiteSpace(light.Id) || light.Id.Length > 80 || light.Id.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('_' or '-')) || !ids.Add(light.Id))
            {
                throw new InvalidDataException("Light IDs must be unique stable identifiers containing 1..80 ASCII letters, digits, underscores or hyphens.");
            }
            if (string.IsNullOrWhiteSpace(light.Name) || light.Name.Length > 128 || light.Name.Any(char.IsControl) || light.Type != "spot")
            {
                throw new InvalidDataException("Every light requires a readable name of at most 128 characters and type 'spot'.");
            }
            if (!Finite(light.Position) || !Finite(light.Direction) || MathF.Abs(light.Direction.LengthSquared() - 1) > 0.0001f)
            {
                throw new InvalidDataException("Light position must be finite and direction must be a finite normalized outward beam vector.");
            }
            if (!Finite(light.Color) || light.Color.X is < 0 or > 1 || light.Color.Y is < 0 or > 1 || light.Color.Z is < 0 or > 1 || !float.IsFinite(light.Intensity) || light.Intensity < 0 || !float.IsFinite(light.Range) || light.Range <= 0)
            {
                throw new InvalidDataException("Light sRGB color channels must be within 0..1, intensity finite and nonnegative, and range finite and positive.");
            }
            if (!float.IsFinite(light.InnerConeDegrees) || !float.IsFinite(light.OuterConeDegrees) || light.InnerConeDegrees < 0 || light.InnerConeDegrees >= light.OuterConeDegrees || light.OuterConeDegrees > 85)
            {
                throw new InvalidDataException("Spot cone half-angles require 0 <= innerConeDegrees < outerConeDegrees <= 85.");
            }
            if (light.Enabled && ++enabled > MaximumEnabledLights)
            {
                throw new InvalidDataException($"A lighting scene supports at most {MaximumEnabledLights} enabled spotlights. Disable a light before enabling another.");
            }
        }
        if (scene.PrimaryLightId is not null && !ids.Contains(scene.PrimaryLightId))
        {
            throw new InvalidDataException("primaryLightId must be null or identify a light in this scene, including a disabled light.");
        }
        if (scene.GroundShadow is not { } shadow || !float.IsFinite(shadow.Opacity) || shadow.Opacity is < 0 or > 1 || !float.IsFinite(shadow.Softness) || shadow.Softness is < 0 or > 3)
        {
            throw new InvalidDataException("Ground-shadow opacity must be within 0..1 and softness within 0..3 texels.");
        }
    }

    public static FieldLightingScene Clone(FieldLightingScene scene)
    {
        Validate(scene);
        return scene with { Lights = Array.AsReadOnly(scene.Lights.ToArray()) };
    }

    public static byte[] Serialize(FieldLightingScene scene)
    {
        Validate(scene);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(scene, JsonOptions);
        if (bytes.Length > MaximumDocumentBytes)
        {
            throw new InvalidDataException("The lighting scene exceeds its document size limit.");
        }
        return bytes;
    }

    public static FieldLightingScene Deserialize(byte[] bytes, string? expectedFieldName = null)
    {
        using JsonDocument document = ParseStrict(bytes);
        RequireProperties(document.RootElement, "schemaVersion", "fieldName", "lights", "primaryLightId", "groundShadow");
        if (document.RootElement.GetProperty("lights").ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement light in document.RootElement.GetProperty("lights").EnumerateArray())
            {
                RequireProperties(light, "id", "name", "enabled", "type", "position", "direction", "color", "intensity", "range", "innerConeDegrees", "outerConeDegrees");
            }
        }
        RequireProperties(document.RootElement.GetProperty("groundShadow"), "opacity", "softness");
        FieldLightingScene scene = document.RootElement.Deserialize<FieldLightingScene>(JsonOptions) ?? throw new InvalidDataException("A lighting scene must be a JSON object.");
        Validate(scene);
        if (expectedFieldName is not null && !scene.FieldName.Equals(expectedFieldName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The lighting field selector must match lighting/fields/<fieldName>.json.");
        }
        return Clone(scene);
    }

    internal static void ValidateComponent(byte[] bytes)
    {
        using JsonDocument document = ParseStrict(bytes);
        RequireProperties(document.RootElement, "schemaVersion");
        if (document.RootElement.EnumerateObject().Count() != 1 || document.RootElement.GetProperty("schemaVersion").ValueKind != JsonValueKind.Number || !document.RootElement.GetProperty("schemaVersion").TryGetInt32(out int version) || version != 1)
        {
            throw new InvalidDataException("A lighting component marker contains only schemaVersion: 1.");
        }
    }

    private static JsonDocument ParseStrict(byte[] bytes)
    {
        if (bytes.Length > MaximumDocumentBytes)
        {
            throw new InvalidDataException("The lighting document exceeds its size limit.");
        }
        JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
        try
        {
            RejectDuplicateProperties(document.RootElement);
            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException($"Duplicate lighting property '{property.Name}'.");
                }
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                RejectDuplicateProperties(item);
            }
        }
    }

    private static void RequireProperties(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object || names.Any(name => !element.TryGetProperty(name, out _)))
        {
            throw new InvalidDataException("A lighting document is missing required properties.");
        }
    }

    private static bool Finite(Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }

    private sealed class VectorConverter : JsonConverter<Vector3>
    {
        public override Vector3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray || !reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetSingle(out float x) || !reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetSingle(out float y) || !reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetSingle(out float z) || !reader.Read() || reader.TokenType != JsonTokenType.EndArray)
            {
                throw new JsonException("A lighting vector must be an array of exactly three finite numbers.");
            }
            return new(x, y, z);
        }

        public override void Write(Utf8JsonWriter writer, Vector3 value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(value.X);
            writer.WriteNumberValue(value.Y);
            writer.WriteNumberValue(value.Z);
            writer.WriteEndArray();
        }
    }
}
