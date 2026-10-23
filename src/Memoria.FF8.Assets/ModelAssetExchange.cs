using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Memoria.FF8.Assets;

public sealed record ModelAssetObject(string Target, string SourceVariant, string Fingerprint, string RigId, EditableModel Model, bool GeometrySelected = true, string? AlternativeGroup = null, string? ReplacedPart = null);
public sealed record ModelAssetClip(string Target, string ModelTarget, string RigId, string GroupId, string SourceVariant, string Fingerprint, EditableAnimation Animation, int NativeFrameCount, float SecondsPerFrame);
public sealed record ModelAssetSelection(IReadOnlyList<ModelAssetObject> Objects, IReadOnlyList<ModelAssetClip> Clips)
{
    public string Edition { get; init; } = "unspecified";
}
public sealed record ModelAssetImportInventory(IReadOnlyList<ModelAssetObject> Objects, IReadOnlyList<ModelAssetClip> Clips, IReadOnlyList<string> Warnings)
{
    public string Edition { get; init; } = "unspecified";
}

/// <summary>Selective, multi-rig GLB interchange. Addresses identify replacement slots independently of providers.</summary>
public static class ModelAssetExchange
{
    private sealed record Manifest(int Version, int ConversionVersion, string Edition, ObjectInfo[] Objects, ClipInfo[] Clips);
    private sealed record ObjectInfo(string Target, string SourceVariant, string Fingerprint, string RigId, string Kind, string Name, string? AlternativeGroup, string? ReplacedPart, int NativeJointCount, string SkeletonSignature, JsonElement NativeMetadata, float NativeFramesPerSecond = ModelExchange.FramesPerSecond);
    private sealed record ClipInfo(string Target, string ModelTarget, string RigId, string GroupId, string SourceVariant, string Fingerprint, int NativeFrameCount, float SecondsPerFrame, string AnimationToken);

    public static byte[] ExportGlb(ModelAssetSelection selection) => ModelExchange.ExportGlb(Compose(selection));

