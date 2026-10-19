using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCarbuncle278MirrorTransform(NativeBattleActionMatrix Matrix, NativeBattleCameraProjection Projection, short PriorOffsetX, short PriorOffsetY);
public sealed record NativeBattleCarbuncle278MirrorWrite(NativeBattleEffectMemory Memory, NativeBattleLogicalOrdering Ordering, int Cursor);

/// <summary>Carbuncle's mirror projection and private environment packets; the prepared matrix remains a draw parameter.</summary>
public static class NativeBattleCarbuncle278Mirror
{
    public static NativeBattleCarbuncle278MirrorTransform Transform6812E0(NativeBattleVector plane, NativeBattleEffectLocalBounds bounds, short focalScratch, NativeBattleCameraProjection projection)
    {
        int width = bounds.Maximum.X - bounds.Minimum.X;
        int depth = bounds.Maximum.Z - bounds.Minimum.Z;
        int quotientX = width / 128;
        int quotientZ = depth / 256;
        if (quotientX % 128 != 0)
        {
            quotientX = unchecked(quotientX + 1);
        }
        if (quotientZ % 256 != 0)
        {
            quotientZ = unchecked(quotientZ + 1);
        }
        int x = unchecked((-bounds.Minimum.X - bounds.Maximum.X) / 2 / quotientX - plane.X / quotientX);
        int y = unchecked(plane.Z / quotientZ - (-bounds.Minimum.Z - bounds.Maximum.Z) / 2 / quotientZ);
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(1024));
        short sine = unchecked((short)NativeBattleCameraMath.Sine(1024));
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, cosine, unchecked((short)-sine), 0, sine, cosine], x, y, focalScratch, 4096);
        int scaleX = 0x1000000 / (unchecked(width << 12) / 128);
        int scaleZ = 0x1000000 / (unchecked(depth << 12) / 256);
        matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scaleX, 0, scaleZ);
        short offsetX = projection.OffsetX ?? throw new NotSupportedException("Native Carbuncle mirror requires its retained GTE screen offset X.");
        short offsetY = projection.OffsetY ?? throw new NotSupportedException("Native Carbuncle mirror requires its retained GTE screen offset Y.");
        return new(matrix, projection with { View = matrix, OffsetX = 64, OffsetY = 128 }, offsetX, offsetY);
    }

    public static NativeBattleCarbuncle278MirrorWrite Append6812E0(NativeBattleEffectMemory memory, NativeBattleLogicalOrdering ordering, int cursor, uint graphicsFlags)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Carbuncle mirror requires its actual effect arena.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native Carbuncle mirror requires its retained primitive process.");
        byte buffer = process.Buffer80 ?? throw new NotSupportedException("Native Carbuncle mirror requires its actual draw buffer selector.");
        if (buffer > 1)
        {
            throw new InvalidDataException("Native Carbuncle mirror draw buffer leaves its two original environments.");
        }
        NativeSoftwareDrawingEnvironmentBuffers environments = process.Software?.EnvironmentBuffers ?? throw new NotSupportedException("Native Carbuncle mirror requires its retained alternate draw environment.");
        ImmutableArray<byte> alternate = buffer == 0 ? environments.Buffer1 : environments.Buffer0;
        byte[] retained = new byte[64];
        arena.Read(cursor, 4).CopyTo(retained, 0);
        ImmutableArray<byte> restore = NativeBattleDrawEnvironmentPackets.Restore45C0F0(retained.ToImmutableArray(), alternate);
        Append(restore, 64, alternate[0x18] != 0 ? 36 : 24, 0);
        if ((graphicsFlags & 0x100) != 0)
        {
            throw new NotSupportedException("Native Carbuncle6812E0 requires its current model context1D989D0 and5099D0 capture renderer.");
        }
        byte[] black = new byte[24];
        BinaryPrimitives.WriteUInt32LittleEndian(black, 0x05000000);
        BinaryPrimitives.WriteUInt32LittleEndian(black.AsSpan(4), 0x28000000);
        BinaryPrimitives.WriteUInt16LittleEndian(black.AsSpan(12), 128);
        BinaryPrimitives.WriteUInt16LittleEndian(black.AsSpan(18), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(black.AsSpan(20), 128);
        BinaryPrimitives.WriteUInt16LittleEndian(black.AsSpan(22), 256);
        Append(black.ToImmutableArray(), 24, 24, 1);
        retained = new byte[12];
        arena.Read(cursor, 4).CopyTo(retained, 0);
        Append(NativeBattleDrawEnvironmentPackets.Area45C940(retained.ToImmutableArray(), 704, 256, 128, 256), 12, 12, 0);
        retained = new byte[12];
        arena.Read(cursor, 4).CopyTo(retained, 0);
        Append(NativeBattleDrawEnvironmentPackets.Offset45C9B0(retained.ToImmutableArray(), 704, 256), 12, 8, 0);
        return new(memory with { Arena = arena }, ordering, cursor);

        void Append(ImmutableArray<byte> bytes, int allocation, int written, ushort flags)
        {
            arena = arena.Write(cursor, bytes.AsSpan().Slice(0, written));
            NativeBattlePrimitiveReference reference = new("20DFAB8", 0, cursor);
            int length = checked(4 + 4 * bytes[3]);
            ImmutableArray<uint> words = Enumerable.Range(0, length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(index * 4))).ToImmutableArray();
            NativeBattleLogicalOrdering.Builder links = new(ordering);
            links.InvalidatePacketRange(reference, allocation);
            bool linked = links.ProxyCount < NativeBattlePrimitiveOrdering.ProxyCapacity;
            links.Prepend(17, reference, words, flags, [0, 0, 0, 0]);
            if (linked)
            {
                arena = arena.Write(cursor, NativeBattleLogicalOrdering.LinkBytes(links.LastHeaderLink));
            }
            ordering = links.ToImmutable();
            cursor = checked(cursor + allocation);
        }
    }
}
