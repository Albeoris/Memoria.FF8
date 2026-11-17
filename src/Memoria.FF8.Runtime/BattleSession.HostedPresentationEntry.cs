using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed record NativeBattleModelEntryFacts(string ActorId, bool IsParty, int ModelId, int? SceneSlot, bool Active, bool Hidden, byte? Information246, ushort SavedStatus, uint BattleStatus);

public sealed partial class BattleSession
{

    private ImmutableDictionary<BattleModelOwner, NativeBattleModelEntryFacts> CaptureHostedEntryFacts(BattleModelRoster roster)
    {
        var facts = ImmutableDictionary.CreateBuilder<BattleModelOwner, NativeBattleModelEntryFacts>();
        foreach (BattleModelOwner owner in roster.Owners)
        {
            BattleCombatant actor = GetActor(RequireCanonicalGraphicsActorId(owner));
            if (owner.Side != actor.Side || actor.Slot is null)
            {
                throw new InvalidDataException("Hosted entry actor has a conflicting roster binding.");
            }
            facts.Add(owner, new(actor.Id, actor.IsPlayer, actor.IsPlayer ? actor.ModelId : actor.ModelId + 16, actor.SceneSlot, actor.IsActive, actor.Hidden, actor.NativeInformation246, NativeSavedStatusBits(actor), NativeBattleStatusBits(actor)));
        }
        return facts.ToImmutable();
    }
}
