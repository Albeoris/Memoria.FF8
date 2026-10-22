namespace Memoria.FF8.Runtime;

public sealed record NativeBattleUiRandomState(uint State)
{
    public NativeBattleUiRandomState Draw(out ushort result)
    {
        if (State >= 0x8000)
        {
            throw new InvalidDataException("Native battle UI random state exceeds its original15-bit initializer.");
        }
        //4A7080's state is initialized by4A94D0 from saved playtimeDWORD1CFE928&7FFF.
        result = NativeBattleRandomTransitions.Draw125(State);
        return new(result);
    }
}

public sealed record BattleNativeRenzokukenInputState(byte Phase = 0, short PoseClock = 0, short SubframeClock = 0, byte ContactIndex = 0, short TriggerTiming = 0);
public sealed record BattleNativeRenzokukenContact(byte Index, byte Total, short TriggerTiming);
public sealed record BattleNativeRenzokukenInputStep(BattleNativeRenzokukenInputState State, BattleNativeRenzokukenContact? Contact, NativeBattleUiRandomState? Random)
{
    public bool PlayTriggerSound { get; init; }
}

public static partial class NativeBattleRenzokukenRules
{
    /// <summary>One4BA6C0 input callback; poseAdvanced is the original widgetBYTE33 clock edge.</summary>
    public static BattleNativeRenzokukenInputStep AdvanceInput(BattleNativeRenzokukenTimeline timeline, BattleNativeRenzokukenInputState state, bool poseAdvanced, bool triggerPressed, bool automatic, NativeBattleUiRandomState? random)
    {
        if (timeline.InputThresholds.IsDefaultOrEmpty || state.ContactIndex > timeline.InputThresholds.Length || state.SubframeClock is < 0 or > 3 || state.Phase is not (0 or 1 or 2 or 3 or 4 or 6 or 8))
        {
            throw new InvalidDataException("Native Renzokuken input callback lacks its valid timing state.");
        }
        if (state.Phase == 8)
        {
            return new(state, null, random);
        }
        if (state.ContactIndex == timeline.InputThresholds.Length)
        {
            throw new InvalidDataException("Native Renzokuken input remains open after its final contact.");
        }
        if (state.Phase == 0)
        {
            return new(state with { Phase = 1 }, null, random);
        }
        if (state.Phase == 1)
        {
            if (!poseAdvanced)
            {
                return new(state, null, random);
            }
            state = state with { Phase = 2 };
        }
        int clock = state.PoseClock + state.SubframeClock;
        if (state.Phase is 2 or 3)
        {
            state = state with { Phase = 4, TriggerTiming = 0 };
        }
        BattleNativeRenzokukenContact? contact = null;
        bool playTriggerSound = false;
        int threshold = timeline.InputThresholds[state.ContactIndex];
        bool before = state.Phase == 4 && clock <= threshold;
        if (before)
        {
            if (triggerPressed && state.TriggerTiming == 0)
            {
                state = state with { TriggerTiming = 1 };
            }
        }
        else
        {
            state = state with { Phase = 6 };
            int distance = clock - threshold;
            bool automaticChoice = automatic && distance == 25;
            if (automaticChoice)
            {
                random = (random ?? throw new NotSupportedException("Automatic Renzokuken requires its actual4A7080 battle UI random state.")).Draw(out ushort roll);
                state = state with { TriggerTiming = roll <= 0x2000 ? (short)1 : (short)25 };
                playTriggerSound = true;
            }
            else
            {
                if (!automatic && triggerPressed && state.TriggerTiming == 0)
                {
                    short timing = checked((short)(distance + 5));
                    state = state with { TriggerTiming = NativeGunbladeAttack.TriggerSucceeded(timing) ? timing : (short)1 };
                    playTriggerSound = NativeGunbladeAttack.TriggerSucceeded(timing);
                }
                if (distance >= 27)
                {
                    byte index = checked((byte)(state.ContactIndex + 1));
                    contact = new(index, checked((byte)timeline.InputThresholds.Length), state.TriggerTiming);
                    state = state with { ContactIndex = index, TriggerTiming = 0, Phase = index == timeline.InputThresholds.Length ? (byte)8 : (byte)3 };
                }
            }
        }
        state = poseAdvanced ? state with { PoseClock = unchecked((short)(state.PoseClock + 4)), SubframeClock = 0 } : state with { SubframeClock = (short)Math.Min(3, state.SubframeClock + 1) };
        return new(state, contact, random) { PlayTriggerSound = playTriggerSound };
    }
}
