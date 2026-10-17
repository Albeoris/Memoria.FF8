using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The signed native 1CD2380 gain latches and 1CD2394 current IDs; no device handles, audible clock or AKAO resource bank is implied.</summary>
public sealed record NativeMusicPlaybackState([property: JsonRequired] int? Gain0, [property: JsonRequired] int? Gain1, [property: JsonRequired] int? CurrentMusicId0, [property: JsonRequired] int? CurrentMusicId1)
{
    [JsonIgnore]
    public bool IsColdProcess => this == FromColdProcess();

    public static NativeMusicPlaybackState FromColdProcess()
    {
        // All four DWORDs are in the authenticated executable's loader-zero BSS tail.
        return new(0, 0, 0, 0);
    }

    public static NativeMusicPlaybackState Unknown()
    {
        return new(null, null, null, null);
    }

    public int ReadGain(int channel)
    {
        ValidateChannel(channel);
        return (channel == 0 ? Gain0 : Gain1) ?? throw new NotSupportedException($"Native music channel {channel} requires its retained 1CD2380 gain latch; the prior value is unknown.");
    }

    public NativeMusicPlaybackState SetGain46BB40(uint channel, int gain)
    {
        if (channel > 1)
        {
            return this with { Gain0 = gain, Gain1 = gain };
        }
        return channel == 0 ? this with { Gain0 = gain } : this with { Gain1 = gain };
    }

    public NativeMusicPlaybackState FadeGain46BBC0(uint channel, byte duration, int target)
    {
        // Duration changes the device call, not this immediately written full-DWORD latch.
        return SetGain46BB40(channel, target);
    }

    public NativeMusicPlaybackState TransitionGain46BC50(uint channel, byte duration, int initial, int target)
    {
        return SetGain46BB40(channel, target);
    }

    public NativeMusicPlaybackState Stop46C060(int channel)
    {
        ValidateChannel(channel);
        return channel == 0 ? this with { CurrentMusicId0 = 0 } : this with { CurrentMusicId1 = 0 };
    }

    public NativeMusicPlaybackState Stop46B800(uint channel)
    {
        return channel > 1 ? Stop46C060(0).Stop46C060(1) : Stop46C060((int)channel);
    }

    public NativeMusicPlaybackState Play46B530(int channel, ReadOnlySpan<byte> resource, int gain, Func<ushort>? readCurrentField = null)
    {
        ValidateChannel(channel);
        int offset = 0;
        while (offset < 8)
        {
            RequireResource(resource, offset + 1);
            if (resource[offset] == (byte)'A')
            {
                break;
            }
            offset++;
        }
        if (offset == 8)
        {
            return this;
        }
        RequireResource(resource, offset + 4);
        if (!resource.Slice(offset, 4).SequenceEqual("AKAO"u8))
        {
            return this;
        }
        RequireResource(resource, offset + 5);
        return PlayEncoded46B530(channel, resource[offset + 4], gain, readCurrentField);
    }

    public NativeMusicPlaybackState PlayEncoded46B530(int channel, byte encodedAkaoId, int gain, Func<ushort>? readCurrentField = null)
    {
        ValidateChannel(channel);
        int id = encodedAkaoId - 1;
        if (id == 17 && ReadCurrentField(readCurrentField) != 339)
        {
            id = 99;
        }
        int current = (channel == 0 ? CurrentMusicId0 : CurrentMusicId1) ?? throw new NotSupportedException($"Native46B530 requires channel {channel}'s retained 1CD2394 current ID before its same-song comparison.");
        if (current == id)
        {
            return this;
        }
        // Native46C060 precedes both writes; on this pure owner it clears only the ID replaced below.
        return channel == 0 ? this with { Gain0 = gain, CurrentMusicId0 = id } : this with { Gain1 = gain, CurrentMusicId1 = id };
    }

    public NativeMusicPlaybackState Play46B510(ReadOnlySpan<byte> resource, Func<ushort>? readCurrentField = null)
    {
        int gain = ReadGain(0);
        return Play46B530(0, resource, gain, readCurrentField);
    }

    public NativeMusicPlaybackState Cross46B850(ReadOnlySpan<byte> resource, int gain, Func<ushort>? readCurrentField = null)
    {
        return Stop46C060(0).Play46B530(0, resource, gain, readCurrentField);
    }

    public NativeMusicPlaybackState Dual46B870(ReadOnlySpan<byte> resource, int gain, Func<ushort>? readCurrentField = null)
    {
        return Play46B530(1, resource, gain, readCurrentField);
    }

    public NativeMusicPlaybackState Concert46B990(uint mask, Func<ushort>? readCurrentField = null)
    {
        if (mask == 4095 && ReadCurrentField(readCurrentField) != 634)
        {
            return this;
        }
        // Every remaining concert mask stops channel0 and writes ID43 before device selection.
        return this with { CurrentMusicId0 = 43 };
    }

    private static ushort ReadCurrentField(Func<ushort>? readCurrentField)
    {
        return readCurrentField is not null ? readCurrentField() : throw new NotSupportedException("Native music selection requires the actual retained 1CD2FC0 current-field WORD for this branch.");
    }

    private static void RequireResource(ReadOnlySpan<byte> resource, int count)
    {
        if (resource.Length < count)
        {
            throw new InvalidDataException("Native46B530 AKAO scan requires the actual reached source bytes.");
        }
    }

    private static void ValidateChannel(int channel)
    {
        if ((uint)channel > 1)
        {
            throw new NotSupportedException("Native music playback addresses only the two owned physical gain/current-ID slots.");
        }
    }
}
