using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicGroundProfile18(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleGraphicsState graphics)
    {
        NativeBattleEffectMemory memory = RequireWaterMemory();
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Native ground profile requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native ground profile requires its current arithmetic registers."));
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native ground profile requires its current camera projection.");
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Native ground profile requires its current depth controls.");
        NativeBattleCinematicGroundProfile18Services services = new(
            (current, selector) =>
            {
                NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
                int index = NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank);
                int selected = NativeBattleWaterSlots.LookupB65370(current.Arena, bank, index, current.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
                return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(current.Arena, selected));
            },
            PrepareBasis,
            current =>
            {
                registers = registers with { Colors = NativeBattleCinematicRibbonInput.Capture(current, unchecked(current.TemporaryFrame.Address + 0xD0), registers.Colors) };
                registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                uint screen = registers.Geometry.Sxy[2] ?? throw new NotSupportedException("Native ground profile projection did not publish SXY.");
                short x = unchecked((short)(unchecked((short)screen) >> 3));
                short y = unchecked((short)(unchecked((short)(screen >> 16)) >> 3));
                PublishRegisters();
                return new(current, unchecked((uint)(ushort)x | ((uint)(ushort)y << 16)), checked((int)((registers.Geometry.Sz[3] ?? throw new NotSupportedException("Native ground profile projection did not publish depth.")) >> 2)), registers.Colors.Flags ?? throw new NotSupportedException("Native ground profile projection did not publish flags."));
            },
            (current, output, ordering, batch) =>
            {
                batch = batch with
                {
                    Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { } primitive ? attempt with { Primitive = primitive with { Bucket = OtBucket(unchecked(ordering + (uint)(primitive.Bucket * 4))) } } : attempt).ToImmutableArray()
                };
                PublishRegisters();
                var written = AppendNativeCinematicPackets(current, output, null, batch, graphics);
                graphics = written.Graphics;
                return written.Frame;
            });
        frame = NativeBattleCinematicGroundProfile18.Draw(frame, services);
        PublishRegisters();
        return (frame, graphics);

        NativeBattleWaterExecutionFrame PrepareBasis(NativeBattleWaterExecutionFrame current)
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            uint address = NativeBattleWaterMatrixCommands.ResolveB65520(current, bank, SlotWord(current, 0x9C));
            byte[] bytes = current.Core.Read(address, 18);
            ImmutableArray<short> rotation = Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 2))).ToImmutableArray();
            NativeBattleActionMatrix matrix = new(rotation, unchecked((int)CoreDword(current, address + 20)), unchecked((int)CoreDword(current, address + 24)), unchecked((int)CoreDword(current, address + 28)), 4096);
            (byte[] padding, bool[] known) = current.Core.ReadSnapshot(address + 18, 2);
            NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, true, null);
            geometry = geometry with { Rotation20 = geometry.Rotation20.SetItem(18, known[0] ? padding[0] : null).SetItem(19, known[1] ? padding[1] : null) };
            registers = new(geometry, NativeBattleCinematicRibbonInput.Capture(current, unchecked(CoreDword(current, 0x27973E8) + 0x94), registers.Colors));
            registers = NativeBattleGeometry.Transform(registers);
            registers = registers with
            {
                Geometry = registers.Geometry with { Translation = [registers.Colors.Mac[0] ?? throw new NotSupportedException("Native ground profile requires its transformed X."), registers.Colors.Mac[1] ?? throw new NotSupportedException("Native ground profile requires its transformed Y."), registers.Colors.Mac[2] ?? throw new NotSupportedException("Native ground profile requires its transformed Z.")] }
            };
            PublishRegisters();
            return current;
        }

        void PublishRegisters()
        {
            NativeBattleEffectMemory current = RequireWaterMemory();
            NativeEffectMemory = current with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors };
        }
    }
}
