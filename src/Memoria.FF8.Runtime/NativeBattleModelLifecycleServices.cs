using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed record NativeBattleModelActorFacts(string ActorId, bool IsParty, int OriginalModelId, short DoomTimer, short PetrifyTimer)
{
    internal uint? RawBattleStatuses { get; init; }
    internal int? CurrentHp { get; init; }
    internal int? MaximumHp { get; init; }
}

internal abstract record NativeBattlePresentationReceipt;
internal sealed record NativeBattlePresentationContactReceived(NativeBattleContactPacket Packet) : NativeBattlePresentationReceipt;
internal sealed record NativeBattlePresentationCompletionRequested(long Token, uint Callback, int Row, string ActorId) : NativeBattlePresentationReceipt;
internal sealed record NativeBattlePresentationSoundCommitted(NativeBattleModuleSound Sound, string? ActorId, NativeBattleSequenceSound? Sequence) : NativeBattlePresentationReceipt;
internal sealed record NativeBattlePresentationMusicCommandCommitted(NativeBattleMusicCommandReceipt Command) : NativeBattlePresentationReceipt;
internal abstract record NativeBattleModelLifecycleReceipt(int Row, string ActorId) : NativeBattlePresentationReceipt;
internal sealed record NativeBattleModelParticipantReleased(int Row, string ActorId) : NativeBattleModelLifecycleReceipt(Row, ActorId);
internal sealed record NativeBattleModelParticipantSuspended(int Row, string ActorId, byte Sequence) : NativeBattleModelLifecycleReceipt(Row, ActorId)
{
    internal NativeBattleActionHeaderSlot? ActiveHeader { get; init; }
}
internal sealed record NativeBattleModelTextPublished(int Row, string ActorId, string Text, int Value, BattleTextIdentity? TextIdentity) : NativeBattleModelLifecycleReceipt(Row, ActorId);
internal sealed record NativeBattleModelTriggerRegistered(int Row, string ActorId, NativeBattleTriggerWidget Widget) : NativeBattleModelLifecycleReceipt(Row, ActorId);

internal sealed class NativeBattleModelLifecycleServices(ImmutableArray<NativeBattleModelActorFacts?> facts)
{
    private readonly ImmutableArray<NativeBattlePresentationReceipt>.Builder receipts = ImmutableArray.CreateBuilder<NativeBattlePresentationReceipt>();

    internal ImmutableArray<NativeBattleModelActorFacts?> Facts { get; } = facts;
    internal ImmutableDictionary<string, float>? ActorOpacities { get; init; }

    internal ImmutableArray<NativeBattleModelLifecycleReceipt> ReadReceipts() => receipts.OfType<NativeBattleModelLifecycleReceipt>().ToImmutableArray();

    internal ImmutableArray<NativeBattlePresentationReceipt> ReadOrderedReceipts() => receipts.ToImmutable();

    internal void Emit(NativeBattleModelLifecycleReceipt receipt) => receipts.Add(receipt);

    internal void EmitContact(NativeBattleContactPacket packet) => receipts.Add(new NativeBattlePresentationContactReceived(packet));
    internal void EmitCompletion(NativeBattlePresentationCompletionRequested request) => receipts.Add(request);
    internal void EmitSound(NativeBattleModuleSound sound, string? actorId, NativeBattleSequenceSound? sequence) => receipts.Add(new NativeBattlePresentationSoundCommitted(sound, actorId, sequence));
    internal void EmitMusicCommand(NativeBattleMusicCommandReceipt command) => receipts.Add(new NativeBattlePresentationMusicCommandCommitted(command));

    internal void Validate(BattleModelRoster roster, NativeBattleGraphicsState graphics)
    {
        if (graphics.Models.IsDefault || graphics.Models.Length != roster.ActorCount ||
            graphics.ActorIds.IsDefault || graphics.ActorIds.Length != roster.ActorCount ||
            Facts.IsDefault || Facts.Length != roster.ActorCount)
        {
            throw new InvalidDataException("Hosted model lifecycle requires graphics and actor facts for its complete roster.");
        }
        for (int row = 0; row < roster.ActorCount; row++)
        {
            BattleModelOwner? actor = roster.Rows[row];
            NativeBattleModelActorFacts? fact = Facts[row];
            if (graphics.ActorModels.Get(row) is not { } model || model.Index != row ||
                !StringComparer.Ordinal.Equals(graphics.ActorIds[row], actor?.ActorId) ||
                !StringComparer.Ordinal.Equals(fact?.ActorId, actor?.ActorId) ||
                fact?.IsParty != actor?.IsParty)
            {
                throw new InvalidDataException("Hosted model lifecycle graphics and actor facts must match their bound roster row.");
            }
        }
    }
}
