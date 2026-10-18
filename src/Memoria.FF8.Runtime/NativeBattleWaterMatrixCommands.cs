using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Selected matrix-row allocation and mode-zero rotation commands over current owners.</summary>
public static class NativeBattleWaterMatrixCommands
{
    public static uint ResolveB65520(NativeBattleWaterExecutionFrame frame, NativeBattleWaterSlotBank bank, ushort selector) => ResolveB65520(frame, bank, CurrentIndex(frame, bank), selector);

    public static uint ResolveB65520(NativeBattleWaterExecutionFrame frame, NativeBattleWaterSlotBank bank, int currentIndex, ushort selector)
    {
        int reference = NativeBattleWaterSlots.LookupB65370(frame.Arena, bank, currentIndex, ActiveOrder(frame), selector);
        ushort id = BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(bank.SlotOffset(frame.Arena, reference) + 0x12, 2));
        for (int row = 0; row < 64; row++)
        {
            if (CoreWord(frame, 0x2797204 + (uint)(row * 2)) == id)
            {
                return 0x27979E8 + (uint)(row * 32);
            }
        }
        return 0x27979E8;
    }

    public static (NativeBattleWaterExecutionFrame Frame, uint MatrixAddress) AllocateB65480(NativeBattleWaterExecutionFrame frame)
    {
        ushort id = SlotWord(frame, 0x12);
        for (int row = 1; row < 64; row++)
        {
            if (CoreWord(frame, 0x2797204 + (uint)(row * 2)) == id)
            {
                return (frame, 0x27979E8 + (uint)(row * 32));
            }
        }
        int selected = 63;
        for (int row = 1; row < 64; row++)
        {
            if (CoreWord(frame, 0x2797204 + (uint)(row * 2)) == 0)
            {
                selected = row;
                break;
            }
        }
        frame = WriteCoreWord(frame, 0x2797204 + (uint)(selected * 2), id);
        return (frame, 0x27979E8 + (uint)(selected * 32));
    }

    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleWaterSlotBank bank)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        uint pc = CoreDword(frame, 0x2797450);
        int advance;
        switch (instruction & 0x1FF)
        {
            case 0x104:
                uint savedDestination = unchecked(0x2798B68u + (uint)(0x20 * (short)Operand(2)));
                NativeBattleActionMatrix savedEuler = NativeBattleGfMatrices.Euler56CD50(Angles(frame));
                frame = WriteEuler(frame, savedEuler, 0x20);
                NativeBattleActionMatrix moduleView = ReadModuleView(frame);
                NativeBattleActionMatrix savedRotation = NativeBattleGfMatrices.Multiply56C090(moduleView, savedEuler);
                frame = WriteRotation(frame, savedDestination, savedRotation);
                NativeBattleFullMacVertex savedTranslation = NativeBattleGfMatrices.TransformMac460840(moduleView, Position(frame));
                frame = WriteCoreDword(frame, unchecked(savedDestination + 20), unchecked((uint)savedTranslation.X));
                frame = WriteCoreDword(frame, unchecked(savedDestination + 24), unchecked((uint)savedTranslation.Y));
                frame = WriteCoreDword(frame, unchecked(savedDestination + 28), unchecked((uint)savedTranslation.Z));
                frame = frame.WriteTemporaryDword(0xF0, savedTranslation.Flags);
                frame = WriteCoreWord(frame, unchecked(savedDestination + 18), 0);
                advance = 4;
                break;
            case 0x105:
                if (instruction >> 12 != 0)
                {
                    throw new NotSupportedException("Native selected saved-matrix load requires rotation mode zero.");
                }
                uint loadedDestination;
                (frame, loadedDestination) = AllocateB65480(frame);
                uint savedSource = unchecked(0x2798B68u + (uint)(0x20 * (short)Operand(2)));
                NativeBattleFullMacVertex loadedTranslation = NativeBattleGfMatrices.TransformMac460840(ReadMatrix(frame, savedSource), Position(frame));
                frame = WriteCoreDword(frame, unchecked(loadedDestination + 20), unchecked((uint)loadedTranslation.X));
                frame = WriteCoreDword(frame, unchecked(loadedDestination + 24), unchecked((uint)loadedTranslation.Y));
                frame = WriteCoreDword(frame, unchecked(loadedDestination + 28), unchecked((uint)loadedTranslation.Z));
                frame = frame.WriteTemporaryDword(0xF0, loadedTranslation.Flags);
                NativeBattleActionMatrix loadedEuler = NativeBattleGfMatrices.Euler56CD50(Angles(frame));
                frame = WriteEuler(frame, loadedEuler);
                NativeBattleActionMatrix loadedRotation = NativeBattleGfMatrices.Multiply56C090(ReadRotation(frame, savedSource), loadedEuler);
                frame = WriteRotation(frame, loadedDestination, loadedRotation);
                frame = WriteCoreWord(frame, unchecked(loadedDestination + 18), 0);
                advance = 6;
                break;
            case 0xD4:
                if (instruction != 0x80D4)
                {
                    throw new NotSupportedException("Native selected uniform color requires its single-operand variant.");
                }
                int channel = unchecked((short)Operand(2));
                uint color = unchecked((uint)(channel | (channel << 8) | (channel << 16)));
                frame = WriteCoreDword(frame, 0x2797450, unchecked(pc + 4));
                uint previousColor = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xCC, 4));
                return WriteSlotDword(frame, 0xCC, (previousColor & 0xFF000000u) | color);
            case 0x67:
                if (instruction >> 12 != 0)
                {
                    throw new NotSupportedException("Native selected matrix allocation requires rotation mode zero.");
                }
                uint destination;
                (frame, destination) = AllocateB65480(frame);
                NativeBattleActionMatrix view = ReadMatrix(frame, 0x27979E8);
                NativeBattleFullMacVertex translated = NativeBattleGfMatrices.TransformMac460840(view, Position(frame));
                frame = WriteCoreDword(frame, destination + 20, unchecked((uint)translated.X));
                frame = WriteCoreDword(frame, destination + 24, unchecked((uint)translated.Y));
                frame = WriteCoreDword(frame, destination + 28, unchecked((uint)translated.Z));
                frame = frame.WriteTemporaryDword(0xF0, translated.Flags);
                NativeBattleActionMatrix euler = NativeBattleGfMatrices.Euler56CD50(Angles(frame));
                frame = WriteEuler(frame, euler);
                NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Multiply56C090(view, euler);
                frame = WriteRotation(frame, destination, rotation);
                frame = WriteCoreWord(frame, destination + 18, 0);
                advance = 4;
                break;
            case 0x63:
                uint original = CoreDword(frame, 0x27973E8);
                ImmutableArray<byte> order = ActiveOrder(frame);
                int reference = NativeBattleWaterSlots.LookupB65370(frame.Arena, bank, CurrentIndex(frame, bank), order, unchecked((short)Operand(2)));
                frame = WriteCoreDword(frame, 0x27973E8, unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(frame.Arena, reference)));
                int basisIndex = NativeBattleWaterSlots.LookupB65370(frame.Arena, bank, reference, order, SlotWord(frame, 0x9C));
                short mode = unchecked((short)Operand(4));
                if (mode != 0)
                {
                    throw new NotSupportedException("Native selected world-position command requires rotation order zero.");
                }
                int basisOffset = bank.SlotOffset(frame.Arena, basisIndex);
                NativeBattleActionMatrix basis = NativeBattleGfMatrices.Euler56CD50(VectorAt(frame, basisOffset + 0x8C));
                frame = WriteEuler(frame, basis);
                NativeBattleVector basePosition = VectorAt(frame, basisOffset + 0x94);
                basis = basis with { X = basePosition.X, Y = basePosition.Y, Z = basePosition.Z };
                NativeBattleFullMacVertex world = NativeBattleGfMatrices.TransformMac460840(basis, Position(frame));
                frame = frame.WriteTemporaryDword(0xF0, unchecked((uint)world.X)).WriteTemporaryDword(0xF4, unchecked((uint)world.Y)).WriteTemporaryDword(0xF8, unchecked((uint)world.Z)).WriteTemporaryDword(0xFC, world.Flags);
                frame = WriteCoreDword(frame, 0x27973E8, original);
                frame = WriteSlotDword(frame, 0x5C, unchecked((uint)world.X << 16));
                frame = WriteSlotDword(frame, 0x60, unchecked((uint)world.Y << 16));
                frame = WriteSlotDword(frame, 0x64, unchecked((uint)world.Z << 16));
                frame = frame with { Arena = NativeBattleWaterSlots.UpdateSelected(frame.Arena, bank, CurrentIndex(frame, bank), order) };
                advance = 6;
                break;
            default:
                throw new NotSupportedException("Native matrix and uniform-color commands admit only their selected finite operations.");
        }
        return WriteCoreDword(frame, 0x2797450, unchecked(pc + (uint)advance));

        ushort Operand(int delta)
        {
            uint relative = unchecked(pc + (uint)delta - program.Address);
            if (program.ArenaOffset < 0 || program.Length < 2 || program.ArenaOffset > NativeBattleEffectArena.Length - program.Length || relative > (uint)(program.Length - 2))
            {
                throw new InvalidDataException("Native matrix command operand leaves its current program owner.");
            }
            return BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(checked(program.ArenaOffset + (int)relative), 2));
        }
    }

    private static NativeBattleWaterExecutionFrame WriteEuler(NativeBattleWaterExecutionFrame frame, NativeBattleActionMatrix euler, int offset = 0xE0)
    {
        for (int lane = 0; lane < 9; lane++)
        {
            frame = frame.WriteTemporaryWord(offset + lane * 2, unchecked((ushort)euler.Rotation[lane]));
        }
        return frame with { TemporaryFrame = frame.TemporaryFrame with { Known = frame.TemporaryFrame.Known.SetItem(offset + 18, false).SetItem(offset + 19, false) } };
    }

    private static NativeBattleWaterExecutionFrame WriteRotation(NativeBattleWaterExecutionFrame frame, uint address, NativeBattleActionMatrix matrix)
    {
        for (int lane = 0; lane < 9; lane++)
        {
            frame = WriteCoreWord(frame, unchecked(address + (uint)(lane * 2)), unchecked((ushort)matrix.Rotation[lane]));
        }
        return frame;
    }

    private static NativeBattleActionMatrix ReadRotation(NativeBattleWaterExecutionFrame frame, uint address)
    {
        byte[] rotation = frame.Core.Read(address, 18);
        return new(Enumerable.Range(0, 9).Select(lane => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(lane * 2))).ToImmutableArray(), 0, 0, 0, 4096);
    }

    private static NativeBattleActionMatrix ReadModuleView(NativeBattleWaterExecutionFrame frame)
    {
        if (frame.ModuleViewBackup.IsDefaultOrEmpty || frame.ModuleViewBackup.Length != 32)
        {
            throw new NotSupportedException("Native saved-matrix publication requires the actual module view backup.");
        }
        byte[] bytes = frame.ModuleViewBackup.ToArray();
        return new(Enumerable.Range(0, 9).Select(lane => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(lane * 2))).ToImmutableArray(), BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(20)), BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(24)), BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(28)), 4096);
    }

    private static NativeBattleActionMatrix ReadMatrix(NativeBattleWaterExecutionFrame frame, uint address)
    {
        byte[] rotation = frame.Core.Read(address, 18);
        return new(Enumerable.Range(0, 9).Select(lane => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(lane * 2))).ToImmutableArray(), unchecked((int)CoreDword(frame, address + 20)), unchecked((int)CoreDword(frame, address + 24)), unchecked((int)CoreDword(frame, address + 28)), 4096);
    }

    private static NativeBattleVector Position(NativeBattleWaterExecutionFrame frame) => new(unchecked((short)SlotWord(frame, 0x94)), unchecked((short)SlotWord(frame, 0x96)), unchecked((short)SlotWord(frame, 0x98)));
    private static NativeBattleVector Angles(NativeBattleWaterExecutionFrame frame) => new(unchecked((short)SlotWord(frame, 0x8C)), unchecked((short)SlotWord(frame, 0x8E)), unchecked((short)SlotWord(frame, 0x90)));
    private static NativeBattleVector VectorAt(NativeBattleWaterExecutionFrame frame, int offset)
    {
        byte[] bytes = frame.Arena.Read(offset, 6);
        return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
    }

    private static int CurrentIndex(NativeBattleWaterExecutionFrame frame, NativeBattleWaterSlotBank bank)
    {
        int offset = ArenaOffset(CoreDword(frame, 0x27973E8), 0x100) - bank.SlotBaseOffset;
        if (offset < 0 || offset % 0x100 != 0)
        {
            throw new InvalidDataException("Native matrix lookup requires its current physical slot in the cached bank.");
        }
        int index = offset / 0x100;
        _ = bank.SlotOffset(frame.Arena, index);
        return index;
    }

    private static ImmutableArray<byte> ActiveOrder(NativeBattleWaterExecutionFrame frame)
    {
        byte[] order = Enumerable.Repeat((byte)0xFF, 128).ToArray();
        for (int index = 0; index < order.Length; index++)
        {
            order[index] = frame.Core.Read(0x2797454 + (uint)index, 1)[0];
            if (order[index] == 0xFF)
            {
                return order.ToImmutableArray();
            }
        }
        throw new NotSupportedException("Native matrix lookup requires its terminated active order.");
    }
}
