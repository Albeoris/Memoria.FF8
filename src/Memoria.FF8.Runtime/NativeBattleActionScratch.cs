using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Retained48D200/485610 action globals; nullable bytes have no observed writer in an older snapshot.</summary>
public sealed record NativeBattleActionScratch
{
    [JsonRequired]
    public bool? DeferredEffectsE00 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? TargetUnavailableB0E { get; init; }
    [JsonRequired]
    public bool? GuardianFormulaE1E { get; init; }
    [JsonRequired]
    public byte? GuardianBoostPercent { get; init; }
    [JsonRequired]
    public bool? FinisherPendingE03 { get; init; }
    [JsonRequired]
    public byte? LastPublishedTargetCountE05 { get; init; }
    [JsonIgnore]
    public int? PublishedTargetCount { get; init; }
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
    public void Validate()
    {
        if (ActorIndex >= 7 || EventActorB0F >= 7 || EventLane >= 3)
        {
            throw new InvalidDataException("Native action scratch contradicts its physical actor, event lane or completion callback.");
        }
    }
}

public sealed partial class BattleSession
{
    private NativeBattleActionScratch nativeActionScratch = new();

    private void RecordPublishedNativeTargetCount(int count)
    {
        nativeActionScratch = nativeActionScratch with
        {
            PublishedTargetCount = count,
            LastPublishedTargetCountE05 = count > byte.MaxValue ? null : unchecked((byte)count)
        };
    }

    private static byte? ProjectOriginalActionActor(BattleCombatant actor)
    {
        return actor.NativeActorIndex is int nativeIndex && NativeBattleSlots.Contains(nativeIndex) ? checked((byte)nativeIndex) : null;
    }

    private void SetNativeActionEvent(BattleCombatant actor, byte lane)
    {
        nativeActionScratch = nativeActionScratch with { EventActorB0F = checked((byte)actor.RequireNativeActorIndex()), EventLane = lane };
    }

    private bool NativeDeathContactAllowed(BattleCombatant recipient)
    {
        if (nativeContactWorking is null)
        {
            return true;
        }
        bool deferred = nativeActionScratch.DeferredEffectsE00 ?? throw new NotSupportedException("Native494410 requires its retained E00 formula/deferred-effects byte.");
        int remaining = recipient.NativeRemainingHits ?? throw new NotSupportedException("Native494410 requires its48EB40 remaining-hit byte.");
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
