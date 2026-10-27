using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Current addressed skeleton pose and local matrix publication shared by embedded models.</summary>
public static class NativeBattleArenaPose
{
    public static NativeBattleDecodedPose ReadCurrentPose(NativeBattleEffectArena arena, int skeleton, int count)
    {
        bool hasScale = (arena.Read(skeleton + 1, 1)[0] & 1) != 0;
        ImmutableArray<NativeBattleBonePose>.Builder bones = ImmutableArray.CreateBuilder<NativeBattleBonePose>(count);
        for (int index = 0; index < count; index++)
        {
            int offset = skeleton + 16 + index * 48;
            bones.Add(new(ReadVector(arena, offset + 4), hasScale ? ReadVector(arena, offset + 10) : new(1024, 1024, 1024)));
        }
        return new(ReadVector(arena, skeleton + 8), bones.MoveToImmutable(), hasScale);
    }

    public static NativeBattleEffectArena WritePose(NativeBattleEffectArena arena, int skeleton, NativeBattleDecodedPose pose)
    {
        NativeBattleEffectArenaWriter writer = new(arena);
        writer.Write(skeleton + 1, [(byte)((arena.Read(skeleton + 1, 1)[0] & ~1) | (pose.HasScale ? 1 : 0))]);
        WriteVector(writer, skeleton + 8, pose.Translation);
        for (int index = 0; index < pose.Bones.Length; index++)
        {
            int offset = skeleton + 16 + index * 48;
            WriteVector(writer, offset + 4, pose.Bones[index].Rotation);
            if (pose.HasScale)
            {
                WriteVector(writer, offset + 10, pose.Bones[index].Scale);
            }
        }
        return writer.Freeze();
    }

    public static NativeBattleEffectArena RestoreBoneMatrices(NativeBattleEffectArena arena, int skeleton, NativeBattleDecodedPose pose)
    {
        short scale = BinaryPrimitives.ReadInt16LittleEndian(arena.Read(skeleton + 2, 2));
        ImmutableArray<NativeBattleRigBone>.Builder definition = ImmutableArray.CreateBuilder<NativeBattleRigBone>(pose.Bones.Length);
        for (int index = 0; index < pose.Bones.Length; index++)
        {
            byte[] raw = arena.Read(skeleton + 16 + index * 48, 4);
            definition.Add(new(BinaryPrimitives.ReadInt16LittleEndian(raw), BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(2))));
        }
        ImmutableArray<NativeBattleRigBone> bones = definition.MoveToImmutable();
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattlePoseMatrices.RigBoneMatrices(scale, bones, pose);
        NativeBattleEffectArenaWriter writer = new(arena);
        for (int index = 0; index < matrices.Length; index++)
        {
            int paddingOffset = skeleton + 50 + index * 48;
            (byte[] Data, bool[] Known) retainedPadding = pose.HasScale ? arena.ReadSnapshot(paddingOffset, 2) : default;
            WriteMatrix(writer, skeleton + 32 + index * 48, matrices[index], bones[index].Parent >= 0 || pose.HasScale);
            if (pose.HasScale)
            {
                //508C30 copies nine rotation WORDs; scaled508C90 never writes the physical padding lane.
                (byte[] data, bool[] known) = retainedPadding;
                writer.Write(paddingOffset, data);
                for (int lane = 0; lane < 2; lane++)
                {
                    if (!known[lane])
                    {
                        writer.MarkUnknown(paddingOffset + lane, 1);
                    }
                }
            }
            else
            {
                short padding = bones[index].Parent < 0 ? NativeBattleBoneMatrixPadding.Euler56CE30(pose.Bones[index].Rotation) : NativeBattleBoneMatrixPadding.UnscaledChild508C90(pose.Bones[index].Rotation);
                writer.Write(paddingOffset, BitConverter.GetBytes(padding));
            }
        }
        return writer.Freeze();
    }

    public static NativeBattleActionMatrix ReadMatrix(NativeBattleEffectArena arena, int offset)
    {
        byte[] rotation = arena.Read(offset, 18);
        byte[] translation = arena.Read(offset + 20, 12);
        return new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(index * 2))).ToImmutableArray(), BinaryPrimitives.ReadInt32LittleEndian(translation), BinaryPrimitives.ReadInt32LittleEndian(translation.AsSpan(4)), BinaryPrimitives.ReadInt32LittleEndian(translation.AsSpan(8)), 4096);
    }

    public static NativeBattleEffectArena WriteMatrix(NativeBattleEffectArena arena, int offset, NativeBattleActionMatrix matrix, bool unknownPadding)
    {
        NativeBattleEffectArenaWriter writer = new(arena);
        WriteMatrix(writer, offset, matrix, unknownPadding);
        return writer.Freeze();
    }

    internal static void WriteMatrix(NativeBattleEffectArenaWriter writer, int offset, NativeBattleActionMatrix matrix, bool unknownPadding)
    {
        byte[] raw = new byte[32];
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(index * 2), matrix.Rotation[index]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(20), matrix.X);
        BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(24), matrix.Y);
        BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(28), matrix.Z);
        writer.Write(offset, raw);
        if (unknownPadding)
        {
            writer.MarkUnknown(offset + 18, 2);
        }
    }

    public static NativeBattleVector ReadVector(NativeBattleEffectArena arena, int offset)
    {
        byte[] raw = arena.Read(offset, 6);
        return new(BinaryPrimitives.ReadInt16LittleEndian(raw), BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(4)));
    }

    public static NativeBattleEffectArena WriteVector(NativeBattleEffectArena arena, int offset, NativeBattleVector vector)
    {
        NativeBattleEffectArenaWriter writer = new(arena);
        WriteVector(writer, offset, vector);
        return writer.Freeze();
    }

    internal static void WriteVector(NativeBattleEffectArenaWriter writer, int offset, NativeBattleVector vector)
    {
        byte[] raw = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(raw, vector.X);
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(2), vector.Y);
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(4), vector.Z);
        writer.Write(offset, raw);
    }
}
