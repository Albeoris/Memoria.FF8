using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEnemyResourceView
{
    public static NativeBattleGraphicsResource Resolve(NativeBattleModelCpuResources cpu, NativeBattleFileStaging staging, int index, NativeBattleGraphicsResource original)
    {
        return Resolve(cpu, staging, index, original, null);
    }

    internal static NativeBattleGraphicsResource Resolve(NativeBattleModelCpuResources cpu, NativeBattleFileStaging staging, int index, NativeBattleGraphicsResource original, NativeBattleEnemyGeometryCache? geometryCache)
    {
        NativeBattleModelCpuResourceRow row = cpu.Rows[index];
        NativeBattleModelCpuReference skeleton = row.Require(12);
        int boneCount = NativeBattleModelEnemyHeap.Read(cpu, staging, skeleton, 1)[0];
        int skeletonLength = checked(16 + boneCount * 48);
        NativeBattlePoseSource pose;
        if (skeleton.Bank == 3)
        {
            (byte[] bytes, bool[] known) = staging.ReadSnapshot(skeleton.SharedAddress, skeletonLength);
            pose = new(bytes.ToImmutableArray(), Block(20)) { SkeletonKnownBytes = known.ToImmutableArray() };
        }
        else
        {
            pose = new(NativeBattleModelEnemyHeap.Read(cpu, staging, skeleton, skeletonLength).ToImmutableArray(), Block(20));
        }
        pose = pose with { Model8FBoneAliases = original.PoseSource?.Model8FBoneAliases ?? [] };
        bool camera = row.Fields.TryGetValue(44, out NativeBattleModelCpuReference? cameraReference) ? cameraReference is not null : throw new NotSupportedException("Native enemy action-camera pointer remains unknown.");
        NativeBattleGraphicsResource result = original with { PoseSource = pose, AnimationFrames = NativeBattlePoseDecoder.ReadFrameCounts(pose.Animations.AsSpan()), SequenceSection = Block(24), Geometry = Geometry(), GeometryRelocated507550 = true, HasActionCamera = camera, ActionCameraBank = camera ? new(Block(44)) : null, TextureAnimations = row.Fields.TryGetValue(48, out NativeBattleModelCpuReference? motion) && motion is not null ? Block(48) : [] };
        result.Validate();
        return result;

        ImmutableArray<byte> Block(int field)
        {
            int length = row.Lengths.TryGetValue(field, out int known) ? known : throw new NotSupportedException("Native enemy resource section extent remains unknown.");
            return NativeBattleModelEnemyHeap.Read(cpu, staging, row.Require(field), length).ToImmutableArray();
        }

        ImmutableArray<NativeBattleRawMesh> Geometry()
        {
            NativeBattleModelCpuReference reference = row.Require(16);
            byte[] bytes = NativeBattleModelEnemyHeap.Read(cpu, staging, reference, row.Lengths.TryGetValue(16, out int length) ? length : throw new NotSupportedException("Native enemy geometry extent remains unknown."));
            return geometryCache is null ? NativeBattleRawGeometryReader.Read(bytes, boneCount) : geometryCache.Read(bytes, boneCount);
        }
    }
}

/// <summary>Memoize parsing only after comparing every currently observed geometry byte.</summary>
internal sealed class NativeBattleEnemyGeometryCache
{
    private byte[]? source;
    private int boneCount;
    private ImmutableArray<NativeBattleRawMesh> geometry;

    public ImmutableArray<NativeBattleRawMesh> Read(byte[] bytes, int bones)
    {
        if (source is not null && boneCount == bones && bytes.AsSpan().SequenceEqual(source))
        {
            return geometry;
        }
        byte[] owned = bytes.AsSpan().ToArray();
        ImmutableArray<NativeBattleRawMesh> parsed = NativeBattleRawGeometryReader.Read(owned, bones);
        source = owned;
        boneCount = bones;
        geometry = parsed;
        return parsed;
    }
}
