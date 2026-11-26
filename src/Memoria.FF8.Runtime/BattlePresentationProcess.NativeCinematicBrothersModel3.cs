using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicBrothersModel3(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleCinematicModelServices modelServices, NativeBattleGraphicsState graphics, bool hideModel = false, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame>? presentGesture = null, ReadOnlyMemory<byte>? finisherGeometry = null)
    {
        if (hideModel)
        {
            return (PrepareModel(frame), graphics);
        }
        NativeBattleCinematicBrothersModelServices services = new(current =>
        {
            var primary = DrawNativeCinematicModel(current, resourceLength, modelServices, graphics, PrepareModel, finisherGeometry);
            graphics = primary.Graphics;
            return primary.Frame;
        }, (current, selector) =>
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            int selected = NativeBattleWaterSlots.LookupB65370(current, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank), selector);
            return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(current.Arena, selected));
        }, (current, address) =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Brothers fan requires its canonical V0 owner.");
            NativeEffectMemory = memory with { ScriptedColorRegisters = NativeBattleCinematicRibbonInput.Capture(current, address, registers) };
            return current;
        }, (current, matrix, address) =>
        {
            NativeBattleScriptedGraphicsMemory controls = RequireWaterMemory().ScriptedGraphics ?? throw new NotSupportedException("Native Brothers fan requires its retained depth-cue controls.");
            NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Brothers fan requires its current projection controls.");
            int offset = checked((int)(address - current.TemporaryFrame.Address));
            NativeBattleVector point = NativeBattleScriptedMeshPreparation.Vector(current.ReadTemporary(offset, 6));
            NativeBattleModuleDepthCueVertex result = NativeBattleModuleProjection.ProjectWithCue45FAA0(matrix, point, projection, controls.Dqa, controls.Dqb);
            return new(current, result.Projected.Screen, result.Cue, result.Projected.Flags) { PilotWorld = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, point), projection) };
        }, () => 0, (current, output, ordering, batch) =>
        {
            var written = AppendNativeCinematicPackets(current, output, ordering, batch, graphics);
            graphics = written.Graphics;
            return written.Frame;
        });
        NativeBattleWaterSlotBank visualBank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, resourceLength);
        services = services with { PilotIdentity = NativeBattleCinematicVisualIdentity.Identity(graphics.OriginalEffect!.Binding.Effect, frame, visualBank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, visualBank)) };
        return (NativeBattleCinematicBrothersModel3.Draw(frame, services), graphics);

        NativeBattleWaterExecutionFrame PrepareModel(NativeBattleWaterExecutionFrame current)
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            current = NativeBattleCinematicModelRefresh89.PrepareBasis(current, (selected, selector) => NativeBattleWaterMatrixCommands.ResolveB65520(selected, bank, selector)).Frame;
            if (CoreDword(current, 0x27973B8) != unchecked(current.TemporaryFrame.Address + 0x100))
            {
                throw new NotSupportedException("Native Brothers model advance requires its current callback pause binding.");
            }
            if (finisherGeometry.HasValue)
            {
                current = ScaleMinotaurFinisherBody(current);
            }
            current = NativeBattleCinematicModel.PrepareDraw(current, modelServices);
            return presentGesture is null ? current : presentGesture(current);
        }
    }
}
