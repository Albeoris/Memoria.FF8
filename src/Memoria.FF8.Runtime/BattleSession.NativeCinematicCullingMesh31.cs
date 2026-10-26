using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicCullingMesh31(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleCinematicResources resources, NativeBattleCinematicProfile profile, NativeBattleGraphicsState graphics)
    {
        NativeBattleEffectMemory memory = RequireWaterMemory();
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Native culled mesh requires its retained geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native culled mesh requires its retained color registers."));
        NativeBattleCinematicCullingMesh31Services services = new((current, selector) => NativeBattleWaterShapeSelection.ResolveCinematic(current, resources, selector).Selected, current =>
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            uint address = NativeBattleWaterMatrixCommands.ResolveB65520(current, bank, SlotWord(current, 0x9C));
            byte[] rotation = current.Core.Read(address, 18);
            NativeBattleActionMatrix original = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(index * 2))).ToImmutableArray(), unchecked((int)CoreDword(current, address + 20)), unchecked((int)CoreDword(current, address + 24)), unchecked((int)CoreDword(current, address + 28)), 4096);
            (byte[] padding, bool[] known) = current.Core.ReadSnapshot(address + 18, 2);
            NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, original, true, true, null);
            geometry = geometry with { Rotation20 = geometry.Rotation20.SetItem(18, known[0] ? padding[0] : null).SetItem(19, known[1] ? padding[1] : null) };
            registers = new(geometry, registers.Colors);
            registers = registers with { Colors = NativeBattleCinematicRibbonInput.Capture(current, 0x27971E4, registers.Colors) };
            registers = NativeBattleGeometry.Transform(registers);
            NativeBattleCinematicScaledBasisResult prepared = NativeBattleCinematicScaledBasis.Prepare(current, resourceLength);
            geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, prepared.Matrix, true, true, null);
            geometry = geometry with { Rotation20 = geometry.Rotation20.SetItem(18, known[0] ? padding[0] : null).SetItem(19, known[1] ? padding[1] : null) };
            registers = registers with { Geometry = geometry };
            Publish();
            return prepared.Frame;
        }, (current, source, output) =>
        {
            registers = registers with { Colors = NativeBattleCinematicRibbonInput.Capture(current, source, registers.Colors) };
            NativeBattleScriptedGraphicsMemory controls = RequireWaterMemory().ScriptedGraphics ?? throw new NotSupportedException("Native culled mesh requires its retained depth controls.");
            registers = NativeBattleGeometry.ProjectSingle(registers, NativeCamera?.Projection ?? throw new NotSupportedException("Native culled mesh requires its current camera."), controls.Dqa, controls.Dqb);
            byte[] screen = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(screen, registers.Geometry.Sxy[2] ?? throw new NotSupportedException("Native culled mesh did not publish its screen position."));
            byte[] depth = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(depth, unchecked((ushort)((registers.Geometry.Sz[3] ?? throw new NotSupportedException("Native culled mesh did not publish its depth.")) >> 2)));
            current = current with { Arena = current.Arena.Write(ArenaOffset(output, 4), screen).Write(ArenaOffset(unchecked(output + 4), 2), depth) };
            current = current.WriteTemporaryDword(0xFC, registers.Colors.Flags ?? throw new NotSupportedException("Native culled mesh did not publish its flags."));
            Publish();
            return current;
        }, (current, material) =>
        {
            NativeBattleScriptedMaterialStep prepared = NativeBattleScriptedMaterial.Prepare(current, registers.Colors, material);
            registers = registers with { Colors = prepared.Registers };
            NativeEffectMemory = RequireWaterMemory() with { ScriptedLighting = prepared.Lighting, ScriptedColorRegisters = prepared.Registers };
            return prepared.Frame;
        }, (current, color) =>
        {
            NativeBattleScriptedLightingMemory lighting = RequireWaterMemory().ScriptedLighting ?? throw new NotSupportedException("Native culled mesh requires its prepared lighting.");
            ImmutableArray<byte> normal = registers.Colors.NormalInput ?? throw new NotSupportedException("Native culled mesh requires its prepared normal input.");
            registers = registers with { Colors = NativeBattleScriptedNormalColor.Transform4601B0(lighting, NativeBattleScriptedMeshPreparation.Vector(normal.AsSpan()), color, registers.Colors) };
            Publish();
            return new(current, registers.Colors.Rgb[2] ?? throw new NotSupportedException("Native culled mesh did not publish its shaded color."));
        }, kind => resources.ReadStatic(unchecked(profile.PrimitiveSizes + (uint)kind), 1)[0], (current, output, ordering, batch) =>
        {
            var written = AppendNativeCinematicPackets(current, output, ordering, batch, graphics);
            graphics = written.Graphics;
            return written.Frame;
        });
        frame = NativeBattleCinematicCullingMesh31.Draw(frame, services);
        Publish();
        return (frame, graphics);

        void Publish()
        {
            NativeEffectMemory = RequireWaterMemory() with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors };
        }
    }
}
