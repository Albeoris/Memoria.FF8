using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed class NativeBattleEffect66PacketWrites(int length)
{
    private readonly byte[] bytes = new byte[length];
    private readonly NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[length];

    internal void Dword(int offset, uint value) => Store(offset, BitConverter.GetBytes(value));
    internal void Word(int offset, ushort value) => Store(offset, BitConverter.GetBytes(value));
    internal void Byte(int offset, byte value) => Store(offset, [value]);
    internal void Copy(int offset, NativeBattleEffectArena source, int sourceOffset, int count)
    {
        (byte[] data, bool[] known) = source.ReadSnapshot(sourceOffset, count);
        data.CopyTo(bytes, offset);
        for (int index = 0; index < count; index++)
        {
            mask[offset + index] = known[index] ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Unknown;
        }
    }
    private void Store(int offset, byte[] data)
    {
        data.CopyTo(bytes, offset);
        Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, data.Length);
    }
    internal NativeBattleGfGeometryAttempt Attempt(int offset, int? bucket, ImmutableArray<int> payload)
    {
        ImmutableArray<uint> words = Enumerable.Range(0, bytes.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
        return new(offset, bytes.ToImmutableArray(), mask.ToImmutableArray(), bucket is int value ? new NativeBattleGeometryPrimitive(value, words, payload) { PhysicalWriteMask = mask.ToImmutableArray() } : null);
    }
}
