using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect210CapturePackets
{
    public static NativeBattleGfGeometryBatch Environment(NativeBattleEffectArena retained64, ImmutableArray<byte> actualOppositeEnvironment92)
    {
        if (actualOppositeEnvironment92.IsDefault || actualOppositeEnvironment92.Length != 92)
        {
            throw new InvalidDataException("Effect210 capture requires its actual opposite drawing environment.");
        }
        (byte[] retained, _) = retained64.ReadSnapshot(0, 64);
        ImmutableArray<byte> data = NativeBattleDrawEnvironmentPackets.Restore45C0F0(retained.ToImmutableArray(), actualOppositeEnvironment92);
        bool clear = actualOppositeEnvironment92[0x18] != 0;
        return Packet(data, clear ? 9 : 6, 3, clear ? 36 : 24, true);
    }

    public static NativeBattleGfGeometryBatch Black(NativeBattleEffectArena retained24, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        (byte[] bytes, _) = retained24.ReadSnapshot(0, 24);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x05000000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x28000000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 192);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 256u << 16);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20), 192u | (256u << 16));
        NativeBattleGfGeometryBatch batch = Packet(bytes.ToImmutableArray(), 6, 0, 24, false);
        return pilotIdentity is { } identity ? batch with { Attempts = batch.Attempts.Select(attempt => attempt with { Primitive = NativeBattleEffect210VisualCapture.Screen(attempt.Primitive!, identity, 8, false) }).ToImmutableArray() } : batch;
    }

    public static NativeBattleGfGeometryBatch Area(NativeBattleEffectArena retained12)
    {
        (byte[] retained, _) = retained12.ReadSnapshot(0, 12);
        ImmutableArray<byte> data = NativeBattleDrawEnvironmentPackets.Area45C940(retained.ToImmutableArray(), 704, 256, 192, 256);
        return Packet(data, 3, 3, 12, true);
    }

    public static NativeBattleGfGeometryBatch Offset(NativeBattleEffectArena retained12)
    {
        (byte[] retained, _) = retained12.ReadSnapshot(0, 12);
        ImmutableArray<byte> data = NativeBattleDrawEnvironmentPackets.Offset45C9B0(retained.ToImmutableArray(), 704, 256);
        return Packet(data, 2, 3, 8, true);
    }

    private static NativeBattleGfGeometryBatch Packet(ImmutableArray<byte> data, int wordCount, int firstWritten, int lastWritten, bool zeroDepths)
    {
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[data.Length];
        for (int index = firstWritten; index < lastWritten; index++)
        {
            mask[index] = NativeBattleGfPacketByteState.Known;
        }
        ImmutableArray<uint> words = Enumerable.Range(0, wordCount).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(index * 4))).ToImmutableArray();
        NativeBattleGeometryPrimitive primitive = new(17, words, zeroDepths ? [0, 0, 0, 0] : []);
        return new([new(0, data, mask.ToImmutableArray(), primitive)], data.Length);
    }
}
