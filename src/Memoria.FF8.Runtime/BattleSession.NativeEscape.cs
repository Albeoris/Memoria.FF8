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
    public bool EscapeDisabled => nativeScriptState.EscapeDisabled ?? (ResolvedNativeFlags & 1) != 0;

    public void PublishNativeEscapeButtons(ushort buttons)
    {
        bool held = PendingSlotsDeal is null && (buttons & 3) == 3;
        nativeEscape = held ? nativeEscape with { Held = true } : nativeEscape with { Held = false, HeldFrames = 0, Requested = false };
    }

    private bool HasEscapeParticipant => combatants.Any(actor => actor.IsPlayer && actor.NativeIndex is >= 0 and < 3 && NativeBattleEscapeRules.Eligible(NativeSavedStatusBits(actor), NativeBattleStatusBits(actor), actor.NativeCombatantFlags));

    private void AdvanceNativeEscapeFrame()
    {
        if (PendingSlotsDeal is not null || Outcome != BattleOutcome.Active || HasPendingNativeOutcome || !NativeEntryReady || NativeGraphics?.UiServices?.PauseDE9 == true)
        {
            return;
        }
        if (nativeEscape.PresentationHeld != nativeEscape.Held)
        {
            if (NativeGraphics is { } graphics)
            {
                NativeGraphics = QueueNativeDescriptor(graphics, nativeEscape.Held ? (ushort)108 : (ushort)109, 0, priority: 240);
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
            byte advantage = NativeEntryAdvantage ?? 0;
            List<byte> information = [];
            if (advantage == 0)
            {
                BattleCombatant[] enemies = combatants.Where(actor => !actor.IsPlayer && actor.NativeIndex is >= 3 and < 7).ToArray();
                bool eligibleEnemy = enemies.Any(actor => (NativeSavedStatusBits(actor) & 0x25) == 0 && (NativeBattleStatusBits(actor) & 0x4001) == 0);
                if (eligibleEnemy)
                {
                    information.AddRange(enemies.Where(actor => (NativeSavedStatusBits(actor) & 1) == 0).Select(actor => actor.SourceDefinition.NativeTemplate?.Information254 ?? throw new NotSupportedException("Native escape requires the enemy's original information BYTE254.")));
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
        if (NativeGraphics is not { } graphics)
        {
            nativeEscape = nativeEscape with { Completed = true };
            CompleteBattleIfNeeded();
            return;
        }
        if (definition.NativeGlobalMessages.Length <= 1 || definition.NativeGlobalMessages[1] is not { } message)
        {
            throw new NotSupportedException("Native4862A0 requires its original miscellaneous text1 binding.");
        }
        int duration = 8 * (definition.NativeMessageSpeed ?? throw new NotSupportedException("Native4862A0 requires its saved message-speed BYTE.")) + 8;
        NativeBattleOutcomeState state = graphics.NativeOutcome ?? new();
        graphics = graphics with { NativeOutcome = state with { ResultByte6E7 = 2, VictoryQueuedDFE = 1, FadeStyleE01 = 2 } };
        graphics = QueueNativeDescriptor(graphics, 112, 0);
        graphics = QueueNativeDescriptor(graphics, 10, 0x47DF60);
        graphics = QueueNativeDescriptorMessage(graphics, new(Frame, "NativeText", null, null, duration, message) { NativeMessageMode = 3 }, 86);
        graphics = QueueNativeDescriptor(graphics, 116, 0);
        NativeGraphics = QueueNativeDescriptor(graphics, 10, 0x47DFC0);
    }
}
