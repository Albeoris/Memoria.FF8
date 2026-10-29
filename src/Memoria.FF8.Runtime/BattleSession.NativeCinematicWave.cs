using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicWave(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleCinematicResources resources, NativeBattleGraphicsState graphics)
    {
        NativeBattleActionMatrix? matrix = null;
        NativeBattleCinematicWaveServices services = new((current, selector) =>
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            int index = NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank);
            int selected = NativeBattleWaterSlots.LookupB65370(current.Arena, bank, index, current.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
            return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(current.Arena, selected));
        }, current =>
        {
            matrix = NativeBattleCinematicAnchor.Prepare(current, NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength));
            return current;
        }, (current, point) =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Native cinematic wave requires its retained depth-cue controls.");
            NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native cinematic wave requires its actual projection controls.");
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native cinematic wave requires its retained packed vector input.");
            NativeEffectMemory = memory with { ScriptedColorRegisters = NativeBattleCinematicRibbonInput.Capture(current, current.TemporaryFrame.Address + 0xD0, registers) };
            NativeBattleModuleDepthCueVertex projected = NativeBattleModuleProjection.ProjectWithCue45FAA0(matrix ?? throw new InvalidOperationException("Native cinematic wave basis has not been prepared."), point, projection, controls.Dqa, controls.Dqb);
            return new(current.WriteTemporaryDword(0xFC, projected.Projected.Flags), projected.Projected.Screen, projected.Projected.Depth >> 2) { PilotWorld = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix!, point), projection) };
        }, sector => resources.ReadStatic(unchecked(0x1877508u + (uint)sector), 1)[0], OtBucket, (current, output, batch) =>
        {
            var written = AppendNativeCinematicPackets(current, output, null, batch, graphics, 1);
            graphics = written.Graphics;
            return written.Frame;
        });
        NativeBattleWaterSlotBank visualBank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, resourceLength);
        NativeBattlePilotVisualIdentity identity = NativeBattleCinematicVisualIdentity.Identity(graphics.OriginalEffect!.Binding.Effect, frame, visualBank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, visualBank));
        return (NativeBattleCinematicWave.Draw(frame, services, identity), graphics);
    }
}
