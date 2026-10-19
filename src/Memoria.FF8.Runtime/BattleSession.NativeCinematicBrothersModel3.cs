using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicBrothersModel3(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleCinematicModelServices modelServices, NativeBattleGraphicsState graphics)
    {
        NativeBattleCinematicBrothersModelServices services = new(current =>
        {
            var primary = DrawNativeCinematicModel(current, resourceLength, modelServices, graphics, active =>
            {
                NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(active, resourceLength);
                active = NativeBattleCinematicModelRefresh89.PrepareBasis(active, (selected, selector) => NativeBattleWaterMatrixCommands.ResolveB65520(selected, bank, selector)).Frame;
                if (CoreDword(active, 0x27973B8) != unchecked(active.TemporaryFrame.Address + 0x100))
                {
                    throw new NotSupportedException("Native Brothers model advance requires its current callback pause binding.");
                }
                return NativeBattleCinematicModel.PrepareDraw(active, modelServices);
            });
            graphics = primary.Graphics;
            return primary.Frame;
        }, (current, selector) =>
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            int selected = NativeBattleWaterSlots.LookupB65370(current.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank), current.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
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
            return new(current, result.Projected.Screen, result.Cue, result.Projected.Flags);
        }, () => 0, (current, output, ordering, batch) =>
        {
            var written = AppendNativeCinematicPackets(current, output, ordering, batch, graphics);
            graphics = written.Graphics;
            return written.Frame;
        });
        return (NativeBattleCinematicBrothersModel3.Draw(frame, services), graphics);
    }
}
