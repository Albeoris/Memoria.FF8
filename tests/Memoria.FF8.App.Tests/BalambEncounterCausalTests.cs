using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BalambEncounterCausalTests
{
    [Fact]
    public void GardenTrainingCenterRandomBattleReturnsToItsFieldAndRecreates()
    {
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed original assets are required.");
        string? prefix = Environment.GetEnvironmentVariable("MEMORIA_FF8_GARDEN_PREFIX");
        GameSession session = prefix is null ? GameSession.CreateColdProcess() : new(GameWorld.RecreateSave(GameSession.LoadReplaySource(prefix), game));
        if (prefix is null)
        {
            session.StartNewGame();
        }
        using GameWorld world = new(session, game, persistReplayJournal: false);
        if (prefix is null)
        {
            BalambTestJourney.OpeningToGate(session, world, encounters: true);
            world.ApplyBattleInput(new(BattleInputKind.ConfigureEncounters, RandomEncounters: false));
            ConfigureCommands(session, world);
            foreach (int destination in new[] { 160, 161, 162, 163, 165, 170 })
            {
                IfritCausalRecreationTests.WalkGateway(world, session, destination);
            }
        }
        world.EnsureField();
        try
        {
            IfritCausalRecreationTests.WalkGateway(world, session, 226, lineObjectId: 27);
        }
        catch (NotSupportedException) when (world.Events.LastFault is null && session.State.FieldId == 170)
        {
            session.SetEventState(world.Events.Capture());
            string routeSource = Path.Combine(Path.GetTempPath(), "Memoria-Garden-route-source-" + Guid.NewGuid().ToString("N") + ".json");
            session.Save(routeSource, registerJournalReference: false);
            Console.WriteLine("Preserved actual route source: " + routeSource);
            // Actual NPC13 blocks the shortest triangle-centroid advisory path.
            // These waypoints are ordinary walking around its current collision circle.
            foreach (Vector2 waypoint in new Vector2[] { new(5650, -2123), new(5700, -1900), new(5790, -1600), new(6000, -1400), new(6350, -1200) })
            {
                IfritCausalRecreationTests.WalkAdvisoryPoint(world, session, waypoint);
            }
            IfritCausalRecreationTests.WalkGateway(world, session, 226, lineObjectId: 27);
        }
        IfritCausalRecreationTests.WalkGateway(world, session, 214);
        IfritCausalRecreationTests.WalkGateway(world, session, 215);
        world.EnsureField();
        Assert.Equal(215, world.Field!.Id);
        IfritCausalRecreationTests.DriveUntil(world, session, () => world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable);
        Vector2 center = new(-5676, -1363);
        BalambTestJourney.WalkRoutedPoint(session, world, center);
        world.ApplyBattleInput(new(BattleInputKind.ConfigureEncounters, RandomEncounters: true));
        for (int callback = 0; callback < 3000 && !world.HasPendingBattle; callback++)
        {
            float sign = callback % 32 < 16 ? 1 : -1;
            world.Apply(new(PlayerActionKind.Move, Horizontal: sign));
            Assert.Equal(215, session.State.FieldId);
        }
        Assert.True(world.HasPendingBattle);
        Assert.Contains(session.State.RandomEncounters!.Pending!.Formation, new ushort[] { 58, 59, 60 });
        long frozen = world.Events.Frame;
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased));
        world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 5));
        VerifyRestoredNextTick(session, world, game);
        Fight(session, world, exerciseCommands: false);
        Assert.Equal(frozen, world.Events.Frame);
        world.ApplyBattleInput(new(BattleInputKind.Return));
        Assert.Equal(215, session.State.FieldId);
        Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        session.SetEventState(world.Events.Capture());
        string save = Path.Combine(Path.GetTempPath(), "Memoria-Garden-battle-" + Guid.NewGuid().ToString("N") + ".json");
        session.Save(save, registerJournalReference: false);
        GameState rebuilt = GameWorld.RecreateSave(GameSession.LoadReplaySource(save), game);
        EqualState(session.State, rebuilt);
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_BALAMB_EVIDENCE");
        if (evidence is not null)
        {
            Directory.CreateDirectory(evidence);
            session.Save(Path.Combine(evidence, "garden-training-battle-current.json"), registerJournalReference: false);
        }
    }

    [Fact]
    public void ActualBalambWorldEncounterUsesCanonicalDrawMagicGuardianForcesAndRecreates()
    {
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed original assets are required.");
        Stopwatch timer = Stopwatch.StartNew();
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld world = new(session, game, persistReplayJournal: false);
        BalambTestJourney.OpeningToGate(session, world, encounters: true);
        Assert.True(session.State.RandomEncounters!.Enabled);
        IfritCausalRecreationTests.WalkGateway(world, session, 0, worldExit: true);
        world.EnsureField();
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        ConfigureCommands(session, world);
        ReachWorldBattle(session, world);
        Assert.InRange(session.State.RandomEncounters!.Pending!.Formation, (ushort)512, (ushort)521);
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased));
        Assert.NotNull(world.Battle);
        string scratch = Path.Combine(Path.GetTempPath(), "Memoria-Balamb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        string midCombat = Path.Combine(scratch, "mid-combat.json");
        session.SetEventState(world.Events.Capture());
        session.Save(midCombat, registerJournalReference: false);
        GameSession loadedSession = new(GameSession.Load(midCombat));
        using (GameWorld loaded = new(loadedSession, game, persistReplayJournal: false))
        {
            loaded.EnsureField();
            EqualState(session.State, loadedSession.State);
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            loaded.ApplyBattleInput(new(BattleInputKind.Tick));
            EqualState(session.State, loadedSession.State);
        }
        Fight(session, world, exerciseCommands: true);
        world.ApplyBattleInput(new(BattleInputKind.Return));
        Assert.Null(world.Battle);
        Assert.Null(session.State.RandomEncounters.Pending);
        int drawerRow = Array.FindIndex(session.State.CharacterVitals!.Records, record => record.MagicInventory.Any(stock => stock.SpellId != 0 && stock.Count > 0));
        Assert.True(drawerRow >= 0);
        NativeMagicStock drawnStock = session.State.CharacterVitals.Records[drawerRow].MagicInventory.First(stock => stock.SpellId != 0 && stock.Count > 0);
        int magicSlot = Enumerable.Range(0, 9).First(slot => session.State.CharacterVitals.JunctionMagicSlotAvailable(drawerRow, slot));
        BalambTestJourney.Apply(world, new(PlayerActionKind.SetJunctionMagic, drawerRow, drawnStock.SpellId.ToString(), Quantity: magicSlot));
        Assert.Equal(drawnStock.Count, session.State.CharacterVitals.Records[drawerRow].MagicInventory.Single(stock => stock.SpellId == drawnStock.SpellId).Count);
        ConfigureCommands(session, world, item: true);
        ReachWorldBattle(session, world);
        int potion = session.State.Economy!.Items.First(stock => stock.Count > 0 && world.BattleResources.ActionDefinitions.Any(action => action.Ability == BattleAbility.Item && action.Id == stock.ItemId && action.Effect == BattleActionEffect.Heal)).ItemId;
        int potionCount = session.State.Economy.Items.Single(stock => stock.ItemId == potion).Count;
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased));
        FightWithItemAndShiva(world, potion);
        world.ApplyBattleInput(new(BattleInputKind.Return));
        Assert.Equal(potionCount - 1, session.State.Economy.Items.Single(stock => stock.ItemId == potion).Count);
        session.SetEventState(world.Events.Capture());
        string save = Path.Combine(scratch, "after-world-battle.json");
        session.Save(save, registerJournalReference: false);
        GameState recreatedState = GameWorld.RecreateSave(GameSession.LoadReplaySource(save), game);
        EqualState(session.State, recreatedState);
        GameSession recreatedSession = new(recreatedState);
        using (GameWorld recreated = new(recreatedSession, game, persistReplayJournal: false))
        {
            recreated.EnsureField();
            Assert.Equal(world.Render(TimeSpan.Zero), recreated.Render(TimeSpan.Zero));
            Assert.Equal(world.Apply(new(PlayerActionKind.Wait)), recreated.Apply(new(PlayerActionKind.Wait)));
            EqualState(session.State, recreatedSession.State);
            Assert.Equal(world.Render(TimeSpan.Zero), recreated.Render(TimeSpan.Zero));
        }
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_BALAMB_EVIDENCE");
        if (evidence is not null)
        {
            Directory.CreateDirectory(evidence);
            session.Save(Path.Combine(evidence, "balamb-world-battle-current.json"), registerJournalReference: false);
            File.WriteAllText(Path.Combine(evidence, "balamb-world-battle-proof.json"), JsonSerializer.Serialize(new { Baseline = "0bbcbcbb + current Balamb battle implementation", Implementations = new[] { "current Memoria actual New Game and ordinary input", "current Memoria cold causal reconstruction" }, Scope = "Balamb Garden opening, GF menu and Balamb island random battles", OriginalRuntimeParity = false, WholeStateExceptJournal = true, WorldCpuPixels = true, MidCombatRestoreAndNextTick = true, NextWaitStateAndPixels = true, DrawStockMagicQuezacotl = true, HealingItemShiva = true, StockedMagicJunction = true, Seconds = timer.Elapsed.TotalSeconds }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    internal static void ConfigureCommands(GameSession session, GameWorld world, bool item = false)
    {
        byte[] party = session.State.CharacterVitals!.CombatParty.Where(row => row != 255).ToArray();
        Assert.True(party.Length >= 2);
        for (int index = 0; index < 2; index++)
        {
            int row = party[index];
            if (!session.State.CharacterVitals.Records[row].Junction.GuardianForceAssignments.Contains(index))
            {
                BalambTestJourney.Apply(world, new(PlayerActionKind.JunctionGuardianForce, row, index.ToString()));
            }
            int[] commands = [2, 3, item ? 4 : 6];
            for (int slot = 0; slot < commands.Length; slot++)
            {
                int ability = 20 + session.State.CharacterVitals.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)commands[slot]);
                Assert.InRange(ability, 20, 38);
                BalambTestJourney.Apply(world, new(PlayerActionKind.SetJunctionCommand, row, ability.ToString(), Quantity: slot));
            }
        }
    }

    internal static void ReachWorldBattle(GameSession session, GameWorld world)
    {
        WorldState origin = session.State.World!;
        int direction = -1;
        for (int callback = 0; callback < 6000 && !world.HasPendingBattle; callback++)
        {
            WorldState current = session.State.World!;
            if (current.X < origin.X - 1000)
            {
                direction = 1;
            }
            else if (current.X > origin.X - 100)
            {
                direction = -1;
            }
            double yaw = current.CameraYaw * Math.Tau / 4096;
            world.Apply(new(PlayerActionKind.Move, Horizontal: (float)(-direction * Math.Cos(yaw)), Vertical: (float)(-direction * Math.Sin(yaw))));
            Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        }
        Assert.True(world.HasPendingBattle, "Ordinary movement did not produce an original Balamb encounter.");
    }

    internal static void Fight(GameSession session, GameWorld world, bool exerciseCommands)
    {
        bool drew = false;
        bool cast = false;
        bool summoned = false;
        int drawnSpell = 0;
        string? drawer = null;
        for (int input = 0; input < 4000 && world.Battle!.Outcome == BattleOutcome.Active; input++)
        {
            BattleSession battle = world.Battle;
            BattleCombatant? actor = battle.Combatants.FirstOrDefault(candidate => candidate.IsPlayer && candidate.IsReady && (!drew || cast || candidate.Id == drawer) && (!exerciseCommands || !cast || summoned || candidate.EquipmentIds.Contains(0)));
            if (actor is not null)
            {
                BattleCombatant target = battle.Combatants.First(candidate => !candidate.IsPlayer && candidate.IsAlive);
                if (exerciseCommands && !drew)
                {
                    BattleDrawSpell spell = target.DrawSpells.First(candidate => battle.ActionDefinitions.Any(action => action.Ability == BattleAbility.Magic && action.Id == candidate.Id && action.Effect == BattleActionEffect.Damage));
                    drawnSpell = spell.Id;
                    drawer = actor.Id;
                    world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: actor.Id, TargetId: target.Id, Ability: BattleAbility.Draw, ActionId: drawnSpell));
                }
                else if (exerciseCommands && !cast && actor.Id == drawer)
                {
                    world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: actor.Id, TargetId: target.Id, Ability: BattleAbility.Magic, ActionId: drawnSpell));
                    cast = true;
                }
                else if (exerciseCommands && !summoned && actor.EquipmentIds.Count > 0)
                {
                    world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: actor.Id, TargetId: target.Id, Ability: BattleAbility.GuardianForce, ActionId: 0));
                    summoned = true;
                }
                else
                {
                    world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: actor.Id, TargetId: target.Id));
                }
            }
            else if (battle.ActiveAction is not null || battle.QueuedActions.Count != 0)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
                if (!drew && drawer is not null && battle.Combatants.Single(candidate => candidate.Id == drawer).MagicStocks.Any(stock => stock.Id == drawnSpell && stock.Count > 0))
                {
                    drew = true;
                }
            }
            else
            {
                world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            }
        }
        Assert.Equal(BattleOutcome.Victory, world.Battle!.Outcome);
        Assert.DoesNotContain(world.Battle.Events, entry => entry.Kind == "debug-victory");
        if (exerciseCommands)
        {
            Assert.True(drew && cast && summoned, "The actual battle must execute Draw, Magic and a junctioned GF.");
        }
    }

    private static void FightWithItemAndShiva(GameWorld world, int potion)
    {
        bool usedItem = false;
        bool summoned = false;
        for (int input = 0; input < 4000 && world.Battle!.Outcome == BattleOutcome.Active; input++)
        {
            BattleSession battle = world.Battle;
            BattleCombatant? actor = battle.Combatants.FirstOrDefault(candidate => candidate.IsPlayer && candidate.IsReady && (!usedItem || summoned || candidate.EquipmentIds.Contains(1)));
            if (actor is not null)
            {
                BattleCombatant enemy = battle.Combatants.First(candidate => !candidate.IsPlayer && candidate.IsAlive);
                if (!usedItem)
                {
                    BattleCombatant ally = battle.Combatants.Where(candidate => candidate.IsPlayer && candidate.IsAlive).MinBy(candidate => candidate.CurrentHp)!;
                    world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: actor.Id, TargetId: ally.Id, Ability: BattleAbility.Item, ActionId: potion));
                    usedItem = true;
                }
                else if (!summoned && actor.EquipmentIds.Contains(1))
                {
                    world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: actor.Id, TargetId: enemy.Id, Ability: BattleAbility.GuardianForce, ActionId: 1));
                    summoned = true;
                }
                else
                {
                    world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: actor.Id, TargetId: enemy.Id));
                }
            }
            else if (battle.ActiveAction is not null || battle.QueuedActions.Count != 0)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
            }
            else
            {
                world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            }
        }
        Assert.True(usedItem && summoned);
        Assert.Equal(BattleOutcome.Victory, world.Battle!.Outcome);
    }

    private static void EqualState(GameState first, GameState second)
    {
        JsonObject left = JsonNode.Parse(JsonSerializer.Serialize(first))!.AsObject();
        JsonObject right = JsonNode.Parse(JsonSerializer.Serialize(second))!.AsObject();
        left.Remove("ReplayJournal");
        right.Remove("ReplayJournal");
        Assert.True(JsonNode.DeepEquals(left, right), "Current ordinary execution and independently recreated state differ.");
    }

    private static void VerifyRestoredNextTick(GameSession session, GameWorld world, string game)
    {
        session.SetEventState(world.Events.Capture());
        string save = Path.Combine(Path.GetTempPath(), "Memoria-Balamb-mid-field-" + Guid.NewGuid().ToString("N") + ".json");
        session.Save(save, registerJournalReference: false);
        GameSession restoredSession = new(GameSession.Load(save));
        using GameWorld restored = new(restoredSession, game, persistReplayJournal: false);
        restored.EnsureField();
        EqualState(session.State, restoredSession.State);
        world.ApplyBattleInput(new(BattleInputKind.Tick));
        restored.ApplyBattleInput(new(BattleInputKind.Tick));
        EqualState(session.State, restoredSession.State);
    }
}
