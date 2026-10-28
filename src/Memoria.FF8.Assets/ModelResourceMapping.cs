using System.Collections.Immutable;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Maps compatible source visuals onto the selected runtime's animation and control ownership.</summary>
public static class ModelResourceMapping
{
    public static FieldModel Select(FieldModel primary, FieldModel donor, ResourceSourceDescriptor descriptor)
    {
        try
        {
            return SelectField(primary, donor, descriptor);
        }
        catch (NotSupportedException error)
        {
            throw new NotSupportedException($"Field model {primary.Name} from {descriptor.Id}: {error.Message}", error);
        }
    }

    private static FieldModel SelectField(FieldModel primary, FieldModel donor, ResourceSourceDescriptor descriptor)
    {
        Require(Identity(primary.Name) == Identity(donor.Name), "Field model identities differ.");
        Require(primary.EditableOverride is null && donor.EditableOverride is null, "Edited models cannot supply a native resource mapping.");
        Require(primary.Bones.SequenceEqual(donor.Bones) && primary.ControlBones == donor.ControlBones, "Field skeleton or control bones differ.");
        Require(primary.MainCharacterTextures == donor.MainCharacterTextures && primary.TextureResourceName == donor.TextureResourceName, "Field material owners differ.");
        Materials(primary.Textures, primary.TextureRegions, donor.Textures, donor.TextureRegions);
        Require(primary.Animations.Count == donor.Animations.Count, "Field animation directories differ.");
        for (int animation = 0; animation < primary.Animations.Count; animation++)
        {
            IReadOnlyList<AnimationFrame> left = primary.Animations[animation].Frames;
            IReadOnlyList<AnimationFrame> right = donor.Animations[animation].Frames;
            Require(left.Count == right.Count, "Field animation frame counts differ.");
            for (int frame = 0; frame < left.Count; frame++)
            {
                Require(left[frame].Translation == right[frame].Translation && left[frame].Rotations.SequenceEqual(right[frame].Rotations), "Field animation poses differ.");
            }
        }
        FieldParts(primary, donor);
        IReadOnlyList<ModelVertex> vertices = donor.Vertices;
        foreach (ModelVertex vertex in vertices)
        {
            Require((uint)vertex.Bone < primary.Bones.Count, "Donor field vertex references an absent bone.");
            Require(vertex.SkinBinding == -1 || donor.NativeParts is { } parts && (uint)vertex.SkinBinding < parts.SkinBindings.Count && parts.SkinBindings[vertex.SkinBinding].Bone == vertex.Bone, "Donor field vertex references an absent or incompatible skin.");
        }
        foreach (ModelFace face in donor.Faces)
        {
            Require((uint)face.Texture < donor.Textures.Count && face.Vertices.Take(face.Quad ? 4 : 3).All(index => (uint)index < vertices.Count), "Donor field face references an absent material or vertex.");
        }
        return primary with { NativeRegistrationParts = primary.NativeRegistrationParts ?? primary.NativeParts, NativeParts = donor.NativeParts, Vertices = vertices, Faces = donor.Faces, Textures = donor.Textures, TextureRegions = donor.TextureRegions, TextureSourceVariant = donor.TextureSourceVariant, TextureSourceDescriptor = descriptor };
    }

    public static BattleModel Select(BattleModel primary, BattleModel donor, ResourceSourceDescriptor descriptor)
    {
        try
        {
            return SelectBattle(primary, donor, descriptor);
        }
        catch (NotSupportedException error)
        {
            throw new NotSupportedException($"Battle model {primary.Name} from {descriptor.Id}: {error.Message}", error);
        }
    }

