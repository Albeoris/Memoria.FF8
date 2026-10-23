using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect298Controller
{
    public static NativeBattleEffect298Frame DrawChunks6725F0(NativeBattleEffect298Frame frame, uint main, NativeBattleEffect298Resources resources, NativeBattleEffect298Inputs inputs)
    {
        (frame, uint temporary) = Allocate(frame, 104);
        for (int index = 0; index < 192; index++)
        {
            uint address = checked(main + 0x2EC + (uint)(index * 172));
            NativeBattleEffectArena chunk = ReadBytes(frame, address, 172);
            int life = Word298(chunk, 6);
            if (life == 0)
            {
                continue;
            }
            NativeBattleEffectArena context = Context(frame, temporary, 104);
            NativeBattleActionMatrix first = ChunkAxis298(resources, Word298(chunk, 16), Word298(chunk, 14), true);
            NativeBattleActionMatrix second = ChunkAxis298(resources, Word298(chunk, 18), 4096, false);
            frame = MultiplyColumns298(frame, first, second, out NativeBattleActionMatrix local);
            frame = MultiplyColumns298(frame, inputs.ReadView(frame).ReadMatrix(), local, out NativeBattleActionMatrix rotation);
            NativeBattleGeometryRegisterStep registers = Registers298(frame);
            NativeBattlePackedMatrix32 camera = inputs.ReadView(frame);
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, camera.ReadMatrix(), false, true, null) };
            registers = NativeBattleGeometry.LoadSignedIr45DC40(registers, Vector298(chunk));
            registers = NativeBattleGeometry.Transform(registers, true, true);
            for (int lane = 0; lane < 9; lane++)
            {
                context = context.Write(lane * 2, BitConverter.GetBytes(rotation.Rotation[lane]));
            }
            for (int axis = 0; axis < 3; axis++)
            {
                context = context.Write(20 + axis * 4, BitConverter.GetBytes(registers.Colors.Mac[axis]!.Value));
            }
            context = context.Write(32, chunk.Read(24, 4)).Write(40, BitConverter.GetBytes(life < 8 ? life << 9 : 4096)).Write(100, BitConverter.GetBytes(address + 28));
            frame = Publish298(frame, registers);
            frame = PublishContext(frame, temporary, context, 104);
            frame = LoadMatrix(frame, PackedContext298(context));
            (frame, context, NativeBattleGfGeometryBatch batch) = DrawChunkMesh672880(frame, context, resources, inputs);
            frame = PublishContext(frame, temporary, context, 104);
            if (!batch.Attempts.IsEmpty)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect298DeliverBatch(batch, 0x25078F4));
            }
        }
        return Release(frame, 104);
    }

    private static NativeBattleActionMatrix ChunkAxis298(NativeBattleEffect298Resources resources, int angle, int scale, bool first)
    {
        int offset = (angle & 4095) * 4;
        short sine = BinaryPrimitives.ReadInt16LittleEndian(resources.TrigPairs16384.AsSpan().Slice(offset, 2));
        short cosine = BinaryPrimitives.ReadInt16LittleEndian(resources.TrigPairs16384.AsSpan().Slice(offset + 2, 2));
        if (!first)
        {
            return new([cosine, 0, unchecked((short)-sine), 0, 4096, 0, sine, 0, cosine], 0, 0, 0, 4096);
        }
        short s = unchecked((short)(unchecked(sine * scale) >> 12));
        short c = unchecked((short)(unchecked(cosine * scale) >> 12));
        return new([unchecked((short)scale), 0, 0, 0, c, s, 0, unchecked((short)-s), c], 0, 0, 0, 4096);
    }

    private static NativeBattleEffect298Frame MultiplyColumns298(NativeBattleEffect298Frame frame, NativeBattleActionMatrix left, NativeBattleActionMatrix right, out NativeBattleActionMatrix result)
    {
        NativeBattleGeometryRegisterStep registers = Registers298(frame);
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, left, true, false, 0) };
        short[] values = new short[9];
        for (int column = 0; column < 3; column++)
        {
            registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)right.Rotation[column]), unchecked((ushort)right.Rotation[column + 3]), unchecked((ushort)right.Rotation[column + 6]));
            registers = NativeBattleGeometry.Transform(registers, false, true);
            for (int row = 0; row < 3; row++)
            {
                values[row * 3 + column] = unchecked((short)registers.Colors.Ir[row]!.Value);
            }
        }
        result = new(values.ToImmutableArray(), 0, 0, 0, 4096);
        return Publish298(frame, registers);
    }

    private static (NativeBattleEffect298Frame Frame, NativeBattleEffectArena Context, NativeBattleGfGeometryBatch Batch) DrawChunkMesh672880(NativeBattleEffect298Frame frame, NativeBattleEffectArena context, NativeBattleEffect298Resources resources, NativeBattleEffect298Inputs inputs)
    {
        uint meshAddress = Dword298(context, 32);
        NativeBattleStaticMeshResource mesh = resources.ReadMesh(meshAddress);
        uint materialAddress = Dword298(context, 100);
        context = context.Write(36, BitConverter.GetBytes(meshAddress + 8)).Write(44, BitConverter.GetBytes(meshAddress + BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()))).Write(96, [128, 128, 128]);
        int cue = Int298(context, 40);
        NativeBattleGeometryRegisterStep registers = Registers298(frame);
        registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)cue), Ir = [128, 128, 128] } };
        registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, NativeBattleGteColorInterpolation.CodeConstants45F270);
        context = context.Write(96, Enumerable.Range(0, 3).Select(lane => unchecked((byte)registers.Colors.Ir[lane]!.Value)).ToArray());
        int polygon = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()));
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(polygon, 4));
        var attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int face = 0; face < count; face++)
        {
            ReadOnlySpan<byte> source = mesh.Bytes.AsSpan().Slice(polygon + 4 + face * 12, 12);
            for (int corner = 0; corner < 3; corner++)
            {
                int vertex = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(4 + corner * 2, 2)) * 4;
                ReadOnlySpan<byte> xyz = mesh.Bytes.AsSpan().Slice(8 + vertex, 8);
                registers = NativeBattleGeometry.LoadVector(registers, corner, new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.Slice(4))), BinaryPrimitives.ReadInt16LittleEndian(xyz.Slice(6)));
            }
            registers = NativeBattleGeometry.ProjectTriple(registers, inputs.ReadProjection(frame), (frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect298 chunk projection requires current graphics controls.")).Dqa, frame.Memory.ScriptedGraphics.Dqb);
            Packet298 packet = new(32, allocated);
            packet.Copy(4, context, 96, 4);
            uint flags = registers.Colors.Flags ?? throw new NotSupportedException("Effect298 chunk projection requires its current RTPT flags.");
            context = context.Write(60, BitConverter.GetBytes(flags));
            if ((flags & 0x60000) != 0)
            {
                attempts.Add(packet.Attempt(null));
                continue;
            }
            registers = NativeBattleGeometry.Clip45EE10(registers);
            NativeBattleEffectArena material = ReadBytes(frame, materialAddress + (uint)(face * 12), 10);
            packet.Copy(12, material, 0, 4);
            packet.Copy(20, material, 4, 4);
            packet.Copy(28, material, 8, 2);
            uint area = registers.Geometry.Mac0 ?? throw new NotSupportedException("Effect298 chunks require their current NCLIP result.");
            context = context.Write(48, BitConverter.GetBytes(area));
            if (unchecked((int)area) < 0)
            {
                attempts.Add(packet.Attempt(null));
                continue;
            }
            uint[] screen = registers.Geometry.Sxy.Select(value => value ?? throw new NotSupportedException("Effect298 chunks require their current screen FIFO.")).ToArray();
            for (int corner = 0; corner < 3; corner++)
            {
                packet.Dword(8 + corner * 8, screen[corner]);
            }
            registers = NativeBattleGeometry.Average45E5C0(registers, frame.Memory.ScriptedGraphics.Zsf3 ?? throw new NotSupportedException("Effect298 chunk averaging requires current ZSF3."));
            bool outsideX = screen.All(value => unchecked((short)value) < 0 || unchecked((short)value) > 2560);
            bool outsideY = screen.All(value => unchecked((short)(value >> 16)) < 0 || unchecked((short)(value >> 16)) > 1728);
            if (outsideX || outsideY)
            {
                attempts.Add(packet.Attempt(null));
                continue;
            }
            uint depth = registers.Geometry.Otz ?? throw new NotSupportedException("Effect298 chunks require their current OTZ.");
            context = context.Write(56, BitConverter.GetBytes(depth));
            int bucket = unchecked(17 + (unchecked((int)depth) >> 2));
            packet.Dword(0, 0x07000000);
            packet.Byte(7, cue == 4096 ? (byte)0x24 : (byte)0x26);
            ImmutableArray<int> payload = TrianglePayload298(registers.Geometry);
            attempts.Add(packet.Attempt(packet.Primitive(bucket, payload)));
            allocated += 32;
            if (cue != 4096)
            {
                Packet298 glow = new(20, allocated);
                glow.Dword(0, 0x04000000);
                glow.Copy(4, context, 96, 4);
                glow.Byte(7, 0x22);
                for (int corner = 0; corner < 3; corner++)
                {
                    glow.Dword(8 + corner * 4, screen[corner]);
                }
                attempts.Add(glow.Attempt(glow.Primitive(bucket, payload)));
                allocated += 20;
                Packet298 mode = new(8, allocated);
                mode.Dword(0, 0x01000000);
                mode.Dword(4, 0xE1000240);
                attempts.Add(mode.Attempt(mode.Primitive(bucket, [0, 0, 0, 0])));
                allocated += 8;
            }
        }
        context = context.Write(44, BitConverter.GetBytes(checked(meshAddress + (uint)(polygon + 4 + count * 12)))).Write(100, BitConverter.GetBytes(checked(materialAddress + (uint)(count * 12))));
        return (Publish298(frame, registers), context, new(attempts.ToImmutable(), allocated) { NativeUnlinkedAllocation = attempts.Any(attempt => attempt.Primitive is null) });
    }

    private static NativeBattlePackedMatrix32 PackedContext298(NativeBattleEffectArena context)
    {
        (byte[] bytes, bool[] known) = context.ReadSnapshot(0, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    private static ImmutableArray<int> TrianglePayload298(NativeBattleGeometryRegisters geometry) => [unchecked((int)(geometry.Sz[1] ?? throw new NotSupportedException("Effect298 triangle insertion requires current SZ1."))), unchecked((int)(geometry.Sz[2] ?? throw new NotSupportedException("Effect298 triangle insertion requires current SZ2."))), unchecked((int)(geometry.Sz[3] ?? throw new NotSupportedException("Effect298 triangle insertion requires current SZ3."))), 0];

    private sealed class Packet298(int length, int offset)
    {
        private readonly byte[] data = new byte[length];
        private readonly NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[length];
        public void Dword(int at, uint value) => Store(at, BitConverter.GetBytes(value));
        public void Byte(int at, byte value) => Store(at, [value]);
        public void Copy(int at, NativeBattleEffectArena source, int sourceOffset, int count)
        {
            (byte[] bytes, bool[] known) = source.ReadSnapshot(sourceOffset, count);
            bytes.CopyTo(data, at);
            for (int index = 0; index < count; index++)
            {
                mask[at + index] = known[index] ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Unknown;
            }
        }
        private void Store(int at, ReadOnlySpan<byte> bytes)
        {
            bytes.CopyTo(data.AsSpan(at));
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, at, bytes.Length);
        }
        public NativeBattleGeometryPrimitive Primitive(int bucket, ImmutableArray<int> payload) => new(bucket, Enumerable.Range(0, data.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4))).ToImmutableArray(), payload);
        public NativeBattleGfGeometryAttempt Attempt(NativeBattleGeometryPrimitive? primitive) => new(offset, data.ToImmutableArray(), mask.ToImmutableArray(), primitive);
    }
}
