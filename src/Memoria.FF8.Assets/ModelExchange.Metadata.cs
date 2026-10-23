using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Assets;

public static partial class ModelExchange
{
    /// <summary>Retains only native rig and selected binding descriptors, never original payload content.</summary>
    public static JsonElement RetainedNativeMetadata(JsonElement metadata, bool includeGeometryBindings = true)
    {
        JsonObject retained = new();
        if (metadata.ValueKind != JsonValueKind.Object)
        {
            return JsonSerializer.SerializeToElement(retained);
        }
        CopyMetadataProperty(metadata, retained, "Scale");
        CopyMetadataProperty(metadata, retained, "NativeWeaponUsesBodySkeleton");
        if (metadata.TryGetProperty("Bones", out JsonElement bones) && bones.ValueKind == JsonValueKind.Array)
        {
            retained["Bones"] = MetadataArray(bones, bone => MetadataObject(bone, "Parent", "Length"));
        }
        if (metadata.TryGetProperty("ControlBones", out JsonElement control))
        {
            retained["ControlBones"] = MetadataObject(control, "First", "Second", "Additional", "RawHeader");
        }
        if (includeGeometryBindings)
        {
            CopyMetadataProperty(metadata, retained, "memoria_preserve_native_parts");
            if (metadata.TryGetProperty("NativeParts", out JsonElement parts) && parts.ValueKind == JsonValueKind.Object)
            {
                JsonObject descriptors = MetadataObject(parts, "SourceHash", "SourceOffset");
                if (parts.TryGetProperty("Parts", out JsonElement records) && records.ValueKind == JsonValueKind.Array)
                {
                    descriptors["Parts"] = MetadataArray(records, record => MetadataObject(record, "Words"));
                }
                if (parts.TryGetProperty("SkinBindings", out JsonElement skins) && skins.ValueKind == JsonValueKind.Array)
                {
                    descriptors["SkinBindings"] = MetadataArray(skins, skin => MetadataObject(skin, "FirstVertex", "VertexCount", "Bone", "OriginalFlags"));
                }
                retained["NativeParts"] = descriptors;
            }
            if (metadata.TryGetProperty("NativeEyes", out JsonElement eyes) && eyes.ValueKind == JsonValueKind.Object)
            {
                JsonObject descriptors = MetadataObject(eyes, "ModelHash", "ModelOffset", "BankOffset");
                if (eyes.TryGetProperty("Texture", out JsonElement texture) && texture.ValueKind == JsonValueKind.Object)
                {
                    descriptors["Texture"] = MetadataObject(texture, "Depth", "WidthWords", "Height");
                }
                // The actual bank/pixel words remain target resources, outside selected geometry.
                retained["NativeEyes"] = descriptors;
            }
        }
        return JsonSerializer.SerializeToElement(retained);
    }

    private static JsonObject MetadataObject(JsonElement source, params string[] properties)
    {
        JsonObject result = new();
        if (source.ValueKind == JsonValueKind.Object)
        {
            foreach (string property in properties)
            {
                CopyMetadataProperty(source, result, property);
            }
        }
        return result;
    }

    private static JsonArray MetadataArray(JsonElement source, Func<JsonElement, JsonNode> transform)
    {
        JsonArray result = [];
        foreach (JsonElement element in source.EnumerateArray())
        {
            result.Add(transform(element));
        }
        return result;
    }

    private static void CopyMetadataProperty(JsonElement source, JsonObject target, string property)
    {
        if (source.TryGetProperty(property, out JsonElement value))
        {
            target[property] = JsonNode.Parse(value.GetRawText());
        }
    }
}

