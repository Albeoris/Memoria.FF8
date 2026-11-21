using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Memoria.FF8.Mods;

/// <summary>An authored field light. RGB channels are sRGB values in 0..1 and intensity is relative.</summary>
public sealed record FieldLight
{
    public const string SpotType = "spot";
    public const string SunlightType = "sunlight";

    public required string Id { get; init; }
    public required string Name { get; init; }
    public bool Enabled { get; init; } = true;
    public string Type { get; init; } = SpotType;
    [JsonIgnore]
    public bool IsSunlight => Type == SunlightType;
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

/// <summary>A rectangular shadow receiver in field world coordinates. Heading measures the horizontal width axis from +X toward +Y.</summary>
public sealed record FieldShadowReceiver
{
    public required string Id { get; init; }
    public required string Kind { get; init; }
    public bool Enabled { get; init; } = true;
    public Vector3 Center { get; init; }
    public float Width { get; init; }
    public float Extent { get; init; }
    public float HeadingDegrees { get; init; }

    public (Vector3 U, Vector3 V) Axes()
    {
        (float sine, float cosine) = MathF.SinCos(HeadingDegrees * MathF.PI / 180);
        Vector3 u = new(cosine, sine, 0);
        return (u, Kind == "wall" ? Vector3.UnitZ : new Vector3(-sine, cosine, 0));
    }

