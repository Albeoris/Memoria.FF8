using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Finite rectangle animation registration and the selected mode-nine draw caller.</summary>
public static class NativeBattleWaterRectangleCommands
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleWaterSlotBank bank, int currentIndex, NativeBattleWater222Resources resources, NativeBattleEffectHeapAllocation map)
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
                uint color = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xCC, 4));
                frame = WriteSlotDword(frame, 0xCC, color & 0x02FFFFFF);
                frame = WriteSlotDword(frame, 0xBC, 0);
                uint shape = NativeBattleWaterShapeSelection.ResolveB657E0(frame, resources, map, Operand());
                frame = WriteSlotDword(frame, 0xD8, shape);
                frame = NativeBattleWaterParticleCommands.RegisterDrawB65320(frame, (instruction & 0x1FF) == 0x3C ? (byte)1 : (byte)(instruction >> 9));
                frame = Advance(4);
                if ((instruction & 0x1FF) == 0x3C)
                {
                    frame = SlotByte(frame, 0x1C, 2);
                }
                return frame;
            case 0x25:
                short index = Operand();
                if (index < 0)
                {
                    throw new NotSupportedException("Native selected rectangle selection requires its nonnegative sequence variant.");
                }
                frame = WriteSlotDword(frame, 0xCC, (BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xCC, 4)) & 0x02FFFFFF) | 0x2C000000);
                uint sequence;
                (frame, sequence) = NativeBattleWaterShapeSelection.SelectB66230(frame, resources, map, index);
                frame = WriteSlotDword(frame, 0xE4, frame.ReadTemporaryDword(0xFC));
                frame = WriteSlotDword(frame, 0xD0, sequence);
                frame = WriteSlotDword(frame, 0xD4, sequence);
                frame = WriteSlotDword(frame, 0xD8, unchecked(frame.ReadTemporaryDword(0xFC) + ArenaDword(frame, sequence)));
                frame = WriteSlotWord(frame, 0xCA, (ushort)(SlotWord(frame, 0xCA) & 0x7FFF));
                frame = WriteSlotWord(frame, 0xDC, SlotWord(frame, 0xCA));
                frame = NativeBattleWaterParticleCommands.RegisterDrawB65320(frame, 5);
                frame = SlotByte(frame, 0x1E, 1);
                frame = Advance(4);
                return SlotByte(frame, 0x1E, 9);
            default:
                throw new NotSupportedException("Native rectangle commands admit only selected operations 25, 2F, 3C and 4A.");
        }

        NativeBattleWaterExecutionFrame Advance(uint amount) => WriteCoreDword(frame, 0x2797450, unchecked(CoreDword(frame, 0x2797450) + amount));

        short Operand()
        {
            uint relative = unchecked(pc + 2 - program.Address);
            if (program.ArenaOffset < 0 || program.Length < 2 || program.ArenaOffset > NativeBattleEffectArena.Length - program.Length || relative > (uint)(program.Length - 2))
            {
                throw new InvalidDataException("Native rectangle selection operand leaves its current program owner.");
            }
            return BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(checked(program.ArenaOffset + (int)relative), 2));
        }
    }

    public static NativeBattleWaterExecutionFrame AdvanceDraw5(NativeBattleWaterExecutionFrame frame, Func<NativeBattleWaterExecutionFrame, ushort, uint> resolveMatrixRow, Func<NativeBattleWaterExecutionFrame, NativeBattleActionMatrix, NativeBattleWaterExecutionFrame> drawRectangles)
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
        NativeBattleActionMatrix matrix = ReadMatrix(frame, matrixAddress, true);
        NativeBattleVector position = new(unchecked((short)SlotWord(frame, 0x94)), unchecked((short)SlotWord(frame, 0x96)), unchecked((short)SlotWord(frame, 0x98)));
        NativeBattleFullMacVertex translated = NativeBattleGfMatrices.TransformMac460840(matrix, position);
        if (ReadSlot(frame, 0x1E, 1)[0] != 9)
        {
            throw new NotSupportedException("Native selected animated rectangles require matrix mode nine.");
        }
        int scaleX = unchecked((short)SlotWord(frame, 0x8C)) << 4;
        int scaleY = unchecked((short)SlotWord(frame, 0x8E)) << 4;
        frame = frame.WriteTemporaryDword(0xD0, unchecked((uint)scaleX)).WriteTemporaryDword(0xD4, unchecked((uint)scaleY)).WriteTemporaryDword(0xD8, 4096);
        frame = frame.WriteTemporaryWord(0xC0, 0).WriteTemporaryWord(0xC2, 0).WriteTemporaryWord(0xC4, SlotWord(frame, 0x90));
        NativeBattleActionMatrix euler = NativeBattleGfMatrices.Euler56CD50(new(0, 0, unchecked((short)frame.ReadTemporaryWord(0xC4))));
        frame = WriteEuler(frame, euler);
        int basis = ReadSlot(frame, 0xDE, 1)[0] & 3;
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Multiply56C090(ReadMatrix(frame, 0x2797968 + (uint)(32 * basis), false), euler);
        frame = WriteEuler(frame, rotation);
        rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, scaleX, scaleY, 4096);
        frame = WriteEuler(frame, rotation);
        matrix = rotation with { X = translated.X, Y = translated.Y, Z = translated.Z };
        frame = frame.WriteTemporaryDword(0x90, BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xD8, 4))).WriteTemporaryDword(0x94, unchecked((uint)(short)SlotWord(frame, 0x92))).WriteTemporaryDword(0x98, BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xCC, 4))).WriteTemporaryDword(0x9C, unchecked((uint)(short)SlotWord(frame, 0x9A)));
        return drawRectangles(frame, matrix);
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
        return frame with { TemporaryFrame = frame.TemporaryFrame with { Known = frame.TemporaryFrame.Known.SetItem(0xF2, false).SetItem(0xF3, false) } };
    }
    private static uint ArenaDword(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
    private static NativeBattleWaterExecutionFrame SlotByte(NativeBattleWaterExecutionFrame frame, int offset, byte value) => frame with { Arena = frame.Arena.Write(SlotOffset(frame, offset, 1), new byte[] { value }) };
}
