using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleHoly175CaptureMemory(ImmutableArray<byte> BlackPackets, short? OffsetX, short? OffsetY)
{
    public const string PacketBank = "holy-capture";

    public byte[] Read(int offset, int count)
    {
        if (BlackPackets.IsDefault || BlackPackets.Length != 48 || offset < 0 || count < 0 || offset > 48 - count)
        {
            throw new InvalidDataException("Native Holy capture requires a bounded physical black packet read.");
        }
        return BlackPackets.AsSpan().Slice(offset, count).ToArray();
    }
}

public sealed record NativeBattleHoly175CaptureWrite(NativeBattlePrimitiveProcess Process, NativeBattleLogicalOrdering Ordering, NativeBattleStageState Stage, NativeBattleHoly175CaptureMemory Memory, int StagePackets);

public static class NativeBattleHoly175CapturePackets
{
    public static NativeBattleHoly175CaptureWrite Write5D29E0(NativeBattlePrimitiveProcess process, NativeBattleLogicalOrdering ordering, NativeBattleStageState stage, ImmutableArray<NativeBattleRawMesh> geometry, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleHoly175Resources resources, NativeBattleHoly175CaptureMemory retained)
    {
        process.Validate();
        if (retained.BlackPackets.IsDefault || retained.BlackPackets.Length != 48)
        {
            throw new InvalidDataException("Native Holy capture requires both retained physical black packets.");
        }
        ImmutableArray<byte> directory = resources.CaptureDirectory?.Data ?? throw new NotSupportedException("Native Holy capture requires its original stage draw directory.");
        if (directory.Length != 32)
        {
            throw new InvalidDataException("Native Holy capture requires four original draw directory pairs.");
        }
        ImmutableArray<NativeBattleHoly175CaptureDirectoryEntry>.Builder pairs = ImmutableArray.CreateBuilder<NativeBattleHoly175CaptureDirectoryEntry>();
        for (int index = 0; index < 4; index++)
        {
            int bucket = BinaryPrimitives.ReadInt32LittleEndian(directory.AsSpan().Slice(index * 8));
            int shift = BinaryPrimitives.ReadInt32LittleEndian(directory.AsSpan().Slice(index * 8 + 4));
            pairs.Add(new(bucket, checked((byte)shift)));
        }
        byte buffer = process.Buffer80 ?? throw new NotSupportedException("Native Holy capture requires its actual draw buffer selector.");
        if (buffer > 1)
        {
            throw new InvalidDataException("Native Holy capture requires one of two physical draw buffers.");
        }
        NativeSoftwareDrawingEnvironmentBuffers environments = process.Software?.EnvironmentBuffers ?? throw new NotSupportedException("Native Holy capture requires its retained alternate draw environment.");
        ImmutableArray<byte> alternate = buffer == 0 ? environments.Buffer1 : environments.Buffer0;
        // Only the bytes written by the producer are consumed from this scratch result.
        ImmutableArray<byte> restore = NativeBattleDrawEnvironmentPackets.Restore45C0F0(new byte[64].ToImmutableArray(), alternate);
        int restoreEnd = alternate[0x18] != 0 ? 36 : 24;
        AppendProduced(restore, 64, 4123, restoreEnd);

        NativeBattleHoly175CaptureStage drawn = NativeBattleHoly175CaptureGeometry.Draw5D29E0(stage, geometry, pairs.ToImmutable(), view, projection, colors);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocation = 0;
        foreach (NativeBattleGeometryPrimitive primitive in drawn.Packets)
        {
            byte[] data = Bytes(primitive.Words);
            attempts.Add(new(allocation, data.ToImmutableArray(), primitive.PhysicalWriteMask ?? throw new NotSupportedException("Native Holy captured geometry requires its original physical write mask."), primitive));
            allocation = checked(allocation + data.Length);
        }
        NativeBattleCommonPacketWrite stageWrite = NativeBattleCommonPacketArena.Append(process, ordering, new(attempts.ToImmutable(), allocation));
        process = stageWrite.Process;
        ordering = stageWrite.Ordering;

        int blackOffset = buffer * 24;
        byte[] black = retained.BlackPackets.AsSpan().Slice(blackOffset, 24).ToArray();
        NativeBattleLogicalOrdering.Builder blackLinks = new(ordering);
        NativeBattlePrimitiveReference blackReference = new(NativeBattleHoly175CaptureMemory.PacketBank, buffer, blackOffset);
        blackLinks.InvalidatePacketRange(blackReference, 24);
        bool blackLinked = blackLinks.ProxyCount < NativeBattlePrimitiveOrdering.ProxyCapacity;
        blackLinks.Prepend(4385, blackReference, Words(black), 1, [0, 0, 0, 0]);
        if (blackLinked)
        {
            byte[] updated = retained.BlackPackets.ToArray();
            NativeBattleLogicalOrdering.LinkBytes(blackLinks.LastHeaderLink).CopyTo(updated, blackOffset);
            retained = retained with { BlackPackets = updated.ToImmutableArray() };
        }
        ordering = blackLinks.ToImmutable();

        AppendProduced(NativeBattleDrawEnvironmentPackets.Area45C940(new byte[12].ToImmutableArray(), 576, 256, 320, 232), 12, 4385, 12);
        retained = retained with { OffsetX = 576, OffsetY = 256 };
        AppendProduced(NativeBattleDrawEnvironmentPackets.Offset45C9B0(new byte[8].ToImmutableArray(), 576, 256), 12, 4385, 8);
        return new(process, ordering, drawn.Stage, retained, drawn.Packets.Length);

        void AppendProduced(ImmutableArray<byte> bytes, int allocationBytes, int bucket, int writtenEnd)
        {
            int cursor = process.ByteCursor ?? throw new NotSupportedException("Native Holy capture requires its actual common packet cursor.");
            string bank = process.PacketBank ?? throw new NotSupportedException("Native Holy capture requires its actual common packet bank.");
            int packetLength = checked(4 + 4 * bytes[3]);
            NativeBattleLogicalOrdering.Builder links = new(ordering);
            NativeBattlePrimitiveReference reference = new(bank, process.PacketBuffer, cursor);
            links.InvalidatePacketRange(reference, allocationBytes);
            bool linked = links.ProxyCount < NativeBattlePrimitiveOrdering.ProxyCapacity;
            links.Prepend(bucket, reference, Words(bytes.AsSpan().Slice(0, packetLength)), 0, [0, 0, 0, 0]);
            ImmutableArray<NativeBattleCommonPacketStore>.Builder stores = ImmutableArray.CreateBuilder<NativeBattleCommonPacketStore>();
            ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Range(0, bytes.Length).Select(index => index >= 3 && index < writtenEnd ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched).ToImmutableArray();
            stores.Add(new(cursor, bytes, mask));
            if (linked)
            {
                stores.Add(new(cursor, NativeBattleLogicalOrdering.LinkBytes(links.LastHeaderLink).ToImmutableArray(), [NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known]));
            }
            process = process.ApplyCommonWrites(stores.ToImmutable()) with { ByteCursor = checked(cursor + allocationBytes) };
            ordering = links.ToImmutable();
        }
    }

    private static ImmutableArray<uint> Words(ReadOnlySpan<byte> bytes)
    {
        ImmutableArray<uint>.Builder words = ImmutableArray.CreateBuilder<uint>();
        for (int offset = 0; offset < bytes.Length; offset += 4)
        {
            words.Add(BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset)));
        }
        return words.ToImmutable();
    }

    private static byte[] Bytes(ImmutableArray<uint> words)
    {
        byte[] bytes = new byte[checked(words.Length * 4)];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        }
        return bytes;
    }
}
