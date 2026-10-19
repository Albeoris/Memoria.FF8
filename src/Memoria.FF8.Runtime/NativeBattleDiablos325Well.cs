using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDiablos325WellDraw(NativeBattleEffectMemory Memory, NativeBattleGfGeometryBatch Packets);

public static class NativeBattleDiablos325Well
{
    public static NativeBattleActionMatrix Matrix65473A(ushort counter, int centerZ, NativeBattleActionMatrix view)
    {
        if (counter is < 31 or >= 91)
        {
            throw new ArgumentOutOfRangeException(nameof(counter));
        }
        int age = counter - 31;
        short scale = checked((short)(34 + (age * 4096 / 60)));
        NativeBattleActionMatrix local = new([scale, 0, 0, 0, scale, 0, 0, 0, scale], 0, age < 30 ? -16384 : -8192, centerZ, 4096);
        return NativeBattleGfMatrices.Compose56C2F0(view, local);
    }

    public static NativeBattleDiablos325WellDraw Emit65473A(NativeBattleDiablos325Frame frame, ushort counter, NativeBattleCameraProjection projection)
    {
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        ImmutableArray<byte> geometry = frame.Effect.Binding.Diablos325Resources?.WellGeometry ?? throw new NotSupportedException("Native Diablos well requires its original geometry.");
        if (geometry.IsDefaultOrEmpty)
        {
            throw new NotSupportedException("Native Diablos well requires its original geometry bytes.");
        }
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Diablos well requires its current view.");
        NativeBattleActionMatrix matrix = Matrix65473A(counter, state.CenterZ51D4, view);
        NativeBattleMeshPacketContext context = new(0, null, null, null, null, null, 0, 17, 2);
        NativeBattleGfGeometryBatch batch = NativeBattleGfGeometryPackets.EmitBatch(geometry, 0, default, matrix, projection, context, null);
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Diablos well requires its retained renderer controls.");
        NativeBattleModelRendererRegisters registers = process.ModelRendererRegisters ?? new(null, null, null, null, null);
        process = process with { ModelRendererRegisters = registers with { FarRed = null, FarGreen = null, FarBlue = null } };
        return new(frame.Memory with { PrimitiveProcess = process }, batch);
    }
}
