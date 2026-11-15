namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleShockwavePulsar260Frame Frame, NativeBattleGraphicsState Graphics) DrawNativeShockwavePulsar260ForeignBody(NativeBattleShockwavePulsar260Frame frame, NativeBattleGraphicsState graphics, NativeBattleShockwavePulsar260DrawBody draw, NativeBattleSummonColors colors, NativeBattleForeignModelGeometry geometry)
    {
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(draw.Actor);
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Host body context requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Host body context requires its current projection.");
        NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Host body context requires its physical temporary memory."), 92);
        NativeBattleTemporaryAllocation scratch = NativeBattleTemporaryMemory.Allocate5082B0(temporary.Staging, 24);
        NativeBattleShockwavePulsar260BodyPreparationStep prepared = NativeBattleShockwavePulsar260BodyPreparation.Prepare693170(NativeShockwavePulsar260TemporaryView(scratch.Staging, temporary.Address, 92), geometry.World, camera.CapturePackedView1D97778(), draw.Rotation8, draw.Offset8, draw.Scale, scratch.Address, NativeShockwavePulsar260TemporaryView(scratch.Staging, 0x1D969A8, 4), model.Color40 ?? throw new NotSupportedException("Host body context requires its current model color."), uint.MaxValue);
        NativeBattleEffectArena context = NativeBattleModelContext44.CopyContext(prepared.Temporary92, 0x30, NativeBattleEffectArena.CreateUnknown(44), 0);
        NativeBattlePilotVisualIdentity identity = draw.PilotIdentity ?? NativeBattlePilotVisualIdentity.ForModel(frame.Effect.Binding.Effect, 0x693170, draw.Actor);
        NativeBattleModelContext44Step drawn = NativeBattleForeignModelDrawing.DrawContext44(frame.Memory, context, geometry, prepared.Matrix, NativeShockwavePulsar260TemporaryView(scratch.Staging, scratch.Address, 24), projection, colors, definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Host body context requires its renderer capability."), identity);
        NativeBattleEffectArena finalTemporary = NativeBattleModelContext44.CopyContext(drawn.Context44, 0, prepared.Temporary92, 0x30);
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(scratch.Staging, scratch.Address, drawn.ProjectedScratch, 24);
        staging = NativeBattleModelContext44.PublishTemporary(staging, temporary.Address, finalTemporary);
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 24);
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 92);
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Host body context requires its COMMON ordering table.");
        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(drawn.Memory.PrimitiveProcess ?? throw new NotSupportedException("Host body context requires its COMMON packet owner."), primitive.Pending, drawn.Batch);
        return (frame with { Memory = drawn.Memory with { PrimitiveProcess = written.Process }, Staging = staging }, graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } });
    }
}
