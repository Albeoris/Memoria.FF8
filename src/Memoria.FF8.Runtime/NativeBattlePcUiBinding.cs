using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Selected PC UI device profile and authentic ICON.SP1 parts, supplied by the resource owner.</summary>
public sealed record NativeBattlePcUiBinding([property: JsonRequired] ImmutableArray<byte> Directory, [property: JsonRequired] NativeBattlePcUiProfile Device, [property: JsonRequired] NativeBattlePcUiIconProfile Opaque, [property: JsonRequired] NativeBattlePcUiIconProfile Semi, [property: JsonRequired] NativeBattlePcUiRules Rules)
{
    [JsonIgnore]
    public int IconCount
    {
        get
        {
            if (Directory.IsDefault || Directory.Length < 4)
            {
                throw new InvalidDataException("Native ICON.SP1 lacks its original directory count.");
            }
            uint count = BinaryPrimitives.ReadUInt32LittleEndian(Directory.AsSpan());
            if (count > (Directory.Length - 4) / 4)
            {
                throw new InvalidDataException("Native ICON.SP1 directory leaves its resource.");
            }
            return (int)count;
        }
    }

    public static NativeBattlePcUiBinding PrimaryOrdinary(ImmutableArray<byte> directory, NativeBattlePcUiIconProfile texture)
    {
        return new(directory, new(640, 480, 2, 2, 0, 16), texture, texture, new(0.001f, BitConverter.Int32BitsToSingle(0x37874CD5), 0.5f, 256));
    }

    public ImmutableArray<byte> Encode(byte icon, int x, int y, uint color)
    {
        int total = IconCount;
        if (icon >= total)
        {
            throw new ArgumentOutOfRangeException(nameof(icon));
        }
        if (icon is >= 128 and < 140)
        {
            throw new NotSupportedException("Native icon128..139 requires its current input mapping before formatting.");
        }
        int offset = BinaryPrimitives.ReadUInt16LittleEndian(Directory.AsSpan().Slice(4 + icon * 4));
        int count = BinaryPrimitives.ReadUInt16LittleEndian(Directory.AsSpan().Slice(6 + icon * 4));
        if (count == 0 || offset < 4 + total * 4 || offset > Directory.Length || count > (Directory.Length - offset) / 8)
        {
            throw new InvalidDataException("Native icon parts leave their original directory resource.");
        }
        return NativeMenuIconPackets.Encode4B76A0(Directory.AsSpan().Slice(offset, count * 8), x, y, color, 0);
    }
}

public sealed partial record NativeBattleGraphicsDefinition
{
    public NativeBattlePcUiBinding? PcUiIcons { get; init; }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattlePcUiFrame? UiImmediateFrame { get; init; }
}
