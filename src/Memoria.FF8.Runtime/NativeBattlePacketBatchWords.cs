using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattlePacketBatchWords
{
    public static NativeBattleGfGeometryBatch Resolve(NativeBattleGfGeometryBatch batch, int cursor, int capacity, Func<int, byte> readRetained)
    {
        return ResolveWithKnownness(batch, cursor, capacity, address => (readRetained(address), true));
    }

    public static NativeBattleGfGeometryBatch ResolveWithKnownness(NativeBattleGfGeometryBatch batch, int cursor, int capacity, Func<int, (byte Value, bool Known)> readRetained)
    {
        if (batch.Attempts.IsDefault || batch.AllocationBytes < 0 || (batch.AllocationBytes & 3) != 0 || cursor < 0 || cursor > capacity - batch.AllocationBytes)
        {
            throw new InvalidDataException("Native packet allocation leaves its current physical owner.");
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = batch.Attempts.ToBuilder();
        Dictionary<int, (byte Value, bool Known)>? observed = null;
        int observedPrefixLength = 0;
        for (int index = 0; index < attempts.Count; index++)
        {
            NativeBattleGfGeometryAttempt attempt = attempts[index];
            if (attempt.Data.IsDefaultOrEmpty || attempt.WriteMask.IsDefault || attempt.WriteMask.Length != attempt.Data.Length || attempt.ByteOffsetRelative < 0 || (attempt.ByteOffsetRelative & 3) != 0 || (attempt.PacketLength & 3) != 0 || attempt.ByteOffsetRelative > capacity - cursor - attempt.PacketLength)
            {
                throw new InvalidDataException("Native packet delivery requires bounded ordered stores.");
            }
            int address = checked(cursor + attempt.ByteOffsetRelative);
            observedPrefixLength = index + 1;
            for (int offset = 0; offset < attempt.PacketLength; offset++)
            {
                switch (attempt.WriteMask[offset])
                {
                    case NativeBattleGfPacketByteState.Known:
                        if (observed is not null)
                        {
                            observed[address + offset] = (attempt.Data[offset], true);
                        }
                        break;
                    case NativeBattleGfPacketByteState.Unknown:
                        if (observed is not null)
                        {
                            observed[address + offset] = (attempt.Data[offset], false);
                        }
                        break;
                    case NativeBattleGfPacketByteState.Untouched:
                        break;
                    default:
                        throw new InvalidDataException("Native packet has an unsupported store mask.");
                }
            }
            if (attempt.Primitive is not { } primitive)
            {
                continue;
            }
            if (primitive.Words.IsDefaultOrEmpty || primitive.Words.Length * 4 > attempt.PacketLength)
            {
                throw new InvalidDataException("Native logical packet leaves its physical stores.");
            }
            byte[] bytes = new byte[primitive.Words.Length * 4];
            for (int word = 0; word < primitive.Words.Length; word++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(word * 4), primitive.Words[word]);
            }
            for (int command = 4; command < bytes.Length;)
            {
                // Resolve the opcode before deciding which fields its consumer reads.
                byte opcode = ReadByte(command + 3);
                bool polygon = (opcode & 0xE0) == 0x20;
                int corners = (opcode & 8) != 0 ? 4 : 3;
                bool gouraud = (opcode & 16) != 0;
                bool textured = (opcode & 4) != 0;
                int stride = (gouraud ? 1 : 0) + 1 + (textured ? 1 : 0);
                int length = polygon ? 4 * (corners * stride + (gouraud ? 0 : 1)) : opcode == 0 || opcode is >= 0xE1 and <= 0xE6 ? 4 : bytes.Length - command;
                if (length > bytes.Length - command)
                {
                    throw new InvalidDataException("Native logical packet ends inside its polygon command.");
                }
                for (int offset = 0; offset < length; offset++)
                {
                    int word = offset / 4;
                    bool colorPadding = polygon && gouraud && word > 0 && word % stride == 0 && offset % 4 == 3;
                    int uv = word - 2;
                    bool uvPadding = polygon && textured && uv >= 2 * stride && uv % stride == 0 && offset % 4 >= 2;
                    // Logical words project consumed fields only. Ignored bytes are canonical;
                    // ordered physical stores and their unknownness remain in the receipt.
                    bytes[command + offset] = colorPadding || uvPadding ? (byte)0 : offset == 3 ? opcode : ReadByte(command + offset);
                }
                command += length;
            }
            ImmutableArray<uint> words = Enumerable.Range(0, primitive.Words.Length).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(word * 4))).ToImmutableArray();
            attempts[index] = attempt with { Primitive = primitive with { Words = words } };

            byte ReadByte(int offset)
            {
                (byte Value, bool Known) value = attempt.WriteMask[offset] switch
                {
                    NativeBattleGfPacketByteState.Known => (attempt.Data[offset], true),
                    NativeBattleGfPacketByteState.Untouched => ReadObserved(address + offset),
                    NativeBattleGfPacketByteState.Unknown => (attempt.Data[offset], false),
                    _ => throw new InvalidDataException("Native packet has an unsupported store mask.")
                };
                return value.Known ? value.Value : throw new NotSupportedException("Native command consumes an unobserved packet byte.");
            }
        }
        return batch with { Attempts = attempts.ToImmutable() };

        (byte Value, bool Known) ReadObserved(int address)
        {
            if (observed is null)
            {
                // Fully authored commands never need a physical store map. On the first
                // retained read, include every validated store through the current attempt.
                observed = [];
                for (int index = 0; index < observedPrefixLength; index++)
                {
                    NativeBattleGfGeometryAttempt prefix = attempts[index];
                    int first = checked(cursor + prefix.ByteOffsetRelative);
                    for (int offset = 0; offset < prefix.PacketLength; offset++)
                    {
                        if (prefix.WriteMask[offset] is NativeBattleGfPacketByteState.Known or NativeBattleGfPacketByteState.Unknown)
                        {
                            observed[first + offset] = (prefix.Data[offset], prefix.WriteMask[offset] == NativeBattleGfPacketByteState.Known);
                        }
                    }
                }
            }
            if (observed.TryGetValue(address, out var value))
            {
                return value;
            }
            return readRetained(address);
        }
    }
}