    private static BattleModel SelectBattle(BattleModel primary, BattleModel donor, ResourceSourceDescriptor descriptor)
    {
        Require(string.Equals(primary.Name, donor.Name, StringComparison.OrdinalIgnoreCase), "Battle model identities differ.");
        Require(primary.EditableOverride is null && donor.EditableOverride is null, "Edited models cannot supply a native resource mapping.");
        Require(primary.Bones.SequenceEqual(donor.Bones) && primary.Scale == donor.Scale, "Battle skeletons or scales differ.");
        Materials(primary.Textures, primary.TextureRegions, donor.Textures, donor.TextureRegions);
        Require(primary.Animations.Count == donor.Animations.Count, "Battle animation directories differ.");
        for (int animation = 0; animation < primary.Animations.Count; animation++)
        {
            IReadOnlyList<BattlePose> left = primary.Animations[animation].Frames;
            IReadOnlyList<BattlePose> right = donor.Animations[animation].Frames;
            Require(left.Count == right.Count, "Battle animation frame counts differ.");
            for (int frame = 0; frame < left.Count; frame++)
            {
                Require(left[frame].Translation == right[frame].Translation && left[frame].Rotations.SequenceEqual(right[frame].Rotations) && left[frame].Scales.SequenceEqual(right[frame].Scales), "Battle animation poses differ.");
            }
        }
        SameBytes(primary.NativePoseSource?.Skeleton, donor.NativePoseSource?.Skeleton);
        SameBytes(primary.NativePoseSource?.Animations, donor.NativePoseSource?.Animations);
        SameBytes(primary.NativePoseSource?.Model8FBoneAliases, donor.NativePoseSource?.Model8FBoneAliases);
        SameBytes(primary.Sequences?.NativeResource.SequenceSection, donor.Sequences?.NativeResource.SequenceSection);
        SameBytes(primary.NativeActionCameraBank?.Data, donor.NativeActionCameraBank?.Data);
        SameBytes(primary.NativeTextureAnimations, donor.NativeTextureAnimations);
        SameBytes(primary.NativeBodyTexturePack?.Data, donor.NativeBodyTexturePack?.Data);
        SameBytes(primary.NativeWeaponTexturePack?.Data, donor.NativeWeaponTexturePack?.Data);
        Require(primary.NativeWeaponUsesBodySkeleton == donor.NativeWeaponUsesBodySkeleton, "Battle weapon skeleton ownership differs.");
        Require(primary.Meshes.Count == donor.Meshes.Count, "Battle mesh control ordinals differ.");
        foreach (BattleMesh mesh in donor.Meshes)
        {
            Require(mesh.Vertices.All(vertex => (uint)vertex.Bone < primary.Bones.Count), "Donor battle vertex references an absent bone.");
            Require(mesh.Faces.All(face => (uint)face.Texture < donor.Textures.Count && face.Vertices.All(index => (uint)index < mesh.Vertices.Count)), "Donor battle face references an absent material or vertex.");
        }
        return primary with { Meshes = donor.Meshes, NativeControlMeshes = primary.NativeControlMeshes ?? primary.Meshes, NativeGeometryBytes = donor.NativeGeometryBytes, NativeRenderGeometry = donor.NativeRenderGeometry, Textures = donor.Textures, TextureRegions = donor.TextureRegions, TextureSourceAddresses = donor.TextureSourceAddresses, TextureSourceDescriptor = descriptor };
    }

    private static void FieldParts(FieldModel primary, FieldModel donor)
    {
        if (primary.NativeParts is null || donor.NativeParts is null)
        {
            Require(primary.NativeParts is null && donor.NativeParts is null && primary.Vertices.All(vertex => vertex.SkinBinding < 0) && donor.Vertices.All(vertex => vertex.SkinBinding < 0), "Field visual resources require both native part tables.");
            return;
        }
        NativeModelPartsDefinition left = primary.NativeParts;
        NativeModelPartsDefinition right = donor.NativeParts;
        left.Validate();
        right.Validate();
        Require(left.Parts.Count == right.Parts.Count, $"Field script part ordinals differ (primary {left.Parts.Count}, selected {right.Parts.Count}).");
        for (int part = 0; part < left.Parts.Count; part++)
        {
            Require(Enumerable.Range(2, 14).Where(word => word is not (9 or 11)).All(word => left.Parts[part].Words[word] == right.Parts[part].Words[word]), "Field part controls differ.");
        }
    }

    private static string Identity(string name)
    {
        Require(name.Length >= 4 && name.Skip(1).Take(3).All(char.IsAsciiDigit), "Field model requires a four-character source identity.");
        return name[..4].ToLowerInvariant();
    }

    private static void Materials(IReadOnlyList<PixelImage> left, IReadOnlyList<TextureSourceRegion> leftRegions, IReadOnlyList<PixelImage> right, IReadOnlyList<TextureSourceRegion> rightRegions)
    {
        Require(left.Count == right.Count && leftRegions.Count == left.Count && rightRegions.Count == right.Count && leftRegions.SequenceEqual(rightRegions), "Model material ordinal or source regions differ.");
        for (int index = 0; index < left.Count; index++)
        {
            Require(left[index].Width == right[index].Width && left[index].Height == right[index].Height, "Model material canvas sizes differ.");
        }
    }

    private static void SameBytes(ImmutableArray<byte>? left, ImmutableArray<byte>? right)
    {
        Require(left.HasValue == right.HasValue && (!left.HasValue || left.Value.AsSpan().SequenceEqual(right!.Value.AsSpan())), "Battle native runtime sections differ.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new NotSupportedException(message);
        }
    }
}
