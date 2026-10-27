using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCurePacketWrite(NativeBattleEffectMemory Memory, NativeBattleCureState State, NativeBattleLogicalOrdering Ordering);

/// <summary>277AEC4's packets overwrite the original effect arena while retaining logical OT links.</summary>
public static class NativeBattleCurePacketArena
{
    public static NativeBattleCurePacketWrite AppendPrimary(NativeBattleEffectMemory memory, NativeBattleCureState state, NativeBattleLogicalOrdering ordering, NativeBattleModulePacketBatch batch)
    {
        int cursor = state.PrimaryDrawCursor ?? throw new NotSupportedException("Native Cure packet writes require the executed8D6B30 primary cursor.");
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, cursor, ordering, batch, NativeBattleEffectGouraudWrites.RgbBytes);
        return new(written.Memory, state with { PrimaryDrawCursor = written.Cursor }, written.Ordering);
    }
}

public enum NativeBattleEffectGouraudWrites
{
    FullDword,
    RgbBytes
}

public sealed record NativeBattleEffectPacketWrite(NativeBattleEffectMemory Memory, int Cursor, NativeBattleLogicalOrdering Ordering);

/// <summary>Native effect packet stores in20DFAB8, with producer-specific secondary Gouraud color writes and retained logical OT links.</summary>
public static partial class NativeBattleEffectPacketArena
{
    public static NativeBattleEffectPacketWrite Append(NativeBattleEffectMemory memory, int cursor, NativeBattleLogicalOrdering ordering, NativeBattleModulePacketBatch batch, NativeBattleEffectGouraudWrites gouraudWrites, int? moduleDraw = null)
    {
        using NativeBattleEffectArenaWriter arena = new(memory.Arena ?? throw new NotSupportedException("Native effect packet writes require the571870 effect arena."));
        using NativeBattleLogicalOrdering.Builder links = new(ordering);
        if (gouraudWrites is not (NativeBattleEffectGouraudWrites.FullDword or NativeBattleEffectGouraudWrites.RgbBytes))
        {
            throw new ArgumentOutOfRangeException(nameof(gouraudWrites));
        }
        if (batch.CapturedWrites is { } captured)
        {
            if (gouraudWrites != NativeBattleEffectGouraudWrites.FullDword)
            {
                throw new InvalidDataException("Native572200 captured stores cannot use another producer's three-byte color policy.");
            }
            return Append(memory, cursor, ordering, captured, moduleDraw: moduleDraw);
        }
        if (batch.Primitives.IsDefault || batch.AllocationBytes < 0 || cursor < 0 || (cursor & 3) != 0 || cursor > NativeBattleEffectArena.Length - batch.AllocationBytes)
        {
            throw new InvalidDataException("Native Cure packets leave their original aligned effect arena.");
        }
        foreach (NativeBattleAllocatedPrimitive allocation in batch.Primitives)
        {
            NativeBattleGeometryPrimitive primitive = allocation.Primitive;
            int length = checked(primitive.Words.Length * 4);
            if (allocation.ByteOffset < 0 || (allocation.ByteOffset & 3) != 0 || allocation.ByteOffset > batch.AllocationBytes - length || primitive.Words.Length < 2)
            {
                throw new InvalidDataException("Native Cure primitive leaves its actual packet allocation.");
            }
            byte command = (byte)(primitive.Words[1] >> 24);
            byte[] bytes = new byte[length];
            for (int word = 0; word < primitive.Words.Length; word++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4 * word), primitive.Words[word]);
            }
            bool[] written = Enumerable.Repeat(true, length).ToArray();
            switch (command & 0xFC)
            {
                case 0x28 when length == 24:
                    break;
                case 0x24 when length == 32:
                    Preserve(30, 2);
                    break;
                case 0x2C when length == 40:
                    Preserve(30, 2);
                    Preserve(38, 2);
                    break;
                case 0x34 when length == 40:
                    Preserve(38, 2);
                    break;
                case 0x3C when length == 52:
                    Preserve(38, 2);
                    Preserve(50, 2);
                    break;
                case 0x30 when length == 28:
                    if (gouraudWrites == NativeBattleEffectGouraudWrites.RgbBytes)
                    {
                        Preserve(15, 1);
                        Preserve(23, 1);
                    }
                    break;
                case 0x38 when length == 36:
                    if (gouraudWrites == NativeBattleEffectGouraudWrites.RgbBytes)
                    {
                        Preserve(15, 1);
                        Preserve(23, 1);
                        Preserve(31, 1);
                    }
                    break;
                case 0xE0 when command == 0xE1 && length is 8 or 12:
                    break;
                default:
                    throw new NotSupportedException($"Native Cure packet command{command:X2} requires its original byte-write contract.");
            }
            int address = checked(cursor + allocation.ByteOffset);
            for (int offset = 0; offset < length;)
            {
                if (!written[offset])
                {
                    offset++;
                    continue;
                }
                int end = offset + 1;
                while (end < length && written[end])
                {
                    end++;
                }
                arena.Write(checked(address + offset), bytes.AsSpan(offset, end - offset));
                offset = end;
            }
            bool linked = links.ProxyCount < NativeBattlePrimitiveOrdering.ProxyCapacity;
            links.InvalidatePacketRange(new("20DFAB8", 0, address), primitive.Words.Length * 4);
            links.Prepend(primitive.Bucket, new("20DFAB8", 0, address), primitive.Words, allocation.Flags20, primitive.VertexPayload4, moduleDraw);
            if (linked)
            {
                arena.Write(address, NativeBattleLogicalOrdering.LinkBytes(links.LastHeaderLink));
            }

            void Preserve(int first, int count) => Array.Fill(written, false, first, count);
        }
        return new(memory with { Arena = arena.Freeze() }, checked(cursor + batch.AllocationBytes), links.ToImmutable());
    }
}
