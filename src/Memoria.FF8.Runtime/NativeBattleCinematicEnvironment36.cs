using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicEnvironment36Services(Func<NativeBattleWaterExecutionFrame, byte> ReadBuffer80, Func<NativeBattleWaterExecutionFrame, byte, ImmutableArray<byte>> ReadDrawingEnvironment, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> Append);

/// <summary>The retained pair of cinematic capture and restore drawing environments.</summary>
public static class NativeBattleCinematicEnvironment36
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicEnvironment36Services services)
    {
        ArgumentNullException.ThrowIfNull(services);
        uint storage = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xBC, 4));
        if (storage == 0)
        {
            uint manager = CoreDword(frame, 0x27973EC);
            storage = CoreDword(frame, unchecked(manager + 0x74));
            frame = WriteCoreDword(frame, unchecked(manager + 0x74), unchecked(storage + 0x100));
            frame = WriteSlotDword(frame, 0xBC, storage);
        }
        byte bank = unchecked((byte)((services.ReadBuffer80(frame) - 1) & 1));
        uint payload = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xB8, 4));
        uint scratch = CoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x74));
        ImmutableArray<byte> source = services.ReadDrawingEnvironment(frame, bank);
        if (source.IsDefault || source.Length < 28)
        {
            throw new InvalidDataException("Native cinematic environment requires its current drawing environment prefix.");
        }
        frame = frame with { Arena = frame.Arena.Write(ArenaOffset(scratch, 28), source.AsSpan().Slice(0, 28)) };
        int parity = unchecked((sbyte)frame.ReadTemporaryByte(0x141));
        uint actualBase = unchecked(storage + (uint)(parity * 128));
        ushort x = PayloadWord(2);
        StoreWord(unchecked(scratch + 8), x);
        StoreWord(scratch, x);
        ushort y = PayloadWord(4);
        StoreWord(unchecked(scratch + 10), y);
        StoreWord(unchecked(scratch + 2), y);
        StoreWord(unchecked(scratch + 4), PayloadWord(6));
        StoreWord(unchecked(scratch + 6), 224);
        StoreWord(unchecked(scratch + 0x16), 0x0101);
        frame = frame with { Arena = frame.Arena.Write(ArenaOffset(unchecked(scratch + 0x18), 4), new byte[] { 1, 0, 0, 0 }) };
        ImmutableArray<byte> capture = Build(frame.Arena.Read(ArenaOffset(scratch, 28), 28));
        ImmutableArray<byte> restore = Build(source.AsSpan().Slice(0, 28).ToArray());
        int restoreBucket = (PayloadWord(0) & 0x3FFC) / 4;
        NativeBattleGfGeometryAttempt restoreAttempt = Attempt(64, restore, source[0x18] != 0 ? 36 : 24, restoreBucket);
        NativeBattleGfGeometryAttempt captureAttempt = Attempt(0, capture, 36, 0x3FC0 / 4);
        uint ordering = frame.ReadTemporaryDword(0x14C);
        return services.Append(frame, actualBase, ordering, new([restoreAttempt, captureAttempt], 128));

        ushort PayloadWord(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(unchecked(payload + (uint)offset), 2), 2));

        void StoreWord(uint address, ushort value)
        {
            byte[] bytes = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), bytes) };
        }
    }

    private static ImmutableArray<byte> Build(byte[] prefix)
    {
        byte[] environment = new byte[92];
        prefix.CopyTo(environment, 0);
        return NativeBattleDrawEnvironmentPackets.Restore45C0F0(new byte[64].ToImmutableArray(), environment.ToImmutableArray());
    }

    private static NativeBattleGfGeometryAttempt Attempt(int offset, ImmutableArray<byte> data, int written, int bucket)
    {
        ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Range(0, 64).Select(index => index >= 3 && index < written ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched).ToImmutableArray();
        ImmutableArray<uint> words = Enumerable.Range(0, 16).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(index * 4))).ToImmutableArray();
        NativeBattleGeometryPrimitive primitive = new(bucket, words, []) { PhysicalWriteMask = mask };
        return new(offset, data, mask, primitive);
    }
}
