using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect321SoundProjection(NativeBattleEffectMemory Memory, NativeBattleEffectArena Output12, byte Pan);

public static class NativeBattleEffect321PositionalSound
{
    public static NativeBattleEffect321SoundProjection Project5011B0(NativeBattleEffectMemory memory, NativeBattlePackedMatrix32 view, NativeBattleCameraProjection projection, NativeBattleEffectArena point8)
    {
        byte[] xyz = point8.Read(0, 6);
        (byte[] bytes, bool[] known) = point8.ReadSnapshot(0, 8);
        NativeBattleGeometryRegisters geometry = memory.GeometryRegisters ?? throw new NotSupportedException("Effect321 sound requires its current geometry registers.");
        geometry = NativeBattleGeometry.LoadMatrix(geometry, view.ReadMatrix(), true, true, null) with { Rotation20 = Enumerable.Range(0, 20).Select(index => view.Known[index] ? (byte?)view.Bytes[index] : null).ToImmutableArray() };
        NativeBattleScriptedColorRegisters colors = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect321 sound requires its current data registers.");
        NativeBattleGeometryRegisterStep step = NativeBattleGeometry.LoadVector(new(geometry, colors), 0, new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), null);
        step = step with { Colors = step.Colors with { NormalInput = bytes.ToImmutableArray(), NormalInputKnown = known.ToImmutableArray() } };
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Effect321 sound requires its current projection controls.");
        step = NativeBattleGeometry.ProjectSingle(step, projection, controls.Dqa, controls.Dqb);
        uint screen = step.Geometry.Sxy[2] ?? throw new InvalidDataException("Effect321 sound projection did not publish screen coordinates.");
        NativeBattleEffectArena output = NativeBattleEffectArena.Clear().MarkUnknown(0, 12).Write(0, BitConverter.GetBytes(screen)).Write(4, BitConverter.GetBytes(step.Colors.Ir0 ?? throw new InvalidDataException("Effect321 sound projection did not publish IR0."))).Write(8, BitConverter.GetBytes(step.Colors.Flags ?? throw new InvalidDataException("Effect321 sound projection did not publish flags.")));
        int pan = 96 * (unchecked((short)screen) - 160) / 320 + 128;
        return new(memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors }, output, checked((byte)Math.Clamp(pan, 80, 175)));
    }
}
