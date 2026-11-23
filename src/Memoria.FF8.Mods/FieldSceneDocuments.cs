using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Mods;

public enum FieldSceneBackgroundMode { Overlay, SelectedModules, Replace }
public enum FieldSceneProjection { Native, Authored }
public enum FieldSceneFrameFit { Contain, Crop, Expand }
public enum FieldSceneCameraMotion { Static, CaptureRelative }
public enum FieldSceneShading { Source, Unlit, Lit, UnlitShadowReceiver }
public enum FieldSceneAlpha { Source, Opaque, Mask, Blend, OpaqueCoreAndBlend }
public enum FieldSceneRole { Surface, Occluder, ShadowReceiver }

public sealed record FieldSceneMaterialOverride(string MaterialId, FieldSceneShading Shading = FieldSceneShading.Source, FieldSceneAlpha Alpha = FieldSceneAlpha.Source, bool? DoubleSided = null, bool CastShadows = false, bool ReceiveShadows = false, FieldSceneRole Role = FieldSceneRole.Surface);
public sealed record FieldScenePlayback(string ClipId, bool Loop = true, float Speed = 1, float PhaseSeconds = 0, bool Autoplay = true);
public sealed record FieldSceneInstance(string Id, string AssetId, float[] AssetToField, FieldSceneMaterialOverride[]? Materials = null, FieldScenePlayback? Playback = null, bool Enabled = true, string? Calibration = null);
public sealed record FieldSceneAssetBinding(string Id, string Sha256, string? Scene = null);
public sealed record FieldSceneScrollBaseline(float X, float Y);
public sealed record FieldSceneView(string Id, int CameraSlot, FieldSceneBackgroundMode Background, FieldSceneInstance[] Instances, int[]? ReplacedModules = null, Dictionary<int, int>? NativeStates = null, FieldSceneProjection Projection = FieldSceneProjection.Native, string? CameraInstance = null, string? CameraId = null, FieldSceneFrameFit FrameFit = FieldSceneFrameFit.Contain, FieldSceneCameraMotion CameraMotion = FieldSceneCameraMotion.Static, FieldSceneScrollBaseline? ScrollBaseline = null, Dictionary<int, int[]>? NativeStateSets = null)
{
    public bool Matches(int slot, IReadOnlyDictionary<int, int> states) => CameraSlot == slot &&
        (NativeStates is null || NativeStates.All(pair => states.GetValueOrDefault(pair.Key, -1) == pair.Value)) &&
        (NativeStateSets is null || NativeStateSets.All(pair => pair.Value.Contains(states.GetValueOrDefault(pair.Key, -1))));

    public bool CoversNativeModule(int module) => NativeStates?.ContainsKey(module) == true || NativeStateSets?.ContainsKey(module) == true;

    internal IEnumerable<int> SelectedModules => NativeStates?.Keys.AsEnumerable() ?? NativeStateSets?.Keys.AsEnumerable() ?? [];

    internal int[]? AllowedStates(int module)
    {
        if (NativeStates?.TryGetValue(module, out int state) == true)
        {
            return [state];
        }
        return NativeStateSets?.GetValueOrDefault(module);
    }
}

public sealed record FieldSceneDocument(int SchemaVersion, string Field, FieldSceneAssetBinding[] Assets, FieldSceneView[] Views);

public sealed record PreparedFieldScene(string PackageId, FieldSceneDocument Document, IReadOnlyDictionary<string, FieldSceneAsset> Assets);
public sealed record PreparedFieldSceneView(PreparedFieldScene Scene, FieldSceneView View);

public static class FieldSceneDocuments
{
    public const string ComponentPath = "scene3d/component.json";
    public const int MaximumGlbBytes = 256 * 1024 * 1024;
    public const int MaximumDocumentBytes = 1024 * 1024;
    public static ReadOnlySpan<byte> Component => "{\"schemaVersion\":1}"u8;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    public static string ScenePath(string field)
    {
        FieldLightingSceneIO.ValidateFieldName(field);
        return "scene3d/fields/" + field + ".json";
    }

    public static string AssetPath(string digest)
    {
        if (digest is not { Length: 64 } || !digest.All(character => char.IsAsciiHexDigitLower(character)))
        {
            throw new InvalidDataException("A scene asset requires a lowercase SHA-256 digest.");
        }
        return "scene3d/assets/" + digest + ".glb";
    }

    public static string Digest(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    public static void ValidateComponent(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > MaximumDocumentBytes || JsonSerializer.Deserialize<ComponentMarker>(bytes, Options) is not { SchemaVersion: 1 })
        {
            throw new InvalidDataException("Expected a version 1 field scene component.");
        }
    }

    private sealed record ComponentMarker(int SchemaVersion);

