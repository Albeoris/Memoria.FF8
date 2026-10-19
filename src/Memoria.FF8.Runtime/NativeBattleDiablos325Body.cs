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

    public static NativeBattleDiablos325BodyStep InitializeSecondary6552FC(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, uint taskAddress)
    {
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(memory);
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Diablos secondary body requires its current model arena.");
        for (int offset = 0; offset < 32; offset += 4)
        {
            task = task.WriteDword(0x1E8 + offset, task.ReadDword(0x14C + offset));
        }
        uint resource = checked(state.ModelBuffer5208 + 0x31B5C);
        task = task.WriteWord(0x1A8, 2).WriteDword(0x224, uint.MaxValue).WriteDword(0x234, 0).WriteDword(0x220, 0).WriteByte(0x209, 0).WriteByte(0x208, 0).WriteDword(0x210, checked(taskAddress + 0x254)).WriteDword(0x20C, checked(taskAddress + 0x254)).WriteByte(0x215, 0).WriteDword(0x1D0, 0x808080);
        for (int index = 0; index < 4; index++)
        {
            uint relative = BinaryPrimitives.ReadUInt32LittleEndian(Read(arena, checked(resource + 4u + (uint)index * 4), 4));
            task = task.WriteDword(0x254 + index * 4, unchecked(resource + relative));
        }
        return Select6574D0(memory, task, 0, 0x1A8, 0x254);
    }

    public static NativeBattleDiablos325BodyStep Select6574D0(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, byte selector, int modelBase = 0x10C, int directoryBase = 0x244)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Diablos pose selection requires its current skeleton.");
        byte flags = task.ReadByte(modelBase + 0x6D);
        RequireSpeed(flags);
        uint skeleton = task.ReadDword(directoryBase);
        int count = Read(arena, skeleton, 1)[0];
        ImmutableArray<byte> animations = Animations(arena, task, modelBase, directoryBase);
        NativeBattlePoseFirstFrame first = NativeBattlePoseDecoder.DecodeFirst509440(animations.AsSpan(), count, selector);
        task = task.WriteByte(modelBase + 0x6C, selector).WriteByte(modelBase + 0x6D, (byte)((flags & 0xF3) | 4)).WriteWord(modelBase + 0x6E, first.ByteOffset).WriteWord(modelBase + 0x70, first.BitOffset).WriteByte(modelBase + 0x72, 1).WriteByte(modelBase + 0x73, NativeBattlePoseDecoder.ReadFrameCounts(animations.AsSpan())[selector]);
        return PublishPose(memory, task, first.Pose, modelBase, directoryBase);
    }

    public static NativeBattleDiablos325BodyStep Advance6FBDB0(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, int modelBase = 0x10C, int directoryBase = 0x244)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Diablos pose advancement requires its current skeleton.");
        byte flags = task.ReadByte(modelBase + 0x6D);
        RequireSpeed(flags);
        if (task.ReadByte(modelBase + 0x72) >= task.ReadByte(modelBase + 0x73))
        {
            return Select6574D0(memory, task, task.ReadByte(modelBase + 0x6C), modelBase, directoryBase);
        }
        int skeleton = NativeBattleWaterExecutionFields.ArenaOffset(task.ReadDword(directoryBase), 1);
        int count = arena.Read(skeleton, 1)[0];
        NativeBattleDecodedPose pose = NativeBattleArenaPose.ReadCurrentPose(arena, skeleton, count);
        NativeBattlePoseFirstFrame next = NativeBattlePoseDecoder.AdvanceRegular508F90(Animations(arena, task, modelBase, directoryBase).AsSpan(), count, task.ReadByte(modelBase + 0x6C), pose, task.ReadWord(modelBase + 0x6E), task.ReadWord(modelBase + 0x70));
        task = task.WriteWord(modelBase + 0x6E, next.ByteOffset).WriteWord(modelBase + 0x70, next.BitOffset).WriteByte(modelBase + 0x72, unchecked((byte)(task.ReadByte(modelBase + 0x72) + 1))).WriteByte(modelBase + 0x6D, (byte)((flags & 0xF3) | ((((flags >> 2) + 1) & 1) << 2)));
        return PublishPose(memory, task, next.Pose, modelBase, directoryBase);
    }

    private static NativeBattleDiablos325BodyStep PublishPose(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, NativeBattleDecodedPose pose, int modelBase = 0x10C, int directoryBase = 0x244)
    {
        int skeleton = NativeBattleWaterExecutionFields.ArenaOffset(task.ReadDword(directoryBase), 1);
        NativeBattleEffectArena arena = NativeBattleArenaPose.WritePose(memory.Arena!, skeleton, pose);
        arena = NativeBattleArenaPose.RestoreBoneMatrices(arena, skeleton, pose);
        return new(memory with { Arena = arena }, task);
    }

    private static ImmutableArray<byte> Animations(NativeBattleEffectArena arena, NativeBattleModulePoolTask task, int modelBase = 0x10C, int directoryBase = 0x244)
    {
        uint start = task.ReadDword(directoryBase + 8);
        int length = checked((int)(task.ReadDword(directoryBase + 12) - start));
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
