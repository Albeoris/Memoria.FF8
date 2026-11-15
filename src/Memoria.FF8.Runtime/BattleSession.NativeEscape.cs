namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEscapeState
{
    public bool Held { get; init; }
    public bool PresentationHeld { get; init; }
    public int HeldFrames { get; init; }
    public bool Requested { get; init; }
    public bool Completed { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public byte ExpandedOutcomeFrames { get; init; }

    public void Validate()
    {
        if (HeldFrames is < 0 or >= 60 || !Held && (HeldFrames != 0 || Requested) || ExpandedOutcomeFrames > NativeBattleOutcomeCallbacks.Duration47DFC0(2) || ExpandedOutcomeFrames > 0 && (Completed || Requested))
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
    public bool IsExpandedEscapeRunning => UsesExpandedRoster && expandedPresentationProcess is null && !HasPendingExpandedEscape && Outcome == BattleOutcome.Active && nativeEscape.PresentationHeld;
    public bool IsExpandedEscapeFinishing => HasPendingExpandedEscape || expandedHostedTerminal?.Outcome == BattleOutcome.Escaped || UsesExpandedRoster && Outcome == BattleOutcome.Escaped;
    public byte? ExpandedEscapeScreenFade
    {
        get
        {
            byte remaining = nativeEscape.ExpandedOutcomeFrames;
            const short duration = NativeBattleOutcomeCallbacks.OutcomeFadeDuration501E10;
            if (expandedPresentationProcess is not null || !IsExpandedEscapeFinishing || remaining > duration)
            {
                return null;
            }
            // 47E030 polls before 500900 runs the first fade callback and before 47CCB0 decrements the outcome clock.
            return NativeBattleOutcomeCallbacks.InterpolateFade501D10((short)(duration - remaining), duration, 128, 0);
        }
    }

    private bool HasPendingExpandedEscape => nativeEscape.ExpandedOutcomeFrames > 0;

    private bool EscapeAdvancesBattleTime => nativeEscape.Held && Outcome == BattleOutcome.Active && !HasPendingNativeOutcome && !HasPendingExpandedTerminal && NativeEntryReady && PendingSlotsDeal is null && BattleClockUi?.PauseDE9 != true;
    private bool UsesRealTimeBattleClock => Mode != BattleAtbMode.TurnBased || EscapeAdvancesBattleTime;

    public void PublishNativeEscapeButtons(ushort buttons)
    {
        bool held = PendingSlotsDeal is null && (buttons & 3) == 3;
        nativeEscape = held ? nativeEscape with { Held = true } : nativeEscape with { Held = false, HeldFrames = 0, Requested = false };
    }

    public bool CanParticipateInEscape(BattleCombatant actor) => actor.IsPlayer && (UsesExpandedRoster ? actor.IsActive : actor.EncodeNativeSlot() is >= 0 and < 3) && NativeBattleEscapeRules.Eligible(NativeSavedStatusBits(actor), NativeBattleStatusBits(actor), actor.NativeCombatantFlags);

    private bool HasEscapeParticipant => allies.Any(CanParticipateInEscape);

    private IEnumerable<BattleCombatant> CurrentEscapeEnemies => enemies.Where(actor => UsesExpandedRoster ? actor.IsActive : actor.EncodeNativeSlot() is >= 3 and < 7);

    private void AdvanceNativeEscapeFrame()
    {
        if (HasPendingExpandedEscape)
        {
            Frame = checked(Frame + 1);
            byte remaining = NativeBattleOutcomeCallbacks.AdvanceCountdown47CCB0(nativeEscape.ExpandedOutcomeFrames);
            nativeEscape = nativeEscape with { ExpandedOutcomeFrames = remaining, Completed = remaining == 0 };
            if (remaining == 0)
            {
                CompleteBattleIfNeeded();
            }
            return;
        }
        if (PendingSlotsDeal is not null || Outcome != BattleOutcome.Active || HasPendingNativeOutcome || HasPendingExpandedTerminal || !NativeEntryReady || BattleClockUi?.PauseDE9 == true)
        {
            return;
        }
        if (nativeEscape.PresentationHeld != nativeEscape.Held)
        {
            if (NativeGraphics is { } graphics)
            {
                NativeGraphics = QueueNativeDescriptor(graphics, nativeEscape.Held ? (ushort)108 : (ushort)109, 0, priority: 240);
            }
            else if (expandedPresentationProcess is { } process)
            {
                process.SetEscapePresentationHeld(this, nativeEscape.Held);
            }
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
            byte advantage = NativeEntryAdvantage ?? (UsesExpandedRoster
                ? (EscapeFlags & 0xE0) switch { 0x20 => (byte)1, 0x40 => (byte)2, _ => (byte)0 }
                : (byte)0);
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
            if (UsesExpandedRoster)
            {
                return ActiveAction is not null || ExpandedTimedLimitOwnsInput || HasExpandedAiDescriptors || enemies.Any(HasExpandedEnemyDeathAction);
            }
            if (nativeCommandBanks is not { } banks)
            {
                return false;
            }
            NativeBattleCommandBank bank = banks.Banks[0];
            for (int slot = 0; slot < bank.Links.Length; slot++)
            {
                NativeBattleCommandPacket packet = bank.Packets[2 * slot];
                if (bank.Links[slot].Previous == 255 && packet.ReadByte(1) == 255 && packet.ReadWord(4) == 3)
                {
                    return true;
                }
            }
            return false;
        }
    }

    private void BeginNativeEscape4862A0()
    {
        StopTurnAdvance();
        if (expandedPresentationProcess is not null)
        {
            AdmitExpandedHostedTerminal(BattleOutcome.Escaped);
            nativeEscape = nativeEscape with { Requested = false };
            return;
        }
        if (UsesExpandedRoster)
        {
            if (definition.NativeGlobalMessages.Length > 1 && definition.NativeGlobalMessages[1] is { } text && definition.NativeMessageSpeed is byte speed)
            {
                QueueNativeDescriptorMessage(new(Frame, "NativeText", null, null, unchecked((byte)(8 * speed + 8)), text) { NativeMessageMode = 3, NativeTextIdentity = BattleTextIdentity.Global(1) });
            }
            nativeEscape = nativeEscape with { Requested = false, ExpandedOutcomeFrames = NativeBattleOutcomeCallbacks.Duration47DFC0(2) };
            return;
        }
        if (NativeGraphics is not { } graphics)
        {
            nativeEscape = nativeEscape with { Completed = true };
            CompleteBattleIfNeeded();
            return;
        }
        NativeGraphics = BuildNativeTerminalGraphics(graphics, BattleOutcome.Escaped);
    }
}
