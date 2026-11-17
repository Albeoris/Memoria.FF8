using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed class InstalledBattlePresentation(GameAssets game, BattleAssets assets, BattleDefinition definition, BattleEncounter? encounter)
{
    private readonly BattleFormationPositions positions = new(definition, encounter);
    private readonly NativeBattlePrimitiveTables tables = assets.ReadNativePrimitiveTables();
    private readonly NativeSoftwarePrimitiveTable softwareTable = assets.ReadNativeSoftwarePrimitiveTable();
    private readonly NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
    private readonly Dictionary<(bool IsPlayer, int Model, int Costume, int? Weapon), NativeBattleGraphicsResource> actorResources = [];

    internal static BattleSession Attach(BattleSession owner, GameAssets game, BattleAssets assets, BattleEncounter? encounter = null)
    {
        return Bind(owner, game, assets, encounter, null);
    }

    internal static BattleSession Restore(BattleSnapshot continuation, GameAssets game, BattleAssets assets)
    {
        return Bind(BattleSession.Restore(continuation), game, assets, null, continuation);
    }

    private static BattleSession Bind(BattleSession owner, GameAssets game, BattleAssets assets, BattleEncounter? encounter, BattleSnapshot? continuation)
    {
        owner.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        BattleDefinition definition = owner.ReadDefinition();
        if (encounter is null && definition.EncounterId is int encounterId && definition.UsesOriginalEnemySlots)
        {
            encounter = assets.LoadEncounter(encounterId);
        }
        InstalledBattlePresentation installed = new(game, assets, definition, encounter);
        BattlePresentationResources resources = installed.BindResources(owner);
        if (continuation is null)
        {
            owner.AttachPresentation(resources, installed.PrepareModelBinding);
        }
        else
        {
            owner.AttachRestoredPresentation(resources, continuation, installed.PrepareModelBinding);
        }
        return owner;
    }

    private BattlePresentationResources BindResources(BattleSession owner)
    {
        BattleModelRoster roster = owner.ReadBattlePresentationRoster();
        ImmutableDictionary<string, NativeBattleVector> initialPositions = positions.ReadPositions(owner);
        ImmutableArray<BattleModelOwner> actors = roster.Owners.ToImmutableArray();
        var combatants = owner.Combatants.ToDictionary(actor => actor.Id, StringComparer.Ordinal);
        ImmutableArray<NativeBattleGraphicsBinding> bindings = actors.Select(modelOwner =>
        {
            BattleCombatant actor = combatants[ActorId(modelOwner)];
            BattleRosterSlot slot = actor.Slot ?? throw new InvalidDataException("An entry model owner requires its allocated slot.");
            return PrepareModelBinding(owner, modelOwner, actor, slot, initialPositions[actor.Id]);
        }).ToImmutableArray();
        ImmutableArray<string> loadOrder = actors.OrderBy(actor => actor.Side == BattleSide.Allies).Select(ActorId).ToImmutableArray();
        NativeBattleGraphicsDefinition graphics = new(rules, bindings, loadOrder)
        {
            PrimitiveTables = tables,
            SoftwarePrimitiveTable = softwareTable,
            PrimitiveCapabilities = NativeBattlePrimitiveCapabilities.PrimaryRgba8(),
            PcUiIcons = assets.ReadNativePcUiIcons().Binding
        };
        return new BattlePresentationResources(definition with { NativeGraphics = graphics });
    }

    private NativeBattleGraphicsBinding PrepareModelBinding(BattleSession owner, BattleModelOwner modelOwner, BattleRosterSlot slot)
    {
        string actorId = ActorId(modelOwner);
        BattleCombatant actor = owner.Combatants.SingleOrDefault(candidate => candidate.Id == actorId)
            ?? throw new InvalidDataException("The requested model owner does not belong to the supplied battle.");
        NativeBattleVector position = positions.ReadPosition(actor, slot);
        return PrepareModelBinding(owner, modelOwner, actor, slot, position);
    }

    private NativeBattleGraphicsBinding PrepareModelBinding(BattleSession owner, BattleModelOwner modelOwner, BattleCombatant actor, BattleRosterSlot slot, NativeBattleVector position)
    {
        bool matches = actor.IsPlayer
            ? modelOwner == new BattlePartyActorModel(new(actor.Id))
            : modelOwner == new BattleEnemyActorModel(new(actor.Id));
        if (!matches || modelOwner.Side != slot.Side || actor.Slot is { } current && current != slot ||
            actor.Slot is null && (actor.IsPlayer || actor.SceneSlot is null || !definition.UsesOriginalEnemySlots))
        {
            throw new InvalidDataException("An installed model binding requires its original allocation, slot and actor identity.");
        }
        byte modelId = checked((byte)(actor.IsPlayer ? actor.ModelId : actor.ModelId + 16));
        return new(actor.Id, modelId, GetResource(owner, actor)) { InitialPosition = position };
    }

    private static string ActorId(BattleModelOwner owner) => owner switch
    {
        BattlePartyActorModel party => party.Actor.ActorId,
        BattleEnemyActorModel enemy => enemy.Actor.ActorId,
        _ => throw new InvalidDataException("Battle presentation requires a canonical actor owner.")
    };

    private NativeBattleGraphicsResource GetResource(BattleSession owner, BattleCombatant actor)
    {
        (bool IsPlayer, int Model, int Costume, int? Weapon) key = (actor.IsPlayer, actor.ModelId, actor.Costume, actor.WeaponId);
        if (!actorResources.TryGetValue(key, out NativeBattleGraphicsResource? resource))
        {
            resource = key.IsPlayer ? assets.ReadNativeGraphicsResource(owner.GetCombatantDefinition(actor.Id)) : assets.ReadEnemyNativeGraphicsResource(key.Model);
            actorResources.Add(key, resource);
        }
        return resource;
    }
}
