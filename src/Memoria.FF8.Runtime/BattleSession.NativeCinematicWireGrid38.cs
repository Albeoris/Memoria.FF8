using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicWireGrid38(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleCinematicResources resources, NativeBattleGraphicsState graphics)
    {
        NativeBattleActionMatrix? basis = null;
        NativeBattleActionMatrix? matrix = null;
        byte[]? raw = null;
        bool[]? known = null;
        NativeBattleCinematicWireGrid38Services services = new((current, selector) => NativeBattleWaterShapeSelection.ResolveCinematic(current, resources, selector).Selected, current =>
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            uint address = NativeBattleWaterMatrixCommands.ResolveB65520(current, bank, SlotWord(current, 0x9C));
            (raw, known) = current.Core.ReadSnapshot(address, 20);
            byte[] rotation = current.Core.Read(address, 18);
            basis = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(index * 2))).ToImmutableArray(), unchecked((int)CoreDword(current, address + 20)), unchecked((int)CoreDword(current, address + 24)), unchecked((int)CoreDword(current, address + 28)), 4096);
            NativeBattleVector global = new(unchecked((short)CoreWord(current, 0x27971E4)), unchecked((short)CoreWord(current, 0x27971E6)), unchecked((short)CoreWord(current, 0x27971E8)));
            NativeBattleFullMacVertex translated = NativeBattleGfMatrices.TransformMac460840(basis, global);
            basis = basis with { X = translated.X, Y = translated.Y, Z = translated.Z };
            return current;
        }, current =>
        {
            current = current.WriteTemporary(0xE0, raw ?? throw new InvalidOperationException("Native angular grid basis has not been prepared."));
            current.TemporaryFrame.SetKnownness(0xE0, known ?? throw new InvalidOperationException("Native angular grid basis knownness is missing."));
            matrix = NativeBattlePoseMatrices.Scale56BEF0(basis ?? throw new InvalidOperationException("Native angular grid basis has not been prepared."), unchecked((int)current.ReadTemporaryDword(0xC0)), unchecked((int)current.ReadTemporaryDword(0xC4)), unchecked((int)current.ReadTemporaryDword(0xC8)));
            for (int index = 0; index < 9; index++)
            {
                current = current.WriteTemporaryWord(0xE0 + index * 2, unchecked((ushort)matrix.Rotation[index]));
            }
            return current;
        }, (current, output) =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Native angular grid requires its current depth-cue controls.");
            NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native angular grid requires its current camera projection.");
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native angular grid requires its retained packed vector input.");
            registers = NativeBattleCinematicRibbonInput.Capture(current, current.TemporaryFrame.Address + 0xD0, registers);
            NativeBattleVector point = NativeBattleScriptedMeshPreparation.Vector(current.ReadTemporary(0xD0, 6));
            NativeBattleModuleDepthCueVertex projected = NativeBattleModuleProjection.ProjectWithCue45FAA0(matrix ?? throw new InvalidOperationException("Native angular grid row basis has not been prepared."), point, projection, controls.Dqa, controls.Dqb);
            current = StoreNativeGridDword(current, output, projected.Projected.Screen).WriteTemporaryDword(0xFC, projected.Projected.Flags);
            NativeEffectMemory = memory with { ScriptedColorRegisters = registers };
            return new(current, projected.Projected.Depth >> 2) { PilotWorld = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix!, point), projection) };
        }, (current, color, factor) =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native angular grid requires its retained color registers.");
            NativeBattleCinematicColorScaleResult scaled = NativeBattleCinematicColorScale.ScaleB66A20(color, factor, registers);
            NativeEffectMemory = memory with { ScriptedColorRegisters = scaled.Registers };
            return new(current, scaled.Color);
        }, (current, output, ordering, batch) =>
        {
            var written = AppendNativeCinematicPackets(current, output, ordering, batch, graphics);
            graphics = written.Graphics;
            return written.Frame;
        });
        NativeBattleWaterSlotBank visualBank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, resourceLength);
        NativeBattlePilotVisualIdentity identity = NativeBattleCinematicVisualIdentity.Identity(graphics.OriginalEffect!.Binding.Effect, frame, visualBank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, visualBank));
        return (NativeBattleCinematicWireGrid38.Draw(frame, services, identity), graphics);
    }
}
