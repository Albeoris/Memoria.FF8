using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattlePandemona291Body
{
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
