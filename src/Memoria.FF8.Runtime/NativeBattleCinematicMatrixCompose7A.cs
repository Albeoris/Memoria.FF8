using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicMatrixCompose7A
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, short, uint> resolveSlot)
    {
        short selector = Operand(2);
        uint selected = resolveSlot(frame, selector);
        uint destination;
        (frame, destination) = NativeBattleWaterMatrixCommands.AllocateB65480(frame);
        uint caller = CoreDword(frame, 0x27973E8);
        frame = WriteCoreDword(frame, 0x27973E8, selected);
        uint parent = 0x27979E8;
        if (selector != 0)
        {
            (frame, parent) = NativeBattleWaterMatrixCommands.AllocateB65480(frame);
        }
        frame = WriteCoreDword(frame, 0x27973E8, caller);
        short sourceOffset = Operand(4);
        uint source = sourceOffset == 0 ? destination : unchecked(caller + (uint)sourceOffset);
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(ReadMatrix(parent), ReadMatrix(source));
        for (int index = 0; index < 9; index++)
        {
            frame = WriteCoreWord(frame, destination + (uint)(index * 2), unchecked((ushort)composed.Rotation[index]));
        }
        frame = WriteCoreDword(frame, destination + 20, unchecked((uint)composed.X));
        frame = WriteCoreDword(frame, destination + 24, unchecked((uint)composed.Y));
        frame = WriteCoreDword(frame, destination + 28, unchecked((uint)composed.Z));
        uint address = unchecked(CoreDword(frame, 0x27973B8) + 0x4B);
        byte flags = address >= frame.TemporaryFrame.Address && (long)address - frame.TemporaryFrame.Address < 0x180 ? frame.ReadTemporaryByte(checked((int)(address - frame.TemporaryFrame.Address))) : frame.Core.Read(address, 1)[0];
        ushort padding = (flags & 0x80) != 0 ? CoreWord(frame, CoreDword(frame, 0x27973EC) + 2) : (ushort)0;
        frame = WriteCoreWord(frame, destination + 18, padding);
        return NativeBattleScriptedCommandReads.Advance(frame, 6);

        short Operand(uint delta) => BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, delta, 2));
        byte[] Read(uint location, int count) => location >= NativeBattleScriptedEffectMemory.Address && (long)location - NativeBattleScriptedEffectMemory.Address <= NativeBattleScriptedEffectMemory.Length - count ? frame.Core.Read(location, count) : frame.Arena.Read(ArenaOffset(location, count), count);
        NativeBattleActionMatrix ReadMatrix(uint location)
        {
            byte[] rotation = Read(location, 18);
            byte[] translation = Read(unchecked(location + 20), 12);
            return new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(index * 2))).ToImmutableArray(), BinaryPrimitives.ReadInt32LittleEndian(translation), BinaryPrimitives.ReadInt32LittleEndian(translation.AsSpan(4)), BinaryPrimitives.ReadInt32LittleEndian(translation.AsSpan(8)), 4096);
        }
    }
}
