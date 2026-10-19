using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDiablos325Ground
{
    public static NativeBattleDiablos325BodyDraw Draw659730(NativeBattleDiablos325Frame frame, ushort counter, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Diablos sink geometry requires its current original resources.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Diablos sink geometry requires its current camera basis.");
        NativeBattleActionMatrix local = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], state.CenterX51D8, NativeBattleDiablos325Recipients.Height654E7E(counter - 368) - 4096, state.CenterZ51D4, 4096);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(view, local);
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Diablos sink geometry requires its current renderer controls.");
        process = process.PublishTemporary5082B0Matrix(matrix);
        NativeBattleGfGeometryBatch first = NativeBattleGfGeometryPackets.EmitBatch(NativeBattleDiablos325Wings.ReadGeometry(arena, checked(state.ModelBuffer5208 + 0x397CC)), 0, default, matrix, projection, new(0, null, null, null, null, null, 0, 17, 2), colors);
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Native Diablos sink geometry requires its current depth averaging controls.");
        NativeBattleEsunaMeshContext context = new(0x30, null, null, -128, 0, (ushort)(((counter - 368) << 4) & 127), new(0, 0, 256, 256), new(0, 128, 256, 128), 17, 2);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = first.Attempts.ToBuilder();
        int allocated = first.AllocationBytes;
        foreach (uint offset in new uint[] { 0x3505C, 0x35F7C })
        {
            ImmutableArray<byte> geometry = NativeBattleDiablos325Wings.ReadGeometry(arena, checked(state.ModelBuffer5208 + offset));
            NativeBattleGfGeometryBatch batch = NativeBattlePandemona291Surface.EmitGeometry701DD0(geometry, matrix, context, projection, colors, controls.Zsf4 ?? throw new NotSupportedException("Native Diablos sink geometry requires its current ZSF4 control."), address =>
            {
                ReadOnlySpan<byte> vertex = geometry.AsSpan().Slice(checked(8 + address), 6);
                return new(System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(vertex), System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(vertex[2..]), System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(vertex[4..]));
            }, zsf3: controls.Zsf3);
            attempts.AddRange(batch.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(allocated + attempt.ByteOffsetRelative) }));
            allocated = checked(allocated + batch.AllocationBytes);
        }
        return new(frame.Memory with { PrimitiveProcess = process }, new(attempts.ToImmutable(), allocated));
    }
}
