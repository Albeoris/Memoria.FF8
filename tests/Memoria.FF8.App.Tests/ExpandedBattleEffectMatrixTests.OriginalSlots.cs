using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    [Fact]
    public void InstalledTowerEntrancePublishesPhysicalSequenceBeforeWedgeActivation()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original battle resources are required.");
        using GameAssets assets = new(directory);
        using GameWorld world = new(new GameSession(PrepareField(directory)), directory);
        world.EnsureField();
        BattleSession battle = StartNativeFormation(world, assets, 29);
        BattleCombatant biggs = Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.ModelId == 74);
        BattleCombatant wedge = Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.ModelId == 73);
        BattleCombatant party = battle.Combatants.First(actor => actor.IsPlayer);
        Assert.True(wedge.Suspended);
        biggs.CurrentHp = 1;
        party.Atb = BattleSession.AtbMaximum;
        battle.QueueAttack(party.Id, biggs.Id);
        BattleSnapshot? queued = null;
        for (int callback = 0; callback < 240 && queued is null; callback++)
        {
            battle.Tick();
            BattleSnapshot current = battle.Capture();
            if (current.NativeActionPackets!.Headers.Any(header => header.FirstContactIndex is not null && header.ReadByte(1) == 0 && header.Action == 3))
            {
                queued = current;
            }
        }
        Assert.NotNull(queued);
        NativeBattleActionHeaderSlot entrance = Assert.Single(queued.NativeActionPackets!.Headers, header => header.FirstContactIndex is not null && header.ReadByte(1) == 0 && header.Action == 3);
        Assert.Equal((byte)3, entrance.ReadByte(0));
        Assert.Equal((byte)0, entrance.Sequence);
        Assert.Equal((ushort)0, entrance.Effect);
        Assert.Equal((byte)1, entrance.ReadContactCount());
        NativeBattleContactPacket contact = queued.NativeActionPackets.ReadContact(entrance.FirstContact);
        Assert.Equal((byte)4, contact.Primary.ActorIndex);
        Assert.Equal((byte)0, contact.Primary.Sequence);
        Assert.Equal((ushort)0, contact.Primary.Amount6);
        Assert.Equal((byte)1, contact.Primary.Flags2);
        Assert.True(wedge.Suspended);
        Assert.DoesNotContain(battle.Events, item => item.Kind == "Activated" && item.ActorId == wedge.Id);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(queued))!);
        restored.AttachNativeEffectResourceResolver(effect => assets.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        for (int callback = 0; callback < 240 && wedge.Suspended; callback++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.False(wedge.Suspended);
        Assert.Single(battle.Events, item => item.Kind == "Activated" && item.ActorId == wedge.Id);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Theory]
    [InlineData(110)]
    [InlineData(17)]
    [InlineData(18)]
    public void InstalledTowerRunsAuthoredEffectOnTheOriginalBattleHost(ushort effect)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original battle resources are required.");
        using GameAssets assets = new(directory);
        RunInstalledTowerEffect(directory, PrepareField(directory), assets, effect);
    }

    private static void RunInstalledTowerEffect(string directory, GameState field, GameAssets assets, ushort requestedEffect)
    {
        using GameWorld world = new(new GameSession(field), directory, expandedBattleAllyCount: 11, sharedAssets: assets);
        world.EnsureField();
        BattleSession battle = StartNativeFormation(world, assets, 29);
        Assert.False(battle.UsesExpandedRoster);
        Assert.All(battle.Combatants.Where(actor => actor.Slot is not null), actor => Assert.InRange(actor.EncodeNativeSlot(), 0, 6));
        BattleCombatant biggs = Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.ModelId == 74);
        BattleCombatant wedge = Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.ModelId == 73);
        BattleCombatant party = battle.Combatants.First(actor => actor.IsPlayer);
        Strike(biggs);
        for (int callback = 0; callback < 480 && (wedge.Suspended || battle.ActiveAction is not null || battle.QueuedActions.Count != 0); callback++)
        {
            battle.Tick();
        }
        Assert.True(biggs.IsAlive && !wedge.Suspended);
        Assert.Null(battle.ActiveAction);
        Assert.Empty(battle.QueuedActions);
        Strike(biggs);
        WaitForRelease(110);
        if (requestedEffect == 110)
        {
            return;
        }

        WaitForRelease(17);
        Assert.False(biggs.IsActive);
        Assert.False(wedge.IsActive);
        if (requestedEffect == 17)
        {
            return;
        }

        BattleCombatant elvoret = Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.ModelId == 91 && actor.IsActive);
        for (int callback = 0; callback < 480 && (battle.ActiveAction is not null || battle.QueuedActions.Count != 0); callback++)
        {
            battle.Tick();
        }
        Strike(elvoret);
        WaitForRelease(18);

        void Strike(BattleCombatant target)
        {
            target.CurrentHp = 1;
            party.Atb = BattleSession.AtbMaximum;
            battle.QueueAttack(party.Id, target.Id);
        }

        void WaitForRelease(ushort effectId)
        {
            bool invoked = false;
            for (int callback = 0; callback < 1200; callback++)
            {
                battle.Tick();
                if (battle.NativeGraphics?.OriginalEffect is { } effect && effect.Binding.Effect == effectId)
                {
                    invoked |= effect.ReturnedDescriptor is > 0;
                    if (invoked && effect.ReturnedDescriptor == 0)
                    {
                        return;
                    }
                }
            }
            Assert.Fail($"Original Tower effect {effectId} did not complete (invoked={invoked}).");
        }
    }


    [Fact]
    public void InstalledNativeFormation164RunsAuthoredReserveGraphics()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original battle resources are required.");
        using GameAssets assets = new(directory);
        GameSession session = new(PrepareField(directory));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        BattleSession battle = StartNativeFormation(world, assets, 164);
        BattleCombatant owner = Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.IsActive);
        BattleCombatant attacker = battle.Combatants.First(actor => actor.IsPlayer);
        owner.CurrentHp = owner.MaximumHp / 10 + 1;
        attacker.Atb = BattleSession.AtbMaximum;
        battle.QueueAttack(attacker.Id, owner.Id);
        bool invoked = false;
        bool packets = false;
        for (int callback = 0; callback < 1200; callback++)
        {
            battle.Tick();
            if (battle.NativeGraphics?.OriginalEffect is not { Binding.Effect: 248 } effect)
            {
                continue;
            }
            if (effect.Parameters is { } parameters)
            {
                invoked = true;
                Assert.Equal((byte)5, parameters.Actor78);
            }
            packets |= !effect.PresentationPackets.IsEmpty;
            if (invoked && effect.ReturnedDescriptor == 0)
            {
                Assert.True(packets);
                return;
            }
        }
        Assert.Fail($"Original 248 did not complete (invoked={invoked}, packets={packets}).");
    }

    private static BattleSession StartNativeFormation(GameWorld world, GameAssets assets, ushort formation)
    {
        BattleDefinition definition = world.CreateEncounterBattle(formation);
        NativeBattleMusicEntryPreparation music = NativeBattleMusicEntry.Prepare52CE30(formation, definition.NativeFlags, new(1, null, null, null, 0, null), 5, -1, NativeGlobalSoundProcess.FromBufferlessColdProcess(), new NativeMusicCatalog(TestResources.GameDirectory).ReadBattleEntryResources());
        BattleSession battle = GameWorld.CreateLiveBattleSession(definition with { NativeMusicProcess = music.MusicProcess, NativeGlobalSoundProcess = music.SoundProcess }, BattleAtbMode.TurnBased);
        battle.AttachNativeEffectResourceResolver(effect => assets.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        for (int callback = 0; callback < 480 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        return battle;
    }

    [Fact]
    public void InstalledDirectGraphicsQueuesFromAnAuthoredReserveAfterTheOriginalHpTrigger()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        RunInstalledExpandedReserveGraphics(directory, PrepareField(directory), game, assets);
    }

    private static void RunInstalledExpandedReserveGraphics(string directory, GameState field, GameAssets game, BattleAssets assets)
    {
        using GameWorld world = StartBattle(directory, field, 164, useAreaEnemyRoster: true);
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        Assert.True(battle.ReadDefinition().UsesOriginalEnemySlots);
        BattleCombatant owner = Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.IsActive);
        Assert.Equal(101, owner.ModelId);
        Assert.Equal(assets.ReadEnemyAi(owner.ModelId, owner.Level).Scripts[2], owner.NativeAi!.Scripts[2]);
        Assert.Contains(assets.LoadEncounter(164).Slots, slot => slot is { Slot: 3, ModelId: 71, Enabled: false });
        BattleCombatant attacker = Assert.Single(battle.Combatants, actor => actor.Id == CasterId);
        owner.CurrentHp = owner.MaximumHp / 10 + 1;
        attacker.Atb = BattleSession.AtbMaximum;
        world.ApplyBattleInput(new(BattleInputKind.Action) { ActorId = attacker.Id, TargetId = owner.Id, Ability = BattleAbility.Attack });
        BattleActionPresentation? graphic = null;
        for (int frame = 0; frame < 480 && graphic is null; frame++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            if (battle.ActiveAction is { NativeGraphic.MagicId: 248 } active)
            {
                graphic = active;
            }
        }
        BattleActionPresentation cast = Assert.IsType<BattleActionPresentation>(graphic);
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.Id == cast.ActorId);
        Assert.Equal(71, caster.ModelId);
        Assert.Equal(3, caster.SceneSlot);
        Assert.Equal((byte)0, cast.NativeAiCallbackStage);
        Assert.Equal((byte)252, cast.NativeQueuedCommand);
        Assert.Equal(1, cast.ActionId);
        Assert.Equal(caster.Id, Assert.Single(cast.AiRecipients!.ActorIds));
        Assert.Contains(battle.Events, entry => entry.Kind == "Damage" && entry.ActorId == attacker.Id && entry.TargetId == owner.Id);
        Assert.Contains(battle.Events, entry => entry.Kind == "Activated" && entry.ActorId == caster.Id);
        int hp = caster.CurrentHp;
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        GameWorld.AttachExpandedEffects(restored, game, assets);
        BattleScenePresentation presentation = new(world.BattleResources, battle.ReadDefinition(), true);
        bool packets = false;
        for (int frame = 0; frame < 1200 && battle.ActiveAction is not null; frame++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            restored.Tick();
            if (battle.ExpandedEffect is { } effect)
            {
                Assert.Equal(caster.Slot!.Value.Position + 3, effect.Source.Index);
                Assert.NotNull(restored.ExpandedEffect);
                if (!packets && !effect.Graphics.OriginalEffect!.PresentationPackets.IsEmpty)
                {
                    Assert.NotEmpty(presentation.Render(battle, 320, 224));
                    packets = true;
                }
            }
        }
        Assert.True(packets);
        BattleEvent contact = Assert.Single(battle.Events, entry => entry.Kind == "EffectContact" && entry.ActorId == caster.Id && entry.TargetId == caster.Id);
        Assert.Equal(0, contact.Amount);
        Assert.Equal(hp, caster.CurrentHp);
        Assert.Equal((byte)3, caster.LastNativeAction!.AttackType);
        Assert.Single(battle.Events, entry => entry.Kind == "ActionComplete" && entry.ActorId == caster.Id);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    private static EnemyRouteResult? ProbeOriginalSlotRoutes(string directory, GameState field, BattleAssets assets, NativeBattleEffectBinding effect, EnemyScenarioScriptedAction[] scripts)
    {
        var candidates = scripts.Where(script => script.Callback <= 3 && IsDiscoveredEnemyDispatch(script))
            .SelectMany(script => script.Formations.Where(scene => scene.Enabled).Select(scene => (Script: script, Scene: scene)))
            .GroupBy(route => route.Scene.FormationId)
            .Where(routes => routes.Any(route =>
            {
                NativeBattleAiDefinition ai = assets.ReadEnemyAi(route.Script.ModelId, 0) with { LevelBand = route.Script.LevelBand };
                List<string> models = [];
                ExpandedAiAdmission ordinary = NativeBattleExpandedAi.Evaluate(ai, assets.NativeActions, routes.Key, explicitModelReferences: models);
                return (ordinary == ExpandedAiAdmission.RequiresOriginalSlots || models.Count != 0 || route.Scene.Hidden || route.Scene.Suspended || route.Scene.Untargetable) &&
                    NativeBattleExpandedAi.Evaluate(ai, assets.NativeActions, routes.Key, originalEnemySlots: true) == ExpandedAiAdmission.Supported;
            }))
            .SelectMany(routes => routes.GroupBy(route => route.Script.LevelBand).Select(band => (Formation: routes.Key, Band: band.Key, Scripts: band.Select(route => route.Script).Distinct().ToArray())))
            .Take(6).ToArray();
        foreach (var route in candidates)
        {
            ushort formation = checked((ushort)route.Formation);
            using GameWorld world = StartBattle(directory, field, formation, requireExpanded: false, prepareHigherBand: route.Band == HigherEnemyBand, useAreaEnemyRoster: true);
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            if (!battle.UsesExpandedRoster || !battle.ReadDefinition().UsesOriginalEnemySlots)
            {
                continue;
            }
            BattleEncounter encounter = assets.LoadEncounter(formation);
            Assert.All(battle.ReadDefinition().Combatants.Where(actor => !actor.IsPlayer && actor.InitialActive), actor =>
                Assert.Contains(encounter.Enemies, original => original.Slot == actor.SceneSlot && original.ModelId == actor.ModelId));
            Dictionary<string, EnemyScenarioScriptedAction[]> owners = [];
            foreach (BattleCombatant actor in battle.Combatants.Where(actor => !actor.IsPlayer && actor.IsActive && actor.NativeAi is not null))
            {
                EnemyScenarioScriptedAction[] matched = route.Scripts.Where(script => script.ModelId == actor.ModelId && script.LevelBand == actor.NativeAi!.LevelBand).ToArray();
                if (matched.Length > 0)
                {
                    owners.Add(actor.Id, matched);
                }
            }
            if (owners.Count == 0)
            {
                continue;
            }
            if (owners.Values.Any(matched => matched.Any(script => script.Callback <= 1)))
            {
                world.ApplyBattleInput(new(BattleInputKind.SetMode, Mode: BattleAtbMode.Normal));
                EnemyRouteResult observed = ObserveEnemyCast(world, battle, effect, owners);
                if (observed.Status is "logical" or "native")
                {
                    return observed with { Detail = $"Formation {formation}, original slots, {observed.Detail}" };
                }
                if (battle.Outcome != BattleOutcome.Active || world.Battle is null)
                {
                    continue;
                }
                world.ApplyBattleInput(new(BattleInputKind.SetMode, Mode: BattleAtbMode.TurnBased));
            }
            foreach (BattleCombatant owner in battle.Combatants.Where(actor => owners.ContainsKey(actor.Id) && actor.IsAlive && !actor.Untargetable).ToArray())
            {
                EnemyScenarioScriptedAction[] authored = owners[owner.Id];
                EnemyRouteResult? observed = authored.Any(script => script.Callback == 3)
                    ? ObserveEnemyDeath(world, battle, effect, owner, authored)
                    : authored.Any(script => script.Callback == 2) ? ObserveEnemyReaction(world, battle, effect, owner, authored, oilboyle: false) : null;
                if (observed is { Status: "logical" or "native" })
                {
                    return observed with { Detail = $"Formation {formation}, original slots, {observed.Detail}" };
                }
            }
        }
        return null;
    }
}
