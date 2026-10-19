using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDiablos325BodyStep(NativeBattleEffectMemory Memory, NativeBattleModulePoolTask Task);

public static class NativeBattleDiablos325Body
{
    public static NativeBattleDiablos325BodyStep Initialize655190(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, uint taskAddress)
    {
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(memory);
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Diablos body requires its current model arena.");
        task = task.WriteWord(0x15A, 0).WriteWord(0x15C, 4096).WriteWord(0x154, 4096).WriteWord(0x14C, 4096).WriteWord(0x158, 0).WriteWord(0x156, 0).WriteWord(0x152, 0).WriteWord(0x150, 0).WriteWord(0x14E, 0).WriteDword(0x160, 0).WriteDword(0x164, unchecked((uint)-8192)).WriteDword(0x168, unchecked((uint)state.FarZ51DC));
        uint resource = checked(state.ModelBuffer5208 + 0x2185C);
        task = task.WriteWord(0x10C, 2).WriteDword(0x188, uint.MaxValue).WriteDword(0x198, 0).WriteDword(0x184, 0).WriteByte(0x16D, 0).WriteByte(0x16C, 0).WriteDword(0x174, checked(taskAddress + 0x244)).WriteDword(0x170, checked(taskAddress + 0x244)).WriteByte(0x179, 0).WriteDword(0x134, 0x808080);
        for (int index = 0; index < 4; index++)
        {
            uint relative = BinaryPrimitives.ReadUInt32LittleEndian(Read(arena, checked(resource + 4u + (uint)index * 4), 4));
            task = task.WriteDword(0x244 + index * 4, unchecked(resource + relative));
        }
        return Select6574D0(memory, task, 0);
    }

    public static NativeBattleDiablos325BodyStep Select6574D0(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, byte selector)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Diablos pose selection requires its current skeleton.");
        byte flags = task.ReadByte(0x179);
        RequireSpeed(flags);
        uint skeleton = task.ReadDword(0x244);
        int count = Read(arena, skeleton, 1)[0];
        ImmutableArray<byte> animations = Animations(arena, task);
        NativeBattlePoseFirstFrame first = NativeBattlePoseDecoder.DecodeFirst509440(animations.AsSpan(), count, selector);
        task = task.WriteByte(0x178, selector).WriteByte(0x179, (byte)((flags & 0xF3) | 4)).WriteWord(0x17A, first.ByteOffset).WriteWord(0x17C, first.BitOffset).WriteByte(0x17E, 1).WriteByte(0x17F, NativeBattlePoseDecoder.ReadFrameCounts(animations.AsSpan())[selector]);
        return PublishPose(memory, task, first.Pose);
    }

    public static NativeBattleDiablos325BodyStep Advance6FBDB0(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Diablos pose advancement requires its current skeleton.");
        byte flags = task.ReadByte(0x179);
        RequireSpeed(flags);
        if (task.ReadByte(0x17E) >= task.ReadByte(0x17F))
        {
            return Select6574D0(memory, task, task.ReadByte(0x178));
        }
        int skeleton = NativeBattleWaterExecutionFields.ArenaOffset(task.ReadDword(0x244), 1);
        int count = arena.Read(skeleton, 1)[0];
        NativeBattleDecodedPose pose = NativeBattleArenaPose.ReadCurrentPose(arena, skeleton, count);
        NativeBattlePoseFirstFrame next = NativeBattlePoseDecoder.AdvanceRegular508F90(Animations(arena, task).AsSpan(), count, task.ReadByte(0x178), pose, task.ReadWord(0x17A), task.ReadWord(0x17C));
        task = task.WriteWord(0x17A, next.ByteOffset).WriteWord(0x17C, next.BitOffset).WriteByte(0x17E, unchecked((byte)(task.ReadByte(0x17E) + 1))).WriteByte(0x179, (byte)((flags & 0xF3) | ((((flags >> 2) + 1) & 1) << 2)));
        return PublishPose(memory, task, next.Pose);
    }

    private static NativeBattleDiablos325BodyStep PublishPose(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, NativeBattleDecodedPose pose)
    {
        int skeleton = NativeBattleWaterExecutionFields.ArenaOffset(task.ReadDword(0x244), 1);
        NativeBattleEffectArena arena = NativeBattleArenaPose.WritePose(memory.Arena!, skeleton, pose);
        arena = NativeBattleArenaPose.RestoreBoneMatrices(arena, skeleton, pose);
        return new(memory with { Arena = arena }, task);
    }

    private static ImmutableArray<byte> Animations(NativeBattleEffectArena arena, NativeBattleModulePoolTask task)
    {
        uint start = task.ReadDword(0x24C);
        int length = checked((int)(task.ReadDword(0x250) - start));
        return Read(arena, start, length).ToImmutableArray();
    }

    private static byte[] Read(NativeBattleEffectArena arena, uint address, int length) => arena.Read(NativeBattleWaterExecutionFields.ArenaOffset(address, length), length);

    private static void RequireSpeed(byte flags)
    {
        if ((flags & 3) != 0)
        {
            throw new NotSupportedException("Native Diablos body pose speed requires its selected cursor contract.");
        }
    }
}