    public static FieldSceneDocument Read(ReadOnlySpan<byte> bytes, string field)
    {
        if (bytes.Length > MaximumDocumentBytes)
        {
            throw new InvalidDataException("The field scene document exceeds 1 MiB.");
        }
        FieldSceneDocument document = JsonSerializer.Deserialize<FieldSceneDocument>(bytes, Options) ?? throw new InvalidDataException("Expected a field scene document.");
        Validate(document, field);
        return document;
    }

    public static byte[] Write(FieldSceneDocument document)
    {
        Validate(document, document.Field);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(document, Options);
        if (bytes.Length > MaximumDocumentBytes)
        {
            throw new InvalidDataException("The field scene document exceeds 1 MiB.");
        }
        return bytes;
    }

    public static PreparedFieldScene Prepare(string packageId, FieldSceneDocument document, Func<string, byte[]> readAsset)
    {
        Validate(document, document.Field);
        Dictionary<string, FieldSceneAsset> assets = new(StringComparer.Ordinal);
        foreach (FieldSceneAssetBinding binding in document.Assets)
        {
            byte[] content = readAsset(AssetPath(binding.Sha256));
            if (content.Length > MaximumGlbBytes || Digest(content) != binding.Sha256)
            {
                throw new InvalidDataException($"Scene asset '{binding.Id}' does not match its declared SHA-256 digest or size limit.");
            }
            assets.Add(binding.Id, FieldSceneAsset.ImportGlb(content, binding.Scene));
        }

        foreach (FieldSceneView view in document.Views)
        {
            foreach (FieldSceneInstance instance in view.Instances)
            {
                FieldSceneAsset asset = assets[instance.AssetId];
                if (instance.Playback is { } playback && !asset.Clips.Any(clip => clip.Id == playback.ClipId))
                {
                    throw new InvalidDataException($"Scene instance '{instance.Id}' references unknown clip '{playback.ClipId}'.");
                }
                foreach (FieldSceneMaterialOverride material in instance.Materials ?? [])
                {
                    if (!asset.Materials.Any(source => source.Id == material.MaterialId))
                    {
                        throw new InvalidDataException($"Scene instance '{instance.Id}' references unknown material '{material.MaterialId}'.");
                    }
                }
            }
            if (view.Projection == FieldSceneProjection.Authored && !view.Instances.Any(instance => instance.Id == view.CameraInstance && instance.Enabled && assets[instance.AssetId].Cameras.Any(camera => camera.Id == view.CameraId)))
            {
                throw new InvalidDataException($"View '{view.Id}' references a missing authored camera.");
            }
        }
        return new(packageId, document, assets);
    }

