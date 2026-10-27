using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTornado146State([property: JsonRequired] NativeBattleEffectArena Storage, [property: JsonRequired] NativeBattleVector FunnelTranslation, [property: JsonRequired] ImmutableArray<byte>? CapturePacket)
{
    public const uint WorkAddress = 0x2491BC0;
    public const int WorkLength = 0x18A6C;
    public const string PacketBank = "2491BC0";
    public static NativeBattleTornado146State FromColdProcess() => new(NativeBattleEffectArena.Clear(), new(0, 0, 0), null);

    public void Validate()
    {
        Storage.Validate();
        Storage.ValidateExtent(WorkLength);
        if (CapturePacket is { } packet && (packet.IsDefault || packet.Length != 24))
        {
            throw new InvalidDataException("Native Tornado requires its complete bounded module and capture packet owners.");
        }
    }
}

public sealed class NativeBattleTornado146Work : IDisposable
{
    private readonly NativeBattleTornado146State state;
    private NativeBattleEffectArenaWriter writer;

    public NativeBattleTornado146Work(NativeBattleTornado146State state)
    {
        state.Validate();
        this.state = state;
        writer = new(state.Storage);
    }

    public byte Byte(uint address)
    {
        Span<byte> bytes = stackalloc byte[1];
        ReadInto(address, bytes);
        return bytes[0];
    }
    public ushort Word(uint address)
    {
        Span<byte> bytes = stackalloc byte[2];
        ReadInto(address, bytes);
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes);
    }
    public short SignedWord(uint address) => unchecked((short)Word(address));
    public uint Dword(uint address)
    {
        Span<byte> bytes = stackalloc byte[4];
        ReadInto(address, bytes);
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }
    public int SignedDword(uint address) => unchecked((int)Dword(address));
    public byte[] Read(uint address, int count)
    {
        RequireRange(address, count);
        int offset = checked((int)(address - NativeBattleTornado146State.WorkAddress));
        return writer.ReadThrough(offset, count);
    }

    private void ReadInto(uint address, Span<byte> bytes)
    {
        RequireRange(address, bytes.Length);
        writer.ReadIntoThrough(checked((int)(address - NativeBattleTornado146State.WorkAddress)), bytes);
    }

    public void WriteByte(uint address, byte value) => Write(address, [value]);
    public void WriteWord(uint address, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        Write(address, bytes);
    }

    public void WriteDword(uint address, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        Write(address, bytes);
    }

    public void Write(uint address, ReadOnlySpan<byte> bytes)
    {
        RequireRange(address, bytes.Length);
        int offset = checked((int)(address - NativeBattleTornado146State.WorkAddress));
        writer = writer.Write(offset, bytes);
    }

    public (byte[] Data, bool[] Known) ReadSnapshot(uint address, int count)
    {
        RequireRange(address, count);
        int offset = checked((int)(address - NativeBattleTornado146State.WorkAddress));
        return writer.ReadSnapshotThrough(offset, count);
    }

    public void MarkUnknown(uint address, int count)
    {
        RequireRange(address, count);
        int offset = checked((int)(address - NativeBattleTornado146State.WorkAddress));
        writer = writer.MarkUnknown(offset, count);
    }

    public NativeBattleTornado146State Freeze() => state with { Storage = writer.Freeze() };
    public void Dispose() => writer.Dispose();

    private static void RequireRange(uint address, int count)
    {
        if (address < NativeBattleTornado146State.WorkAddress || count < 0 || address - NativeBattleTornado146State.WorkAddress > NativeBattleTornado146State.WorkLength || count > NativeBattleTornado146State.WorkLength - (address - NativeBattleTornado146State.WorkAddress))
        {
            throw new InvalidDataException("Native Tornado access leaves its addressed module work bank.");
        }
    }
}

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleTornado146State? Tornado146 { get; init; }
}
