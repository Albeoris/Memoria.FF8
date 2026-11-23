using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed class BattleFormationPositions(BattleDefinition definition, BattleEncounter? encounter, Func<int, NativeBattleGraphicsResource> readEnemy, IReadOnlyDictionary<string, NativeBattleVector>? retainedPositions = null)
{
    private readonly record struct ActorPlacement(string Id, bool IsParty, BattleRosterSlot? Slot, int? SceneSlot);
    private readonly int allyCount = definition.Combatants.Count(actor => actor.IsPlayer);
    private readonly Dictionary<int, NativeBattleVector> addedPositions = encounter is not null && definition.AddedEnemySources is { Count: > 0 } sources
        ? AddedBattleEnemyLayout.Place(definition, encounter, sources, readEnemy, retainedPositions)
        : [];

    internal ImmutableDictionary<string, NativeBattleVector> ReadPositions(BattleSession owner)
    {
        return owner.Combatants.Where(actor => actor.Slot.HasValue).Select(Placement).ToImmutableDictionary(actor => actor.Id, Position, StringComparer.Ordinal);
    }

    internal NativeBattleVector ReadPosition(BattleCombatant actor, BattleRosterSlot slot)
    {
        return Position(Placement(actor) with { Slot = slot });
    }

    private static ActorPlacement Placement(BattleCombatant actor)
    {
        return new(actor.Id, actor.IsPlayer, actor.Slot, actor.SceneSlot);
    }

    private NativeBattleVector Position(ActorPlacement actor)
    {
        if (!actor.IsParty && encounter is not null)
        {
            if (actor.SceneSlot is not int sceneSlot)
            {
                throw new InvalidDataException("An original enemy requires its authored scene slot.");
            }
            if (addedPositions.TryGetValue(sceneSlot, out NativeBattleVector added))
            {
                return added;
            }
            return AuthoredPosition(definition, encounter, sceneSlot);
        }
        int position = actor.Slot?.Position ?? throw new InvalidDataException("Battle actor has no roster position.");
        if (actor.IsParty && allyCount <= 3)
        {
            return NativeBattleFormation.PartyPosition(position, allyCount);
        }
        return ExpandedBattleFormation.Position(position, actor.IsParty ? allyCount : definition.Combatants.Length - allyCount, actor.IsParty);
    }

    internal static NativeBattleVector AuthoredPosition(BattleDefinition definition, BattleEncounter encounter, int sceneSlot)
    {
        BattleEnemyPlacement? placement = definition.UsesOriginalEnemySlots
            ? encounter.Slots.SingleOrDefault(slot => slot.Slot == sceneSlot)
            : encounter.Enemies.ElementAtOrDefault(sceneSlot);
        return placement?.NativePosition ?? throw new InvalidDataException($"Original enemy scene slot {sceneSlot} lacks its authored position.");
    }
}
