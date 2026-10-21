using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStageSpecializedPacket(string Bank, int Offset, ushort Flags, ImmutableArray<byte> Bytes);
public sealed record NativeBattleStageSpecializedPacketStep(NativeBattleStageSpecializedState State, ImmutableArray<NativeBattleStageSpecializedPacket> Packets);

public static class NativeBattleStageSpecializedPackets
{
    public static NativeBattleStageSpecializedPacketStep Build(NativeBattleStageSpecializedState state, uint callback, byte buffer, uint rgb)
    {
        state.Validate();
        if (buffer > 1)
        {
            throw new InvalidDataException("Native stage overlay requires one of its two physical framebuffers.");
        }
        byte[] bank = Bank(state, callback).ToArray();
        int offset = buffer * 256;
        BinaryPrimitives.WriteUInt32LittleEndian(bank.AsSpan(offset), 0x03000000);
        BinaryPrimitives.WriteUInt32LittleEndian(bank.AsSpan(offset + 4), (rgb & 0xFFFFFF) | 0x62000000);
        if (callback != 0x518290)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bank.AsSpan(offset + 8), 0);
            BinaryPrimitives.WriteInt16LittleEndian(bank.AsSpan(offset + 10), 0);
            BinaryPrimitives.WriteInt16LittleEndian(bank.AsSpan(offset + 12), 320);
            BinaryPrimitives.WriteInt16LittleEndian(bank.AsSpan(offset + 14), 224);
        }
        uint oldModeTag = BinaryPrimitives.ReadUInt32LittleEndian(bank.AsSpan(offset + 16));
        BinaryPrimitives.WriteUInt32LittleEndian(bank.AsSpan(offset + 16), (oldModeTag & 0xFFFFFF) | 0x01000000);
        BinaryPrimitives.WriteUInt32LittleEndian(bank.AsSpan(offset + 20), 0xE1000620);
        state = Store(state, callback, bank.ToImmutableArray());
        string identity = callback == 0x518440 ? "1D9BDC8" : callback == 0x518290 ? "1D9BB90" : "1D9B958";
        return new(state, [new(identity, offset, 1, bank.AsSpan(offset, 16).ToArray().ToImmutableArray()), new(identity, offset + 16, 0, bank.AsSpan(offset + 16, 8).ToArray().ToImmutableArray())]);
    }

    public static NativeBattleStageSpecializedState Write(NativeBattleStageSpecializedState state, uint callback, int offset, ImmutableArray<byte> bytes)
    {
        byte[] bank = Bank(state, callback).ToArray();
        bytes.AsSpan().CopyTo(bank.AsSpan(offset));
        return Store(state, callback, bank.ToImmutableArray());
    }

    private static ImmutableArray<byte> Bank(NativeBattleStageSpecializedState state, uint callback) => callback switch
    {
        0x518440 => state.Packets44,
        0x518290 => state.Packets45,
        0x5180C0 => state.Packets46,
        _ => throw new NotSupportedException("Native stage has no color overlay packet bank.")
    };

    private static NativeBattleStageSpecializedState Store(NativeBattleStageSpecializedState state, uint callback, ImmutableArray<byte> bytes) => callback switch
    {
        0x518440 => state with { Packets44 = bytes },
        0x518290 => state with { Packets45 = bytes },
        0x5180C0 => state with { Packets46 = bytes },
        _ => throw new NotSupportedException("Native stage has no color overlay packet bank.")
    };
}
