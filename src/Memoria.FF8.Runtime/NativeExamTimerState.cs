using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum NativeTimerCallbackKind
{
    NormalField,
    World,
    Menu,
    Battle,
    MovieField
}

/// <summary>An inclusive phase interval correlated with one exact native remaining DWORD.</summary>
public sealed record NativeTimerAlternative([property: JsonRequired] uint FirstPhase, [property: JsonRequired] uint LastPhase, [property: JsonRequired] int Remaining)
{
    public NativeTimerDisplayBlink? DisplayBlink { get; init; }
}

/// <summary>Exact possibilities for native 4701B0; missing historical phase is never replaced with zero.</summary>
public sealed partial record NativeExamTimerState
{
    public const uint Increment = 2191;
    public const uint Threshold = 0x20000;
    [JsonRequired]
    public required int Version { get; init; }
    [JsonRequired]
    public required NativeTimerAlternative[] Alternatives { get; init; }
    [JsonRequired]
    public required long ClockCalls { get; init; }
    [JsonRequired]
    public required bool TimerMemoryActivated { get; init; }
    [JsonRequired]
    public required bool DisplayEnabled { get; init; }
    [JsonRequired]
    public required ushort DisplayX { get; init; }
    [JsonRequired]
    public required byte DisplayY { get; init; }
    [JsonRequired]
    public required bool? Event182Mask4 { get; init; }
    [JsonRequired]
    public required bool? SaveGlobalMask4 { get; init; }
    [JsonRequired]
    public required bool? Event183Mask1 { get; init; }

    public static NativeExamTimerState FromReachedSet(int remaining)
    {
        return new NativeExamTimerState
        {
            Version = 1,
            Alternatives = [new NativeTimerAlternative(0, uint.MaxValue, remaining)],
            ClockCalls = 0,
            TimerMemoryActivated = true,
            DisplayEnabled = false,
            DisplayX = 0,
            DisplayY = 0,
            Event182Mask4 = null,
            SaveGlobalMask4 = null,
            Event183Mask1 = null
        };
    }

    public static NativeExamTimerState FromColdProcess()
    {
        // The executable's zero-filled process data establishes phase0 once.
        // Title menu25 does not call4701B0. Timer memory claims begin at a command.
        return FromReachedSet(0) with { Alternatives = [new NativeTimerAlternative(0, 0, 0) { DisplayBlink = new(1, false, 0) }], TimerMemoryActivated = false, DisplayIntensity814 = 0 };
    }

    public void Validate()
    {
        // Native523230 can replace WORD182/1CFF6E2 without calling the timer UI.
        // DisplayEnabled records that UI call independently of these mutable mask4 bits.
        if (Version != 1 || ClockCalls < 0 || Alternatives is null || Alternatives.Length == 0 || Alternatives.Any(item => item is null || item.FirstPhase > item.LastPhase) || !TimerMemoryActivated && Alternatives.Any(item => item.Remaining != 0))
        {
            throw new InvalidDataException("Invalid native exam timer interval state.");
        }
        foreach (NativeTimerAlternative alternative in Alternatives)
        {
            alternative.DisplayBlink?.Validate();
        }
        NativeTimerAlternative[] canonical = Normalize(Alternatives);
        if (!canonical.SequenceEqual(Alternatives))
        {
            throw new InvalidDataException("Native exam timer alternatives must be ordered, disjoint and merged for each remaining value.");
        }
    }

    public NativeExamTimerState SetRemaining(int remaining)
    {
        Validate();
        return this with { Alternatives = Normalize(Alternatives.Select(item => item with { Remaining = remaining })), TimerMemoryActivated = true };
    }

    public NativeExamTimerState ClearSavedStateForNewGame()
    {
        Validate();
        // 56D9F0->47E410 clears saved remaining, never the process fraction or UI.
        // init.out and later field commands own their event flags independently.
        return this with
        {
            Alternatives = Normalize(Alternatives.Select(item => item with { Remaining = 0 })),
            TimerMemoryActivated = false,
            Event182Mask4 = null,
            SaveGlobalMask4 = null,
            Event183Mask1 = null
        };
    }

    public NativeExamTimerState Display(byte x, byte y)
    {
        Validate();
        return SetDisplayEnabled4A6CC0(true) with { TimerMemoryActivated = true, DisplayX = x, DisplayY = y, Event182Mask4 = true, SaveGlobalMask4 = true };
    }

    public NativeExamTimerState KillDisplay()
    {
        Validate();
        return SetDisplayEnabled4A6CC0(false) with { TimerMemoryActivated = true, Event182Mask4 = false, SaveGlobalMask4 = false };
    }

