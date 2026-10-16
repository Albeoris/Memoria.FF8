using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Participant DWORD16/20, initialized by484490 and advanced by4842B0; HUD units are a separate view.</summary>
public sealed record NativeBattleAtbState([property: JsonRequired] int Maximum, [property: JsonRequired] int Current)
{
    public static bool CanAdvance(uint actorFlags, ushort savedStatuses, uint battleStatuses) => (actorFlags & 0x81) == 1 && (savedStatuses & 5) == 0 && (battleStatuses & 9) == 0;
    public int Display => checked((int)((long)Current * BattleSession.AtbMaximum / Maximum));

    public void Validate()
    {
        if (Maximum is < 4000 or > 1_024_000 || Maximum % 4000 != 0 || Current < 0 || Current > Maximum)
        {
            throw new InvalidDataException("Native ATB requires its original bounded DWORD maximum/current owners.");
        }
    }

    public NativeBattleAtbState Advance(byte speed, byte multiplier, uint actorFlags, ushort savedStatuses, uint battleStatuses)
    {
        Validate();
        if (!CanAdvance(actorFlags, savedStatuses, battleStatuses))
        {
            return this;
        }
        int rate = (battleStatuses & 4) != 0 ? 5 : (battleStatuses & 2) != 0 ? 15 : 10;
        int gain = rate * multiplier * (speed + 30) / 100;
        return this with { Current = Math.Min(Maximum, Current + gain) };
    }
}

public sealed partial class BattleCombatant
{
    private int displayAtb;
    private byte? nativePlayerAtbSpeed;
    public NativeBattleAtbState? NativeAtb { get; internal set; }

    internal byte NativeAtbSpeed
    {
        get
        {
            int speed = NativePlayerStatRules is not null ? nativePlayerAtbSpeed ?? throw new NotSupportedException("Native ATB requires the current player's projected speedBYTE193.") : Speed;
            return speed is >= 0 and <= 255 ? (byte)speed : throw new NotSupportedException("Native ATB requires its actual speedBYTE193 rather than a scaffold frame gain.");
        }
    }

    private void RefreshNativeAtbSpeed()
    {
        nativePlayerAtbSpeed = NativePlayerStatRules is { } rules && NativeGrowth is { } growth ? checked((byte)rules.Project(NativeMagicSlots, growth).Speed) : null;
    }
}

public sealed partial class BattleSession
{
    private bool HasNativeUiServiceClock => nativeBattleClock is not null && NativeGraphics?.UiServices is not null;

    private void AdvanceNativeUiAtbService()
    {
        if (nativeBattleClock is null)
        {
            // TODO(TD-009): Graphics-only components do not own kernel ATB inputs.
            return;
        }
        NativeBattleUiServices ui = NativeGraphics?.UiServices ?? throw new InvalidDataException("Native4A84E0 lacks its initialized UI services.");
        if (ui.PauseDE9)
        {
            return;
        }
        bool uiAllows = ui.AllowsAtb(definition.NativeSavedOptions is uint options ? unchecked((ushort)options) : null);
        bool stepAllows = nativeUiForceGaugeAdvance || !WaitingForTurnBasedDecision;
        if (!stepAllows && NativeBattleClockRules.Admit(nativeBattleClock, uiAllows, false, NativeScriptAllocationPaused).AtbAdmitted != 0)
        {
            //The host decision hold freezes gauge/GF growth, not registration of an already-full native gauge.
            foreach (BattleCombatant actor in combatants)
            {
                PublishNativeUiReady(actor);
            }
        }
        if (!AdmitNativeBattleClock(uiAllows && stepAllows, false))
        {
            return;
        }
        foreach (BattleCombatant actor in combatants.ToArray())
        {
            AdvanceNativeAtbParticipant(actor, nativeUiBecameReady);
        }
        DispatchGuardianForceCasts();
    }

    private void AdvanceNativeAtbParticipant(BattleCombatant actor, HashSet<string>? becameReady)
    {
        if (actor.NativeAtb is not { } nativeGauge || !NativeBattleAtbState.CanAdvance(actor.NativeCombatantFlags, NativeSavedStatusBits(actor), NativeBattleStatusBits(actor)))
        {
            return;
        }
        bool wasReady = actor.IsReady;
        actor.NativeAtb = nativeGauge.Advance(actor.NativeAtbSpeed, definition.NativeClockRules!.AtbSpeedMultiplier, actor.NativeCombatantFlags, NativeSavedStatusBits(actor), NativeBattleStatusBits(actor));
        PublishNativeUiReady(actor);
        if (!wasReady && actor.IsReady)
        {
            becameReady?.Add(actor.Id);
            Log("Ready", actor.Id, null, 0, $"{actor.Name} is ready.");
        }
    }

    private void InitializeNativeAtb(BattleCombatant actor)
    {
        if (definition.NativeClockRules is null)
        {
            return;
        }
        byte speed = definition.NativeBattleSpeed ?? throw new NotSupportedException("Native484490 requires the actual saved battle-speed BYTE.");
        int maximum = 4000 * (speed + 1);
        actor.NativeAtb = new(maximum, checked((int)((long)maximum * actor.Atb / AtbMaximum)));
    }

    private void RestoreNativeAtb(BattleCombatant actor, NativeBattleAtbState? saved, int display)
    {
        if ((saved is null) != (definition.NativeClockRules is null))
        {
            throw new InvalidDataException("Native raw ATB owner is missing; migrate the standalone snapshot explicitly.");
        }
        saved?.Validate();
        if (saved is not null && (saved.Maximum != actor.NativeAtb!.Maximum || saved.Display != display))
        {
            throw new InvalidDataException("Native raw ATB contradicts its allocated maximum or HUD projection.");
        }
        actor.NativeAtb = saved;
        if (saved is null)
        {
            actor.Atb = display;
        }
    }
}
