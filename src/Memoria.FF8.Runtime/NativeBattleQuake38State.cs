using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuake38State([property: JsonRequired] ImmutableArray<byte> Header, [property: JsonRequired] ImmutableArray<byte> Middle, [property: JsonRequired] ImmutableArray<byte> Pointers, [property: JsonRequired] ImmutableArray<byte> Tail, [property: JsonRequired] ImmutableArray<byte> End, [property: JsonRequired] ImmutableArray<NativeBattleQuake38SavedActor> SavedActors)
{
    public static NativeBattleQuake38State FromColdProcess() => new(new byte[0xD8].ToImmutableArray(), new byte[0x34].ToImmutableArray(), new byte[0x10].ToImmutableArray(), new byte[0x10].ToImmutableArray(), new byte[0x5C].ToImmutableArray(), Enumerable.Range(0, 7).Select(_ => NativeBattleQuake38SavedActor.FromColdProcess()).ToImmutableArray());

    public void Validate()
    {
        if (Header.IsDefault || Header.Length != 0xD8 || Middle.IsDefault || Middle.Length != 0x34 || Pointers.IsDefault || Pointers.Length != 0x10 || Tail.IsDefault || Tail.Length != 0x10 || End.IsDefault || End.Length != 0x5C || SavedActors.IsDefault || SavedActors.Length != 7 || SavedActors.Any(actor => actor is null))
        {
            throw new InvalidDataException("Native Quake requires its complete retained module fields, independent of task pools and the process effect arena.");
        }
    }

    public ImmutableArray<byte> Read(uint address, int length)
    {
        (int bank, int offset) = Resolve(address, length);
        return Banks()[bank].AsSpan().Slice(offset, length).ToArray().ToImmutableArray();
    }

    public NativeBattleQuake38State Write(uint address, ReadOnlySpan<byte> source)
    {
        (int bank, int offset) = Resolve(address, source.Length);
        byte[] bytes = Banks()[bank].ToArray();
        source.CopyTo(bytes.AsSpan(offset));
        ImmutableArray<byte> updated = bytes.ToImmutableArray();
        return bank switch
        {
            0 => this with { Header = updated },
            1 => this with { Middle = updated },
            2 => this with { Pointers = updated },
            3 => this with { Tail = updated },
            4 => this with { End = updated },
            _ => throw new InvalidDataException("Native Quake field bank is unknown.")
        };
    }

    public byte Byte(uint address) => Read(address, 1)[0];
    public short Word(uint address) => BinaryPrimitives.ReadInt16LittleEndian(Read(address, 2).AsSpan());
    public uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4).AsSpan());
    public NativeBattleQuake38State SetByte(uint address, byte value) => Write(address, [value]);
    public NativeBattleQuake38State SetWord(uint address, short value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleQuake38State SetDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));

    private ImmutableArray<byte>[] Banks() => [Header, Middle, Pointers, Tail, End];

    private (int Bank, int Offset) Resolve(uint address, int length)
    {
        Validate();
        uint[] starts = [0x278C8E0, 0x279299C, 0x2792B40, 0x2792D68, 0x2792E04];
        ImmutableArray<byte>[] banks = Banks();
        for (int index = 0; index < starts.Length; index++)
        {
            if (length >= 0 && address >= starts[index] && (ulong)address + (uint)length <= (ulong)starts[index] + (uint)banks[index].Length)
            {
                return (index, checked((int)(address - starts[index])));
            }
        }
        throw new InvalidDataException("Native Quake access leaves its actual retained field banks.");
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleQuake38State? Quake38 { get; init; }
}
