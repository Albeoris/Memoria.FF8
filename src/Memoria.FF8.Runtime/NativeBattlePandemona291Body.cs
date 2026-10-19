using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattlePandemona291Body
{
    public static NativeBattlePandemona291ChildStep Advance6FBDB0(NativeBattlePandemona291Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Pandemona pose advancement requires its actual skeleton.");
        int start = NativeBattleWaterExecutionFields.ArenaOffset(address, 0x430);
        byte flags = ReadByte(0x1C1);
        if ((flags & 3) != 0)
        {
            throw new NotSupportedException("Native Pandemona pose advancement requires its selected speed cursor contract.");
        }
        uint skeletonAddress = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(start + 0x41C, 4));
        int skeleton = NativeBattleWaterExecutionFields.ArenaOffset(skeletonAddress, 1);
        int count = arena.Read(skeleton, 1)[0];
        uint animationAddress = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(start + 0x424, 4));
        uint animationEnd = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(start + 0x428, 4));
        ImmutableArray<byte> animations = arena.Read(NativeBattleWaterExecutionFields.ArenaOffset(animationAddress, checked((int)(animationEnd - animationAddress))), checked((int)(animationEnd - animationAddress))).ToImmutableArray();
        byte selector = ReadByte(0x1C0);
        NativeBattlePoseFirstFrame next;
        if (ReadByte(0x1C6) >= ReadByte(0x1C7))
        {
            next = NativeBattlePoseDecoder.DecodeFirst509440(animations.AsSpan(), count, selector);
            Byte(0x1C1, (byte)((flags & 0xF3) | 4));
            Byte(0x1C6, 1);
            Byte(0x1C7, NativeBattlePoseDecoder.ReadFrameCounts(animations.AsSpan())[selector]);
        }
        else
        {
            NativeBattleDecodedPose pose = NativeBattleArenaPose.ReadCurrentPose(arena, skeleton, count);
            next = NativeBattlePoseDecoder.AdvanceRegular508F90(animations.AsSpan(), count, selector, pose, ReadWord(0x1C2), ReadWord(0x1C4));
            Byte(0x1C6, unchecked((byte)(ReadByte(0x1C6) + 1)));
            Byte(0x1C1, (byte)((flags & 0xF3) | ((((flags >> 2) + 1) & 1) << 2)));
        }
        Word(0x1C2, next.ByteOffset);
        Word(0x1C4, next.BitOffset);
        arena = NativeBattleArenaPose.WritePose(arena, skeleton, next.Pose);
        arena = NativeBattleArenaPose.RestoreBoneMatrices(arena, skeleton, next.Pose);
        return new(frame with { Memory = frame.Memory with { Arena = arena } }, task, 0);

        byte ReadByte(int offset) => arena.Read(start + offset, 1)[0];
        ushort ReadWord(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(start + offset, 2));
        void Byte(int offset, byte value)
        {
            task = task.WriteByte(offset, value);
            arena = arena.Write(start + offset, [value]);
        }
        void Word(int offset, ushort value)
        {
            task = task.WriteWord(offset, value);
            arena = arena.Write(start + offset, BitConverter.GetBytes(value));
        }
    }

    public static NativeBattlePandemona291ChildStep Initialize99(NativeBattlePandemona291Frame frame, uint address, NativeBattleModulePoolTask task, Action clearCasterAlpha)
    {
        NativeBattlePandemona291State state = frame.Memory.Pandemona291 ?? throw new NotSupportedException("Native Pandemona model requires its current module owner.");
        uint buffer = state.ModelBuffer25562F4 ?? throw new NotSupportedException("Native Pandemona model requires its actual resource buffer.");
        int center = state.Center25562E8 ?? throw new NotSupportedException("Native Pandemona model requires its current center.");
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Pandemona model requires its current arena.");
        int start = NativeBattleWaterExecutionFields.ArenaOffset(address, 0x430);
        Word(0x1A2, 0);
        Word(0x1A4, 6144);
        Word(0x19C, 6144);
        Word(0x194, 6144);
        Word(0x1A0, 0);
        Word(0x19E, 0);
        Word(0x19A, 0);
        Word(0x198, 0);
        Word(0x196, 0);
        Dword(0x1A8, 0);
        Word(0x170, 0);
        Dword(0x1AC, unchecked((uint)-8192));
        Word(0x172, unchecked((ushort)-8192));
        Dword(0x1B0, unchecked((uint)center));
        Word(0x174, unchecked((ushort)center));
        Word(0x154, 2);
        Dword(0x1D0, uint.MaxValue);
        Dword(0x1E0, 0);
        Dword(0x1CC, 0);
        Byte(0x1B5, 0);
        Byte(0x1B4, 0);
        Dword(0x1BC, unchecked(address + 0x41C));
        Dword(0x1B8, unchecked(address + 0x41C));
        Byte(0x1C1, 0);
        Dword(0x17C, 0x00808080);
        Dword(0x180, 0x32808080);
        clearCasterAlpha();
        uint resource = unchecked(buffer + 0x31220);
        for (int index = 0; index < 4; index++)
        {
            uint relative = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(NativeBattleWaterExecutionFields.ArenaOffset(unchecked(resource + 4u + (uint)index * 4), 4), 4));
            Dword(0x41C + index * 4, unchecked(resource + relative));
        }
        int skeleton = NativeBattleWaterExecutionFields.ArenaOffset(task.ReadDword(0x41C), 1);
        int count = arena.Read(skeleton, 1)[0];
        int animationStart = NativeBattleWaterExecutionFields.ArenaOffset(task.ReadDword(0x424), 1);
        int length = checked((int)(task.ReadDword(0x428) - task.ReadDword(0x424)));
        ImmutableArray<byte> animations = arena.Read(animationStart, length).ToImmutableArray();
        NativeBattlePoseFirstFrame first = NativeBattlePoseDecoder.DecodeFirst509440(animations.AsSpan(), count, 0);
        Byte(0x1C0, 0);
        Byte(0x1C1, 4);
        Word(0x1C2, first.ByteOffset);
        Word(0x1C4, first.BitOffset);
        Byte(0x1C6, 1);
        Byte(0x1C7, NativeBattlePoseDecoder.ReadFrameCounts(animations.AsSpan())[0]);
        arena = NativeBattleArenaPose.WritePose(arena, skeleton, first.Pose);
        arena = NativeBattleArenaPose.RestoreBoneMatrices(arena, skeleton, first.Pose);
        return new(frame with { Memory = frame.Memory with { Arena = arena } }, task, 0);

        void Word(int offset, ushort value)
        {
            task = task.WriteWord(offset, value);
            arena = arena.Write(start + offset, BitConverter.GetBytes(value));
        }

        void Dword(int offset, uint value)
        {
            task = task.WriteDword(offset, value);
            arena = arena.Write(start + offset, BitConverter.GetBytes(value));
        }

        void Byte(int offset, byte value)
        {
            task = task.WriteByte(offset, value);
            arena = arena.Write(start + offset, new byte[] { value });
        }
    }
}
