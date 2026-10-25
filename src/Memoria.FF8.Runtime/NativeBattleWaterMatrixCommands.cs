using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Matrix-row allocation and selected rotation commands over current owners.</summary>
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

    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleWaterSlotBank bank, NativeBattleWaterActorAnchorServices? registerServices = null)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        uint pc = CoreDword(frame, 0x2797450);
        NativeBattleGeometryRegisterStep? registerOutput = null;
        int advance;
        switch (instruction & 0x1FF)
        {
            case 0x66:
                uint cameraDestination;
                (frame, cameraDestination) = AllocateB65480(frame);
                NativeBattleFullMacVertex cameraTranslation = NativeBattleGfMatrices.TransformMac460840(ReadMatrix(frame, 0x27979E8), Position(frame));
                frame = WriteCoreDword(frame, cameraDestination + 20, unchecked((uint)cameraTranslation.X));
                frame = WriteCoreDword(frame, cameraDestination + 24, unchecked((uint)cameraTranslation.Y));
                frame = WriteCoreDword(frame, cameraDestination + 28, unchecked((uint)cameraTranslation.Z));
                frame = frame.WriteTemporaryDword(0xF0, cameraTranslation.Flags);
                frame = frame with { Core = frame.Core.Copy(0x27979E8, cameraDestination, 20) };
                frame = WriteCoreWord(frame, cameraDestination + 18, CoreWord(frame, CoreDword(frame, 0x27973EC) + 2));
                advance = 4;
                break;
            case 0x104:
                uint savedDestination = unchecked(0x2798B68u + (uint)(0x20 * (short)Operand(2)));
                NativeBattleActionMatrix savedEuler = NativeBattleGfMatrices.Euler56CD50(Angles(frame));
                frame = WriteEuler(frame, savedEuler, 0x20);
                NativeBattleActionMatrix moduleView = ReadModuleView(frame);
                NativeBattleActionMatrix savedRotation = NativeBattleGfMatrices.Multiply56C090(moduleView, savedEuler);
                frame = WriteRotation(frame, savedDestination, savedRotation);
                NativeBattleFullMacVertex savedTranslation = TransformSaved(moduleView, frame.ModuleViewBackup[18], frame.ModuleViewBackup[19]);
                frame = WriteCoreDword(frame, unchecked(savedDestination + 20), unchecked((uint)savedTranslation.X));
                frame = WriteCoreDword(frame, unchecked(savedDestination + 24), unchecked((uint)savedTranslation.Y));
                frame = WriteCoreDword(frame, unchecked(savedDestination + 28), unchecked((uint)savedTranslation.Z));
                frame = frame.WriteTemporaryDword(0xF0, savedTranslation.Flags);
                frame = WriteCoreWord(frame, unchecked(savedDestination + 18), 0);
                advance = 4;
                break;
            case 0x105:
                uint loadedDestination;
                (frame, loadedDestination) = AllocateB65480(frame);
                uint savedSource = unchecked(0x2798B68u + (uint)(0x20 * (short)Operand(2)));
                (byte[] loadedPadding, bool[] loadedPaddingKnown) = frame.Core.ReadSnapshot(unchecked(savedSource + 18), 2);
                NativeBattleFullMacVertex loadedTranslation = TransformSaved(ReadMatrix(frame, savedSource), loadedPaddingKnown[0] ? loadedPadding[0] : null, loadedPaddingKnown[1] ? loadedPadding[1] : null);
                frame = WriteCoreDword(frame, unchecked(loadedDestination + 20), unchecked((uint)loadedTranslation.X));
                frame = WriteCoreDword(frame, unchecked(loadedDestination + 24), unchecked((uint)loadedTranslation.Y));
                frame = WriteCoreDword(frame, unchecked(loadedDestination + 28), unchecked((uint)loadedTranslation.Z));
                frame = frame.WriteTemporaryDword(0xF0, loadedTranslation.Flags);
                NativeBattleActionMatrix loadedEuler;
                (frame, loadedEuler) = BuildRotationB65590(frame, Angles(frame), instruction >> 12);
                NativeBattleActionMatrix loadedRotation = NativeBattleGfMatrices.Multiply56C090(ReadRotation(frame, savedSource), loadedEuler);
                frame = WriteRotation(frame, loadedDestination, loadedRotation);
                frame = WriteCoreWord(frame, unchecked(loadedDestination + 18), 0);
                advance = 6;
                break;
            case 0xD4:
                int red = unchecked((short)Operand(2));
                bool uniform = (instruction & 0x8000) != 0;
                int green = uniform ? red : unchecked((short)Operand(4));
                int blue = uniform ? red : unchecked((short)Operand(6));
                uint color = unchecked((uint)(red | (green << 8) | (blue << 16)));
                frame = WriteCoreDword(frame, 0x2797450, unchecked(pc + (uniform ? 4u : 8u)));
                uint previousColor = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xCC, 4));
                return WriteSlotDword(frame, 0xCC, (previousColor & 0xFF000000u) | color);
            case 0x6A:
            case 0xC4:
                uint publishedDestination;
                (frame, publishedDestination) = AllocateB65480(frame);
                NativeBattleActionMatrix publishedEuler;
                (frame, publishedEuler) = BuildRotationB65590(frame, Angles(frame), instruction >> 12);
                NativeBattleActionMatrix publishedRotation = (instruction & 0x1FF) == 0xC4 ? publishedEuler : NativeBattleGfMatrices.Multiply56C090(ReadRotation(frame, 0x27979E8), publishedEuler);
                frame = WriteRotation(frame, publishedDestination, publishedRotation);
                uint currentManager = CoreDword(frame, 0x27973EC);
                for (int lane = 0; lane < 3; lane++)
                {
                    frame = WriteCoreDword(frame, publishedDestination + 20u + (uint)(lane * 4), unchecked((uint)(int)(short)SlotWord(frame, 0x94 + lane * 2)));
                }
                frame = WriteCoreWord(frame, publishedDestination + 18, CoreWord(frame, currentManager + 2));
                advance = 4;
                break;
            case 0x116:
                uint plainDestination;
                (frame, plainDestination) = AllocateB65480(frame);
                NativeBattleFullMacVertex plainTranslation = NativeBattleGfMatrices.TransformMac460840(ReadMatrix(frame, 0x27979E8), Position(frame));
                frame = WriteCoreDword(frame, plainDestination + 20, unchecked((uint)plainTranslation.X));
                frame = WriteCoreDword(frame, plainDestination + 24, unchecked((uint)plainTranslation.Y));
                frame = WriteCoreDword(frame, plainDestination + 28, unchecked((uint)plainTranslation.Z));
                frame = frame.WriteTemporaryDword(0xF0, plainTranslation.Flags);
                frame = WriteRotation(frame, plainDestination, NativeBattleGfMatrices.Euler56CD50(Angles(frame)));
                frame = WriteCoreWord(frame, plainDestination + 18, 0);
                advance = 4;
                break;
            case 0x69:
                uint combinedDestination;
                (frame, combinedDestination) = AllocateB65480(frame);
                uint combinedReference = ResolveB65520(frame, bank, Operand(4));
                frame = WriteRotation(frame, combinedDestination, NativeBattleGfMatrices.Euler56CD50(Angles(frame)));
                frame = WriteRotation(frame, combinedDestination, NativeBattleGfMatrices.Multiply56C090(ReadRotation(frame, combinedDestination), ReadRotation(frame, 0x27979E8)));
                frame = WriteRotation(frame, combinedDestination, NativeBattleGfMatrices.Multiply56C090(ReadRotation(frame, combinedDestination), ReadRotation(frame, combinedReference)));
                for (int lane = 0; lane < 3; lane++)
                {
                    frame = WriteCoreDword(frame, combinedDestination + 20u + (uint)(lane * 4), unchecked((uint)(int)(short)SlotWord(frame, 0x94 + lane * 2)));
                }
                frame = WriteCoreWord(frame, combinedDestination + 18, 0);
                advance = 6;
                break;
            case 0x67:
                uint destination;
                (frame, destination) = AllocateB65480(frame);
                NativeBattleActionMatrix view = ReadMatrix(frame, 0x27979E8);
                (byte[] viewPadding, bool[] viewPaddingKnown) = frame.Core.ReadSnapshot(0x27979FA, 2);
                NativeBattleFullMacVertex translated = TransformSaved(view, viewPaddingKnown[0] ? viewPadding[0] : null, viewPaddingKnown[1] ? viewPadding[1] : null);
                frame = WriteCoreDword(frame, destination + 20, unchecked((uint)translated.X));
                frame = WriteCoreDword(frame, destination + 24, unchecked((uint)translated.Y));
                frame = WriteCoreDword(frame, destination + 28, unchecked((uint)translated.Z));
                frame = frame.WriteTemporaryDword(0xF0, translated.Flags);
                NativeBattleActionMatrix euler;
                (frame, euler) = BuildRotationB65590(frame, Angles(frame), instruction >> 12);
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
                int basisOffset = bank.SlotOffset(frame.Arena, basisIndex);
                NativeBattleActionMatrix basis;
                (frame, basis) = BuildRotationB65590(frame, VectorAt(frame, basisOffset + 0x8C), mode);
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
        frame = WriteCoreDword(frame, 0x2797450, unchecked(pc + (uint)advance));
        return registerOutput is { } output ? registerServices!.PublishRegisters(frame, output) : frame;

        NativeBattleFullMacVertex TransformSaved(NativeBattleActionMatrix matrix, byte? paddingLow, byte? paddingHigh)
        {
            NativeBattleWaterActorAnchorServices services = registerServices ?? throw new NotSupportedException("Native saved matrix commands require their current geometry and arithmetic register owners.");
            NativeBattleGeometryRegisterStep registers = services.ReadRegisters(frame);
            NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, true, null);
            geometry = geometry with { Rotation20 = geometry.Rotation20.SetItem(18, paddingLow).SetItem(19, paddingHigh) };
            uint input = unchecked(CoreDword(frame, 0x27973E8) + 0x94);
            registers = registers with { Geometry = geometry, Colors = NativeBattleCinematicRibbonInput.Capture(frame, input, registers.Colors) };
            registerOutput = NativeBattleGeometry.Transform(registers);
            return new(registerOutput.Colors.Mac[0]!.Value, registerOutput.Colors.Mac[1]!.Value, registerOutput.Colors.Mac[2]!.Value, registerOutput.Colors.Flags!.Value);
        }

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

    public static (NativeBattleWaterExecutionFrame Frame, NativeBattleActionMatrix Matrix) BuildRotationB65590(NativeBattleWaterExecutionFrame frame, NativeBattleVector angles, int mode)
    {
        if (mode == 0)
        {
            NativeBattleActionMatrix ordinary = NativeBattleGfMatrices.Euler56CD50(angles);
            return (WriteEuler(frame, ordinary), ordinary);
        }
        (byte[] seed, bool[] known) = frame.Core.ReadSnapshot(0x2797968, 24);
        foreach (int offset in new[] { 0xA0, 0xC0, 0xE0 })
        {
            ImmutableArray<byte>.Builder bytes = frame.TemporaryFrame.Bytes.ToBuilder();
            ImmutableArray<bool>.Builder mask = frame.TemporaryFrame.Known.ToBuilder();
            for (int lane = 0; lane < seed.Length; lane++)
            {
                bytes[offset + lane] = seed[lane];
                mask[offset + lane] = known[lane];
            }
            frame = frame with { TemporaryFrame = frame.TemporaryFrame with { Bytes = bytes.ToImmutable(), Known = mask.ToImmutable() } };
        }
        NativeBattleActionMatrix basis = ReadRotation(frame, 0x2797968);
        NativeBattleActionMatrix x = NativeBattleGfMatrices.RotateX56CFB0(basis, angles.X);
        NativeBattleActionMatrix y = NativeBattleGfMatrices.RotateY56D020(basis, angles.Y);
        NativeBattleActionMatrix z = NativeBattleGfMatrices.RotateZ56D090(basis, angles.Z);
        frame = WriteEuler(frame, x, 0xA0);
        frame = WriteEuler(frame, y, 0xC0);
        frame = WriteEuler(frame, z, 0xE0);
        NativeBattleActionMatrix result = z;
        switch (mode)
        {
            case 1:
                x = NativeBattleGfMatrices.Multiply56C090(z, x);
                frame = WriteEuler(frame, x, 0xA0);
                result = NativeBattleGfMatrices.Multiply56C090(x, y);
                frame = WriteEuler(frame, result);
                break;
            case 2:
                result = NativeBattleGfMatrices.Multiply56C090(x, z);
                frame = WriteEuler(frame, result);
                result = NativeBattleGfMatrices.Multiply56C090(result, y);
                frame = WriteEuler(frame, result);
                break;
            case 3:
                result = NativeBattleGfMatrices.Euler56CE30(angles);
                frame = WriteEuler(frame, result);
                break;
        }
        return (frame, result);
    }

    private static NativeBattleWaterExecutionFrame WriteEuler(NativeBattleWaterExecutionFrame frame, NativeBattleActionMatrix euler, int offset = 0xE0)
    {
        for (int lane = 0; lane < 9; lane++)
        {
            frame = frame.WriteTemporaryWord(offset + lane * 2, unchecked((ushort)euler.Rotation[lane]));
        }
        return frame;
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
