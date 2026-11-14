using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed record NativeBattleModelActorFacts(string ActorId, bool IsParty, int OriginalModelId, short DoomTimer, short PetrifyTimer);

internal abstract record NativeBattleModelLifecycleReceipt(int Row, string ActorId);
internal sealed record NativeBattleModelParticipantReleased(int Row, string ActorId) : NativeBattleModelLifecycleReceipt(Row, ActorId);
internal sealed record NativeBattleModelParticipantSuspended(int Row, string ActorId, byte Sequence) : NativeBattleModelLifecycleReceipt(Row, ActorId);
internal sealed record NativeBattleModelTextPublished(int Row, string ActorId, string Text, int Value, BattleTextIdentity? TextIdentity) : NativeBattleModelLifecycleReceipt(Row, ActorId);
internal sealed record NativeBattleModelTriggerRegistered(int Row, string ActorId, NativeBattleTriggerWidget Widget) : NativeBattleModelLifecycleReceipt(Row, ActorId);

internal sealed class NativeBattleModelLifecycleServices(ImmutableArray<NativeBattleModelActorFacts?> facts)
{
    private readonly ImmutableArray<NativeBattleModelLifecycleReceipt>.Builder receipts = ImmutableArray.CreateBuilder<NativeBattleModelLifecycleReceipt>();

    internal ImmutableArray<NativeBattleModelActorFacts?> Facts { get; } = facts;

    internal ImmutableArray<NativeBattleModelLifecycleReceipt> ReadReceipts() => receipts.ToImmutable();

    internal void Emit(NativeBattleModelLifecycleReceipt receipt) => receipts.Add(receipt);

    internal void Validate(NativeBattleGraphicsRoster roster, NativeBattleGraphicsState graphics)
    {
        if (graphics.Models.IsDefault || graphics.Models.Length != roster.ActorCount ||
            graphics.ActorIds.IsDefault || graphics.ActorIds.Length != roster.ActorCount ||
            Facts.IsDefault || Facts.Length != roster.ActorCount)
        {
            throw new InvalidDataException("Hosted model lifecycle requires graphics and actor facts for its complete roster.");
        }
        for (int row = 0; row < roster.ActorCount; row++)
        {
            NativeBattleGraphicsActor? actor = roster.Rows[row];
            NativeBattleModelActorFacts? fact = Facts[row];
            if (graphics.Models[row] is not { } model || model.Index != row ||
                !StringComparer.Ordinal.Equals(graphics.ActorIds[row], actor?.ActorId) ||
                !StringComparer.Ordinal.Equals(fact?.ActorId, actor?.ActorId) ||
                fact?.IsParty != actor?.IsParty)
            {
                throw new InvalidDataException("Hosted model lifecycle graphics and actor facts must match their bound roster row.");
            }
        }
    }
}
