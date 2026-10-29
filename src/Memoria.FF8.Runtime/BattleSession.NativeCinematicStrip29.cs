using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicStrip29(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleCinematicResources resources, NativeBattleGraphicsState graphics)
    {
        NativeBattleActionMatrix? matrix = null;
        NativeBattleCinematicStrip29Services services = new((current, texture) => NativeBattleWaterTextureMetadata.SelectTPageB665C0(current, resources, texture), (current, texture) => NativeBattleWaterTextureMetadata.ReadClutB66640(current, resources, texture), current =>
        {
            NativeBattleCinematicScaledBasisResult prepared = NativeBattleCinematicScaledBasis.Prepare(current, resourceLength);
            matrix = prepared.Matrix;
            return prepared.Frame;
        }, (current, point) =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Native cinematic strip requires its current depth-cue controls.");
            NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native cinematic strip requires its current projection controls.");
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native cinematic strip requires its retained packed vector input.");
            NativeEffectMemory = memory with { ScriptedColorRegisters = NativeBattleCinematicRibbonInput.Capture(current, current.TemporaryFrame.Address + 0xC0, registers) };
            NativeBattleModuleDepthCueVertex projected = NativeBattleModuleProjection.ProjectWithCue45FAA0(matrix ?? throw new InvalidOperationException("Native cinematic strip basis has not been prepared."), point, projection, controls.Dqa, controls.Dqb);
            current = current.WriteTemporaryDword(0xF8, projected.Cue).WriteTemporaryDword(0xFC, projected.Projected.Flags);
            return new(current, projected.Projected.Screen, projected.Projected.Depth >> 2) { PilotWorld = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix!, point), projection) };
        }, (current, output, ordering, batch) =>
        {
            var written = AppendNativeCinematicPackets(current, output, ordering, batch, graphics);
            graphics = written.Graphics;
            return written.Frame;
        });
        NativeBattleWaterSlotBank visualBank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, resourceLength);
        NativeBattlePilotVisualIdentity identity = NativeBattleCinematicVisualIdentity.Identity(graphics.OriginalEffect!.Binding.Effect, frame, visualBank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, visualBank));
        return (NativeBattleCinematicStrip29.Draw(frame, services, identity), graphics);
    }
}