    public Vector3[] Corners()
    {
        (Vector3 u, Vector3 v) = Axes();
        Vector3 width = u * (Width * 0.5f);
        Vector3 extent = v * (Extent * 0.5f);
        return [Center - width - extent, Center + width - extent, Center + width + extent, Center - width + extent];
    }
}

/// <summary>The complete lighting for one native field. An empty active scene deliberately illuminates no model surfaces.</summary>
public sealed record FieldLightingScene
{
    public int SchemaVersion { get; init; } = 1;
    public required string FieldName { get; init; }
    public IReadOnlyList<FieldLight> Lights { get; init; } = [];
    public string? PrimaryLightId { get; init; }
    public FieldGroundShadowSettings GroundShadow { get; init; } = new();
    public IReadOnlyList<FieldShadowReceiver> ShadowReceivers { get; init; } = [];

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
    public const int MaximumShadowReceivers = 128;
    public const int MaximumDocumentBytes = 512 * 1024;
    public const string ComponentPath = "lighting/component.json";
    private static readonly string[] SpotProperties = ["position", "range", "innerConeDegrees", "outerConeDegrees"];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
        Converters = { new VectorConverter() },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { ConfigureLightProperties } }
    };

    private static void ConfigureLightProperties(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type != typeof(FieldLight))
        {
            return;
        }
        foreach (JsonPropertyInfo property in typeInfo.Properties.Where(property => SpotProperties.Contains(property.Name)))
        {
            property.ShouldSerialize = (light, _) => !((FieldLight)light).IsSunlight;
        }
    }

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
        foreach (FieldLight light in scene.Lights)
        {
            if (light is null || !ValidId(light.Id) || !ids.Add(light.Id))
            {
                throw new InvalidDataException("Light IDs must be unique stable identifiers containing 1..80 ASCII letters, digits, underscores or hyphens.");
            }
            if (string.IsNullOrWhiteSpace(light.Name) || light.Name.Length > 128 || light.Name.Any(char.IsControl) || light.Type is not (FieldLight.SpotType or FieldLight.SunlightType))
            {
                throw new InvalidDataException("Every light requires a readable name of at most 128 characters and type 'spot' or 'sunlight'.");
            }
            if (!Finite(light.Direction) || MathF.Abs(light.Direction.LengthSquared() - 1) > 0.0001f)
            {
                throw new InvalidDataException("Light direction must be a finite normalized vector pointing along the rays.");
            }
            if (!Finite(light.Color) || light.Color.X is < 0 or > 1 || light.Color.Y is < 0 or > 1 || light.Color.Z is < 0 or > 1 || !float.IsFinite(light.Intensity) || light.Intensity < 0)
            {
                throw new InvalidDataException("Light sRGB color channels must be within 0..1 and intensity finite and nonnegative.");
            }
            if (!light.IsSunlight)
            {
                if (!Finite(light.Position) || !float.IsFinite(light.Range) || light.Range <= 0)
                {
                    throw new InvalidDataException("Spot position must be finite and range finite and positive.");
                }
                if (!float.IsFinite(light.InnerConeDegrees) || !float.IsFinite(light.OuterConeDegrees) || light.InnerConeDegrees < 0 || light.InnerConeDegrees >= light.OuterConeDegrees || light.OuterConeDegrees > 85)
                {
                    throw new InvalidDataException("Spot cone half-angles require 0 <= innerConeDegrees < outerConeDegrees <= 85.");
                }
            }
            if (light.Enabled && ++enabled > MaximumEnabledLights)
            {
                throw new InvalidDataException($"A lighting scene supports at most {MaximumEnabledLights} enabled lights. Disable a light before enabling another.");
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
        if (scene.ShadowReceivers is null || scene.ShadowReceivers.Count > MaximumShadowReceivers)
        {
            throw new InvalidDataException($"A lighting scene supports at most {MaximumShadowReceivers} authored shadow receivers.");
        }
        ids.Clear();
        foreach (FieldShadowReceiver receiver in scene.ShadowReceivers)
        {
            if (receiver is null || !ValidId(receiver.Id) || !ids.Add(receiver.Id))
            {
                throw new InvalidDataException("Shadow receiver IDs must be unique stable identifiers containing 1..80 ASCII letters, digits, underscores or hyphens.");
            }
            if (receiver.Kind is not ("floor" or "wall") || !Finite(receiver.Center) || !float.IsFinite(receiver.HeadingDegrees) || receiver.HeadingDegrees is < -180 or > 180 || !float.IsFinite(receiver.Width) || receiver.Width <= 0 || !float.IsFinite(receiver.Extent) || receiver.Extent <= 0)
            {
                throw new InvalidDataException("A shadow receiver requires kind 'floor' or 'wall', a finite center, positive finite dimensions and a heading within -180..180 degrees.");
            }
            (Vector3 u, Vector3 v) = receiver.Axes();
            Vector3 halfSize = Vector3.Abs(u * (receiver.Width * 0.5f)) + Vector3.Abs(v * (receiver.Extent * 0.5f));
            if (!Finite(receiver.Center - halfSize) || !Finite(receiver.Center + halfSize))
            {
                throw new InvalidDataException("Shadow receiver corners must be finite world positions.");
            }
        }
    }

    public static FieldLightingScene Clone(FieldLightingScene scene)
    {
        Validate(scene);
        return scene with { Lights = Array.AsReadOnly(scene.Lights.ToArray()), ShadowReceivers = Array.AsReadOnly(scene.ShadowReceivers.ToArray()) };
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
                RequireProperties(light, "id", "name", "enabled", "type", "direction", "color", "intensity");
                JsonElement type = light.GetProperty("type");
                if (type.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException("Light type must be a string.");
                }
                if (type.ValueEquals(FieldLight.SpotType))
                {
                    RequireProperties(light, SpotProperties);
                }
                else if (type.ValueEquals(FieldLight.SunlightType) && SpotProperties.Any(name => light.TryGetProperty(name, out _)))
                {
                    throw new InvalidDataException("Sunlight cannot define position, range or cone angles.");
                }
            }
        }
        RequireProperties(document.RootElement.GetProperty("groundShadow"), "opacity", "softness");
        if (document.RootElement.TryGetProperty("shadowReceivers", out JsonElement receivers) && receivers.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement receiver in receivers.EnumerateArray())
            {
                RequireProperties(receiver, "id", "kind", "enabled", "center", "width", "extent", "headingDegrees");
            }
        }
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

    private static bool ValidId(string? id)
    {
        return !string.IsNullOrWhiteSpace(id) && id.Length <= 80 && id.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');
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
