using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDoom8Strips
{
    public static NativeBattleDoom8Step AdvanceStrips8C84A0(NativeBattleDoom8Frame frame, NativeBattleDoom8StripTables tables, NativeBattlePackedMatrix32 camera, NativeBattleCameraProjection projection, Func<NativeBattleDoom8Frame, NativeBattleGfGeometryBatch, NativeBattleDoom8Frame> deliver)
    {
        tables.Validate();
        frame.State.Validate();
        if (frame.Effect.Binding.Effect != 8 || frame.Effect.Binding.Entry != NativeBattleDoom8Controller.Entry)
        {
            throw new InvalidDataException("Doom strip drawing requires its original module.");
        }
        short index = Signed(frame.State, TargetAddress + 0x54);
        if ((uint)index >= tables.Ranges.Length)
        {
            throw new NotSupportedException("Doom strips require their bounded current range row.");
        }
        NativeBattleDoom8StripRange range = tables.Ranges[index];
        if (range.First == 127)
        {
            return new(frame, 0);
        }
        frame = frame with { State = frame.State.WriteWord(TargetAddress + 0x50, unchecked((ushort)range.First)).WriteWord(TargetAddress + 0x52, unchecked((ushort)range.Last)) };
        frame = DrawBank8C8500(frame, NearVertices, camera, projection, deliver);
        frame = DrawBank8C8500(frame, FarVertices, camera, projection, deliver);
        frame = frame with { State = frame.State.WriteWord(TargetAddress + 0x54, unchecked((ushort)(frame.State.ReadWord(TargetAddress + 0x54) + 1))) };
        return new(frame, 1);
    }

    private static NativeBattleDoom8Frame DrawBank8C8500(NativeBattleDoom8Frame frame, uint bank, NativeBattlePackedMatrix32 camera, NativeBattleCameraProjection projection, Func<NativeBattleDoom8Frame, NativeBattleGfGeometryBatch, NativeBattleDoom8Frame> deliver)
    {
        short first = Signed(frame.State, TargetAddress + 0x50);
        short last = Signed(frame.State, TargetAddress + 0x52);
        NativeBattleDoom8State state = frame.State;
        byte red = 0;
        byte blue = 0;
        for (int row = first; row <= last; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                uint address = ColorsAddress + (uint)(row * 16 + column * 4);
                state = state.Write(address, column is 0 or 3 ? [0, 0, 0] : [red, 0, blue]);
                if (column is 1 or 2)
                {
                    state = state with { Globals = state.Globals.MarkUnknown(checked((int)(address + 3 - NativeBattleDoom8State.BaseAddress)), 1) };
                }
            }
            red = unchecked((byte)(red + 6));
            blue = unchecked((byte)(blue + 2));
        }
        frame = frame with { State = state };
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Doom strips require their actual geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Doom strips require their actual data registers."));
        NativeBattlePackedMatrix32 target = ReadTargetMatrix(frame.State);
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(target.ReadMatrix(), unchecked((int)state.ReadDword(TargetAddress + 0x20)), unchecked((int)state.ReadDword(TargetAddress + 0x24)), unchecked((int)state.ReadDword(TargetAddress + 0x28)));
        NativeBattleActionMatrix view = camera.ReadMatrix();
        short? cameraPadding = camera.Known[18] && camera.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(camera.Bytes.AsSpan().Slice(18, 2)) : null;
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, view, true, false, cameraPadding) };
        short[] rotation = local.Rotation.ToArray();
        for (int column = 0; column < 3; column++)
        {
            registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)local.Rotation[column]), unchecked((ushort)local.Rotation[3 + column]), unchecked((ushort)local.Rotation[6 + column]));
            registers = NativeBattleGeometry.Transform(registers, false, true);
            for (int row = 0; row < 3; row++)
            {
                rotation[row * 3 + column] = unchecked((short)(registers.Colors.Ir[row] ?? throw new InvalidDataException("Doom strip matrix product did not publish its IR result.")));
            }
        }
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, view, false, true, null) };
        registers = NativeBattleGeometry.LoadVector(registers, 0, new(unchecked((short)local.X), unchecked((short)local.Y), unchecked((short)local.Z)), unchecked((short)(local.Z >> 16)));
        registers = NativeBattleGeometry.Transform(registers);
        NativeBattleActionMatrix matrix = local with { Rotation = rotation.ToImmutableArray(), X = registers.Colors.Mac[0]!.Value, Y = registers.Colors.Mac[1]!.Value, Z = registers.Colors.Mac[2]!.Value };
        short? padding = target.Known[18] && target.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(target.Bytes.AsSpan().Slice(18, 2)) : null;
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, true, padding) };
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
        for (int row = first; row < last; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Doom strips require their actual projection controls.");
                registers = new(frame.Memory.GeometryRegisters!, frame.Memory.ScriptedColorRegisters!);
                uint[] addresses = [bank + (uint)(row * 32 + column * 8), bank + (uint)(row * 32 + (column + 1) * 8), bank + (uint)((row + 1) * 32 + column * 8), bank + (uint)((row + 1) * 32 + (column + 1) * 8)];
                System.Numerics.Vector3[] world = new System.Numerics.Vector3[4];
                for (int corner = 0; corner < 3; corner++)
                {
                    registers = LoadVertex(registers, frame.State, addresses[corner], corner);
                    NativeBattleVector point = Vector(frame.State, addresses[corner]);
                    world[corner] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, point), projection with { View = view });
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                uint[] screens = registers.Geometry.Sxy.Select(value => value ?? throw new InvalidDataException("Doom strip projection did not publish its screens.")).ToArray();
                registers = LoadVertex(registers, frame.State, addresses[3], 0);
                NativeBattleVector fourthPoint = Vector(frame.State, addresses[3]);
                world[3] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, fourthPoint), projection with { View = view });
                registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                uint fourth = registers.Geometry.Sxy[2]!.Value;
                byte[] data = new byte[36];
                BinaryPrimitives.WriteUInt32LittleEndian(data, 0x08000000);
                data[7] = 0x3A;
                int[] colorOffsets = [row * 16 + column * 4, row * 16 + (column + 1) * 4, (row + 1) * 16 + column * 4, (row + 1) * 16 + (column + 1) * 4];
                for (int corner = 0; corner < 4; corner++)
                {
                    frame.State.Read(ColorsAddress + (uint)colorOffsets[corner], 3).CopyTo(data, 4 + corner * 8);
                    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8 + corner * 8), corner < 3 ? screens[corner] : fourth);
                }
                registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Doom strips require their actual ZSF4 control."));
                int bucket = unchecked((int)registers.Geometry.Mac0!.Value) >> 2;
                NativeBattleGfPacketByteState[] mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 36).ToArray();
                mask[15] = NativeBattleGfPacketByteState.Untouched;
                mask[23] = NativeBattleGfPacketByteState.Untouched;
                mask[31] = NativeBattleGfPacketByteState.Untouched;
                ImmutableArray<uint> words = Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4, 4))).ToImmutableArray();
                ImmutableArray<int> depths = registers.Geometry.Sz.Select(value => (int)unchecked((ushort)(value ?? throw new InvalidDataException("Doom strips require all four actual depth FIFO values.")))).ToImmutableArray();
                NativeBattleGeometryPrimitive primitive = new(bucket, words, depths) { PilotGeometry = new(NativeBattlePilotVisualLifetimes.Identity(8, frame.State.Globals, bank, 0, row * 3 + column), world[0], world[1], world[2], world[3], 4) { DepthDivisor = 4, SourceDepths = new(depths[0], depths[1], depths[2], depths[3]) } };
                NativeBattleGfGeometryAttempt attempt = new(0, data.ToImmutableArray(), mask.ToImmutableArray(), primitive);
                frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
                frame = deliver(frame, new([attempt], 36));
            }
        }
        return frame;
    }

    private static NativeBattleGeometryRegisterStep LoadVertex(NativeBattleGeometryRegisterStep registers, NativeBattleDoom8State state, uint address, int slot)
    {
        (byte[] bytes, bool[] known) = state.Globals.ReadSnapshot(checked((int)(address - NativeBattleDoom8State.BaseAddress)), 8);
        NativeBattleVector vector = new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
        if (!known.Take(6).All(value => value))
        {
            throw new NotSupportedException("Doom strip vertex XYZ bytes are unresolved.");
        }
        short? w = known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)) : null;
        return NativeBattleGeometry.LoadVector(registers, slot, vector, w);
    }
}
