using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Builds a retained camera-relative matrix from two current cached positions.</summary>
public static class NativeBattleCinematicMatrixAimCB
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, short, uint> resolveSlot)
    {
        uint from = Resolve(2);
        uint to = Resolve(4);
        uint destination;
        (frame, destination) = NativeBattleWaterMatrixCommands.AllocateB65480(frame);
        NativeBattleCinematicRibbonBasisResult basis = NativeBattleCinematicRibbonBasis.Prepare(frame, Position(from), Position(to), false);
        frame = basis.Frame;
        for (int index = 0; index < 9; index++)
        {
            frame = WriteCoreWord(frame, unchecked(destination + (uint)(index * 2)), unchecked((ushort)basis.Matrix.Rotation[index]));
        }
        uint flagsAddress = unchecked(CoreDword(frame, 0x27973B8) + 0x4B);
        byte flags = flagsAddress >= frame.TemporaryFrame.Address && (long)flagsAddress - frame.TemporaryFrame.Address < 0x180 ? frame.ReadTemporaryByte(checked((int)(flagsAddress - frame.TemporaryFrame.Address))) : frame.Core.Read(flagsAddress, 1)[0];
        uint selected = (flags & 0x10) != 0 ? to : from;
        frame = WriteCoreDword(frame, destination + 20, unchecked((uint)(int)SignedWord(selected + 0x94)));
        frame = WriteCoreDword(frame, destination + 24, unchecked((uint)(int)SignedWord(selected + 0x96)));
        frame = WriteCoreDword(frame, destination + 28, unchecked((uint)(int)SignedWord(selected + 0x98)));
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(Matrix(0x27979E8), Matrix(destination));
        for (int index = 0; index < 9; index++)
        {
            frame = WriteCoreWord(frame, unchecked(destination + (uint)(index * 2)), unchecked((ushort)composed.Rotation[index]));
        }
        frame = WriteCoreDword(frame, destination + 20, unchecked((uint)composed.X));
        frame = WriteCoreDword(frame, destination + 24, unchecked((uint)composed.Y));
        frame = WriteCoreDword(frame, destination + 28, unchecked((uint)composed.Z));
        frame = WriteCoreWord(frame, destination + 18, 0);
        return NativeBattleScriptedCommandReads.Advance(frame, 6);

        uint Resolve(uint offset)
        {
            short selector = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, offset, 2));
            return selector == 0 ? CoreDword(frame, 0x27973E8) : resolveSlot(frame, selector);
        }
        short SignedWord(uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        NativeBattleVector Position(uint address) => new(SignedWord(address + 0x94), SignedWord(address + 0x96), SignedWord(address + 0x98));
        NativeBattleActionMatrix Matrix(uint address)
        {
            byte[] rotation = frame.Core.Read(address, 18);
            return new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(index * 2))).ToImmutableArray(), unchecked((int)CoreDword(frame, address + 20)), unchecked((int)CoreDword(frame, address + 24)), unchecked((int)CoreDword(frame, address + 28)), 4096);
        }
    }
}
