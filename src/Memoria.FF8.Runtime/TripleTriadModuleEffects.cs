using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Saved native WORD counters at1CFEFAC/1CFEFAE/1CFEFB0.</summary>
public sealed record TripleTriadStatistics([property: JsonRequired] ushort Wins, [property: JsonRequired] ushort Losses, [property: JsonRequired] ushort Draws)
{
    public TripleTriadStatistics Complete(int nativeResult)
    {
        return nativeResult switch
        {
            -1 => this,
            0 => this with { Wins = unchecked((ushort)(Wins + 1)) },
            1 => this with { Losses = unchecked((ushort)(Losses + 1)) },
            2 => this with { Draws = unchecked((ushort)(Draws + 1)) },
            _ => throw new ArgumentOutOfRangeException(nameof(nativeResult))
        };
    }
}

public enum TripleTriadPromptPhase
{
    FadingIn,
    AwaitingChoice,
    FadingOut,
    Accepted,
    Cancelled
}

/// <summary>Native536CB0 rule prompt. Each Advance is one actual card-module callback, without assuming a wall-clock rate.</summary>
public readonly record struct TripleTriadPromptState([property: JsonRequired] TripleTriadPromptPhase Phase, [property: JsonRequired] int PhaseCallbacks, [property: JsonRequired] uint RandomState)
{
    public static TripleTriadPromptState Create(uint randomState)
    {
        return new TripleTriadPromptState(TripleTriadPromptPhase.FadingIn, 0, randomState);
    }

    /// <param name="choice">Native prompt result: null no selection,0 play,1 quit. Early fade-in inputs are not consumed by the original prompt.</param>
    public TripleTriadPromptState Advance(int? choice = null)
    {
        Validate();
        if (choice is not (null or 0 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(choice));
        }
        if (Phase is TripleTriadPromptPhase.Accepted or TripleTriadPromptPhase.Cancelled)
        {
            throw new InvalidOperationException("The original card prompt has already returned.");
        }
        if (Phase == TripleTriadPromptPhase.FadingIn && PhaseCallbacks + 1 < 15)
        {
            return this with { PhaseCallbacks = PhaseCallbacks + 1 };
        }
        if (Phase == TripleTriadPromptPhase.FadingOut)
        {
            return this with { Phase = PhaseCallbacks + 1 == 15 ? TripleTriadPromptPhase.Cancelled : TripleTriadPromptPhase.FadingOut, PhaseCallbacks = PhaseCallbacks + 1 };
        }
        // On fade-in callback15,536D55 jumps directly to phase1 in the SAME callback.
        //536D94 increments the saved DWORD before querying the prompt result, including a quit choice.
        uint next = unchecked(RandomState + 1);
        return choice switch
        {
            0 => new TripleTriadPromptState(TripleTriadPromptPhase.Accepted, 0, next),
            1 => new TripleTriadPromptState(TripleTriadPromptPhase.FadingOut, 0, next),
            _ => new TripleTriadPromptState(TripleTriadPromptPhase.AwaitingChoice, 0, next)
        };
    }

    public void Validate()
    {
        if (!Enum.IsDefined(Phase) || Phase is TripleTriadPromptPhase.FadingIn or TripleTriadPromptPhase.FadingOut && PhaseCallbacks is < 0 or >= 15 || Phase is TripleTriadPromptPhase.AwaitingChoice or TripleTriadPromptPhase.Accepted && PhaseCallbacks != 0 || Phase == TripleTriadPromptPhase.Cancelled && PhaseCallbacks != 15)
        {
            throw new InvalidDataException("Native card prompt has an invalid phase or callback counter.");
        }
    }
}
