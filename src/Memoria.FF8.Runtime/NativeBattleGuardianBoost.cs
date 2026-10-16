using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>56DCE0/56DD70's widget6 input state. Timers advance only on the original widgetBYTE33 edge.</summary>
public sealed record NativeBattleGuardianBoost([property: JsonRequired] byte Phase, [property: JsonRequired] short Remaining, [property: JsonRequired] short WindowRemaining, [property: JsonRequired] sbyte Window, [property: JsonRequired] bool Enabled, [property: JsonRequired] byte InputGrace, [property: JsonRequired] ushort Percent, [property: JsonRequired] ushort DisplayPercent)
{
    public ushort? ActionId { get; init; }
    public bool PendingPress { get; init; }
    [JsonRequired]
    public long ComponentSubframeTicks { get; init; }
    public static NativeBattleGuardianBoost Register(byte initialWindowFrames, byte durationFrames, bool enabled)
    {
        return new(0, checked((short)(15 * durationFrames)), checked((short)(15 * initialWindowFrames)), -1, enabled, 0, 0, 0);
    }

    public void Validate()
    {
        if (Phase > 6 || Window is < -1 or > 1 || InputGrace > 4 || Percent > 250 || DisplayPercent > 250 || Phase == 6 && Enabled || ComponentSubframeTicks is < 0 or >= TimeSpan.TicksPerSecond)
        {
            throw new InvalidDataException("Native GF Boost state differs from56DD70's retained widget fields.");
        }
    }
}

public sealed record NativeBattleGuardianBoostStep(NativeBattleGuardianBoost Widget, NativeBattleUiRandomState? Random, byte? RecomputePercent, bool Unregister);

public static class NativeBattleGuardianBoostRules
{
    /// <summary>One56DD70 callback. InputEnabled is widgetBYTE30 bit0; Pressed is the original input mask80 edge.</summary>
    public static NativeBattleGuardianBoostStep Advance(NativeBattleGuardianBoost widget, bool frame33, bool inputEnabled, bool pressed, NativeBattleUiRandomState? random)
    {
        widget.Validate();
        byte grace = widget.InputGrace == 0 ? (byte)0 : (byte)(widget.InputGrace - 1);
        widget = widget with { InputGrace = grace };
        byte? recompute = null;
        bool unregister = false;
        switch (widget.Phase)
        {
            case 0:
            case 1:
            case 2:
                widget = widget with { Phase = 2 };
                if (frame33)
                {
                    short window = unchecked((short)(widget.WindowRemaining - 1));
                    widget = widget with { WindowRemaining = window, InputGrace = 4, Phase = window <= 0 ? (byte)3 : (byte)2 };
                }
                break;
            case 3:
                int first = DrawWindow(ref random);
                int second = DrawWindow(ref random);
                widget = widget with { Phase = 4, Window = 1, WindowRemaining = checked((short)(15 * (first + second))) };
                break;
            case 4:
                if (frame33)
                {
                    widget = widget with { Remaining = unchecked((short)(widget.Remaining - 1)), WindowRemaining = unchecked((short)(widget.WindowRemaining - 1)), InputGrace = 4 };
                }
                if (widget.Remaining <= 0)
                {
                    widget = widget with { Phase = 5, Window = 0 };
                    goto case 5;
                }
                if (widget.Enabled && widget.InputGrace != 0)
                {
                    if (inputEnabled && pressed)
                    {
                        ushort value = widget.Percent == 0 ? (ushort)75 : widget.Percent;
                        value = widget.Window != 0 ? (ushort)Math.Min(250, value + 1) : (ushort)75;
                        widget = widget with { Percent = value, DisplayPercent = widget.Window != 0 ? value : widget.DisplayPercent };
                    }
                    if (widget.WindowRemaining <= 0)
                    {
                        sbyte window = widget.Window == 0 ? (sbyte)1 : (sbyte)0;
                        int duration = DrawWindow(ref random);
                        if (window != 0)
                        {
                            duration += DrawWindow(ref random);
                        }
                        widget = widget with { Window = window, WindowRemaining = checked((short)(15 * duration)) };
                    }
                }
                break;
            case 5:
                recompute = widget.Percent == 0 ? (byte)100 : checked((byte)widget.Percent);
                widget = widget with { Phase = 6, Window = 0, Enabled = false };
                break;
            case 6:
                unregister = true;
                break;
        }
        if (widget.DisplayPercent > widget.Percent)
        {
            widget = widget with { DisplayPercent = (ushort)Math.Max(widget.Percent, widget.DisplayPercent - 3) };
        }
        return new(widget, random, recompute, unregister);
    }

    private static int DrawWindow(ref NativeBattleUiRandomState? random)
    {
        NativeBattleUiRandomState source = random ?? throw new NotSupportedException("Native GF Boost requires the actual4A7080 battle UI random owner.");
        random = source.Draw(out ushort value);
        return Math.Clamp(value / 10922 % 3 + 1, 1, 3);
    }
}
