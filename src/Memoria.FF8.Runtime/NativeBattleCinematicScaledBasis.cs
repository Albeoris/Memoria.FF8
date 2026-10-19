using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicScaledBasisResult(NativeBattleWaterExecutionFrame Frame, NativeBattleActionMatrix Matrix);

/// <summary>Scale the selected basis while retaining the original untranslated matrix in scratch.</summary>
public static class NativeBattleCinematicScaledBasis
{
    public static NativeBattleCinematicScaledBasisResult Prepare(NativeBattleWaterExecutionFrame current, int resourceLength, NativeBattleVector? anchor = null)
    {
        NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
        uint address = NativeBattleWaterMatrixCommands.ResolveB65520(current, bank, SlotWord(current, 0x9C));
        (byte[] raw, bool[] known) = current.Core.ReadSnapshot(address, 32);
        byte[] rotation = current.Core.Read(address, 18);
        NativeBattleActionMatrix basis = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(index * 2))).ToImmutableArray(), unchecked((int)CoreDword(current, address + 20)), unchecked((int)CoreDword(current, address + 24)), unchecked((int)CoreDword(current, address + 28)), 4096);
        NativeBattleVector global = anchor ?? new(unchecked((short)CoreWord(current, 0x27971E4)), unchecked((short)CoreWord(current, 0x27971E6)), unchecked((short)CoreWord(current, 0x27971E8)));
        NativeBattleFullMacVertex translated = NativeBattleGfMatrices.TransformMac460840(basis, global);
        int x = unchecked((short)SlotWord(current, 0x8C)) << 4;
        int y = unchecked((short)SlotWord(current, 0x8E)) << 4;
        int z = unchecked((short)SlotWord(current, 0x90)) << 4;
        current = current.WriteTemporaryDword(0xD4, unchecked((uint)x)).WriteTemporaryDword(0xD8, unchecked((uint)y)).WriteTemporaryDword(0xDC, unchecked((uint)z)).WriteTemporary(0xE0, raw);
        bool[] temporaryKnown = current.TemporaryFrame.Known.ToArray();
        known.CopyTo(temporaryKnown, 0xE0);
        current = current with { TemporaryFrame = current.TemporaryFrame with { Known = temporaryKnown.ToImmutableArray() } };
        NativeBattleActionMatrix scaled = NativeBattlePoseMatrices.Scale56BEF0(basis, x, y, z);
        for (int index = 0; index < 9; index++)
        {
            current = current.WriteTemporaryWord(0xE0 + index * 2, unchecked((ushort)scaled.Rotation[index]));
        }
        NativeBattleActionMatrix matrix = scaled with { X = translated.X, Y = translated.Y, Z = translated.Z };
        return new(current, matrix);
    }
}
