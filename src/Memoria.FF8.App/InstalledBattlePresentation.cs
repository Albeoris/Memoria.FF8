using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed class InstalledBattlePresentation
{
    private readonly GameAssets game;
    private readonly BattleAssets assets;
    private readonly BattleDefinition definition;
    private readonly BattleFormationPositions positions;
    private readonly NativeBattlePrimitiveTables tables;
    private readonly NativeSoftwarePrimitiveTable softwareTable;
    private readonly NativeBattleGraphicsRules rules;
    private readonly Dictionary<(int Model, int Costume, int? Weapon), NativeBattleGraphicsResource> partyResources = [];
    private readonly Dictionary<int, NativeBattleGraphicsResource> enemyResources = [];

    private InstalledBattlePresentation(GameAssets game, BattleAssets assets, BattleDefinition definition, BattleEncounter? encounter, IReadOnlyDictionary<string, NativeBattleVector>? retainedPositions)
    {
        this.game = game;
        this.assets = assets;
        this.definition = definition;
        positions = new(definition, encounter, ReadEnemyResource, retainedPositions);
        tables = assets.ReadNativePrimitiveTables();
        softwareTable = assets.ReadNativeSoftwarePrimitiveTable();
        rules = game.ReadNativeBattleGraphicsRules();
    }

    internal static BattleSession Attach(BattleSession owner, GameAssets game, BattleAssets assets, BattleEncounter? encounter = null)
    {
        return Bind(owner, game, assets, encounter, null);
    }

    internal static BattleSession Restore(BattleSnapshot continuation, GameAssets game, BattleAssets assets)
    {
        continuation = continuation with { Definition = BindLegacyGuardianFormResources(continuation.Definition, assets) };
        return Bind(BattleSession.Restore(continuation), game, assets, null, continuation);
    }

    internal static BattleDefinition BindLegacyGuardianFormResources(BattleDefinition definition, BattleAssets assets)
    {
        if (!definition.GuardianFormsEnabled || !definition.GuardianFormResources.Any(resource => resource.GuardianForceId == 2 && (resource.InnateBindings.IsDefault || resource.CommandBindings.IsDefault)))
        {
            return definition;
        }
        BattleGuardianFormResources installed = assets.ReadIfritGuardianFormResources();
        return definition with
        {
            GuardianFormResources = definition.GuardianFormResources.Select(resource => resource.GuardianForceId == 2 ? resource with
            {
                InnateBindings = resource.InnateBindings.IsDefault ? installed.InnateBindings : resource.InnateBindings,
                CommandBindings = resource.CommandBindings.IsDefault ? installed.CommandBindings : resource.CommandBindings,
                AbilityBands = null
            } : resource).ToImmutableArray()
        };
    }

    private static BattleSession Bind(BattleSession owner, GameAssets game, BattleAssets assets, BattleEncounter? encounter, BattleSnapshot? continuation)
    {
        owner.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        BattleDefinition definition = owner.ReadDefinition();
        if (encounter is null && definition.EncounterId is int encounterId && definition.UsesOriginalEnemySlots)
        {
            encounter = assets.LoadEncounter(encounterId);
        }
        InstalledBattlePresentation installed = new(game, assets, definition, encounter, continuation?.ReadPresentationInitialPositions());
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
            PcUiIcons = assets.ReadNativePcUiIcons().Binding,
            LimitPresentations = assets.ReadNativeLimitPresentations(bindings.Select(binding => owner.GetCombatantDefinition(binding.ActorId)).ToArray(), bindings)
        };
        return new BattlePresentationResources(definition, graphics, game.ReadNativeCinematicModelOperands);
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
        if (!actor.IsPlayer)
        {
            return ReadEnemyResource(actor.ModelId);
        }
        var key = (actor.ModelId, actor.Costume, actor.WeaponId);
        if (!partyResources.TryGetValue(key, out NativeBattleGraphicsResource? resource))
        {
            resource = assets.ReadNativeGraphicsResource(owner.GetCombatantDefinition(actor.Id));
            partyResources.Add(key, resource);
        }
        return resource;
    }

    private NativeBattleGraphicsResource ReadEnemyResource(int model)
    {
        if (!enemyResources.TryGetValue(model, out NativeBattleGraphicsResource? resource))
        {
            resource = assets.ReadEnemyNativeGraphicsResource(model);
            enemyResources.Add(model, resource);
        }
        return resource;
    }
}
