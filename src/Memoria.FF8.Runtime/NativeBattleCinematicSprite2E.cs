using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicSpriteProjection(NativeBattleWaterExecutionFrame Frame, int QuarterDepth);
public sealed record NativeBattleCinematicSprite2EServices(Func<NativeBattleWaterExecutionFrame, NativeBattleCinematicSpriteProjection> ProjectAnchor, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> Append);

/// <summary>The cinematic textured quad centered on the projected anchor.</summary>
public static class NativeBattleCinematicSprite2E
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicSprite2EServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        uint manager = CoreDword(frame, 0x27973EC);
        uint output = CoreDword(frame, unchecked(manager + 0x7C));
        uint payload = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xB8, 4));
        byte[] data = new byte[40];
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[40];
        data[3] = 9;
        mask[3] = NativeBattleGfPacketByteState.Known;
        StoreDword(4, BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xCC, 4)) | 0x2C000000);
        StoreWord(0x16, SlotWord(frame, 0x92));
        StoreWord(0xE, SlotWord(frame, 0x9A));
        NativeBattleCinematicSpriteProjection projected = services.ProjectAnchor(frame);
        frame = projected.Frame;
        int halfWidth = ReadPayloadWord(4) / 2;
        int halfHeight = ReadPayloadWord(6) / 2;
        ushort left = unchecked((ushort)(frame.ReadTemporaryWord(0xD0) - halfWidth));
        StoreWord(0x18, left);
        StoreWord(8, left);
        ushort right = unchecked((ushort)(frame.ReadTemporaryWord(0xD0) + halfWidth));
        StoreWord(0x20, right);
        StoreWord(0x10, right);
        ushort top = unchecked((ushort)(frame.ReadTemporaryWord(0xD2) - halfHeight));
        StoreWord(0x12, top);
        StoreWord(0xA, top);
        uint bucketOffset = unchecked((uint)((projected.QuarterDepth >> 2) & ~3));
        ushort bottom = unchecked((ushort)(frame.ReadTemporaryWord(0xD2) + halfHeight));
        StoreWord(0x22, bottom);
        StoreWord(0x1A, bottom);
        byte u = unchecked((byte)ReadPayloadWord(0));
        byte v = unchecked((byte)ReadPayloadWord(2));
        byte rightU = unchecked((byte)(u + 2 * halfWidth));
        byte bottomV = unchecked((byte)(v + 2 * halfHeight));
        StoreByte(0x15, v);
        StoreByte(0x1C, u);
        StoreByte(0xC, u);
        StoreByte(0x24, rightU);
        StoreByte(0x14, rightU);
        StoreByte(0xD, v);
        StoreByte(0x25, bottomV);
        StoreByte(0x1D, bottomV);
        uint ordering = unchecked(frame.ReadTemporaryDword(0x14C) + bucketOffset);
        ImmutableArray<uint> words = Enumerable.Range(0, 10).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4))).ToImmutableArray();
        ImmutableArray<NativeBattleGfPacketByteState> physicalMask = mask.ToImmutableArray();
        NativeBattleGeometryPrimitive primitive = new(0, words, []) { PhysicalWriteMask = physicalMask };
        NativeBattleGfGeometryBatch batch = new([new(0, data.ToImmutableArray(), physicalMask, primitive)], 40);
        frame = services.Append(frame, output, ordering, batch);
        manager = CoreDword(frame, 0x27973EC);
        return WriteCoreDword(frame, unchecked(manager + 0x7C), unchecked(output + 40));

        short ReadPayloadWord(int offset) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(unchecked(payload + (uint)offset), 2), 2));

        void StoreByte(int offset, byte value)
        {
            data[offset] = value;
            mask[offset] = NativeBattleGfPacketByteState.Known;
        }

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
}
