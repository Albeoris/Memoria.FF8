using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicTrail2B(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleCinematicResources resources, NativeBattleGraphicsState graphics)
    {
        Dictionary<uint, NativeBattleCinematicTrail2BSample> pilotSamples = [];
        NativeBattleActionMatrix? matrix = null;
        NativeBattleCinematicTrail2BServices services = new((current, texture) => NativeBattleWaterTextureMetadata.SelectTPageB665C0(current, resources, texture), (current, texture) => NativeBattleWaterTextureMetadata.ReadClutB66640(current, resources, texture), (current, selector) => NativeBattleWaterShapeSelection.ResolveCinematic(current, resources, selector).Selected, current =>
        {
            NativeBattleCinematicScaledBasisResult prepared = NativeBattleCinematicScaledBasis.Prepare(current, resourceLength);
            current = prepared.Frame;
            matrix = prepared.Matrix with { X = unchecked((int)current.ReadTemporaryDword(0xF4)), Y = unchecked((int)current.ReadTemporaryDword(0xF8)), Z = unchecked((int)current.ReadTemporaryDword(0xFC)) };
            return current;
        }, (current, address, output) =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Native cinematic trail requires its current depth-cue controls.");
            NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native cinematic trail requires its current projection controls.");
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native cinematic trail requires its retained packed vector input.");
            registers = NativeBattleCinematicRibbonInput.Capture(current, address, registers);
            byte[] raw = current.Arena.Read(ArenaOffset(address, 6), 6);
            NativeBattleVector point = NativeBattleScriptedMeshPreparation.Vector(raw);
            NativeBattleActionMatrix basis = matrix ?? throw new InvalidOperationException("Native cinematic trail basis has not been prepared.");
            NativeBattleModuleDepthCueVertex projected = NativeBattleModuleProjection.ProjectWithCue45FAA0(basis, point, projection, controls.Dqa, controls.Dqb);
            pilotSamples[output] = new(NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(basis, point), projection), address);
            current = StoreNativeGridDword(current, output, projected.Projected.Screen);
            current = current.WriteTemporaryDword(0x98, projected.Cue).WriteTemporaryDword(0x9C, projected.Projected.Flags);
            current = current with { Arena = current.Arena.Write(ArenaOffset(unchecked(output + 4), 2), BitConverter.GetBytes(unchecked((ushort)(projected.Projected.Depth >> 2)))) };
            NativeBattleFullMacVertex position = NativeBattleGfMatrices.TransformMac460840(basis, point);
            current = current.WriteTemporaryDword(0xD4, unchecked((uint)position.X)).WriteTemporaryDword(0xD8, unchecked((uint)position.Y)).WriteTemporaryDword(0xDC, unchecked((uint)position.Z)).WriteTemporaryDword(0x9C, position.Flags);
            byte[] rotation = current.Core.Read(0x2797968, 18);
            NativeBattleActionMatrix billboard = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(index * 2))).ToImmutableArray(), position.X, position.Y, position.Z, 4096);
            ushort width = BinaryPrimitives.ReadUInt16LittleEndian(current.Arena.Read(ArenaOffset(unchecked(address + 6), 2), 2));
            current = current.WriteTemporaryWord(0xC0, width);
            registers = NativeBattleCinematicRibbonInput.Capture(current, current.TemporaryFrame.Address + 0xC0, registers);
            NativeBattleModuleDepthCueVertex edge = NativeBattleModuleProjection.ProjectWithCue45FAA0(billboard, new(unchecked((short)width), unchecked((short)current.ReadTemporaryWord(0xC2)), unchecked((short)current.ReadTemporaryWord(0xC4))), projection, controls.Dqa, controls.Dqb);
            current = current.WriteTemporaryDword(0x94, edge.Projected.Screen).WriteTemporaryDword(0x98, edge.Cue).WriteTemporaryDword(0x9C, edge.Projected.Flags);
            ushort projectedWidth = unchecked((ushort)(unchecked((short)edge.Projected.Screen) - unchecked((short)projected.Projected.Screen)));
            current = current with { Arena = current.Arena.Write(ArenaOffset(unchecked(output + 0x10), 2), BitConverter.GetBytes(projectedWidth)) };
            NativeEffectMemory = memory with { ScriptedColorRegisters = registers };
            return current;
        }, (current, command, factor) =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native cinematic trail requires its retained color registers.");
            NativeBattleCinematicColorScaleResult scaled = NativeBattleCinematicColorScale.ScaleB66A20(command, factor, registers);
            NativeEffectMemory = memory with { ScriptedColorRegisters = scaled.Registers };
            return new(current, scaled.Color);
        }, (current, output, ordering, batch) =>
        {
            var written = AppendNativeCinematicPackets(current, output, ordering, batch, graphics);
            graphics = written.Graphics;
            return written.Frame;
        });
        NativeBattleWaterSlotBank visualBank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, resourceLength);
        services = services with { PilotSamples = pilotSamples, PilotIdentity = NativeBattleCinematicVisualIdentity.Identity(graphics.OriginalEffect!.Binding.Effect, frame, visualBank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, visualBank)) };
        return (NativeBattleCinematicTrail2B.Draw(frame, services), graphics);
    }
}
