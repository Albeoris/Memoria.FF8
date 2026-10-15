using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum NativeWorldEntryMemoryOrigin
{
    NativeNewGame,
    ExecutedFieldClear
}

/// <summary>Executed event bytes256..267, including story WORD256 and world loader BYTE264/266.</summary>
public sealed record NativeWorldEntryMemory([property: JsonRequired] int Version, [property: JsonRequired] string Rule, [property: JsonRequired] NativeWorldEntryMemoryOrigin Origin, [property: JsonRequired] ImmutableArray<byte> Bytes)
{
    public const string NativeRule = "ff8-en-world-entry-bytes-56d9f0-51d8b0-v1";
    public const int FirstAddress = 256;
    public const int ByteCount = 12;

    [JsonIgnore]
    public ushort StoryWord => unchecked((ushort)ReadBytes(256, 2));
    [JsonIgnore]
    public byte ReplacementFlags264 => unchecked((byte)ReadBytes(264, 1));
    [JsonIgnore]
    public byte StoryByte266 => unchecked((byte)ReadBytes(266, 1));

    public static NativeWorldEntryMemory CreateNewGame() => new(1, NativeRule, NativeWorldEntryMemoryOrigin.NativeNewGame, ImmutableArray.CreateRange(new byte[ByteCount]));
    public static NativeWorldEntryMemory CreateCleared() => new(1, NativeRule, NativeWorldEntryMemoryOrigin.ExecutedFieldClear, ImmutableArray.CreateRange(new byte[ByteCount]));

    public void Validate()
    {
        if (Version != 1 || Rule != NativeRule || !Enum.IsDefined(Origin) || Bytes.IsDefault || Bytes.Length != ByteCount)
        {
            throw new InvalidDataException("Native world-entry bytes require their executed NewGame/CLEAR origin and complete selected bank.");
        }
    }

    public static bool Overlaps(int address, int width) => width is 1 or 2 or 4 && address < FirstAddress + ByteCount && (long)address + width > FirstAddress;

    public int ReadBytes(int address, int width)
    {
        Validate();
        if (width is not (1 or 2 or 4) || address < FirstAddress || (long)address + width > FirstAddress + ByteCount)
        {
            throw new NotSupportedException("Native world-entry reads require wholly represented bytes256..267.");
        }
        uint value = 0;
        for (int index = 0; index < width; index++)
        {
            value |= (uint)Bytes[address - FirstAddress + index] << (8 * index);
        }
        return unchecked((int)value);
    }

    public NativeWorldEntryMemory WriteBytes(int address, int width, int value)
    {
        Validate();
        if (!Overlaps(address, width) || address < FirstAddress || (long)address + width > FirstAddress + ByteCount)
        {
            throw new NotSupportedException("Native world-entry stores require wholly represented bytes256..267.");
        }
        ImmutableArray<byte>.Builder next = Bytes.ToBuilder();
        for (int index = 0; index < width; index++)
        {
            long target = (long)address + index;
            if (target >= FirstAddress && target < FirstAddress + ByteCount)
            {
                next[(int)target - FirstAddress] = unchecked((byte)((uint)value >> (8 * index)));
            }
        }
        return this with { Bytes = next.ToImmutable() };
    }

    public NativeWorldEntryMemory ClearFieldBytes()
    {
        Validate();
        return this with { Bytes = ImmutableArray.CreateRange(new byte[ByteCount]) };
    }

    public IReadOnlyDictionary<int, int> ProjectVariables(IReadOnlyDictionary<int, int> values)
    {
        Validate();
        Dictionary<int, int> variables = new(values);
        foreach ((int address, int value) in new[] { (256, (int)StoryWord), (264, (int)ReplacementFlags264), (265, ReadBytes(265, 1)), (266, (int)StoryByte266) })
        {
            if (value != 0)
            {
                variables[address] = value;
            }
        }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<int, int>(variables);
    }

    public void ValidateEventMemory(NativeEventMemory memory)
    {
        Validate();
        if (memory.Cells.Any(pair => Overlaps(pair.Key, pair.Value.Width)))
        {
            throw new InvalidDataException("Native world-entry byte owner overlaps generic event memory.");
        }
    }
}