    public static EditableModel Compose(ModelAssetSelection selection)
    {
        Unique(selection.Objects.Select(item => item.Target), "model target");
        Unique(selection.Clips.Select(item => item.Target), "clip target");
        List<EditableBone> bones = [];
        List<EditableMesh> meshes = [];
        List<Albeoris.Games.FF8.Imaging.PixelImage> textures = [];
        Dictionary<string, int[]> rigs = new(StringComparer.Ordinal);
        foreach (ModelAssetObject item in selection.Objects)
        {
            ModelExchange.Validate(item.Model);
            if (!rigs.TryGetValue(item.RigId, out int[]? map))
            {
                int rigRoot = bones.Count;
                bones.Add(new EditableBone(Token(item.RigId) + "_rig", -1, Vector3.Zero, Quaternion.Identity, Vector3.One, false) { RigId = item.RigId });
                int offset = bones.Count;
                map = Enumerable.Range(0, item.Model.Bones.Count).Select(index => offset + index).ToArray();
                rigs.Add(item.RigId, map);
                for (int index = 0; index < map.Length; index++)
                {
                    EditableBone bone = item.Model.Bones[index];
                    bones.Add(bone with { Parent = bone.Parent < 0 ? rigRoot : map[bone.Parent], RigId = item.RigId, StableId = $"{item.RigId}/joint/{(bone.NativeIndex < 0 ? index : bone.NativeIndex)}", Name = $"{Token(item.RigId)}_bone_{(bone.NativeIndex < 0 ? index : bone.NativeIndex)}" });
                }
            }
            else if (map.Length != item.Model.Bones.Count || SkeletonSignature(item.Model.Bones) != SkeletonSignature(selection.Objects.First(other => other.RigId == item.RigId).Model.Bones))
            {
                throw new InvalidDataException("Shared-rig objects must have identical joint identities and hierarchy.");
            }
            if (!item.GeometrySelected)
            {
                continue;
            }
            int textureOffset = textures.Count;
            textures.AddRange(item.Model.Textures);
            meshes.AddRange(item.Model.Meshes.Where(mesh => !selection.Objects.Any(other => other.GeometrySelected && other.Target != item.Target && other.RigId == item.RigId && other.ReplacedPart == $"mesh/{mesh.NativePart}")).Select(mesh => Remap(mesh, map) with { Name = $"{Token(item.Target)}_{mesh.Name}", Texture = mesh.Texture < 0 ? -1 : textureOffset + mesh.Texture, ObjectTarget = item.Target, RigId = item.RigId }));
        }
        foreach (ModelAssetClip clip in selection.Clips)
        {
            if (!selection.Objects.Any(item => item.Target == clip.ModelTarget && item.RigId == clip.RigId) || !float.IsFinite(clip.SecondsPerFrame) || clip.SecondsPerFrame <= 0 || clip.NativeFrameCount < 1)
            {
                throw new InvalidDataException("A selected clip requires its explicit owner, rig and native timing contract.");
            }
        }
        foreach (IGrouping<string, ModelAssetClip> group in selection.Clips.GroupBy(clip => clip.GroupId, StringComparer.Ordinal))
        {
            Unique(group.Select(clip => clip.RigId), "rig component within a synchronized clip group");
        }
        EditableAnimation[] animations = selection.Clips.GroupBy(clip => clip.GroupId, StringComparer.Ordinal).Select(group => new EditableAnimation(Token(group.Key), group.SelectMany(clip => clip.Animation.Channels.Select(channel => channel with { Bone = rigs[clip.RigId][channel.Bone] })).ToArray())).ToArray();
        Manifest manifest = new(1, 1, selection.Edition, selection.Objects.Select(item => new ObjectInfo(item.Target, item.SourceVariant, item.Fingerprint, item.RigId, item.Model.Kind, item.Model.Name, item.AlternativeGroup, item.ReplacedPart, item.Model.Bones.Count(bone => bone.IsJoint), SkeletonSignature(item.Model.Bones), ModelExchange.RetainedNativeMetadata(item.Model.NativeMetadata, item.GeometrySelected), item.Model.NativeFramesPerSecond)).ToArray(), selection.Clips.Select(clip => new ClipInfo(clip.Target, clip.ModelTarget, clip.RigId, clip.GroupId, clip.SourceVariant, clip.Fingerprint, clip.NativeFrameCount, clip.SecondsPerFrame, Token(clip.GroupId))).ToArray());
        EditableModel combined = new("Memoria linked assets", "assembly", bones, meshes, textures, animations, JsonSerializer.SerializeToElement(new { })) { ExchangeManifest = JsonSerializer.Serialize(manifest) };
        return combined;
    }

