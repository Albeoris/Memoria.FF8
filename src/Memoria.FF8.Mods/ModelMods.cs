using System.Numerics;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Mods;

public sealed record ModelModVariant(string ModelId, string PackageId, string RelativePath)
{
    public string Bank { get; init; } = "embedded";
    public int? ClipSlot { get; init; }
    public bool GeometryOnly { get; init; }
}

/// <summary>Immutable model replacements in package precedence order.</summary>
public sealed class ModelMods : IModelOverrideSource
{
    private readonly IReadOnlyList<(ModelModVariant Variant, ModFile File)> files;
    private readonly Dictionary<(string Id, string Bank, object Original), object> replacements = [];

    private ModelMods(IReadOnlyList<(ModelModVariant Variant, ModFile File)> files)
    {
        this.files = files;
        Variants = Array.AsReadOnly(files.Select(item => item.Variant).ToArray());
    }

    public static ModelMods Empty { get; } = new([]);
    public IReadOnlyList<ModelModVariant> Variants { get; }
    public bool HasModelOverride(string modelId) => files.Any(item => item.Variant.ModelId.Equals(modelId, StringComparison.OrdinalIgnoreCase) && item.Variant.Bank == "embedded");
    public IReadOnlyList<string> BattleBanks(string modelId) => files.Where(item => item.Variant.ModelId.Equals(modelId, StringComparison.OrdinalIgnoreCase)).Select(item => item.Variant.Bank).Where(bank => bank != "embedded").Distinct(StringComparer.Ordinal).ToArray();

    public static bool TryPrepare(ModCatalogSnapshot catalog, out ModelMods result, out IReadOnlyList<ModDiagnostic> diagnostics)
    {
        List<(ModelModVariant Variant, ModFile File)> files = [];
        List<ModDiagnostic> errors = [];
        foreach (ModFile file in catalog.Files.Where(file => file.Format == ModFormat.Memoria && file.RelativePath.StartsWith("models/", StringComparison.OrdinalIgnoreCase) && file.RelativePath.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                ModelModVariant variant = Parse(file);
                EditableModel model = ModelExchange.ImportGlb(file.ReadBytes(), allowBoneCountChange: true).Model;
                if (variant.ClipSlot is not null && (model.Meshes.Count != 0 || model.Animations.Count != 1))
                {
                    throw new InvalidDataException("A selective clip file must contain exactly one animation and no geometry.");
                }
                files.Add((variant, file));
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException or ArgumentException or OverflowException or System.Text.Json.JsonException)
            {
                errors.Add(new("Memoria.InvalidModel", exception.Message, file.PackageId, file.RelativePath));
            }
        }
        result = errors.Count == 0 ? new(Array.AsReadOnly(files.ToArray())) : Empty;
        diagnostics = Array.AsReadOnly(errors.ToArray());
        return errors.Count == 0;
    }

    private static ModelModVariant Parse(ModFile file)
    {
        string address = file.RelativePath[7..^4].ToLowerInvariant();
        string bank = "embedded";
        int? slot = null;
        bool geometry = address.EndsWith(".geometry", StringComparison.Ordinal);
        if (geometry)
        {
            address = address[..^9];
        }
        int marker = address.IndexOf(".clips/", StringComparison.Ordinal);
        if (marker >= 0)
        {
            string[] suffix = address[(marker + 7)..].Split('/');
            if (suffix.Length != 2 || !IsBank(suffix[0]) || !int.TryParse(suffix[1], out int index) || index < 0 || index > 65535)
            {
                throw new InvalidDataException("A clip replacement requires models/<model>.clips/<bank>/<slot>.glb.");
            }
            bank = suffix[0];
            slot = index;
            address = address[..marker];
        }
        if (!IsModelId(address))
        {
            throw new InvalidDataException("Model replacements require a supported logical model address.");
        }
        return new(address, file.PackageId, file.RelativePath) { Bank = bank, ClipSlot = slot, GeometryOnly = geometry };
    }

