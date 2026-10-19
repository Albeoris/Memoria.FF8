using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicShapePositionServices(Func<NativeBattleWaterExecutionFrame, short, uint> ResolveSlot, Func<NativeBattleWaterExecutionFrame, short, uint> ResolveShape, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterExecutionFrame> CaptureInput, Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame> Update);

/// <summary>Current shape-vertex position, with native parent transform or per-axis morph arithmetic.</summary>
public static class NativeBattleCinematicShapePosition5C
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleCinematicShapePositionServices services)
    {
        uint pc = CoreDword(frame, 0x2797450);
        uint originalSlot = CoreDword(frame, 0x27973E8);
        frame = frame.WriteTemporaryDword(0x60, originalSlot);
        uint selected = services.ResolveSlot(frame, Operand(2));
        frame = WriteCoreDword(frame, 0x27973E8, selected);
        uint controlFlags = unchecked(CoreDword(frame, 0x27973B8) + 0x4B);
        byte flags = controlFlags >= frame.TemporaryFrame.Address && (long)controlFlags - frame.TemporaryFrame.Address < 0x180 ? frame.ReadTemporaryByte(checked((int)(controlFlags - frame.TemporaryFrame.Address))) : frame.Core.Read(controlFlags, 1)[0];
        (int X, int Y, int Z) result;
        uint advance;
        if ((flags & 0x80) != 0)
        {
            uint parent = services.ResolveSlot(frame, unchecked((short)SlotWord(frame, 0x9C)));
            NativeBattleActionMatrix matrix;
            (frame, matrix) = NativeBattleWaterMatrixCommands.BuildRotationB65590(frame, Vector(parent + 0x8C), Operand(6));
            int x = unchecked((short)SlotWord(frame, 0x8C)) << 4;
            int y = unchecked((short)SlotWord(frame, 0x8E)) << 4;
            int z = unchecked((short)SlotWord(frame, 0x90)) << 4;
            frame = frame.WriteTemporaryDword(0x80, unchecked((uint)x)).WriteTemporaryDword(0x84, unchecked((uint)y)).WriteTemporaryDword(0x88, unchecked((uint)z));
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, x, y, z) with { X = SignedWord(parent + 0x94), Y = SignedWord(parent + 0x96), Z = SignedWord(parent + 0x98) };
            for (int lane = 0; lane < 9; lane++)
            {
                frame = frame.WriteTemporaryWord(0xE0 + lane * 2, unchecked((ushort)matrix.Rotation[lane]));
            }
            frame = WriteTranslation(frame, matrix.X, matrix.Y, matrix.Z);
            frame = services.CaptureInput(frame, selected + 0x94);
            NativeBattleFullMacVertex anchor = NativeBattleGfMatrices.TransformMac460840(matrix, Vector(selected + 0x94));
            frame = frame.WriteTemporaryDword(0x80, anchor.Flags);
            matrix = matrix with { X = anchor.X, Y = anchor.Y, Z = anchor.Z };
            frame = WriteTranslation(frame, anchor.X, anchor.Y, anchor.Z);
            uint shape = SlotDword(0xD8);
            uint point = Vertex(shape, Operand(4));
            frame = services.CaptureInput(frame, point);
            NativeBattleFullMacVertex transformed = NativeBattleGfMatrices.TransformMac460840(matrix, Vector(point));
            frame = frame.WriteTemporaryDword(0xF0, unchecked((uint)transformed.X)).WriteTemporaryDword(0xF4, unchecked((uint)transformed.Y)).WriteTemporaryDword(0xF8, unchecked((uint)transformed.Z)).WriteTemporaryDword(0xFC, transformed.Flags);
            result = (transformed.X, transformed.Y, transformed.Z);
            advance = 8;
        }
        else
        {
            uint morph = SlotDword(0xBC);
            uint weights = services.ResolveSlot(frame, SignedWord(morph));
            uint firstShape = services.ResolveShape(frame, SignedWord(morph + 2));
            uint secondShape = services.ResolveShape(frame, SignedWord(morph + 4));
            short vertex = Operand(4);
            uint first = Vertex(firstShape, vertex);
            uint second = Vertex(secondShape, vertex);
            for (int lane = 0; lane < 3; lane++)
            {
                int start = SignedWord(first + (uint)(lane * 2));
                int end = SignedWord(second + (uint)(lane * 2));
                int weight = SignedWord(weights + 0x94u + (uint)(lane * 2));
                int interpolated = unchecked(start + unchecked((end - start) * weight) / 256);
                frame = frame.WriteTemporaryDword(0xE0 + lane * 4, unchecked((uint)interpolated));
            }
            for (int lane = 0; lane < 3; lane++)
            {
                int scale = unchecked((short)SlotWord(frame, 0x8C + lane * 2));
                int value = unchecked((int)frame.ReadTemporaryDword(0xE0 + lane * 4));
                int scaled = unchecked(unchecked(scale * value) << 4) / 4096;
                frame = frame.WriteTemporaryDword(0xE0 + lane * 4, unchecked((uint)scaled));
            }
            uint parent = services.ResolveSlot(frame, unchecked((short)SlotWord(frame, 0x9C)));
            result = (unchecked(SignedWord(parent + 0x94) + (int)frame.ReadTemporaryDword(0xE0)), unchecked(SignedWord(parent + 0x96) + (int)frame.ReadTemporaryDword(0xE4)), unchecked(SignedWord(parent + 0x98) + (int)frame.ReadTemporaryDword(0xE8)));
            advance = 6;
        }
        frame = WriteCoreDword(frame, 0x2797450, unchecked(pc + advance));
        frame = WriteCoreDword(frame, 0x27973E8, frame.ReadTemporaryDword(0x60));
        frame = WriteSlotDword(frame, 0x5C, unchecked((uint)(result.X << 16)));
        frame = WriteSlotDword(frame, 0x60, unchecked((uint)(result.Y << 16)));
        frame = WriteSlotDword(frame, 0x64, unchecked((uint)(result.Z << 16)));
        return services.Update(frame, ReadSlot(frame, 0x18, 1)[0]);

        short Operand(int delta) => BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, (uint)delta, 2));
        short SignedWord(uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
        uint SlotDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
        uint Vertex(uint shape, short index) => unchecked(shape + Dword(shape + 0x14) + (uint)(index * 8));
        NativeBattleVector Vector(uint address) => new(SignedWord(address), SignedWord(address + 2), SignedWord(address + 4));
    }

    private static NativeBattleWaterExecutionFrame WriteTranslation(NativeBattleWaterExecutionFrame frame, int x, int y, int z) => frame.WriteTemporaryDword(0xF4, unchecked((uint)x)).WriteTemporaryDword(0xF8, unchecked((uint)y)).WriteTemporaryDword(0xFC, unchecked((uint)z));
}
