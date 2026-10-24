using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWaterParticleRectangleResult(NativeBattleWaterExecutionFrame Frame, NativeBattleGfGeometryBatch Packets, uint NextCursor);

/// <summary>Rectangle packet stores using the caller's current GTE state and current particle records.</summary>
public static class NativeBattleWaterParticleRectanglePackets
{
    public static NativeBattleWaterParticleRectangleResult DrawA9EE90(NativeBattleWaterExecutionFrame frame, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, short zsf3, short dqa, int dqb, Func<uint, int> resolveActualOtBucket)
    {
        uint manager = CoreDword(frame, 0x27973EC);
        uint cursor = CoreDword(frame, checked(manager + 0x7C));
        uint source = frame.ReadTemporaryDword(0x90);
        int count = unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(source, 4), 4)));
        if (count < 0)
        {
            throw new NotSupportedException("Native particle rectangle negative record counts are unsupported.");
        }
        byte[] temporary = frame.TemporaryFrame.Bytes.ToArray();
        bool[] temporaryKnown = frame.TemporaryFrame.Known.ToArray();
        foreach (int offset in new[] { 0xE4, 0xEC, 0xF4, 0xFC })
        {
            TemporaryWord(offset, 0);
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < count; index++)
        {
            uint record = unchecked(source + 4 + (uint)index * 0x14);
            byte selector = Read(0x10, 1)[0];
            byte slotFlags = ReadSlot(frame, 0xDE, 1)[0];
            uint color = selector == 0x80 || (slotFlags & 8) != 0
                ? frame.ReadTemporaryDword(0x98) | 0x2C000000
                : 0x2C000000 | (BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xCC, 4)) & 0x02000000) | selector | ((uint)selector << 8) | ((uint)selector << 16);
            ushort uv = Word(0xA);
            ushort size = Word(0xC);
            int uSize = size & 0xFF;
            int vPart = size & 0xFF00;
            int baseWidth = uSize << 4;
            int baseHeight = (vPart >> 4) & unchecked((int)0xFFFFFFF0);
            int scaledWidth = unchecked((short)Word(4) * baseWidth) / 4096;
            int scaledHeight = unchecked((short)Word(6) * baseHeight) / 4096;
            int left = unchecked((short)Word(0) + baseWidth / 2 - scaledWidth / 2);
            int top = unchecked((short)Word(2) - scaledHeight / 2 + baseHeight / 2);
            NativeBattleVector[] points =
            [
                new(unchecked((short)left), unchecked((short)top), 0),
                new(unchecked((short)(left + scaledWidth)), unchecked((short)top), 0),
                new(unchecked((short)left), unchecked((short)(top + scaledHeight)), 0),
                new(unchecked((short)(left + scaledWidth)), unchecked((short)(top + scaledHeight)), 0)
            ];
            NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[4];
            for (int corner = 0; corner < 4; corner++)
            {
                int offset = 0xE0 + corner * 8;
                TemporaryWord(offset, unchecked((ushort)points[corner].X));
                TemporaryWord(offset + 2, unchecked((ushort)points[corner].Y));
                TemporaryWord(offset + 4, 0);
                projected[corner] = corner == 3
                    ? NativeBattleModuleProjection.Project45FAA0(matrix, points[corner], projection, dqa, dqb)
                    : NativeBattleModuleProjection.Project45FE10(matrix, points[corner], projection);
            }
            uint depthSum = unchecked((uint)projected[0].Depth + projected[1].Depth + projected[2].Depth);
            uint average = unchecked(depthSum * (uint)zsf3) >> 12;
            uint fourth = (uint)(projected[3].Depth >> 2);
            TemporaryDword(0xE0, average);
            TemporaryDword(0xE4, fourth);
            int depth = unchecked((int)(average + fourth)) / 2;
            uint ot = unchecked(frame.ReadTemporaryDword(0x14C) + (uint)((depth >> 2) * 4));
            int bucket = resolveActualOtBucket(ot);
            ushort tpage = (slotFlags & 4) != 0 ? unchecked((ushort)frame.ReadTemporaryDword(0x94)) : Word(0x12);
            int length = (tpage & 0x60) == 0x20 ? 40 : 44;
            byte[] data = new byte[length];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[length];
            data[3] = checked((byte)(length / 4 - 1));
            mask[3] = NativeBattleGfPacketByteState.Known;
            StoreDword(4, color);
            StoreWord(0xE, Word(0xE));
            StoreWord(0xC, uv);
            StoreWord(0x14, unchecked((ushort)(uv + uSize)));
            StoreWord(0x1C, unchecked((ushort)(uv + vPart)));
            StoreWord(0x24, unchecked((ushort)(uv + uSize + vPart)));
            StoreWord(0x16, tpage);
            for (int corner = 0; corner < 4; corner++)
            {
                StoreDword(8 + corner * 8, projected[corner].Screen);
            }
            if (length == 44)
            {
                StoreDword(0x28, 0xE1000220);
            }
            ImmutableArray<uint> words = Enumerable.Range(0, length / 4).Select(i => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(i * 4))).ToImmutableArray();
            NativeBattleGeometryPrimitive primitive = new(bucket, words, projected.Select(value => (int)value.Depth).ToImmutableArray());
            attempts.Add(new(allocated, data.ToImmutableArray(), mask.ToImmutableArray(), primitive));
            allocated = checked(allocated + length);

            byte[] Read(int offset, int byteCount) => frame.Arena.Read(ArenaOffset(unchecked(record + (uint)offset), byteCount), byteCount);
            ushort Word(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(Read(offset, 2));

            void StoreWord(int offset, ushort value)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), value);
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 2);
            }

            void StoreDword(int offset, uint value)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 4);
            }
        }
        cursor = unchecked(cursor + (uint)allocated);
        frame = frame with { TemporaryFrame = frame.TemporaryFrame with { Bytes = temporary.ToImmutableArray(), Known = temporaryKnown.ToImmutableArray() } };
        frame = WriteCoreDword(frame, checked(manager + 0x7C), cursor);
        return new(frame, new(attempts.ToImmutable(), allocated), cursor);

        void TemporaryWord(int offset, ushort value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(temporary.AsSpan(offset), value);
            Array.Fill(temporaryKnown, true, offset, 2);
        }

        void TemporaryDword(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(temporary.AsSpan(offset), value);
            Array.Fill(temporaryKnown, true, offset, 4);
        }
    }
}