    public static void ExportGlbAtomic(string path, ModelAssetSelection selection)
    {
        byte[] bytes = ExportGlb(selection);
        InspectGlb(bytes);
        string destination = Path.GetFullPath(path);
        string temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, destination, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public static ModelAssetImportInventory InspectGlb(ReadOnlySpan<byte> bytes)
    {
        ModelExchangeImportResult imported = ModelExchange.ImportGlb(bytes);
        EditableModel combined = imported.Model;
        if (combined.ExchangeManifest is null)
        {
            return new ModelAssetImportInventory([], [], imported.Warnings.Concat(["Missing memoria_ff8 manifest; explicit model and clip mapping is required."]).ToArray());
        }
        Manifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<Manifest>(combined.ExchangeManifest) ?? throw new InvalidDataException("Missing exchange manifest.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Invalid exchange manifest.", exception);
        }
        if (manifest.Version != 1 || manifest.ConversionVersion != 1 || manifest.Objects is null || manifest.Clips is null || manifest.Objects.Length > 4096 || manifest.Clips.Length > 16384)
        {
            throw new InvalidDataException("Unsupported exchange manifest version or size.");
        }
        if (manifest.Objects.Any(item => item is null || string.IsNullOrWhiteSpace(item.RigId) || !float.IsFinite(item.NativeFramesPerSecond) || item.NativeFramesPerSecond <= 0) || manifest.Clips.Any(item => item is null || item.NativeFrameCount < 1 || !float.IsFinite(item.SecondsPerFrame) || item.SecondsPerFrame <= 0 || string.IsNullOrWhiteSpace(item.AnimationToken) || string.IsNullOrWhiteSpace(item.GroupId) || !manifest.Objects.Any(owner => owner.Target == item.ModelTarget && owner.RigId == item.RigId)))
        {
            throw new InvalidDataException("The manifest has an invalid rig owner or native timing contract.");
        }
        Unique(manifest.Objects.Select(item => item.Target), "model target");
        Unique(manifest.Clips.Select(item => item.Target), "clip target");
        List<ModelAssetObject> objects = [];
        List<ModelAssetClip> clips = [];
        List<string> warnings = imported.Warnings.ToList();
        foreach (ObjectInfo info in manifest.Objects)
        {
            int[] indices = Enumerable.Range(0, combined.Bones.Count).Where(index => combined.Bones[index].RigId == info.RigId || combined.Bones[index].RigId is null && combined.Bones[index].Name.StartsWith(Token(info.RigId) + "_bone_", StringComparison.Ordinal)).ToArray();
            Dictionary<int, int> local = indices.Select((global, index) => (global, index)).ToDictionary(item => item.global, item => item.index);
            EditableMesh[] geometry = combined.Meshes.Where(mesh => mesh.ObjectTarget == info.Target || mesh.ObjectTarget is null && mesh.Name.StartsWith(Token(info.Target) + "_", StringComparison.Ordinal)).ToArray();
            if (indices.Length == 0 && geometry.Length == 0)
            {
                warnings.Add($"{info.Target}: no surviving object or rig; omitted.");
                continue;
            }
            EditableBone[] rig = indices.Select(index => combined.Bones[index] with { Parent = local.GetValueOrDefault(combined.Bones[index].Parent, -1) }).ToArray();
            int[] map = Enumerable.Range(0, combined.Bones.Count).Select(index => local.GetValueOrDefault(index, -1)).ToArray();
            int nativeCount = rig.Count(bone => bone.IsJoint);
            if (nativeCount != info.NativeJointCount)
            {
                warnings.Add($"{info.Target}: native joint count changed from {info.NativeJointCount} to {nativeCount}; explicit approval required.");
            }
            else if (SkeletonSignature(rig) != info.SkeletonSignature)
            {
                warnings.Add($"{info.Target}: native joint hierarchy or rest transforms changed; retained clips and attachments require validation.");
            }
            EditableModel model = new(info.Name, info.Kind, rig, geometry.Select(mesh => Remap(mesh, map)).ToArray(), combined.Textures, [], info.NativeMetadata) { NativeFramesPerSecond = info.NativeFramesPerSecond };
            objects.Add(new ModelAssetObject(info.Target, info.SourceVariant, info.Fingerprint, info.RigId, model, geometry.Length != 0, info.AlternativeGroup, info.ReplacedPart));
            foreach (ClipInfo clip in manifest.Clips.Where(clip => clip.ModelTarget == info.Target))
            {
                EditableAnimation[] matches = combined.Animations.Where(animation => animation.Name.Contains(clip.AnimationToken, StringComparison.Ordinal)).ToArray();
                if (matches.Length > 1)
                {
                    throw new InvalidDataException($"Ambiguous animation identity for {clip.Target}.");
                }
                if (matches.Length == 0)
                {
                    continue;
                }
                EditableAnimationChannel[] channels = matches[0].Channels.Where(channel => local.ContainsKey(channel.Bone)).Select(channel => channel with { Bone = local[channel.Bone] }).ToArray();
                if (channels.Length == 0)
                {
                    continue;
                }
                float nativeDuration = (clip.NativeFrameCount - 1) * clip.SecondsPerFrame;
                float groupDuration = manifest.Clips.Where(component => component.GroupId == clip.GroupId).Max(component => (component.NativeFrameCount - 1) * component.SecondsPerFrame);
                if ((groupDuration > nativeDuration + 0.0001f || clip.NativeFrameCount == 1) && channels.Max(channel => channel.Times[^1]) <= MathF.Max(groupDuration, clip.NativeFrameCount == 1 ? clip.SecondsPerFrame : groupDuration) + 0.0001f && channels.Any(channel => channel.Times[^1] > nativeDuration + 0.0001f) && channels.All(channel => channel.Interpolation != "CUBICSPLINE" && channel.Times.Zip(channel.Values).Where(pair => pair.First > nativeDuration).All(pair => Vector4.Distance(pair.Second, ModelExchange.Sample(channel, nativeDuration)) < 0.0001f)))
                {
                    channels = channels.Select(channel =>
                    {
                        if (channel.Times[^1] <= nativeDuration + 0.0001f)
                        {
                            return channel;
                        }
                        float[] times = channel.Times.Where(time => time < nativeDuration).Append(nativeDuration).ToArray();
                        return channel with { Times = times, Values = times.Select(time => ModelExchange.Sample(channel, time)).ToArray() };
                    }).ToArray();
                    warnings.Add($"{clip.Target}: removed a constant composite hold tail after the recorded native duration.");
                }
                EditableAnimation animation = new(clip.Target, channels);
                if (MathF.Abs(animation.Duration - (clip.NativeFrameCount - 1) * clip.SecondsPerFrame) > 0.0001f)
                {
                    warnings.Add($"{clip.Target}: duration changed; game scripts may depend on native frame positions. Timing is not rescaled.");
                }
                clips.Add(new ModelAssetClip(clip.Target, clip.ModelTarget, clip.RigId, clip.GroupId, clip.SourceVariant, clip.Fingerprint, animation, clip.NativeFrameCount, clip.SecondsPerFrame));
            }
        }
        if (combined.Meshes.Any(mesh => mesh.ObjectTarget is null))
        {
            warnings.Add("Some meshes have no stable model target; explicit mapping is required.");
        }
        return new ModelAssetImportInventory(objects, clips, warnings.Distinct().ToArray()) { Edition = manifest.Edition };
    }

