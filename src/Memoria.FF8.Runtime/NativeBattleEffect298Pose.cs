using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect298PosePreparation(NativeBattleGraphicsModel Model, NativeBattleEffectArena Temporary96);

public static class NativeBattleEffect298Pose
{
    public static NativeBattleEffect298PosePreparation Prepare662C00(NativeBattleGraphicsModel model, NativeBattlePoseSource source, NativeBattleEffectArena offsets, NativeBattleEffectArena temporary96)
    {
        source.Validate();
        NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadDecodedPose(model.SequenceState ?? throw new NotSupportedException("Offset-pose preparation requires its current decoded pose owner."), source);
        ImmutableArray<NativeBattleBonePose>.Builder changed = decoded.Bones.ToBuilder();
        for (int bone = 0; bone < source.BoneCount; bone++)
        {
            NativeBattleVector rotation = decoded.Bones[bone].Rotation;
            rotation = new(unchecked((short)(rotation.X + Word(offsets, bone * 16 + 8))), unchecked((short)(rotation.Y + Word(offsets, bone * 16 + 10))), unchecked((short)(rotation.Z + Word(offsets, bone * 16 + 12))));
            changed[bone] = changed[bone] with { Rotation = rotation };
        }
        NativeBattleDecodedPose pose = decoded with { Bones = changed.ToImmutable() };
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattlePoseMatrices.StageBoneMatrices(source, pose);
        NativeBattleBodyBoneMatrices publication = new(NativeBattleBodyBoneMatrices.Identity(source), matrices);
        publication.Validate();
        short scale = BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan().Slice(2, 2));
        for (int bone = 0; bone < source.BoneCount; bone++)
        {
            NativeBattleVector angles = pose.Bones[bone].Rotation;
            temporary96 = temporary96.Write(0x58, BitConverter.GetBytes(angles.X)).Write(0x5A, BitConverter.GetBytes(angles.Y)).Write(0x5C, BitConverter.GetBytes(angles.Z));
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(angles);
            int parent = BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan().Slice(16 + bone * 48, 2));
            if (parent < 0)
            {
                temporary96 = temporary96.Write(0x40, BitConverter.GetBytes((int)scale)).Write(0x44, BitConverter.GetBytes((int)scale)).Write(0x48, BitConverter.GetBytes((int)scale));
                if (!pose.HasScale)
                {
                    local = NativeBattlePoseMatrices.Scale56BEF0(local, scale, scale, scale);
                }
            }
            else
            {
                short length = BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan().Slice(18 + parent * 48, 2));
                if (pose.HasScale)
                {
                    temporary96 = temporary96.Write(0x50, new byte[4]).Write(0x54, BitConverter.GetBytes(length));
                    (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(matrices[parent], new(0, 0, length));
                    temporary96 = temporary96.Write(0x40, BitConverter.GetBytes(x)).Write(0x44, BitConverter.GetBytes(y)).Write(0x48, BitConverter.GetBytes(z));
                }
                else
                {
                    temporary96 = temporary96.Write(0x14, new byte[8]).Write(0x1C, BitConverter.GetBytes((int)length));
                }
            }
            for (int lane = 0; lane < 9; lane++)
            {
                temporary96 = temporary96.Write(lane * 2, BitConverter.GetBytes(local.Rotation[lane]));
            }
            temporary96 = temporary96.Write(18, new byte[2]);
        }
        return new(model with { CurrentBodyBoneMatrices = publication }, temporary96);
    }

    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
}