    public int RequireRemaining()
    {
        Validate();
        int remaining = Alternatives[0].Remaining;
        if (Alternatives.Any(item => item.Remaining != remaining))
        {
            throw new NotSupportedException("GETTIMER requires one exact remaining DWORD; prior native fractional phase still permits different values.");
        }
        return remaining;
    }

    public NativeExamTimerState AdvanceCallbacks(NativeTimerCallbackKind kind, int callbacks, bool paused = false, bool clockDisabled = false)
    {
        Validate();
        if (!Enum.IsDefined(kind) || callbacks < 0 || callbacks > 30000)
        {
            throw new ArgumentOutOfRangeException(nameof(callbacks));
        }
        if (paused || clockDisabled || callbacks == 0)
        {
            return this;
        }
        int perCallback = kind switch
        {
            NativeTimerCallbackKind.NormalField or NativeTimerCallbackKind.World => 2,
            NativeTimerCallbackKind.Menu => 1,
            NativeTimerCallbackKind.Battle or NativeTimerCallbackKind.MovieField => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        int calls = checked(callbacks * perCallback);
        NativeTimerAlternative[] alternatives = Alternatives;
        for (int call = 0; call < calls; call++)
        {
            List<NativeTimerAlternative> shifted = [];
            foreach (NativeTimerAlternative item in alternatives)
            {
                ulong first = (ulong)item.FirstPhase + Increment;
                ulong last = (ulong)item.LastPhase + Increment;
                if (last <= uint.MaxValue)
                {
                    AddShifted(shifted, (uint)first, (uint)last, item);
                }
                else if (first > uint.MaxValue)
                {
                    AddShifted(shifted, (uint)(first - 0x100000000UL), (uint)(last - 0x100000000UL), item);
                }
                else
                {
                    AddShifted(shifted, (uint)first, uint.MaxValue, item);
                    AddShifted(shifted, 0, (uint)(last - 0x100000000UL), item);
                }
            }
            alternatives = Normalize(shifted);
        }
        return this with { Alternatives = alternatives, ClockCalls = checked(ClockCalls + calls) };
    }

    public string FieldDisplayText()
    {
        Validate();
        int[] values = Alternatives.Select(item => Math.Clamp(unchecked(item.Remaining - 1), 0, 6039)).Distinct().Order().ToArray();
        string Format(int value)
        {
            return $"{value / 60:00}:{value % 60:00}";
        }
        return values.Length == 1 ? Format(values[0]) : $"{Format(values[0])}–{Format(values[^1])}";
    }

    private static void AddShifted(List<NativeTimerAlternative> target, uint first, uint last, NativeTimerAlternative source)
    {
        int remaining = source.Remaining;
        if (remaining == 0 || last < Threshold)
        {
            target.Add(source with { FirstPhase = first, LastPhase = last });
            return;
        }
        if (first < Threshold)
        {
            target.Add(source with { FirstPhase = first, LastPhase = Threshold - 1 });
            first = Threshold;
        }
        int decremented = unchecked(remaining - 1);
        if ((ulong)last - first >= ushort.MaxValue)
        {
            target.Add(source with { FirstPhase = 0, LastPhase = ushort.MaxValue, Remaining = decremented });
            return;
        }
        uint lowFirst = first & ushort.MaxValue;
        uint lowLast = last & ushort.MaxValue;
        if (lowFirst <= lowLast)
        {
            target.Add(source with { FirstPhase = lowFirst, LastPhase = lowLast, Remaining = decremented });
        }
        else
        {
            target.Add(source with { FirstPhase = lowFirst, LastPhase = ushort.MaxValue, Remaining = decremented });
            target.Add(source with { FirstPhase = 0, LastPhase = lowLast, Remaining = decremented });
        }
    }

    private static NativeTimerAlternative[] Normalize(IEnumerable<NativeTimerAlternative> alternatives)
    {
        List<NativeTimerAlternative> result = [];
        foreach (NativeTimerAlternative item in alternatives.OrderBy(item => item.Remaining).ThenBy(item => item.DisplayBlink?.PhaseMask0To63 ?? 0).ThenBy(item => item.DisplayBlink?.Phase64Possible ?? false).ThenBy(item => (int?)item.DisplayBlink?.CachedRemaining817 ?? -1).ThenBy(item => item.FirstPhase))
        {
            if (result.Count != 0 && result[^1].Remaining == item.Remaining && result[^1].DisplayBlink == item.DisplayBlink && (ulong)item.FirstPhase <= (ulong)result[^1].LastPhase + 1)
            {
                result[^1] = result[^1] with { LastPhase = Math.Max(result[^1].LastPhase, item.LastPhase) };
            }
            else
            {
                result.Add(item);
            }
        }
        return result.ToArray();
    }
}
