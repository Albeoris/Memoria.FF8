using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect303Controller
{
    public static NativeBattleEffect303Frame DrawFragments66B120(NativeBattleEffect303Frame frame, uint main, NativeBattleEffect303Resources resources, NativeBattleEffect303Inputs inputs)
    {
        frame = LoadMatrix(frame, inputs.ReadView(frame));
        (frame, uint temporary) = Allocate(frame, 8);
        var controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect303 fragments require current projection controls.");
        for (int index = 0; index < 128; index++)
        {
            NativeBattleEffectArena row = ReadBytes(frame, main + 32 + (uint)(index * 76), 76);
            int kind = Word303(row, 6);
            if (kind == 0)
            {
                continue;
            }
            bool quad = kind == 2;
            Packet303 packet = new(quad ? 40 : 32);
            NativeBattleGeometryRegisterStep registers = Registers303(frame);
            for (int corner = 0; corner < 3; corner++)
            {
                NativeBattleEffectArena point = Context(frame, temporary, 8);
                for (int axis = 0; axis < 3; axis++)
                {
                    point = point.Write(axis * 2, BitConverter.GetBytes(unchecked((short)(Word303(row, axis * 2) + Word303(row, 40 + corner * 6 + axis * 2)))));
                }
                frame = PublishContext(frame, temporary, point, 8);
                registers = NativeBattleGeometry.LoadVector(registers, corner, Vector303(point), W303(point));
            }
            registers = NativeBattleGeometry.ProjectTriple(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
            packet.Copy(12, row, 64, 4);
            packet.Copy(20, row, 68, 4);
            packet.Copy(28, row, 72, 2);
            uint[] screen = registers.Geometry.Sxy.Select(value => value ?? throw new NotSupportedException("Effect303 fragments require current SXY.")).ToArray();
            for (int corner = 0; corner < 3; corner++)
            {
                packet.Dword(8 + corner * 8, screen[corner]);
            }
            bool outsideX = screen.All(value => unchecked((ushort)value) > 2560);
            bool outsideY = screen.All(value => unchecked((ushort)(value >> 16)) > 1728);
            if (quad)
            {
                NativeBattleEffectArena point = Context(frame, temporary, 8);
                for (int axis = 0; axis < 3; axis++)
                {
                    point = point.Write(axis * 2, BitConverter.GetBytes(unchecked((short)(Word303(row, axis * 2) + Word303(row, 58 + axis * 2)))));
                }
                frame = PublishContext(frame, temporary, point, 8);
                registers = NativeBattleGeometry.ProjectSingle(NativeBattleGeometry.LoadVector(registers, 0, Vector303(point), W303(point)), inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
                uint fourth = registers.Geometry.Sxy[2]!.Value;
                packet.Copy(36, row, 74, 2);
                packet.Dword(32, fourth);
                outsideX &= unchecked((ushort)fourth) > 2560;
                outsideY &= unchecked((ushort)(fourth >> 16)) > 1728;
                registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Effect303 fragments require current ZSF4."));
            }
            else
            {
                registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Effect303 fragments require current ZSF3."));
            }
            NativeBattleGeometryPrimitive? primitive = null;
            int allocation = 0;
            if (!outsideX && !outsideY)
            {
                if (!quad)
                {
                    packet.Dword(0, 0x07000000);
                    packet.Copy(4, row, 16, 4);
                }
                uint depth = registers.Geometry.Otz ?? throw new NotSupportedException("Effect303 fragments require current averaged depth.");
                frame = PublishContext(frame, temporary, Context(frame, temporary, 8).Write(0, BitConverter.GetBytes(depth)), 8);
                if (unchecked((short)depth) > 0)
                {
                    if (quad)
                    {
                        packet.Dword(0, 0x09000000);
                        packet.Copy(4, row, 16, 4);
                    }
                    allocation = quad ? 40 : 32;
                    byte opcode = row.Read(19, 1)[0];
                    ImmutableArray<int> payload = (opcode & 8) != 0 ? registers.Geometry.Sz.Select(value => unchecked((int)(value ?? throw new NotSupportedException("Effect303 insertion requires current SZ.")))).ToImmutableArray() : [unchecked((int)registers.Geometry.Sz[1]!.Value), unchecked((int)registers.Geometry.Sz[2]!.Value), unchecked((int)registers.Geometry.Sz[3]!.Value), 0];
                    primitive = packet.Primitive(unchecked((short)depth) >> 2, payload);
                }
            }
            frame = Publish303(frame, registers);
            var batch = new NativeBattleGfGeometryBatch([packet.Attempt(primitive)], allocation) { NativeUnlinkedAllocation = primitive is null };
            frame = inputs.Execute(frame, new NativeBattleEffect303DeliverBatch(batch, 0x2507644));
        }
        return Release(frame, 8);
    }

    private sealed class Packet303(int length)
    {
        private readonly byte[] data = new byte[length];
        private readonly NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[length];
        public void Dword(int offset, uint value) => Store(offset, BitConverter.GetBytes(value));
        public void Copy(int offset, NativeBattleEffectArena source, int sourceOffset, int count)
        {
            var snapshot = source.ReadSnapshot(sourceOffset, count);
            snapshot.Data.CopyTo(data, offset);
            for (int index = 0; index < count; index++)
            {
                mask[offset + index] = snapshot.Known[index] ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Unknown;
            }
        }
        private void Store(int offset, ReadOnlySpan<byte> bytes)
        {
            bytes.CopyTo(data.AsSpan(offset));
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, bytes.Length);
        }
        public NativeBattleGeometryPrimitive Primitive(int bucket, ImmutableArray<int> payload) => new(bucket, Enumerable.Range(0, length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4))).ToImmutableArray(), payload);
        public NativeBattleGfGeometryAttempt Attempt(NativeBattleGeometryPrimitive? primitive) => new(0, data.ToImmutableArray(), mask.ToImmutableArray(), primitive);
    }
}
