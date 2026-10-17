namespace Memoria.FF8.Runtime;

public sealed record NativeTimerDisplayBlink(ulong PhaseMask0To63, bool Phase64Possible, byte? CachedRemaining817)
{
    public void Validate()
    {
        if (PhaseMask0To63 == 0 && !Phase64Possible)
        {
            throw new InvalidDataException("Native timer display requires at least one correlated visual phase.");
        }
    }

    public static NativeTimerDisplayBlink Advance4A6D00(NativeTimerDisplayBlink? source, byte remaining)
    {
        if (source?.CachedRemaining817 is byte cached && cached != remaining)
        {
            return new(1, false, remaining);
        }
        ulong mask = source is null ? ulong.MaxValue : source.PhaseMask0To63 << 1;
        bool phase64 = source is null || source.Phase64Possible || (source.PhaseMask0To63 & (1UL << 63)) != 0;
        if (source?.CachedRemaining817 is null)
        {
            mask |= 1;
        }
        return new(mask, phase64, remaining);
    }
}

public sealed partial record NativeExamTimerState
{
    public bool HasSameClock(NativeExamTimerState other)
    {
        Validate();
        other.Validate();
        return ClockCalls == other.ClockCalls && Normalize(Alternatives.Select(item => item with { DisplayBlink = null })).SequenceEqual(Normalize(other.Alternatives.Select(item => item with { DisplayBlink = null })));
    }

    public ushort? DisplayIntensity814 { get; init; }

    public NativeExamTimerState SetDisplayIntensity4A6CB0(ushort intensity)
    {
        Validate();
        return this with { DisplayIntensity814 = intensity };
    }

    public NativeExamTimerState SetDisplayEnabled4A6CC0(bool enabled)
    {
        Validate();
        return this with { DisplayEnabled = enabled, Alternatives = enabled ? Normalize(Alternatives.Select(item => item with { DisplayBlink = new(1, false, unchecked((byte)item.Remaining)) })) : Alternatives };
    }

    public NativeExamTimerState AdvanceDisplay4A6D00()
    {
        Validate();
        return this with { Alternatives = Normalize(Alternatives.Select(item => item with { DisplayBlink = NativeTimerDisplayBlink.Advance4A6D00(item.DisplayBlink, unchecked((byte)item.Remaining)) })) };
    }

    public byte ReadDisplayColor4A6EB0()
    {
        ushort intensity = DisplayIntensity814 ?? throw new NotSupportedException("Native timer display intensity WORD1D2B814 is unknown.");
        return unchecked((byte)(intensity >> 5));
    }

    public bool ReadDisplayColon4A6EB0()
    {
        Validate();
        bool[] visible = Alternatives.SelectMany(item =>
        {
            NativeTimerDisplayBlink blink = item.DisplayBlink ?? throw new NotSupportedException("Native timer visual phase BYTE1D2B816 is unknown.");
            List<bool> values = [];
            if ((blink.PhaseMask0To63 & ((1UL << 30) - 1)) != 0)
            {
                values.Add(true);
            }
            if ((blink.PhaseMask0To63 >> 30) != 0 || blink.Phase64Possible)
            {
                values.Add(false);
            }
            return values;
        }).Distinct().ToArray();
        return visible.Length == 1 ? visible[0] : throw new NotSupportedException("Native timer colon differs across retained visual phase alternatives.");
    }
}
