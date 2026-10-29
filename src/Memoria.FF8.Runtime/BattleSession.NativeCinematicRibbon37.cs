using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicRibbon37(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleGraphicsState graphics)
    {
        NativeBattlePrimitiveFrame primitiveFrame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native cinematic ribbon requires its current ordering table.");
        using NativeBattleLogicalOrderingTransaction? orderingTransaction = primitiveFrame.Pending.Transaction is null ? new(primitiveFrame.Pending) : null;
        if (orderingTransaction is not null)
        {
            graphics = graphics with { PrimitiveFrame = primitiveFrame with { Pending = orderingTransaction.View } };
        }
        NativeBattleCinematicRibbon37Services services = new((current, selector) =>
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            int index = NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank);
            int selected = NativeBattleWaterSlots.LookupB65370(current.Arena, bank, index, current.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
            return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(current.Arena, selected));
        }, PublishInput, (current, first, second, output) =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native cinematic ribbon requires its retained color registers.");
            NativeBattleCinematicRibbonRampResult result = NativeBattleCinematicRibbonWork.BuildColorRamp(current, registers, first, second, output);
            NativeEffectMemory = memory with { ScriptedColorRegisters = result.Registers };
            return result.Frame;
        }, current =>
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            uint address = NativeBattleWaterMatrixCommands.ResolveB65520(current, bank, SlotWord(current, 0x9C));
            byte[] rotation = current.Core.Read(address, 18);
            NativeBattleActionMatrix basis = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(index * 2))).ToImmutableArray(), unchecked((int)CoreDword(current, address + 20)), unchecked((int)CoreDword(current, address + 24)), unchecked((int)CoreDword(current, address + 28)), 4096);
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleCinematicRibbonInputResult result = NativeBattleCinematicRibbonInput.PrepareParent(basis, memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native cinematic ribbon requires its retained V0 registers."));
            NativeEffectMemory = memory with { ScriptedColorRegisters = result.Registers };
            return new(current, result.Matrix);
        }, (current, matrix, address) =>
        {
            current = PublishInput(current, address);
            NativeBattleScriptedGraphicsMemory controls = RequireWaterMemory().ScriptedGraphics ?? throw new NotSupportedException("Native cinematic ribbon requires its retained depth-cue controls.");
            NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native cinematic ribbon requires its actual projection controls.");
            NativeBattleVector point = NativeBattleScriptedMeshPreparation.Vector(current.Arena.Read(ArenaOffset(address, 6), 6));
            NativeBattleModuleDepthCueVertex result = NativeBattleModuleProjection.ProjectWithCue45FAA0(matrix, point, projection, controls.Dqa, controls.Dqb);
            return new(current, result.Projected.Screen, result.Projected.Depth, result.Projected.Flags) { PilotWorld = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, point), projection) };
        }, (current, output, ordering, batch) =>
        {
            var written = AppendNativeCinematicPackets(current, output, ordering, batch, graphics);
            graphics = written.Graphics;
            return written.Frame;
        });
        NativeBattleWaterSlotBank visualBank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, resourceLength);
        NativeBattlePilotVisualIdentity identity = NativeBattleCinematicVisualIdentity.Identity(graphics.OriginalEffect!.Binding.Effect, frame, visualBank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, visualBank));
        frame = NativeBattleCinematicRibbon37.Draw(frame, services, identity);
        if (orderingTransaction is not null)
        {
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame! with { Pending = orderingTransaction.Freeze() } };
        }
        return (frame, graphics);

        NativeBattleWaterExecutionFrame PublishInput(NativeBattleWaterExecutionFrame current, uint address)
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native cinematic ribbon requires its retained V0 registers.");
            NativeEffectMemory = memory with { ScriptedColorRegisters = NativeBattleCinematicRibbonInput.Capture(current, address, registers) };
            return current;
        }
    }
}
