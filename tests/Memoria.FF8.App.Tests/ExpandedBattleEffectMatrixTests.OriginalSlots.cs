using static Memoria.FF8.App.Tests.NativeBattleOriginalModelAssertions;
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
            if (current.NativeActionPackets!.PublishedHeaders().Any(header => header.Slot.Command == 0 && header.Slot.Action == 3))
            {
                queued = current;
            }
        }
        Assert.NotNull(queued);
        NativeBattleActionHeaderSlot entrance = Assert.Single(queued.NativeActionPackets!.PublishedHeaders(), header => header.Slot.Command == 0 && header.Slot.Action == 3).Slot;
        Assert.Equal((byte)3, ReadOriginalModelOperand(queued.NativeActionPackets!.ModelAliases, entrance.Source));
        Assert.Equal((byte)0, entrance.Sequence);
        Assert.Equal((ushort)0, entrance.Effect);
        Assert.Equal((byte)1, entrance.ReadContactCount());
        NativeBattleContactPacket contact = queued.NativeActionPackets!.ReadContact(entrance.FirstContact);
        Assert.Equal((byte)4, ReadOriginalModelOperand(queued.NativeActionPackets!.ModelAliases, contact.Primary.Recipient));
        Assert.Equal((byte)0, contact.Primary.Sequence);
        Assert.Equal((ushort)0, contact.Primary.Amount6);
        Assert.Equal((byte)1, contact.Primary.Flags2);
        Assert.True(wedge.Suspended);
        Assert.DoesNotContain(battle.Events, item => item.Kind == "Activated" && item.ActorId == wedge.Id);
        BattleSession restored = BattleSession.Restore(NativeBattleStorageSnapshots.Capture(queued));
        restored.AttachNativeEffectResourceResolver(effect => assets.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        for (int callback = 0; callback < 240 && wedge.Suspended; callback++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.False(wedge.Suspended);
        Assert.Single(battle.Events, item => item.Kind == "Activated" && item.ActorId == wedge.Id);
        BattleSnapshot observed = battle.Capture();
        BattleSnapshot continued = restored.Capture();
        Assert.Equivalent(observed.Combatants, continued.Combatants, strict: true);
        Assert.Equal(observed.Random!.Lane, continued.Random!.Lane);
        Assert.Equal(observed.Random.Indexes.ToArray(), continued.Random.Indexes.ToArray());
        Assert.Equal(battle.Events, restored.Events);
        Assert.Equal(observed.NativeActionPackets!.PublishedHeaders().ToArray(), continued.NativeActionPackets!.PublishedHeaders().ToArray());
        Assert.Equivalent(observed.NativeActionPackets.ExportRetained(), continued.NativeActionPackets.ExportRetained(), strict: true);
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
        Assert.All(battle.Combatants.Where(actor => actor.Slot is not null), actor => Assert.InRange(actor.RequireNativeActorIndex(), 0, 6));
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
                Assert.Equal((byte)5, ReadOriginalModelOperand(battle.NativeActionPackets.ModelAliases, parameters.Source));
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
            using GameWorld world = StartBattle(directory, field, formation, requireExpanded: false, prepareHigherBand: route.Band == HigherEnemyBand);
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
