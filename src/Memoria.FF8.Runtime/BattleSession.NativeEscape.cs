namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEscapeState
{
    public bool Held { get; init; }
    public bool PresentationHeld { get; init; }
    public int HeldFrames { get; init; }
    public bool Requested { get; init; }
    public bool Completed { get; init; }

    public void Validate()
    {
        if (HeldFrames is < 0 or >= 60 || !Held && (HeldFrames != 0 || Requested))
        {
            throw new InvalidDataException("Invalid consecutive escape-button hold state.");
        }
    }
}

public static class NativeBattleEscapeRules
{
    public static bool Eligible(ushort savedStatuses, uint battleStatuses, uint combatantFlags)
    {
        return (combatantFlags & 1) != 0 && (savedStatuses & 5) == 0 && (battleStatuses & 0x4009) == 0 && (combatantFlags & 0x4000) == 0;
    }

    public static byte Threshold(byte advantage, IReadOnlyList<byte> enemyInformation)
    {
        return advantage switch
        {
            1 or 2 => 16,
            3 or 4 => 255,
            0 => enemyInformation.Count == 0 ? (byte)255 : enemyInformation.Any(value => (value & 0x10) != 0) ? (byte)16 : enemyInformation.Any(value => (value & 8) != 0) ? (byte)128 : (byte)64,
            _ => throw new ArgumentOutOfRangeException(nameof(advantage))
        };
    }

    public static bool Succeeds(byte random, byte threshold) => random <= threshold;
}

public sealed partial class BattleSession
{
    private NativeBattleEscapeState nativeEscape = new();
    public NativeBattleEscapeState Escape => nativeEscape;
    private ushort EscapeFlags => definition.ExpandedFormationFlags is byte formationFlags ? NativeBattleEntryRules.MergeFlags(definition.NativeFlags, formationFlags) : ResolvedNativeFlags;
    public bool EscapeDisabled => nativeScriptState.EscapeDisabled ?? (EscapeFlags & 1) != 0;
    private bool EscapeAdvancesBattleTime => nativeEscape.Held && Outcome == BattleOutcome.Active && !HasPendingExpandedTerminal && NativeEntryReady && PendingSlotsDeal is null && BattleClockUi?.PauseDE9 != true;
    private bool UsesRealTimeBattleClock => Mode != BattleAtbMode.TurnBased || EscapeAdvancesBattleTime;

    public void PublishNativeEscapeButtons(ushort buttons)
    {
        bool held = PendingSlotsDeal is null && (buttons & 3) == 3;
        nativeEscape = held ? nativeEscape with { Held = true } : nativeEscape with { Held = false, HeldFrames = 0, Requested = false };
    }

    public bool CanParticipateInEscape(BattleCombatant actor) => actor.IsPlayer && (actor.IsActive) && NativeBattleEscapeRules.Eligible(NativeSavedStatusBits(actor), NativeBattleStatusBits(actor), actor.NativeCombatantFlags);

    private bool HasEscapeParticipant => allies.Any(CanParticipateInEscape);

    private IEnumerable<BattleCombatant> CurrentEscapeEnemies => enemies.Where(actor => actor.IsActive);

    private void AdvanceNativeEscapeFrame()
    {
        if (PendingSlotsDeal is not null || Outcome != BattleOutcome.Active || HasPendingExpandedTerminal || !NativeEntryReady || BattleClockUi?.PauseDE9 == true)
        {
            return;
        }
        if (nativeEscape.PresentationHeld != nativeEscape.Held)
        {
            BattlePresentationProcess process = Presentation ?? throw new InvalidOperationException("Escape input requires its current presentation.");
            process.SetEscapePresentationHeld(nativeEscape.Held);
            nativeEscape = nativeEscape with { PresentationHeld = nativeEscape.Held };
        }
        if (!nativeEscape.Held)
        {
            return;
        }
        int heldFrames = (nativeEscape.HeldFrames + 1) % 60;
        nativeEscape = nativeEscape with { HeldFrames = heldFrames };
        if (!EscapeDisabled && heldFrames == 0 && HasEscapeParticipant)
        {
            byte advantage = NativeEntryAdvantage ?? ((EscapeFlags & 0xE0) switch { 0x20 => (byte)1, 0x40 => (byte)2, _ => (byte)0 });
            List<byte> information = [];
            if (advantage == 0)
            {
                BattleCombatant[] enemies = CurrentEscapeEnemies.ToArray();
                bool eligibleEnemy = enemies.Any(actor => (NativeSavedStatusBits(actor) & 0x25) == 0 && (NativeBattleStatusBits(actor) & 0x4001) == 0);
                if (eligibleEnemy)
                {
                    information.AddRange(enemies.Where(actor => (NativeSavedStatusBits(actor) & 1) == 0).Select(actor => actor.SourceDefinition.NativeTemplate?.Information254 ?? actor.SourceDefinition.Information254 ?? throw new NotSupportedException("Native escape requires the enemy's original information BYTE254.")));
                }
            }
            byte threshold = NativeBattleEscapeRules.Threshold(advantage, information);
            if (NativeBattleEscapeRules.Succeeds(checked((byte)NextRandomByte()), threshold))
            {
                nativeEscape = nativeEscape with { Requested = true };
            }
        }
        if (nativeEscape.Requested && HasEscapeParticipant && !HasNativeEscapeInterlock)
        {
            BeginNativeEscape4862A0();
        }
    }

    private bool HasNativeEscapeInterlock
    {
        get
        {
            return ActiveAction is not null || ExpandedTimedLimitOwnsInput || HasQueuedRosterOperations || enemies.Any(HasExpandedEnemyDeathAction);
        }
    }

    private void BeginNativeEscape4862A0()
    {
        StopTurnAdvance();
        AdmitExpandedHostedTerminal(BattleOutcome.Escaped);
        nativeEscape = nativeEscape with { Requested = false };
    }
}
