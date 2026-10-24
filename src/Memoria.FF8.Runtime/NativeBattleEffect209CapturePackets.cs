using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect209CapturePackets
{
    public static NativeBattleGfGeometryBatch Environment7E90(NativeBattleEffectArena retained64, ImmutableArray<byte> currentEnvironment92, short x, short y, int bucket)
    {
        if (currentEnvironment92.IsDefault || currentEnvironment92.Length != 92)
        {
            throw new InvalidDataException("Effect209 capture requires its actual current drawing environment.");
        }
        byte[] environment = new byte[92];
        Word(0, x);
        Word(2, y);
        Word(4, 320);
        Word(6, 216);
        Word(8, x);
        Word(10, y);
        currentEnvironment92.AsSpan().Slice(0x14, 4).CopyTo(environment.AsSpan(0x14));
        environment[0x18] = 1;
        (byte[] retained, _) = retained64.ReadSnapshot(0, 64);
        ImmutableArray<byte> packet = NativeBattleDrawEnvironmentPackets.Restore45C0F0(retained.ToImmutableArray(), environment.ToImmutableArray());
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[64];
        for (int index = 3; index < 36; index++)
        {
            mask[index] = NativeBattleGfPacketByteState.Known;
        }
        ImmutableArray<uint> words = Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan().Slice(index * 4))).ToImmutableArray();
        NativeBattleGeometryPrimitive primitive = new(bucket, words, [0, 0, 0, 0]);
        return new([new(0, packet, mask.ToImmutableArray(), primitive)], 64);

        void Word(int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(environment.AsSpan(offset), value);
    }
}