    public static Matrix4x4 Matrix(float[] values)
    {
        if (values is not { Length: 16 } || values.Any(value => !float.IsFinite(value) || MathF.Abs(value) > 1e8f) || values[3] != 0 || values[7] != 0 || values[11] != 0 || values[15] != 1)
        {
            throw new InvalidDataException("Placement requires 16 finite row-major values forming an affine matrix.");
        }
        Matrix4x4 result = new(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], values[8], values[9], values[10], values[11], values[12], values[13], values[14], values[15]);
        if (!Matrix4x4.Invert(result, out Matrix4x4 inverse) || !float.IsFinite(inverse.GetDeterminant()))
        {
            throw new InvalidDataException("The scene placement is singular or ill-conditioned.");
        }
        return result;
    }

    public static float[] Values(Matrix4x4 value) => [value.M11, value.M12, value.M13, value.M14, value.M21, value.M22, value.M23, value.M24, value.M31, value.M32, value.M33, value.M34, value.M41, value.M42, value.M43, value.M44];

    private static void Validate(FieldSceneDocument document, string field)
    {
        _ = ScenePath(field);
        if (document.SchemaVersion != 1 || document.Field != field || document.Assets is not { Length: <= 64 } || document.Views is not { Length: <= 128 })
        {
            throw new InvalidDataException("Expected a version 1 scene with a canonical field selector matching its filename.");
        }
        HashSet<string> assetIds = new(StringComparer.Ordinal);
        foreach (FieldSceneAssetBinding asset in document.Assets)
        {
            if (asset is null || !ValidId(asset.Id) || !assetIds.Add(asset.Id) || asset.Scene is { Length: > 256 })
            {
                throw new InvalidDataException("Scene asset IDs must be unique and readable.");
            }
            _ = AssetPath(asset.Sha256);
        }
        HashSet<string> viewIds = new(StringComparer.Ordinal);
        foreach (FieldSceneView view in document.Views)
        {
            if (view is null || !ValidId(view.Id) || !viewIds.Add(view.Id) || view.CameraSlot is < 0 or > 255 || !Enum.IsDefined(view.Background) || !Enum.IsDefined(view.Projection) || !Enum.IsDefined(view.FrameFit) || !Enum.IsDefined(view.CameraMotion) || view.Instances is not { Length: > 0 and <= 64 })
            {
                throw new InvalidDataException("Expected unique views with a native camera slot and one or more scene instances.");
            }
            if (view.NativeStates is { } states && states.Any(pair => pair.Key is < 0 or >= 255 || pair.Value is < -1 or > 255))
            {
                throw new InvalidDataException("Native background state selectors require a valid animation module and a state between -1 and 255.");
            }
            if (view.NativeStateSets is { } sets && (view.NativeStates is not null || sets.Any(pair => pair.Key is < 0 or >= 255 || pair.Value is not { Length: > 0 and <= 257 } || pair.Value.Any(state => state is < -1 or > 255) || pair.Value.Distinct().Count() != pair.Value.Length)))
            {
                throw new InvalidDataException("Supply either exact native states or explicit sets of distinct native states.");
            }
            if (view.Background == FieldSceneBackgroundMode.SelectedModules && (view.ReplacedModules is not { Length: > 0 and <= 256 } modules || modules.Any(module => module is < 0 or > 255) || modules.Distinct().Count() != modules.Length) ||
                view.Background != FieldSceneBackgroundMode.SelectedModules && view.ReplacedModules is not null ||
                view.Background == FieldSceneBackgroundMode.Replace && view.NativeStates is null && view.NativeStateSets is null ||
                view.Projection == FieldSceneProjection.Authored && (view.Background != FieldSceneBackgroundMode.Replace || !ValidId(view.CameraInstance) || !ValidId(view.CameraId)) ||
                view.Projection == FieldSceneProjection.Native && (view.CameraInstance is not null || view.CameraId is not null || view.CameraMotion != FieldSceneCameraMotion.Static) ||
                (view.CameraMotion == FieldSceneCameraMotion.CaptureRelative) != (view.ScrollBaseline is not null) ||
                view.ScrollBaseline is { } baseline && (!float.IsFinite(baseline.X) || !float.IsFinite(baseline.Y) || MathF.Abs(baseline.X) > 65536 || MathF.Abs(baseline.Y) > 65536))
            {
                throw new InvalidDataException($"View '{view.Id}' has incompatible camera, replacement scope or native state settings.");
            }
            HashSet<string> instanceIds = new(StringComparer.Ordinal);
            foreach (FieldSceneInstance instance in view.Instances)
            {
                if (instance is null || !ValidId(instance.Id) || !instanceIds.Add(instance.Id) || !ValidId(instance.AssetId) || !assetIds.Contains(instance.AssetId) || instance.Calibration is { Length: > 512 })
                {
                    throw new InvalidDataException($"View '{view.Id}' contains an invalid instance or missing asset binding.");
                }
                _ = Matrix(instance.AssetToField);
                if (instance.Playback is { } playback && (!ValidId(playback.ClipId) || !float.IsFinite(playback.Speed) || playback.Speed is < 0 or > 16 || !float.IsFinite(playback.PhaseSeconds) || MathF.Abs(playback.PhaseSeconds) > 86_400))
                {
                    throw new InvalidDataException($"Instance '{instance.Id}' has invalid clip timing.");
                }
                if (instance.Materials is { } overrides && (overrides.Length > 1024 || overrides.Any(material => material is null || !ValidId(material.MaterialId) || !Enum.IsDefined(material.Shading) || !Enum.IsDefined(material.Alpha) || !Enum.IsDefined(material.Role)) || overrides.Select(material => material.MaterialId).Distinct().Count() != overrides.Length))
                {
                    throw new InvalidDataException($"Instance '{instance.Id}' has invalid or duplicate material overrides.");
                }
            }
            if (view.Background != FieldSceneBackgroundMode.Overlay && !view.Instances.Any(instance => instance.Enabled))
            {
                throw new InvalidDataException($"Replacement view '{view.Id}' must have an enabled instance.");
            }
        }
        for (int index = 0; index < document.Views.Length; index++)
        {
            FieldSceneView view = document.Views[index];
            if (view.Background != FieldSceneBackgroundMode.Overlay && document.Views.Take(index).Any(other => other.Background != FieldSceneBackgroundMode.Overlay && Overlap(other, view)))
            {
                throw new InvalidDataException($"View '{view.Id}' overlaps another replacement in the same package.");
            }
        }
    }

    internal static bool Overlap(FieldSceneView first, FieldSceneView second) => first.CameraSlot == second.CameraSlot &&
        first.SelectedModules.All(module => second.AllowedStates(module) is not { } states || states.Intersect(first.AllowedStates(module)!).Any());

    private static bool ValidId(string? value) => value is { Length: > 0 and <= 1024 } && !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);
}
