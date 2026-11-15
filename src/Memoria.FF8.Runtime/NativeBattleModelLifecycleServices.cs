using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed record NativeBattleModelActorFacts(string ActorId, bool IsParty, int OriginalModelId, short DoomTimer, short PetrifyTimer)
{
    internal uint? RawBattleStatuses { get; init; }
    internal int? CurrentHp { get; init; }
    internal int? MaximumHp { get; init; }
    internal byte? Information246 { get; init; }
    internal ushort SavedStatus { get; init; }
    internal bool PendingAllocationStatistics { get; init; }
}

internal sealed record HostedNativeAllocationFrame(BattleEnemyActorModel Owner, byte SceneSlot, bool Hidden);
internal sealed record HostedShotFrameOwner(BattleModelOwner Owner, string ActorId, long ActionToken, uint Callback, BattleNativeShotPhase? Phase);
internal abstract record NativeBattlePresentationReceipt;
internal sealed record NativeBattlePresentationContactReceived(NativeBattleContactPacket Packet) : NativeBattlePresentationReceipt;
internal sealed record NativeBattlePresentationCompletionRequested(long Token, uint Callback, BattleModelOwner Owner, string ActorId) : NativeBattlePresentationReceipt;
internal sealed record NativeBattlePresentationSoundCommitted(NativeBattleModuleSound Sound, string? ActorId, NativeBattleSequenceSound? Sequence) : NativeBattlePresentationReceipt;
internal sealed record NativeBattlePresentationStopAllSoundsRequested(string Message) : NativeBattlePresentationReceipt;
internal sealed record NativeBattlePresentationMusicCommandCommitted(NativeBattleMusicCommandReceipt Command) : NativeBattlePresentationReceipt;
internal sealed record NativeBattlePresentationDescriptorText(BattleEvent Message) : NativeBattlePresentationReceipt;
internal abstract record NativeBattleModelLifecycleReceipt(BattleModelOwner Owner, string ActorId) : NativeBattlePresentationReceipt;
internal sealed record NativeBattleAllocationStatisticsRequested(BattleEnemyActorModel Model, byte SceneSlot) : NativeBattleModelLifecycleReceipt(Model, Model.Actor.ActorId);
internal sealed record NativeBattleAllocationGateReached : NativeBattlePresentationReceipt;
internal sealed record NativeBattleModelParticipantReleased(BattleModelOwner Owner, string ActorId) : NativeBattleModelLifecycleReceipt(Owner, ActorId);
internal sealed record NativeBattleModelParticipantActivated(BattleModelOwner Owner, string ActorId) : NativeBattleModelLifecycleReceipt(Owner, ActorId)
{
    internal NativeBattleModelSlot? OriginalSlot { get; init; }
}
internal sealed record NativeBattleModelParticipantSuspended(BattleModelOwner Owner, string ActorId, byte Sequence) : NativeBattleModelLifecycleReceipt(Owner, ActorId)
{
    internal NativeBattleActionHeaderSlot? ActiveHeader { get; init; }
}
internal sealed record NativeBattleModelTextPublished(BattleModelOwner Owner, string ActorId, string Text, int Value, BattleTextIdentity? TextIdentity) : NativeBattleModelLifecycleReceipt(Owner, ActorId);
internal sealed record NativeBattleModelTurnAdvanceStopRequested(BattleModelOwner Owner, string ActorId) : NativeBattleModelLifecycleReceipt(Owner, ActorId);
internal sealed record NativeBattleShotReadinessReached(HostedShotFrameOwner Shot) : NativeBattleModelLifecycleReceipt(Shot.Owner, Shot.ActorId);
internal sealed record NativeBattleModelTriggerRegistered(BattleModelOwner Owner, string ActorId, NativeBattleTriggerWidget Widget) : NativeBattleModelLifecycleReceipt(Owner, ActorId);

internal sealed class NativeBattleModelLifecycleServices(ImmutableDictionary<BattleModelOwner, NativeBattleModelActorFacts> facts)
{
    private readonly ImmutableArray<NativeBattlePresentationReceipt>.Builder receipts = ImmutableArray.CreateBuilder<NativeBattlePresentationReceipt>();

    internal ImmutableDictionary<BattleModelOwner, NativeBattleModelActorFacts> Facts { get; } = facts ?? throw new ArgumentNullException(nameof(facts));
    internal HostedNativeAllocationFrame? Allocation { get; init; }
    internal ImmutableDictionary<string, float>? ActorOpacities { get; init; }
    internal HostedShotFrameOwner? Shot { get; init; }
    internal Func<BattleModelOwner, NativeBattleScanByteOwner>? ReadScanDescriptor { get; init; }
    internal Func<BattleModelOwner, NativeBattleScanByteOwner>? ReadScanExtra { get; init; }

    internal ImmutableArray<NativeBattleModelLifecycleReceipt> ReadReceipts() => receipts.OfType<NativeBattleModelLifecycleReceipt>().ToImmutableArray();

    internal ImmutableArray<NativeBattlePresentationReceipt> ReadOrderedReceipts() => receipts.ToImmutable();

    internal void Emit(NativeBattleModelLifecycleReceipt receipt) => receipts.Add(receipt);

    internal void EmitAllocationGate() => receipts.Add(new NativeBattleAllocationGateReached());

    internal void EmitContact(NativeBattleContactPacket packet) => receipts.Add(new NativeBattlePresentationContactReceived(packet));
    internal void EmitCompletion(NativeBattlePresentationCompletionRequested request) => receipts.Add(request);
    internal void EmitSound(NativeBattleModuleSound sound, string? actorId, NativeBattleSequenceSound? sequence) => receipts.Add(new NativeBattlePresentationSoundCommitted(sound, actorId, sequence));
    internal void EmitStopAllSounds(string message) => receipts.Add(new NativeBattlePresentationStopAllSoundsRequested(message));
    internal void EmitMusicCommand(NativeBattleMusicCommandReceipt command) => receipts.Add(new NativeBattlePresentationMusicCommandCommitted(command));
    internal void EmitDescriptorText(BattleEvent message) => receipts.Add(new NativeBattlePresentationDescriptorText(message));
    internal void EmitTerminal(NativeBattleTerminalEntered receipt) => receipts.Add(receipt);
    internal void EmitTerminal(NativeBattleTerminalFinished receipt) => receipts.Add(receipt);

    internal void Validate(BattleModelRoster roster, NativeBattleGraphicsState graphics)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(graphics);
        if (Facts.Count != roster.Count)
        {
            throw new InvalidDataException("Hosted model lifecycle requires actor facts for its complete planned roster.");
        }
        foreach (BattleModelOwner owner in roster.Owners)
        {
            (string actorId, bool isParty) = owner switch
            {
                BattlePartyActorModel party => (party.Actor.ActorId, true),
                BattleEnemyActorModel enemy => (enemy.Actor.ActorId, false),
                _ => throw new InvalidDataException("A foreign model owner cannot supply hosted logical actor facts.")
            };
            if (!Facts.TryGetValue(owner, out NativeBattleModelActorFacts? fact) || fact is null || !StringComparer.Ordinal.Equals(fact.ActorId, actorId) || fact.IsParty != isParty)
            {
                throw new InvalidDataException("Hosted model lifecycle actor facts must match the planned model owner.");
            }
        }
        foreach (BattleModelOwner owner in graphics.ActorModels.Owners)
        {
            if (!roster.Contains(owner))
            {
                throw new InvalidDataException("Hosted model lifecycle graphics contains a model outside the planned roster.");
            }
        }
    }
}
