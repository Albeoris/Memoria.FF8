using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicLeviathan24(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleCinematicResources resources, NativeBattleCinematicModelServices modelServices, NativeBattleGraphicsState graphics)
    {
        NativeBattleCinematicLeviathanServices services = new(modelServices, (current, shape) => NativeBattleWaterShapeSelection.ResolveCinematic(current, resources, shape).Selected, (current, selector) =>
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            int selected = NativeBattleWaterSlots.LookupB65370(current.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank), current.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
            return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(current.Arena, selected));
        }, (current, texture) => NativeBattleCinematicTextureMetadata.ResolvePixelB664A0(current, resources, texture), current =>
        {
            NativeBattleVector point = new(unchecked((short)SlotWord(current, 0x94)), unchecked((short)SlotWord(current, 0x96)), unchecked((short)SlotWord(current, 0x98)));
            return NativeBattleCinematicScaledBasis.Prepare(current, resourceLength, point).Frame;
        }, (current, source, output, matrix) =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Native cinematic model projection requires its current depth-cue controls and depth workspace.");
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native cinematic model projection requires its current packed vector and arithmetic registers.");
            NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native cinematic model projection requires its current camera controls.");
            NativeBattleCinematicLeviathanProjection drawn = NativeBattleCinematicLeviathanProjector.Project(current, source, output, matrix, projection, controls, registers);
            NativeEffectMemory = memory with { ScriptedGraphics = drawn.Graphics, ScriptedColorRegisters = drawn.Registers };
            return drawn.Frame;
        }, OtBucket, (current, output, batch) =>
        {
            var written = AppendNativeCinematicPackets(current, output, null, batch, graphics);
            graphics = written.Graphics;
            return written.Frame;
        });
        return (NativeBattleCinematicLeviathan24.Draw(frame, services), graphics);
    }
}
