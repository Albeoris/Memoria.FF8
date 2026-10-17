using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>4A94D0/4A8E30's publication clock and actual942/957 menu owners.</summary>
public sealed record NativeBattleUiServices([property: JsonRequired] uint Counter, [property: JsonRequired] uint LastPublication, [property: JsonRequired] uint Duration, [property: JsonRequired] bool PendingPublication, [property: JsonRequired] bool? Frame33, [property: JsonRequired] ushort Flags942, [property: JsonRequired] sbyte Menu957, [property: JsonRequired] uint ReadyMask, [property: JsonRequired] bool PauseDE9)
{
    [JsonRequired]
    public NativeBattleUiCommands Commands { get; init; } = NativeBattleUiCommands.Clear();

    public sbyte? Menu955 { get; init; }
    public NativeBattleUiWindows? Windows { get; init; }
    public static NativeBattleUiServices Initialize()
    {
        //506CF0 phase1→47D890→4A94D0;4AD670 sets942bit40 and clears1D75410.
        return new(0, 0, 1, false, null, 0x40, -1, 0, false) { Windows = NativeBattleUiWindows.Initialize4A94D0() };
    }

    public NativeBattleUiServices AdvancePublication()
    {
        uint next = unchecked(Counter + 1);
        bool published = PendingPublication && Duration <= unchecked(next - LastPublication);
        return this with { Counter = next, LastPublication = published ? next : LastPublication, PendingPublication = !published && PendingPublication, Frame33 = published };
    }

    public NativeBattleUiServices QueuePublication(uint duration)
    {
        //4A9220 normalizes a zero duration to1; publication remains pending until4A8E30 observes it.
        return this with { Duration = duration == 0 ? 1u : duration, PendingPublication = true };
    }

    public NativeBattleUiServices SetFlags(ushort set, ushort clear = 0)
    {
        return this with { Flags942 = (ushort)((Flags942 & ~clear) | set) };
    }

    public NativeBattleUiServices ApplyWindowCommand4AD400(ushort command)
    {
        if (command == 64)
        {
            NativeBattleUiServices state = this;
            if ((Flags942 & 0x40) == 0)
            {
                NativeBattleUiWindows windows = Windows ?? throw new NotSupportedException("Native UI64 requires its retained physical window bank; migrate the named snapshot explicitly.");
                state = state with { Windows = windows.Refresh4B9C40(7) };
            }
            return state.SetFlags(0x40);
        }
        if (command == 80)
        {
            NativeBattleUiWindows windows = Windows ?? throw new NotSupportedException("Native UI80 requires its retained vitals window process; migrate the named snapshot explicitly.");
            return this with { Windows = windows.OpenVitals4B0430() };
        }
        throw new ArgumentOutOfRangeException(nameof(command));
    }

    public bool AllowsAtb(ushort? savedOptions)
    {
        //4A9450 can be resolved without an unknown settings WORD only when another original OR operand proves admission.
        if (Menu957 < 0 || Menu957 == 4 || (Flags942 & 0x10) == 0 || ReadyMask == 0)
        {
            return true;
        }
        ushort options = savedOptions ?? throw new NotSupportedException("Native4A9450 Wait admission requires its actual saved options WORD1CFE73C.");
        return (options & 1) == 0;
    }

    public void Validate()
    {
        Commands.Validate(ReadyMask);
        Windows?.Validate();
        if (Duration == 0)
        {
            throw new InvalidDataException("Native4A9220 duration must include its zero-to-one producer normalization.");
        }
    }
}
