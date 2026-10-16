using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Retained48D200/485610 action globals; nullable bytes have no observed writer in an older snapshot.</summary>
public sealed record NativeBattleActionScratch
{
    [JsonRequired]
    public bool? DeferredEffectsE00 { get; init; }
    [JsonRequired]
    public bool? GuardianFormulaE1E { get; init; }
    [JsonRequired]
    public byte? GuardianBoostPercent { get; init; }
    [JsonRequired]
    public bool? FinisherPendingE03 { get; init; }
    [JsonRequired]
    public byte? LastPublishedTargetCountE05 { get; init; }
    [JsonRequired]
    public byte? FinisherActionIndexE2E { get; init; }
    [JsonRequired]
    public byte? ActorIndex { get; init; }
    [JsonRequired]
    public byte? Command { get; init; }
    [JsonRequired]
    public ushort? Action { get; init; }
    [JsonRequired]
    public byte? EventActorB0F { get; init; }
    [JsonRequired]
    public byte? EventLane { get; init; }
    //TODO(TD-009): Without the descriptor bank this retains its single pending10 callback until component presentation completes.
    [JsonRequired]
    public uint? ComponentCompletion { get; init; }

    public void Validate()
    {
        if (ActorIndex >= 7 || EventActorB0F >= 7 || EventLane >= 3 || ComponentCompletion is not null and not (0x47DDA0 or 0x47DE70 or 0x47E120))
        {
            throw new InvalidDataException("Native action scratch contradicts its physical actor, event lane or completion callback.");
        }
    }
}

public sealed partial class BattleSession
{
    private NativeBattleActionScratch nativeActionScratch = new();

    private void SetNativeActionEvent(BattleCombatant actor, byte lane)
    {
        nativeActionScratch = nativeActionScratch with { EventActorB0F = checked((byte)actor.NativeIndex), EventLane = lane };
    }

    private bool NativeDeathContactAllowed(BattleCombatant recipient)
    {
        if (nativeContactWorking is null)
        {
            return true;
        }
        bool deferred = nativeActionScratch.DeferredEffectsE00 ?? throw new NotSupportedException("Native494410 requires its retained E00 formula/deferred-effects byte.");
        byte remaining = recipient.NativeRemainingHits ?? throw new NotSupportedException("Native494410 requires its48EB40 remaining-hit byte.");
        return !deferred && remaining == 0;
    }

    private bool NativeSurvivingContactStoresMemory(BattleCombatant recipient)
    {
        if (nativeContactWorking?.CommittingSecondary == true || nativeActionScratch.Command is 0 or 14)
        {
            return false;
        }
        return recipient.IsPlayer || (nativeActionScratch.EventLane ?? throw new NotSupportedException("Native494410 requires its actual dispatch lane for enemy action memory.")) != 0;
    }
}
