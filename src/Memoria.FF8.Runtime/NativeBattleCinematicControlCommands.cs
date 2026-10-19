using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicControlCommands
{
    public static NativeBattleWaterExecutionFrame StoreTexturePair92(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program)
    {
        ushort page = BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        frame = WriteSlotWord(frame, 0x92, page);
        ushort clut = BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 4, 2));
        frame = WriteSlotWord(frame, 0x9A, clut);
        return NativeBattleScriptedCommandReads.Advance(frame, 6);
    }

    public static NativeBattleWaterExecutionFrame DebugParametersB7(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program)
    {
        if (frame.ReadTemporaryWord(0x14A) >> 12 != 1)
        {
            return frame;
        }
        _ = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 4, 2));
        _ = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        // Native46BD40 emits a diagnostic and returns zero without changing the effect owners.
        return NativeBattleScriptedCommandReads.Advance(frame, 6);
    }

    public static NativeBattleWaterExecutionFrame WaitWhileCachedAtLeastFB(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program)
    {
        short offset = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        short threshold = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 4, 2));
        uint address = unchecked(CoreDword(frame, 0x27973E8) + (uint)(0x8C + offset));
        short value = BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        return value >= threshold ? frame.WriteTemporaryWord(0x13E, SlotWord(frame, 0xC8)) : NativeBattleScriptedCommandReads.Advance(frame, 6);
    }

    public static NativeBattleWaterExecutionFrame StoreRenderMaskB2(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program)
    {
        byte value = NativeBattleScriptedCommandReads.Program(frame, program, 2, 1)[0];
        uint manager = CoreDword(frame, 0x27973EC);
        frame = frame with { Core = frame.Core.Write(unchecked(manager + 0xA2), [value]) };
        return NativeBattleScriptedCommandReads.Advance(frame, 4);
    }

    public static NativeBattleWaterExecutionFrame ClearFlags9B(NativeBattleWaterExecutionFrame frame)
    {
        byte mask = (byte)((frame.ReadTemporaryWord(0x14A) >> 9) & 0x7F);
        byte current = ReadSlot(frame, 0xDE, 1)[0];
        frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, 0xDE, 1), [(byte)(current & ~mask)]) };
        return NativeBattleScriptedCommandReads.Advance(frame, 2);
    }

    public static NativeBattleWaterExecutionFrame BakeCachedPositionF2(NativeBattleWaterExecutionFrame frame)
    {
        for (int lane = 0; lane < 3; lane++)
        {
            int value = unchecked((short)SlotWord(frame, 0x94 + lane * 2));
            frame = WriteSlotDword(frame, 0x5C + lane * 4, unchecked((uint)(value << 16)));
        }
        frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, 0x18, 1), [0]) };
        return NativeBattleScriptedCommandReads.Advance(frame, 2);
    }

    public static NativeBattleWaterExecutionFrame RegisterUpdateBlock1A(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program)
    {
        ushort operand = BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, 0x18, 1), [(byte)(operand >> 8)]) };
        frame = WriteSlotDword(frame, 0xA0, unchecked(CoreDword(frame, 0x2797450) + 4));
        return NativeBattleScriptedCommandReads.Advance(frame, unchecked((uint)(4 + (operand & 15) * 2)));
    }

    public static NativeBattleWaterExecutionFrame StoreRelative1C(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program)
    {
        short displacement = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        uint target = unchecked(CoreDword(frame, 0x27973E8) + (uint)displacement);
        int length = frame.ReadTemporaryWord(0x14A) >> 9 == 1 ? 2 : 1;
        byte[] value = NativeBattleScriptedCommandReads.Program(frame, program, 4, length);
        frame = frame with { Arena = frame.Arena.Write(ArenaOffset(target, length), value) };
        return NativeBattleScriptedCommandReads.Advance(frame, 6);
    }

    public static NativeBattleWaterExecutionFrame WaitRandomB0(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program)
    {
        short operand = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        NativeCrtRandomState random = frame.Random.Draw(out ushort draw);
        int sampled = unchecked((int)((uint)(draw * operand) >> 15));
        int delay = unchecked((short)SlotWord(frame, 0xC8) * sampled);
        frame = NativeBattleScriptedCommandReads.Advance(frame with { Random = random }, 4);
        return delay == 0 ? frame : frame.WriteTemporaryWord(0x13E, unchecked((ushort)delay));
    }

    public static NativeBattleWaterExecutionFrame ScaleReferencedPositionColorD3(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, short, uint> resolveSlot)
    {
        short selector = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        uint source = resolveSlot(frame, selector);
        short factor = BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(unchecked(source + 0x8C), 2), 2));
        uint color = 0;
        for (int lane = 0; lane < 3; lane++)
        {
            short component = BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(unchecked(source + 0x94 + (uint)(2 * lane)), 2), 2));
            color |= unchecked((uint)Math.Clamp(component * factor / 256, 0, 255)) << (lane * 8);
        }
        color |= (uint)ReadSlot(frame, 0xCF, 1)[0] << 24;
        frame = WriteSlotDword(frame, 0xCC, color);
        return NativeBattleScriptedCommandReads.Advance(frame, 4);
    }

    public static NativeBattleWaterExecutionFrame SetRelativeActorIdCE(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program)
    {
        byte actor = ReadSlot(frame, 0x1B, 1)[0];
        ushort displacement = BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        frame = WriteSlotWord(frame, 0x12, unchecked((ushort)(actor + displacement)));
        return NativeBattleScriptedCommandReads.Advance(frame, 4);
    }

    public static NativeBattleWaterExecutionFrame SetResourceCursor120(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program)
    {
        int page = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        uint manager = CoreDword(frame, 0x27973EC);
        uint resource = CoreDword(frame, unchecked(manager + 0x70));
        frame = WriteCoreDword(frame, unchecked(manager + 0x74), unchecked(resource + (uint)(page << 16)));
        return NativeBattleScriptedCommandReads.Advance(frame, 4);
    }

    public static NativeBattleWaterExecutionFrame Wait10D(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, short, uint> resolveSlot)
    {
        short selector = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        uint selected = resolveSlot(frame, selector);
        ushort selectedFlags = BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(unchecked(selected + 0x4A), 2), 2));
        ushort mask = BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 4, 2));
        bool matched = (selectedFlags & mask & 0x3FFF) != 0;
        bool inverted = (frame.ReadTemporaryWord(0x14A) & 0x1000) != 0;
        return matched != inverted ? NativeBattleScriptedCommandReads.Advance(frame, 6) : frame.WriteTemporaryWord(0x13E, SlotWord(frame, 0xC8));
    }

    public static NativeBattleWaterExecutionFrame Execute51(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program)
    {
        byte flags = unchecked((byte)(frame.ReadTemporaryWord(0x14A) >> 9));
        byte current = ReadSlot(frame, 0xDE, 1)[0];
        frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, 0xDE, 1), new byte[] { (byte)(current | flags) }) };
        ushort operand = BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        frame = WriteSlotWord(frame, 0xC0, operand);
        return NativeBattleScriptedCommandReads.Advance(frame, 4);
    }

    public static NativeBattleWaterExecutionFrame BranchD1(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, byte stageResourceIndex)
    {
        ushort expected = BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        if (stageResourceIndex != expected)
        {
            return NativeBattleScriptedCommandReads.Advance(frame, 6);
        }
        short displacement = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 4, 2));
        return WriteCoreDword(frame, 0x2797450, unchecked(CoreDword(frame, 0x2797450) + (uint)displacement));
    }
}
