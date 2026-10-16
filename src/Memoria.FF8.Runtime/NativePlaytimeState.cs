using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>4701B0's saved playtimeDWORD1CFE928 and separate process fractionDWORD1CDC73C.</summary>
public sealed record NativePlaytimeState([property: JsonRequired] uint Seconds, [property: JsonRequired] uint Phase, [property: JsonRequired] long ClockCalls)
{
    public static NativePlaytimeState FromColdProcess()
    {
        //The preserved executable's zero-filled data owns both initial DWORDs; New Game clears only saved seconds.
        return new(0, 0, 0);
    }

    public void Validate()
    {
        if (Phase >= NativeExamTimerState.Threshold || ClockCalls < 0)
        {
            throw new InvalidDataException("Native playtime has an invalid process fraction or admitted callback count.");
        }
    }

    public NativePlaytimeState ClearSavedStateForNewGame()
    {
        Validate();
        //56D9F0 clears1CFDC58..1CFEFF8;56DA10's2812-byte init.out copy ends1CFE7A4 before1CFE928.
        return this with { Seconds = 0 };
    }

    public NativePlaytimeState AdvanceTo(long clockCalls)
    {
        Validate();
        if (clockCalls < ClockCalls)
        {
            throw new InvalidDataException("Native playtime cannot consume a reversed4701B0 call count.");
        }
        uint phase = Phase;
        uint seconds = Seconds;
        for (long call = ClockCalls; call < clockCalls; call++)
        {
            phase += NativeExamTimerState.Increment;
            if ((phase & 0xFFFE0000) != 0)
            {
                seconds = unchecked(seconds + 1);
                phase = (ushort)phase;
            }
        }
        return new(seconds, phase, clockCalls);
    }

    public NativeBattleUiRandomState CreateBattleUiRandom()
    {
        Validate();
        return new(Seconds & 0x7FFF);
    }
}
