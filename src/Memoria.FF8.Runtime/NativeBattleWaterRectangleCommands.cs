using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Finite rectangle animation registration and the selected mode-nine draw caller.</summary>
public static class NativeBattleWaterRectangleCommands
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleWaterSlotBank bank, int currentIndex, NativeBattleWater222Resources resources, NativeBattleEffectHeapAllocation map)
    {
        return Execute(frame, program, bank, currentIndex, (current, index) => NativeBattleWaterShapeSelection.ResolveB657E0(current, resources, map, index), (current, index) => NativeBattleWaterShapeSelection.SelectB66230(current, resources, map, index));
    }

    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleWaterSlotBank bank, int currentIndex, Func<NativeBattleWaterExecutionFrame, short, uint> resolveShape, Func<NativeBattleWaterExecutionFrame, short, (NativeBattleWaterExecutionFrame Frame, uint Address)> selectSequence)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        uint pc = CoreDword(frame, 0x2797450);
        switch (instruction & 0x1FF)
        {
            case 0x2F:
                for (int i = 0; i < 9; i++)
                {
                    frame = WriteSlotDword(frame, 0x68 + 4 * i, 0);
                }
                if (CoreDword(frame, 0x27973E8) != NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(frame.Arena, currentIndex))
                {
                    throw new InvalidDataException("Native acceleration refresh requires its actual current cached-bank slot.");
                }
                frame = frame with { Arena = NativeBattleWaterSlots.RefreshAccelerationAA27F0(frame.Arena, bank, currentIndex) };
                return Advance(2);
            case 0x4A:
            case 0x3C:
            case 0x56:
                uint color = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xCC, 4));
                frame = WriteSlotDword(frame, 0xCC, color & 0x02FFFFFF);
                frame = WriteSlotDword(frame, 0xBC, 0);
                uint shape = resolveShape(frame, Operand());
                frame = WriteSlotDword(frame, 0xD8, shape);
                frame = NativeBattleWaterParticleCommands.RegisterDrawB65320(frame, (instruction & 0x1FF) is 0x3C or 0x56 ? (byte)1 : (byte)(instruction >> 9));
                frame = Advance(4);
                if ((instruction & 0x1FF) == 0x3C)
                {
                    frame = SlotByte(frame, 0x1C, 2);
                }
                if ((instruction & 0x1FF) == 0x56)
                {
                    frame = SlotByte(frame, 0x1C, 9);
                    frame = WriteSlotWord(frame, 0xC2, unchecked((ushort)ReadWord(CoreDword(frame, 0x2797450))));
                    frame = Advance(2);
                }
                return frame;
            case 0x25:
            case 0x81:
            case 0x73:
                frame = InitializeSequenceB66270(frame, Operand(), selectSequence);
                frame = SlotByte(frame, 0x1E, 1);
                frame = Advance(4);
                if ((instruction & 0x1FF) == 0x73)
                {
                    frame = SlotByte(frame, 0x1E, 2);
                    frame = WriteSlotDword(frame, 0xB8, unchecked((uint)(int)ReadWord(CoreDword(frame, 0x2797450))));
                    return Advance(2);
                }
                return SlotByte(frame, 0x1E, (instruction & 0x1FF) == 0x25 ? (byte)9 : (byte)(frame.ReadTemporaryWord(0x14A) >> 12));
            default:
                throw new NotSupportedException("Native rectangle commands admit only selected operations 25, 2F, 3C, 4A, 56, 73 and 81.");
        }

        NativeBattleWaterExecutionFrame Advance(uint amount) => WriteCoreDword(frame, 0x2797450, unchecked(CoreDword(frame, 0x2797450) + amount));

        short Operand() => ReadWord(unchecked(pc + 2));

        short ReadWord(uint address)
        {
            uint relative = unchecked(address - program.Address);
            if (program.ArenaOffset < 0 || program.Length < 2 || program.ArenaOffset > NativeBattleEffectArena.Length - program.Length || relative > (uint)(program.Length - 2))
            {
                throw new InvalidDataException("Native rectangle selection operand leaves its current program owner.");
            }
            return BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(checked(program.ArenaOffset + (int)relative), 2));
        }
    }

    public static NativeBattleWaterExecutionFrame InitializeSequenceB66270(NativeBattleWaterExecutionFrame frame, short selection, Func<NativeBattleWaterExecutionFrame, short, (NativeBattleWaterExecutionFrame Frame, uint Address)> selectSequence)
    {
        frame = WriteSlotDword(frame, 0xCC, (BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xCC, 4)) & 0x02FFFFFF) | 0x2C000000);
        bool fixedFrame = (selection & 0x8000) != 0;
        short index = fixedFrame ? (short)((selection >> 8) & 0x7F) : selection;
        uint sequence;
        (frame, sequence) = selectSequence(frame, index);
        frame = WriteSlotDword(frame, 0xE4, frame.ReadTemporaryDword(0xFC));
        frame = WriteSlotDword(frame, 0xD0, sequence);
        frame = WriteSlotDword(frame, 0xD4, sequence);
        uint entry = fixedFrame ? unchecked(sequence + (uint)((selection & 0xFF) * 8)) : sequence;
        frame = WriteSlotDword(frame, 0xD8, unchecked(frame.ReadTemporaryDword(0xFC) + ArenaDword(frame, entry)));
        ushort speed = fixedFrame ? (ushort)(SlotWord(frame, 0xCA) | 0x8000) : (ushort)(SlotWord(frame, 0xCA) & 0x7FFF);
        frame = WriteSlotWord(frame, 0xCA, speed);
        frame = WriteSlotWord(frame, 0xDC, SlotWord(frame, 0xCA));
        return NativeBattleWaterParticleCommands.RegisterDrawB65320(frame, 5);
    }

    public static NativeBattleWaterExecutionFrame AdvanceDraw5(NativeBattleWaterExecutionFrame frame, Func<NativeBattleWaterExecutionFrame, ushort, uint> resolveMatrixRow, Func<NativeBattleWaterExecutionFrame, NativeBattleActionMatrix, NativeBattleWaterExecutionFrame> drawRectangles, Func<NativeBattleWaterExecutionFrame, NativeBattleGeometryRegisterStep> readRegisters, Func<NativeBattleWaterExecutionFrame, NativeBattleGeometryRegisterStep, NativeBattleWaterExecutionFrame> publishRegisters)
    {
        if (frame.ReadTemporaryByte(0x145) == 0)
        {
            short speed = unchecked((short)SlotWord(frame, 0xCA));
            if (speed >= 0)
            {
                frame = WriteSlotWord(frame, 0xDC, unchecked((ushort)(SlotWord(frame, 0xDC) - speed)));
                if (unchecked((short)SlotWord(frame, 0xDC)) <= 0)
                {
                    uint sequence = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xD0, 4));
                    frame = WriteSlotDword(frame, 0xD8, unchecked(BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xE4, 4)) + ArenaDword(frame, sequence)));
                    frame = WriteSlotWord(frame, 0xDC, unchecked((ushort)(SlotWord(frame, 0xDC) + BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(unchecked(sequence + 4), 2), 2)))));
                    sbyte command = unchecked((sbyte)frame.Arena.Read(ArenaOffset(unchecked(sequence + 6), 1), 1)[0]);
                    if (command == 1)
                    {
                        frame = SlotByte(frame, 0x1C, 0);
                        frame = NativeBattleWaterStopCommand.RemoveDrawB65270(frame, frame.ReadTemporaryByte(0x142));
                        return frame.WriteTemporaryDword(0, unchecked(frame.ReadTemporaryDword(0) - 1));
                    }
                    uint next = command == 2 ? BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xD4, 4)) : unchecked(sequence + 8);
                    frame = WriteSlotDword(frame, 0xD0, next);
                }
            }
        }
        uint matrixAddress = resolveMatrixRow(frame, SlotWord(frame, 0x9C));
        (byte[] sourceBytes, bool[] sourceKnown) = frame.Core.ReadSnapshot(matrixAddress, 32);
        NativeBattlePackedMatrix32 packedSource = new(sourceBytes.ToImmutableArray(), sourceKnown.ToImmutableArray());
        NativeBattleActionMatrix matrix = packedSource.ReadMatrix();
        NativeBattleGeometryRegisterStep registers = readRegisters(frame);
        short? sourcePadding = sourceKnown[18] && sourceKnown[19] ? BinaryPrimitives.ReadInt16LittleEndian(sourceBytes.AsSpan(18)) : null;
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, true, sourcePadding) };
        NativeBattleVector position = new(unchecked((short)SlotWord(frame, 0x94)), unchecked((short)SlotWord(frame, 0x96)), unchecked((short)SlotWord(frame, 0x98)));
        (byte[] sourceWBytes, bool[] sourceWKnown) = frame.Arena.ReadSnapshot(SlotOffset(frame, 0x9A, 2), 2);
        short? sourceW = sourceWKnown.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(sourceWBytes) : null;
        registers = NativeBattleGeometry.LoadVector(registers, 0, position, sourceW);
        registers = NativeBattleGeometry.Transform(registers);
        registers = registers with { Geometry = registers.Geometry with { Translation = registers.Colors.Mac } };
        byte mode = ReadSlot(frame, 0x1E, 1)[0];
        if (mode == 2)
        {
            uint basisAddress = 0x2797968 + (uint)(32 * (ReadSlot(frame, 0xDE, 1)[0] & 3));
            (byte[] bytes, bool[] known) = frame.Core.ReadSnapshot(basisAddress, 20);
            byte[] temporary = frame.TemporaryFrame.Bytes.ToArray();
            bool[] temporaryKnown = frame.TemporaryFrame.Known.ToArray();
            bytes.CopyTo(temporary, 0xD0);
            known.CopyTo(temporaryKnown, 0xD0);
            frame = frame with { TemporaryFrame = frame.TemporaryFrame with { Bytes = temporary.ToImmutableArray(), Known = temporaryKnown.ToImmutableArray() } };
            int scale = unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xB8, 4)) << 4);
            frame = frame.WriteTemporaryDword(0xF0, unchecked((uint)scale)).WriteTemporaryDword(0xF4, unchecked((uint)scale)).WriteTemporaryDword(0xF8, unchecked((uint)scale));
            NativeBattleActionMatrix scaled = NativeBattlePoseMatrices.Scale56BEF0(ReadMatrix(frame, basisAddress, false), scale, scale, scale);
            for (int component = 0; component < 9; component++)
            {
                frame = frame.WriteTemporaryWord(0xD0 + 2 * component, unchecked((ushort)scaled.Rotation[component]));
            }
            matrix = scaled with { X = registers.Geometry.Translation[0]!.Value, Y = registers.Geometry.Translation[1]!.Value, Z = registers.Geometry.Translation[2]!.Value };
            short? padding = known[18] && known[19] ? BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(18)) : null;
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, false, padding) };
            return FinishDraw5(frame, registers, matrix, drawRectangles, publishRegisters);
        }
        if (mode is not (7 or 8 or 9))
        {
            throw new NotSupportedException($"Native selected animated rectangle matrix mode {mode} requires its reached contract.");
        }
        uint modeMatrix = mode is 7 or 8 ? resolveMatrixRow(frame, SlotWord(frame, 0x9C)) : 0;
        int scaleX = unchecked((short)SlotWord(frame, 0x8C)) << 4;
        int scaleY = mode == 8 ? unchecked((short)SlotWord(frame, 0x8C)) << 4 : unchecked((short)SlotWord(frame, 0x8E)) << 4;
        int scaleOffset = mode == 8 ? 0x30 : 0xD0;
        int angleOffset = mode == 8 ? 0x20 : 0xC0;
        frame = frame.WriteTemporaryDword(scaleOffset, unchecked((uint)scaleX)).WriteTemporaryDword(scaleOffset + 4, unchecked((uint)scaleY)).WriteTemporaryDword(scaleOffset + 8, 4096);
        short xRotation = mode == 7 ? (short)1024 : (short)0;
        short yRotation = mode == 8 ? unchecked((short)SlotWord(frame, 0x8E)) : (short)0;
        frame = frame.WriteTemporaryWord(angleOffset, unchecked((ushort)xRotation)).WriteTemporaryWord(angleOffset + 2, unchecked((ushort)yRotation)).WriteTemporaryWord(angleOffset + 4, SlotWord(frame, 0x90));
        NativeBattleVector angles = new(xRotation, yRotation, unchecked((short)frame.ReadTemporaryWord(angleOffset + 4)));
        NativeBattleActionMatrix euler;
        if (mode == 8)
        {
            (frame, euler) = NativeBattleWaterMatrixCommands.BuildRotationB65590(frame, angles, 1);
        }
        else
        {
            euler = NativeBattleGfMatrices.Euler56CD50(angles);
            frame = WriteEuler(frame, euler);
        }
        if (mode == 9)
        {
            modeMatrix = 0x2797968 + (uint)(32 * (ReadSlot(frame, 0xDE, 1)[0] & 3));
        }
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Multiply56C090(ReadMatrix(frame, modeMatrix, false), euler);
        frame = WriteEuler(frame, rotation);
        if (mode is 7 or 9)
        {
            frame = frame with { TemporaryFrame = frame.TemporaryFrame with { Known = frame.TemporaryFrame.Known.SetItem(0xF2, false).SetItem(0xF3, false) } };
        }
        rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, scaleX, scaleY, 4096);
        frame = WriteEuler(frame, rotation);
        matrix = rotation with { X = registers.Geometry.Translation[0]!.Value, Y = registers.Geometry.Translation[1]!.Value, Z = registers.Geometry.Translation[2]!.Value };
        short? rotationPadding = frame.TemporaryFrame.Known[0xF2] && frame.TemporaryFrame.Known[0xF3] ? unchecked((short)frame.ReadTemporaryWord(0xF2)) : null;
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, false, rotationPadding) };
        return FinishDraw5(frame, registers, matrix, drawRectangles, publishRegisters);
    }

    private static NativeBattleWaterExecutionFrame FinishDraw5(NativeBattleWaterExecutionFrame frame, NativeBattleGeometryRegisterStep registers, NativeBattleActionMatrix matrix, Func<NativeBattleWaterExecutionFrame, NativeBattleActionMatrix, NativeBattleWaterExecutionFrame> drawRectangles, Func<NativeBattleWaterExecutionFrame, NativeBattleGeometryRegisterStep, NativeBattleWaterExecutionFrame> publishRegisters)
    {
        frame = frame.WriteTemporaryDword(0x90, BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xD8, 4))).WriteTemporaryDword(0x94, unchecked((uint)(short)SlotWord(frame, 0x92))).WriteTemporaryDword(0x98, BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xCC, 4))).WriteTemporaryDword(0x9C, unchecked((uint)(short)SlotWord(frame, 0x9A)));
        return drawRectangles(publishRegisters(frame, registers), matrix);
    }

    private static NativeBattleActionMatrix ReadMatrix(NativeBattleWaterExecutionFrame frame, uint address, bool translation)
    {
        byte[] bytes = frame.Core.Read(address, 18);
        return new(Enumerable.Range(0, 9).Select(i => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2 * i))).ToImmutableArray(), translation ? unchecked((int)CoreDword(frame, address + 20)) : 0, translation ? unchecked((int)CoreDword(frame, address + 24)) : 0, translation ? unchecked((int)CoreDword(frame, address + 28)) : 0, 4096);
    }
    private static NativeBattleWaterExecutionFrame WriteEuler(NativeBattleWaterExecutionFrame frame, NativeBattleActionMatrix matrix)
    {
        for (int i = 0; i < 9; i++)
        {
            frame = frame.WriteTemporaryWord(0xE0 + 2 * i, unchecked((ushort)matrix.Rotation[i]));
        }
        return frame;
    }
    private static uint ArenaDword(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
    private static NativeBattleWaterExecutionFrame SlotByte(NativeBattleWaterExecutionFrame frame, int offset, byte value) => frame with { Arena = frame.Arena.Write(SlotOffset(frame, offset, 1), new byte[] { value }) };
}
