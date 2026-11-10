using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public readonly record struct Ff7BattleEscapeState(ushort Counter, ushort AcknowledgedCounter, ushort Remainder = 0, ushort ElapsedTicks = 0, ushort HeldTicks = 0, ushort CachedRoll = 0);

public readonly record struct Ff7BattleEscapeInput(Ff7BattleEscapeState State, bool Escaped, bool ShowBlockedMessage);

/// <summary>Escape progress has separate battle-clock and presentation owners.</summary>
public static class Ff7NativeBattleEscape
{
    public const int FieldResult = 8;
    private const int Window = 8192;

    public static Ff7BattleEscapeState Start(SceneFormation formation, byte battleType)
    {
        ArgumentNullException.ThrowIfNull(formation);
        ushort counter = battleType is 1 or 3 ? (ushort)1 : unchecked((ushort)formation.EscapeCounter);
        return new(counter, counter);
    }

    public static bool IsAllowed(SceneFormation formation, bool requestDisablesEscape = false) => (formation.BattleFlags & 4) != 0 && !requestDisablesEscape;

    public static Ff7BattleEscapeState AdvanceClock(Ff7BattleEscapeState state, bool bothButtonsHeld, byte battleSpeed, byte battleType)
    {
        if (state.Counter == 0 || state.Counter != state.AcknowledgedCounter)
        {
            return state;
        }

        ushort elapsed = unchecked((ushort)(state.ElapsedTicks + 1));
        ushort held = unchecked((ushort)(state.HeldTicks + (bothButtonsHeld ? 1 : 0)));
        int time = state.Remainder + Ff7NativeAtb.BaseRate(battleSpeed);
        if (time <= Window)
        {
            return state with { Remainder = (ushort)time, ElapsedTicks = elapsed, HeldTicks = held };
        }

        ushort counter = state.Counter;
        if (elapsed < held * 2 && (state.CachedRoll < 0x4000 || battleType is 1 or 3))
        {
            counter--;
        }
        return state with { Counter = counter, Remainder = (ushort)(time - Window), ElapsedTicks = 0, HeldTicks = 0 };
    }

    public static Ff7BattleEscapeInput UpdatePresentation(Ff7BattleEscapeState state, bool bothButtonsHeld, bool escapeAllowed, bool anyPartyActorCanMove, bool blockedByBothPincerGroups, bool battleEnding, Ff7BattleRng random)
    {
        ArgumentNullException.ThrowIfNull(random);
        state = state with { CachedRoll = random.NextWord() };
        if (!bothButtonsHeld || battleEnding)
        {
            return new(state, false, false);
        }
        if (!escapeAllowed || !anyPartyActorCanMove || blockedByBothPincerGroups)
        {
            return new(state, false, true);
        }

        bool escaped = state.Counter == 0 && state.AcknowledgedCounter != 0;
        return new(state with { AcknowledgedCounter = state.Counter }, escaped, false);
    }
}
