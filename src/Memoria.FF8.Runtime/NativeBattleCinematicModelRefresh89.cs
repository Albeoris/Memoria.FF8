using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Refreshes the selected embedded model with the current prepared parent matrix.</summary>
public static class NativeBattleCinematicModelRefresh89
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, Func<NativeBattleWaterExecutionFrame, ushort, uint> resolveMatrix, NativeBattleCinematicModelServices services)
    {
        uint model = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xBC, 4));
        frame = PrepareBasis(frame, resolveMatrix).Frame;
        ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(unchecked(model + 2), 2), 2));
        frame = frame with { Arena = frame.Arena.Write(ArenaOffset(unchecked(model + 2), 2), BitConverter.GetBytes((ushort)(flags & 0xFFFD))) };
        if (CoreDword(frame, 0x27973B8) != unchecked(frame.TemporaryFrame.Address + 0x100))
        {
            throw new NotSupportedException("Native embedded model refresh requires its actual current callback control binding.");
        }
        frame = NativeBattleCinematicModel.AdvanceAndCompose(frame, model, services);
        byte nextFlags = frame.Arena.Read(ArenaOffset(unchecked(model + 2), 1), 1)[0];
        frame = frame with { Arena = frame.Arena.Write(ArenaOffset(unchecked(model + 2), 1), [(byte)(nextFlags | 2)]) };
        return NativeBattleScriptedCommandReads.Advance(frame, 2);
    }

    public static (NativeBattleWaterExecutionFrame Frame, NativeBattleActionMatrix Matrix) PrepareBasis(NativeBattleWaterExecutionFrame frame, Func<NativeBattleWaterExecutionFrame, ushort, uint> resolveMatrix)
    {
        ArgumentNullException.ThrowIfNull(resolveMatrix);
        uint source = resolveMatrix(frame, SlotWord(frame, 0x9C));
        (byte[] raw, bool[] known) = frame.Core.ReadSnapshot(source, 32);
        byte[] rotation = frame.Core.Read(source, 18);
        NativeBattleActionMatrix basis = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(index * 2))).ToImmutableArray(), unchecked((int)CoreDword(frame, source + 20)), unchecked((int)CoreDword(frame, source + 24)), unchecked((int)CoreDword(frame, source + 28)), 4096);
        NativeBattleVector point = new(unchecked((short)SlotWord(frame, 0x94)), unchecked((short)SlotWord(frame, 0x96)), unchecked((short)SlotWord(frame, 0x98)));
        NativeBattleFullMacVertex translated = NativeBattleGfMatrices.TransformMac460840(basis, point);
        int x = unchecked((short)SlotWord(frame, 0x8C)) << 4;
        int y = unchecked((short)SlotWord(frame, 0x8E)) << 4;
        int z = unchecked((short)SlotWord(frame, 0x90)) << 4;
        frame = frame.WriteTemporaryDword(0xD4, unchecked((uint)x)).WriteTemporaryDword(0xD8, unchecked((uint)y)).WriteTemporaryDword(0xDC, unchecked((uint)z)).WriteTemporary(0xE0, raw);
        bool[] temporaryKnown = frame.TemporaryFrame.Known.ToArray();
        known.CopyTo(temporaryKnown, 0xE0);
        frame = frame with { TemporaryFrame = frame.TemporaryFrame with { Known = temporaryKnown.ToImmutableArray() } };
        NativeBattleActionMatrix scaled = NativeBattlePoseMatrices.Scale56BEF0(basis, x, y, z);
        for (int index = 0; index < 9; index++)
        {
            frame = frame.WriteTemporaryWord(0xE0 + index * 2, unchecked((ushort)scaled.Rotation[index]));
        }
        return (frame, scaled with { X = translated.X, Y = translated.Y, Z = translated.Z });
    }
}
