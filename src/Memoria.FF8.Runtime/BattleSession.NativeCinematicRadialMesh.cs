using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicRadialMesh(NativeBattleWaterExecutionFrame frame, bool intensity, int resourceLength, NativeBattleCinematicResources resources, NativeBattleGraphicsState graphics)
    {
        NativeBattleActionMatrix? basis = null;
        NativeBattleCinematicRadialMeshServices services = new((current, shape) => NativeBattleWaterShapeSelection.ResolveCinematic(current, resources, shape).Selected, (current, selector) =>
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            int found = NativeBattleWaterSlots.LookupB65370(current.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank), current.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
            return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(current.Arena, found));
        }, (current, palette) =>
        {
            NativeBattleCinematicTextureSelection selected = NativeBattleCinematicTextureMetadata.ResolveClutB66560(current, resources, palette);
            current = current.WriteTemporaryDword(0xF0, selected.RectangleAddress).WriteTemporaryDword(0xFC, selected.SourceAddress);
            return new(current, selected.SourceAddress);
        }, (current, scale) =>
        {
            var prepared = PrepareNativeCinematicLatticeBasis(current, resourceLength, scale);
            basis = prepared.Matrix;
            return prepared.Frame;
        }, (current, source, output) =>
        {
            NativeBattleCinematicGridProjection projected = ProjectNativeCinematicLatticeVertex(current, basis ?? throw new InvalidOperationException("Native radial mesh basis has not been prepared."), source, output);
            return projected.Frame with { Arena = projected.Frame.Arena.Write(ArenaOffset(output + 4, 2), BitConverter.GetBytes(unchecked((ushort)projected.QuarterDepth))) };
        }, (current, color, factor) =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleCinematicColorScaleResult result = NativeBattleCinematicColorScale.ScaleB66A20(color, factor, memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native radial mesh requires its color registers."));
            NativeEffectMemory = memory with { ScriptedColorRegisters = result.Registers };
            return new(current, result.Color);
        }, (current, output, ordering, batch) =>
        {
            var written = AppendNativeCinematicPackets(current, output, ordering, batch, graphics);
            graphics = written.Graphics;
            return written.Frame;
        });
        return (NativeBattleCinematicRadialMesh.Draw(frame, intensity, services), graphics);
    }
}
