using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Saved options DWORD1CFE73C and the twelve physical mapping bytes1CFE740.</summary>
public sealed record NativeInputConfiguration([property: JsonRequired] string InitOutSha256, [property: JsonRequired] uint Options, [property: JsonRequired] ImmutableArray<byte> ButtonMap)
{
    public const string OriginalInitHash = "4D0D23473AB54AC078B0C77A1672D970BF51E7824BAC1724027B5766B706C9EA";
    /// <summary>Native1CFF6E8 is published with this high BYTE by52CA90/51E910/51E990/51E9C0.</summary>
    [JsonIgnore]
    public byte CommandRestrictions => unchecked((byte)(Options >> 24));

    public byte? BattleMessageSpeed { get; init; }
    public byte? NativeBattleSpeedByte { get; init; }

    public void Validate()
    {
        if (InitOutSha256 != OriginalInitHash || (Options & 0x00FFFFFF) != 0x00020001 || ButtonMap.IsDefault || ButtonMap.Length != 12 || !ButtonMap.SequenceEqual(Enumerable.Range(1, 12).Select(value => (byte)value)))
        {
            throw new InvalidDataException("Native input options require the authentic56DA10 template producer; a changed options-menu mapping has no implemented producer; the high BYTE is owned by native command sealing.");
        }
    }

    public ushort Map(ushort raw)
    {
        Validate();
        return Project(raw, Options, ButtonMap);
    }

    public ushort RawForMappedPulse(ushort mapped)
    {
        Validate();
        return InvertPulse(mapped, Options, ButtonMap);
    }

    public static ushort InvertPulse(ushort mapped, uint options, IReadOnlyList<byte> map)
    {
        if (map is null || map.Count != 12)
        {
            throw new InvalidDataException("Native input inverse requires twelve actual mapping bytes.");
        }
        if ((options & 0x20) == 0)
        {
            return mapped;
        }
        ushort raw = 0;
        for (int target = 0; target < 16; target++)
        {
            ushort bit = (ushort)(1 << target);
            if ((mapped & bit) == 0)
            {
                continue;
            }
            ushort[] sources = Enumerable.Range(0, 16).Select(source => (ushort)(1 << source)).Where(source => Project(source, options, map) == bit).ToArray();
            if (sources.Length != 1)
            {
                throw new NotSupportedException("Logical input pulse has no unique native physical source in the current mapping.");
            }
            raw |= sources[0];
        }
        return raw;
    }

    /// <summary>Pure4A2D60 kernel;476882/476892 explicitly narrow its result to an unsignedWORD.</summary>
    public static ushort Project(ushort raw, uint options, IReadOnlyList<byte> map)
    {
        if (map is null || map.Count != 12)
        {
            throw new InvalidDataException("Native input remapping requires twelve actual mapping bytes.");
        }
        if ((options & 0x20) == 0)
        {
            return raw;
        }
        uint result = (uint)(raw & 0xF000);
        for (int bit = 0; bit < 12; bit++)
        {
            if ((raw & (1 << bit)) != 0 && map[bit] != 0)
            {
                result |= 1u << ((map[bit] - 1) & 31);
            }
        }
        return unchecked((ushort)result);
    }
}
