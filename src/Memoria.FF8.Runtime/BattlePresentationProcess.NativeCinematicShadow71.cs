using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) ExecuteNativeCinematicShadow71(NativeBattleWaterExecutionFrame frame, NativeBattleGraphicsState graphics, NativeBattleWaterSlotBank bank)
    {
        NativeBattleCinematicShadow71Services services = new(
            address => graphics.ActorModels.Get(address).Flags0,
            (address, flags) =>
            {
                BattleModelOwner index = address;
                graphics = graphics with { ActorModels = graphics.ActorModels.Set(index, graphics.ActorModels.Get(index) with { Flags0 = flags }) };
            },
            (current, actorAddress, ordering, shift, output) =>
            {
                NativeBattleGraphicsModel actor = graphics.ActorModels.Get(actorAddress);
                NativeBattleEffectMemory memory = RequireWaterMemory();
                NativeBattleGraphicsDefinition native = this.resources.NativeGraphics ?? throw new NotSupportedException("Native explicit shadow requires its current graphics definition.");
                NativeBattleFileStaging staging = NativeFileStaging ?? throw new NotSupportedException("Native explicit shadow requires its current staging owner.");
                NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native explicit shadow requires its current camera projection.");
                NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Native explicit shadow requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native explicit shadow requires its current color registers."));
                NativeBattlePilotVisualIdentity identity = NativeBattleCinematicVisualIdentity.Identity(graphics.OriginalEffect!.Binding.Effect, current, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank)) with { ModelOwner = actorAddress };
                NativeBattleShadowDrawStep shadow = NativeBattleShadowDrawing.Prepare5088A0(staging, registers, actor, projection, memory.ScriptedGraphics ?? throw new NotSupportedException("Native explicit shadow requires its current depth controls."), native.Rules.ShadowSource ?? throw new NotSupportedException("Native explicit shadow requires its original vector source."), native.PrimitiveCapabilities?.GouraudAlpha ?? throw new NotSupportedException("Native explicit shadow requires its current primitive capabilities."), OtBucket(ordering), shift, identity);
                staging = NativeBattleTemporaryMemory.PublishView(shadow.Staging, shadow.TemporaryAddress, shadow.Temporary78, 0x78);
                NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(staging, 0x78);
                NativeEffectMemory = memory with { Arena = current.Arena, ScriptedEffects = current.Core, GeometryRegisters = shadow.Registers.Geometry, ScriptedColorRegisters = shadow.Registers.Colors };
                var written = AppendNativeCinematicPackets(current, output, null, shadow.Batch, graphics);
                graphics = written.Graphics;
                return new(written.Frame, unchecked(output + (uint)shadow.Batch.AllocationBytes));
            });
        frame = NativeBattleCinematicShadow71.Execute(frame, services);
        return (frame, graphics);
    }
}
