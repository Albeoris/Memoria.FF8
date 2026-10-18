using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEnemyResourceView
{
    public static NativeBattleGraphicsResource Resolve(NativeBattleModelCpuResources cpu, NativeBattleFileStaging staging, int index, NativeBattleGraphicsResource original)
    {
        NativeBattleModelCpuResourceRow row = cpu.Rows[index];
        NativeBattleModelCpuReference skeleton = row.Require(12);
        int boneCount = NativeBattleModelEnemyHeap.Read(cpu, staging, skeleton, 1)[0];
        NativeBattlePoseSource pose = new(NativeBattleModelEnemyHeap.Read(cpu, staging, skeleton, checked(16 + boneCount * 48)).ToImmutableArray(), Block(20));
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
            return NativeBattleRawGeometryReader.Read(bytes, boneCount);
        }
    }
}
