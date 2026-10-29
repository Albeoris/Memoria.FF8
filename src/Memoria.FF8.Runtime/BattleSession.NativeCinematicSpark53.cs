using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicSpark53(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleGraphicsState graphics)
    {
        NativeBattleActionMatrix? basis = null;
        NativeBattleCinematicSpark53Services services = new((current, selector) =>
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            int index = NativeBattleWaterSlots.LookupB65370(current.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank), current.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
            return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(current.Arena, index));
        }, current =>
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            uint address = NativeBattleWaterMatrixCommands.ResolveB65520(current, bank, SlotWord(current, 0x9C));
            byte[] raw = current.Core.Read(address, 18);
            basis = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(index * 2))).ToImmutableArray(), unchecked((int)CoreDword(current, address + 20)), unchecked((int)CoreDword(current, address + 24)), unchecked((int)CoreDword(current, address + 28)), 4096);
            return current;
        }, current =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Native accelerated particles require retained projection controls.");
            NativeBattleScriptedColorRegisters registers = NativeBattleCinematicRibbonInput.Capture(current, current.TemporaryFrame.Address + 0xF0, memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native accelerated particles require retained packed input registers."));
            NativeBattleModuleDepthCueVertex projected = NativeBattleModuleProjection.ProjectWithCue45FAA0(basis ?? throw new InvalidOperationException("Native particle basis has not been prepared."), NativeBattleScriptedMeshPreparation.Vector(current.ReadTemporary(0xF0, 6)), NativeCamera?.Projection ?? throw new NotSupportedException("Native accelerated particles require the current camera projection."), controls.Dqa, controls.Dqb);
            current = current.WriteTemporaryDword(0xFC, projected.Projected.Flags);
            NativeEffectMemory = memory with { ScriptedColorRegisters = registers };
            return new(current, projected.Projected.Screen, projected.Projected.Depth >> 2) { PilotWorld = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(basis!, NativeBattleScriptedMeshPreparation.Vector(current.ReadTemporary(0xF0, 6))), NativeCamera!.Projection!) };
        }, (current, output, ordering, batch) =>
        {
            var written = AppendNativeCinematicPackets(current, output, ordering, batch, graphics);
            graphics = written.Graphics;
            return written.Frame;
        });
        NativeBattleWaterSlotBank visualBank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, resourceLength);
        NativeBattlePilotVisualIdentity identity = NativeBattleCinematicVisualIdentity.Identity(graphics.OriginalEffect!.Binding.Effect, frame, visualBank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, visualBank));
        return (NativeBattleCinematicSpark53.Draw(frame, services, identity), graphics);
    }
}