    public static bool IsBank(string bank) => bank.Length is > 0 and < 128 && bank.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    public EditableModel Resolve(string modelId, EditableModel original, string? packageId = null, string bank = "embedded")
    {
        EditableModel model = original;
        foreach (var item in files.Where(item => item.Variant.ModelId.Equals(modelId, StringComparison.OrdinalIgnoreCase) && item.Variant.Bank == bank && (packageId is null || item.Variant.PackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase))).Reverse())
        {
            model = Merge(model, item.Variant, ModelExchange.ImportGlb(item.File.ReadBytes(), allowBoneCountChange: true).Model);
        }
        return model;
    }

    public EditableModel Load(ModelModVariant variant, EditableModel original)
    {
        var item = files.FirstOrDefault(item => item.Variant == variant);
        if (item.File is null)
        {
            throw new FileNotFoundException("The model variant is no longer present in the active catalog.");
        }
        return Merge(original, item.Variant, ModelExchange.ImportGlb(item.File.ReadBytes(), allowBoneCountChange: true).Model);
    }

    public FieldModel Apply(string modelId, FieldModel original) => Apply(modelId, "embedded", original, model => ModelExchange.FromField(model, modelId.StartsWith("world/", StringComparison.OrdinalIgnoreCase) ? ModelExchange.WorldFramesPerSecond : ModelExchange.FramesPerSecond), (model, source) => ModelModAnimationConverter.ToField(model, HasGeometryOverride(modelId, "embedded") ? ModelExchange.ToField(model, source) : source with { EditableOverride = model }));
    public BattleModel Apply(string modelId, BattleModel original) => ApplyBattleBank(modelId, "embedded", original);
    public BattleModel ApplyBattleBank(string modelId, string bank, BattleModel original) => Apply(modelId, bank, original, ModelExchange.FromBattle, (model, source) => ModelModAnimationConverter.ToBattle(model, HasGeometryOverride(modelId, bank) ? ModelExchange.ToBattle(model, source) with { NativeControlMeshes = source.NativeControlMeshes ?? source.Meshes } : source with { EditableOverride = model }));
    public BattleStage Apply(string modelId, BattleStage original) => Apply(modelId, "embedded", original, ModelExchange.FromStage, ModelExchange.ToStage);
    public WorldStaticModel Apply(string modelId, WorldStaticModel original) => Apply(modelId, "embedded", original, ModelExchange.FromWorld, ModelExchange.ToWorld);

    private bool HasGeometryOverride(string modelId, string bank) => files.Any(item => item.Variant.ModelId.Equals(modelId, StringComparison.OrdinalIgnoreCase) && item.Variant.Bank == bank && item.Variant.ClipSlot is null);

    private T Apply<T>(string modelId, string bank, T original, Func<T, EditableModel> from, Func<EditableModel, T, T> to) where T : class
    {
        var matches = files.Where(item => item.Variant.ModelId.Equals(modelId, StringComparison.OrdinalIgnoreCase) && item.Variant.Bank == bank && (bank == "embedded" || item.Variant.ClipSlot is not null)).ToArray();
        if (matches.Length == 0)
        {
            return original;
        }
        var key = (modelId.ToLowerInvariant(), bank, (object)original);
        if (replacements.TryGetValue(key, out object? cached))
        {
            return (T)cached;
        }
        EditableModel model = from(original);
        foreach (var match in matches.Reverse())
        {
            model = Merge(model, match.Variant, ModelExchange.ImportGlb(match.File.ReadBytes(), allowBoneCountChange: true).Model);
        }
        T replacement = to(model, original);
        replacements.Add(key, replacement);
        return replacement;
    }

    internal static EditableModel Merge(EditableModel original, ModelModVariant variant, EditableModel edited)
    {
        if (variant.ClipSlot is null && !variant.GeometryOnly)
        {
            return edited;
        }
        if (variant.ClipSlot is not null && Math.Abs(original.NativeFramesPerSecond - edited.NativeFramesPerSecond) > 0.0001f)
        {
            throw new NotSupportedException("The imported clip clock does not match the target native playback contract.");
        }
        int[] retainedParts = variant.GeometryOnly && edited.NativeMetadata.ValueKind == System.Text.Json.JsonValueKind.Object && edited.NativeMetadata.TryGetProperty("memoria_preserve_native_parts", out var preservedParts) ? preservedParts.EnumerateArray().Select(part => part.GetInt32()).ToArray() : [];
        edited = AlignRetainedRig(original, edited);
        if (variant.GeometryOnly)
        {
            return RetainParts(original, original with { Meshes = edited.Meshes, Textures = edited.Textures }, retainedParts);
        }
        int slot = variant.ClipSlot!.Value;
        if (slot >= original.Animations.Count)
        {
            throw new InvalidDataException($"Clip slot {slot} is absent from the target bank; adding gameplay slots is unsupported.");
        }
        EditableAnimation[] animations = original.Animations.ToArray();
        ValidateNativeTracks(edited);
        animations[slot] = edited.Animations.Single();
        return original with { Animations = animations };
    }

    public static EditableModel Preview(EditableModel original, ModelModReplacement replacement)
    {
        EditableModel aligned = AlignRetainedRig(original, replacement.Model);
        EditableModel result = replacement.ReplaceGeometry ? RetainParts(original, original with { Meshes = aligned.Meshes, Textures = aligned.Textures }, replacement.PreserveNativeParts) : original;
        EditableAnimation[] animations = original.Animations.ToArray();
        HashSet<(string Bank, int Slot)> targets = [];
        foreach (ModelModClipReplacement clip in replacement.Clips)
        {
            if (Math.Abs(original.NativeFramesPerSecond - replacement.Model.NativeFramesPerSecond) > 0.0001f)
            {
                throw new NotSupportedException("The imported clip clock does not match the target native playback contract.");
            }
            if (!targets.Add((clip.Bank, clip.Slot)) || clip.Slot < 0 || clip.Slot >= animations.Length)
            {
                throw new InvalidDataException("The selection duplicates a target clip or maps to an absent gameplay slot.");
            }
            EditableModel selected = AlignRetainedRig(original, replacement.Model with { Animations = [clip.Animation] });
            ValidateNativeTracks(selected);
            ModelModAnimationConverter.Validate(selected with { NativeMetadata = original.NativeMetadata });
            animations[clip.Slot] = selected.Animations[0];
        }
        return result with { Animations = animations };
    }

    private static EditableModel RetainParts(EditableModel original, EditableModel edited, IReadOnlyList<int> retained)
    {
        List<EditableMesh> meshes = edited.Meshes.ToList();
        List<Albeoris.Games.FF8.Imaging.PixelImage> textures = edited.Textures.ToList();
        foreach (int part in retained.Distinct())
        {
            if (part < 0 || !original.Meshes.Any(mesh => mesh.NativePart == part))
            {
                throw new InvalidDataException("A retained native geometry part is absent from the target model.");
            }
            if (meshes.Any(mesh => mesh.NativePart == part))
            {
                continue;
            }
            foreach (EditableMesh mesh in original.Meshes.Where(mesh => mesh.NativePart == part))
            {
                int texture = mesh.Texture;
                if (texture >= 0)
                {
                    texture = textures.Count;
                    textures.Add(original.Textures[mesh.Texture]);
                }
                meshes.Add(mesh with { Texture = texture });
            }
        }
        return edited with { Meshes = meshes, Textures = textures };
    }

    private static void ValidateNativeTracks(EditableModel model)
    {
        foreach (EditableAnimation animation in model.Animations)
        {
            double frames = Math.Round(animation.Duration * model.NativeFramesPerSecond) + 1;
            if (frames < 1 || frames > (model.Kind == "battle" ? 255 : 65535) || Math.Abs((frames - 1) / model.NativeFramesPerSecond - animation.Duration) > 0.001)
            {
                throw new NotSupportedException("The clip duration must fit the native frame grid and frame-count range.");
            }
            foreach (EditableAnimationChannel channel in animation.Channels)
            {
                EditableBone bone = model.Bones[channel.Bone];
                IEnumerable<Vector4> keys = channel.Interpolation == "CUBICSPLINE" ? channel.Values.Where((_, index) => index % 3 == 1) : channel.Values;
                if (!bone.IsJoint)
                {
                    throw new NotSupportedException("Animation of exporter helper nodes cannot be represented by the native rig.");
                }
                if (channel.Path == "translation" && bone.Parent >= 0 && keys.Any(value => Vector3.Distance(new(value.X, value.Y, value.Z), bone.Translation) > 0.001f))
                {
                    throw new NotSupportedException("Native clips cannot animate non-root joint translations.");
                }
                if (model.Kind == "field" && channel.Path == "scale" && keys.Any(value => Vector3.Distance(new(value.X, value.Y, value.Z), Vector3.One) > 0.001f))
                {
                    throw new NotSupportedException("Native field clips cannot represent animated scale.");
                }
            }
        }
    }

    internal static EditableModel AlignRetainedRig(EditableModel original, EditableModel edited)
    {
        int[] originals = Enumerable.Range(0, original.Bones.Count).Where(index => original.Bones[index].IsJoint).ToArray();
        int[] incoming = Enumerable.Range(0, edited.Bones.Count).Where(index => edited.Bones[index].IsJoint).ToArray();
        if (original.Kind != edited.Kind || originals.Length != incoming.Length)
        {
            throw new NotSupportedException("Selective replacements must retain the target model kind and native rig.");
        }
        Dictionary<int, int> map = [];
        foreach (int index in incoming)
        {
            EditableBone bone = edited.Bones[index];
            int[] matches = originals.Where(target => bone.NativeIndex >= 0 ? original.Bones[target].NativeIndex == bone.NativeIndex : original.Bones[target].Name == bone.Name).ToArray();
            if (matches.Length != 1 || map.ContainsValue(matches[0]))
            {
                throw new NotSupportedException("Native joint identities must map unambiguously to the retained rig.");
            }
            map.Add(index, matches[0]);
        }
        foreach ((int index, int target) in map)
        {
            EditableBone a = original.Bones[target];
            EditableBone b = edited.Bones[index];
            int parent = b.Parent;
            while (parent >= 0 && !map.ContainsKey(parent))
            {
                EditableBone helper = edited.Bones[parent];
                if (helper.Translation.Length() > 0.001f || Vector3.Distance(helper.Scale, Vector3.One) > 0.001f || 1 - Math.Abs(Quaternion.Dot(Quaternion.Normalize(helper.Rotation), Quaternion.Identity)) > 0.00001f)
                {
                    throw new NotSupportedException("Apply helper transforms before selective import.");
                }
                parent = helper.Parent;
            }
            int expectedParent = parent < 0 ? -1 : map[parent];
            if (a.Parent != expectedParent || Vector3.Distance(a.Translation, b.Translation) > 0.001f || Vector3.Distance(a.Scale, b.Scale) > 0.001f || 1 - Math.Abs(Quaternion.Dot(Quaternion.Normalize(a.Rotation), Quaternion.Normalize(b.Rotation))) > 0.00001f)
            {
                throw new NotSupportedException("Selective replacements require the native hierarchy and rest transforms unchanged.");
            }
        }
        EditableAnimation[] animations = edited.Animations.Select(animation => animation with { Channels = animation.Channels.Select(channel => channel with { Bone = map.TryGetValue(channel.Bone, out int target) ? target : throw new NotSupportedException("Animation of exporter helper nodes is unsupported.") }).ToArray() }).ToArray();
        EditableMesh[] meshes = edited.Meshes.Select(mesh => mesh with
        {
            Node = -1,
            RigId = original.Bones.FirstOrDefault(bone => bone.IsJoint)?.RigId,
            Vertices = mesh.Vertices.Select(vertex => vertex with { Joints = new(Map(vertex.Joints.X, vertex.Weights.X), Map(vertex.Joints.Y, vertex.Weights.Y), Map(vertex.Joints.Z, vertex.Weights.Z), Map(vertex.Joints.W, vertex.Weights.W)) }).ToArray(),
            InverseBindMatrices = mesh.InverseBindMatrices?.ToDictionary(pair => map[pair.Key], pair => pair.Value)
        }).ToArray();
        return edited with { Bones = original.Bones, Meshes = meshes, Animations = animations, NativeFramesPerSecond = original.NativeFramesPerSecond, NativeMetadata = original.NativeMetadata };

        float Map(float joint, float weight) => weight == 0 ? 0 : map.TryGetValue((int)joint, out int target) ? target : throw new NotSupportedException("A weighted joint is absent from the retained rig.");
    }
    public static bool IsModelId(string id)
    {
        string[] parts = id.Split('/');
        if (parts.Any(part => part.Length == 0 || part is "." or ".." || part.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-' and not '.')))
        {
            return false;
        }
        return parts.Length == 3 && parts[0] == "field" && int.TryParse(parts[2], out int fieldIndex) && fieldIndex >= 0
            || parts.Length == 2 && parts[0] == "battle" && (parts[1].EndsWith(".dat", StringComparison.OrdinalIgnoreCase) || parts[1].EndsWith(".x", StringComparison.OrdinalIgnoreCase))
            || parts.Length == 2 && parts[0] == "world" && int.TryParse(parts[1], out int shape) && shape is >= 0 and < 64
            || parts.Length == 3 && parts[0] == "world" && parts[1] == "static" && int.TryParse(parts[2], out int staticShape) && staticShape is >= 64 and <= 255;
    }
}
