using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicAnchor
{
    public static NativeBattleActionMatrix Prepare(NativeBattleWaterExecutionFrame frame, NativeBattleWaterSlotBank bank)
    {
        uint address = NativeBattleWaterMatrixCommands.ResolveB65520(frame, bank, SlotWord(frame, 0x9C));
        byte[] bytes = frame.Core.Read(address, 18);
        ImmutableArray<short> rotation = Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 2))).ToImmutableArray();
        NativeBattleActionMatrix matrix = new(rotation, unchecked((int)CoreDword(frame, address + 20)), unchecked((int)CoreDword(frame, address + 24)), unchecked((int)CoreDword(frame, address + 28)), 4096);
        NativeBattleVector cached = new(unchecked((short)SlotWord(frame, 0x94)), unchecked((short)SlotWord(frame, 0x96)), unchecked((short)SlotWord(frame, 0x98)));
        NativeBattleFullMacVertex translated = NativeBattleGfMatrices.TransformMac460840(matrix, cached);
        matrix = matrix with { X = translated.X, Y = translated.Y, Z = translated.Z };
        return matrix;
    }

    public static NativeBattleCinematicSpriteProjection Project(NativeBattleWaterExecutionFrame frame, NativeBattleWaterSlotBank bank, NativeBattleCameraProjection projection, short dqa, int dqb)
    {
        NativeBattleActionMatrix matrix = Prepare(frame, bank);
        byte[] point = frame.Core.Read(0x27971E4, 6);
        NativeBattleModuleDepthCueVertex projected = NativeBattleModuleProjection.ProjectWithCue45FAA0(matrix, NativeBattleScriptedMeshPreparation.Vector(point), projection, dqa, dqb);
        short x = unchecked((short)(unchecked((short)projected.Projected.Screen) >> 3));
        short y = unchecked((short)(unchecked((short)(projected.Projected.Screen >> 16)) >> 3));
        frame = frame.WriteTemporaryWord(0xD0, unchecked((ushort)x)).WriteTemporaryWord(0xD2, unchecked((ushort)y)).WriteTemporaryDword(0xFC, projected.Projected.Flags);
        return new(frame, unchecked((int)projected.Cue));
    }
}