    private static EditableMesh Remap(EditableMesh mesh, int[] map)
    {
        int Joint(int index)
        {
            return (uint)index < map.Length && map[index] >= 0 ? map[index] : throw new InvalidDataException("A mesh references a joint outside its recorded rig.");
        }
        return mesh with
        {
            Node = mesh.Node < 0 ? -1 : Joint(mesh.Node),
            Vertices = mesh.Vertices.Select(vertex => vertex with { Joints = new Vector4(vertex.Weights.X == 0 ? 0 : Joint((int)vertex.Joints.X), vertex.Weights.Y == 0 ? 0 : Joint((int)vertex.Joints.Y), vertex.Weights.Z == 0 ? 0 : Joint((int)vertex.Joints.Z), vertex.Weights.W == 0 ? 0 : Joint((int)vertex.Joints.W)) }).ToArray(),
            InverseBindMatrices = mesh.InverseBindMatrices?.ToDictionary(pair => Joint(pair.Key), pair => pair.Value)
        };
    }

    private static void Unique(IEnumerable<string> values, string description)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string value in values)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 4096 || !seen.Add(value))
            {
                throw new InvalidDataException($"Missing or duplicate {description}; choose one source variant per replacement slot.");
            }
        }
    }

    private static string SkeletonSignature(IReadOnlyList<EditableBone> bones)
    {
        string shape = JsonSerializer.Serialize(bones.Where(bone => bone.IsJoint).Select(bone => new { bone.NativeIndex, Parent = bone.Parent < 0 ? -1 : bones[bone.Parent].NativeIndex, T = new[] { bone.Translation.X, bone.Translation.Y, bone.Translation.Z }, R = new[] { bone.Rotation.X, bone.Rotation.Y, bone.Rotation.Z, bone.Rotation.W }, S = new[] { bone.Scale.X, bone.Scale.Y, bone.Scale.Z } }));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(shape)));
    }

    private static string Token(string address) => "memoria_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(address)))[..20].ToLowerInvariant();
}
