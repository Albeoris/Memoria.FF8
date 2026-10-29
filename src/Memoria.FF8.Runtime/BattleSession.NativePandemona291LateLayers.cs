using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattlePandemona291Frame Frame, NativeBattleGraphicsState Graphics) DrawNativePandemona291LateLayers(NativeBattlePandemona291Frame frame, NativeBattleActionMatrix parent, ImmutableArray<NativeBattlePandemona291LateLayerRequest> requests, NativeBattleGraphicsState graphics)
    {
        NativeBattlePandemona291Resources resources = frame.Effect.Binding.Pandemona291Resources!;
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Pandemona late layers require their actual projection.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Pandemona late layers require their authenticated color arithmetic.");
        foreach (NativeBattlePandemona291LateLayerRequest request in requests)
        {
            NativeBattlePandemona291State state = frame.Memory.Pandemona291!;
            NativeBattlePandemona291Animation animation = (state.Animations ?? throw new NotSupportedException("Native Pandemona late layers require their rewritten sources.")).Single(current => current.Address == request.Source);
            NativeBattleStaticMeshResource geometry = resources.LateLayerGeometry.Single(current => current.Address == request.Source);
            NativeBattleScriptedColorRegisters registers = frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Pandemona late layers require their canonical GTE owner.");
            NativeBattlePandemona291LateLayer prepared = NativeBattlePandemona291LateLayers.Prepare6F1C10(animation, resources.TrailTrigonometry, parent, projection.View ?? throw new NotSupportedException("Native Pandemona requires its actual current view."), request, input => registers = registers with { NormalInput = input, NormalInputKnown = null });
            NativeBattleEffectArena arena = frame.Memory.Arena!;
            uint buffer = state.ModelBuffer25562F4 ?? throw new NotSupportedException("Native Pandemona late layers require their original work buffer.");
            int scratch = NativeBattleWaterExecutionFields.ArenaOffset(unchecked(buffer + 0x31D4), prepared.Vertices.Length);
            arena = arena.Write(scratch, prepared.Vertices.AsSpan());
            NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Native Pandemona late layers require their actual projection controls.");
            short zsf4 = controls.Zsf4 ?? throw new NotSupportedException("Native Pandemona late layers require their current ZSF4.");
            NativeBattleGfGeometryBatch batch = NativeBattlePandemona291LateLayers.Emit6F1E10(geometry.Bytes, prepared, projection, colors, zsf4, offset =>
            {
                byte[] xyz = arena.Read(scratch + offset, 6);
                return new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4)));
            }, offset => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(scratch + offset + 6, 2)), offset => registers = registers with { NormalInput = arena.Read(scratch + offset, 8).ToImmutableArray(), NormalInputKnown = null }, flags => registers = registers with { Flags = flags }, point =>
            {
                NativeBattleModuleDepthCueVertex projected = NativeBattleModuleProjection.ProjectWithCue45FAA0(prepared.Matrix, point, projection, controls.Dqa, controls.Dqb);
                registers = registers with { Flags = projected.Projected.Flags };
                return projected.Projected;
            }, NativePandemona291VisualIdentity(frame, 300 + resources.LateLayerGeometry.IndexOf(geometry)));
            NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Pandemona late layers require their canonical far-color owner.");
            NativeBattleModelRendererRegisters renderer = process.ModelRendererRegisters ?? new(null, null, null, null, null);
            renderer = renderer.PublishModelColor(0);
            registers = registers with { Mac = [null, null, null], Ir = [null, null, null], Material = null, Rgb = [null, null, null], Flags = null };
            frame = frame with { Memory = frame.Memory with { Arena = arena, ScriptedColorRegisters = registers, PrimitiveProcess = process with { ModelRendererRegisters = renderer } } };
            uint output = state.PacketCursor25562DC ?? throw new NotSupportedException("Native Pandemona late layers require their actual packet cursor.");
            NativeBattlePrimitiveFrame primitiveFrame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Pandemona late layers require their ordering table.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, NativeBattleWaterExecutionFields.ArenaOffset(output, 0), primitiveFrame.Pending, batch);
            frame = frame with { Memory = written.Memory with { Pandemona291 = frame.Memory.Pandemona291! with { PacketCursor25562DC = unchecked(output + (uint)batch.AllocationBytes) } } };
            graphics = graphics with { PrimitiveFrame = primitiveFrame with { Pending = written.Ordering } };
        }
        return (frame, graphics);
    }
}
