using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBodyBonePreparation(NativeBattleGraphicsModel Model, NativeBattleEffectArena Temporary58);

public static class NativeBattleBodyBoneTemporary
{
    public static NativeBattleBodyBonePreparation Prepare508C90(NativeBattleGraphicsModel actor, NativeBattlePoseSource source, NativeBattleEffectArena temporary)
    {
        NativeBattleDecodedPose decoded = NativeBattleBodyBoneMatrices.ReadCurrentPose508C90(actor, source);
        actor = NativeBattleBodyBoneMatrices.Publish508C90(actor, source);
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(actor, source);
        return new(actor, Prepare508C90(source, decoded, matrices, temporary));
    }

    public static NativeBattleEffectArena Prepare508C90(NativeBattlePoseSource source, NativeBattleDecodedPose decoded, ImmutableArray<NativeBattleActionMatrix> matrices, NativeBattleEffectArena temporary)
    {
        short scale = BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan().Slice(2));
        for (int bone = 0; bone < source.BoneCount; bone++)
        {
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(decoded.Bones[bone].Rotation);
            int parent = BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan().Slice(16 + bone * 48));
            if (parent < 0)
            {
                temporary = temporary.Write(0x40, DwordBytes(unchecked((uint)scale))).Write(0x44, DwordBytes(unchecked((uint)scale))).Write(0x48, DwordBytes(unchecked((uint)scale)));
                if (!decoded.HasScale)
                {
                    local = NativeBattlePoseMatrices.Scale56BEF0(local, scale, scale, scale);
                }
            }
            else if (decoded.HasScale)
            {
                short length = BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan().Slice(18 + parent * 48));
                temporary = temporary.Write(0x50, new byte[4]).Write(0x54, WordBytes(unchecked((ushort)length)));
                NativeBattleActionMatrix basis = matrices[parent] with { X = 0, Y = 0, Z = 0 };
                NativeBattleFullMacVertex transformed = NativeBattleGfMatrices.TransformMac460840(basis, new(0, 0, length));
                temporary = temporary.Write(0x40, DwordBytes(unchecked((uint)transformed.X))).Write(0x44, DwordBytes(unchecked((uint)transformed.Y))).Write(0x48, DwordBytes(unchecked((uint)transformed.Z)));
            }
            else
            {
                short length = BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan().Slice(18 + parent * 48));
                temporary = temporary.Write(0x14, new byte[8]).Write(0x1C, DwordBytes(unchecked((uint)length)));
            }
            for (int lane = 0; lane < 9; lane++)
            {
                temporary = temporary.Write(lane * 2, WordBytes(unchecked((ushort)local.Rotation[lane])));
            }
            temporary = temporary.Write(18, new byte[2]);
        }
        return temporary;
    }

    private static byte[] DwordBytes(uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] WordBytes(ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return bytes;
    }
}
