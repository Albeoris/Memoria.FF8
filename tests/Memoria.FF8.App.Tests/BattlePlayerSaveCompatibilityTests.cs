using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattlePlayerSaveCompatibilityTests(ITestOutputHelper output)
{
    [Fact]
    public void Legacy_player_checkpoint_preserves_field_continuation_and_progress()
    {
        string input = TestResources.FixturePath("App.Tests.BattlePlayerSaveCompatibilityTests.1");
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        byte[] before = SHA256.HashData(File.ReadAllBytes(input));
        GameState source = GameSession.Load(input);
        GameState loaded = PlayerStateLoader.Load(input, directory, sharedAssets: assets);
        Assert.Equal(SceneKind.Field, loaded.Scene);
        Assert.Equal(source.FieldId, loaded.FieldId);
        Assert.Null(loaded.Battle);
        Assert.Equal(source.GlobalRandom, loaded.GlobalRandom);
        Assert.Equal(source.CrtRandom, loaded.CrtRandom);
        Assert.Equal(source.MenuRandom, loaded.MenuRandom);
        Assert.Equal(JsonSerializer.Serialize(source.EventRandom), JsonSerializer.Serialize(loaded.EventRandom));
        Assert.Equal(JsonSerializer.Serialize(source.CharacterVitals), JsonSerializer.Serialize(loaded.CharacterVitals));
        Assert.Equal(JsonSerializer.Serialize(source.Economy), JsonSerializer.Serialize(loaded.Economy));
        Assert.Equal(JsonSerializer.Serialize(source.CardInventory), JsonSerializer.Serialize(loaded.CardInventory));
        Assert.Equal(source.AppliedBattleResultIds, loaded.AppliedBattleResultIds);
        Assert.Equal(JsonSerializer.Serialize(source.BattleStatistics), JsonSerializer.Serialize(loaded.BattleStatistics));
        JsonElement sourceEvent = source.EventState!.Value;
        JsonElement loadedEvent = loaded.EventState!.Value;
        Assert.Equal(sourceEvent.GetProperty("Frame").GetInt64(), loadedEvent.GetProperty("Frame").GetInt64());
        Assert.True(JsonElement.DeepEquals(sourceEvent.GetProperty("Fibers"), loadedEvent.GetProperty("Fibers")));

        GameSession session = new(loaded);
        using GameWorld resumed = new(session, directory, sharedAssets: assets);
        resumed.EnsureField();
        long frame = resumed.Events.Frame;
        resumed.Events.Step();
        Assert.Equal(frame + 1, resumed.Events.Frame);
        Assert.Null(resumed.Events.LastFault);
        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(input)));
    }

    [Fact]
    public void Installed_formation_115_draw_stock_and_next_action_complete_with_the_selected_party_order()
    {
        string directory = TestResources.GameDirectory;
        string input = TestResources.FixturePath("App.Tests.BattlePlayerSaveCompatibilityTests.1");
        using GameAssets assets = new(directory);
        GameSession session = new(PlayerStateLoader.Load(input, directory, sharedAssets: assets));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        session.SetCharacterVitals(session.CharacterVitals!.WithBattleParty([4, 0, 5], 3));

        // An independent combat component from the selected original formation and the saved character rows.
        BattleSession battle = world.PrepareBattleEntry(115, 0, 41, BattleAtbMode.TurnBased).Battle;
        Assert.NotNull(battle.Presentation);
        Assert.Equal(new[] { "party-4", "party-0", "party-5" }, battle.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.Id));
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == "party-4");
        BattleCombatant enemy = Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.IsActive);
        Assert.Equal("enemy-0", enemy.Id);
        Assert.Contains(BattleAbility.Draw, caster.Abilities);
        Assert.Contains(enemy.NativeDrawSlots, slot => slot.Id == 31);
        for (int frame = 0; frame < 512 && !battle.NativeEntryReady; frame++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);

        const int spell = 31;
        int initialStock = caster.MagicStocks.SingleOrDefault(stock => stock.Id == spell)?.Count ?? 0;
        int initialHp = enemy.CurrentHp;
        caster.Atb = BattleSession.AtbMaximum;
        battle.QueueAction(caster.Id, enemy.Id, BattleAbility.Draw, spell);
        BattleNativeDrawContext? draw = null;
        bool visible = false;
        for (int frame = 0; frame < 512 && !Completed(battle, caster.Id, 1); frame++)
        {
            battle.Tick();
            if (draw is null && battle.ActiveAction is { Ability: BattleAbility.Draw, NativeDraw: { } result } && battle.Presentation?.Graphics.ActionGraphics?.Header is { } reference)
            {
                draw = result;
                NativeBattleActionHeaderSlot header = battle.Presentation!.NativeActionPackets.ReadHeader(reference);
                Assert.Equal((byte)6, header.Command);
                Assert.Equal((byte)30, header.Sequence);
                Assert.Equal((ushort)spell, header.Action);
                Assert.Equal((ushort)15, header.Effect);
                Assert.Equal((byte)0, header.ReadRepeatModeMinusOne());
                NativeBattleContactHalf contact = Assert.Single(battle.Presentation!.NativeActionPackets.ReadHeaderContacts(reference)).Primary;
                Assert.Equal(new BattleEnemyActorModel(new(enemy.Id)), contact.Recipient);
                Assert.Equal((ushort)0, contact.Amount6);
            }
            visible |= battle.Presentation?.Graphics?.OriginalEffect is { Binding.Effect: 15, ReturnedDescriptor: > 0 };
        }
        Assert.True(draw is not null, JsonSerializer.Serialize(new { battle.Frame, battle.Outcome, battle.ActiveAction, battle.QueuedActions, RecentEvents = battle.Events.TakeLast(12) }));
        Assert.False(draw.Failed);
        Assert.Equal((byte)9, draw.Quantity);
        Assert.Equal((byte)9, draw.Added);
        Assert.True(visible);
        Assert.True(Completed(battle, caster.Id, 1));
        Assert.Equal(initialHp, enemy.CurrentHp);
        Assert.Equal(initialStock + draw.Added, caster.MagicStocks.Single(stock => stock.Id == spell).Count);
        Assert.Single(battle.Events, item => item.Kind == "Draw" && item.ActorId == caster.Id && item.Amount == draw.Added);
        output.WriteLine($"Formation 115: Draw completed at frame {battle.Frame}, amount {draw.Quantity}, added {draw.Added}, enemy initial HP {initialHp}.");

        BattleSession restored = BattleSession.Restore(battle.Capture());
        restored.AttachNativeEffectResourceResolver(effect => assets.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        caster.Atb = BattleSession.AtbMaximum;
        restored.Combatants.Single(actor => actor.Id == caster.Id).Atb = BattleSession.AtbMaximum;
        battle.QueueAttack(caster.Id, enemy.Id);
        restored.QueueAttack(caster.Id, enemy.Id);
        for (int frame = 0; frame < 512 && !Completed(battle, caster.Id, 2); frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.True(Completed(battle, caster.Id, 2));
        Assert.True(enemy.CurrentHp < initialHp);
        Assert.Equal(initialStock + draw.Added, caster.MagicStocks.Single(stock => stock.Id == spell).Count);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        output.WriteLine($"Following attack completed at frame {battle.Frame}, enemy HP {enemy.CurrentHp}.");

        static bool Completed(BattleSession active, string actorId, int actions) =>
            active.Events.Count(item => item.Kind == "ActionComplete" && item.ActorId == actorId) == actions;
    }


    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Installed_field_encounter_table_runs_entry_and_first_enemy_turn_in_both_roster_modes(bool expanded)
    {
        string directory = TestResources.GameDirectory;
        string input = TestResources.FixturePath("App.Tests.BattlePlayerSaveCompatibilityTests.1");
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(692);
        NativeFieldEncounterDefinition encounters = Assert.IsType<NativeFieldEncounterDefinition>(field.RandomEncounters);
        ushort[] formations = encounters.Formations.Distinct().ToArray();
        Assert.Contains((ushort)115, formations);

        GameSession session = new(PlayerStateLoader.Load(input, directory, sharedAssets: assets));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        byte[] party = expanded ? [4, 0, 5, 3] : [4, 0, 5];
        session.SetCharacterVitals(session.CharacterVitals!.WithBattleParty(party, party.Length));
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = party.Length, AdditionalEnemies = 0 });
        foreach (ushort formation in formations)
        {
            BattleSession battle = world.PrepareBattleEntry(formation, 0, 41, BattleAtbMode.TurnBased).Battle;
            Assert.NotNull(battle.Presentation);
            Assert.Equal(party.Select(row => $"party-{row}"), battle.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.Id));
            BattleSession restored = battle.ForkForContinuation();
            for (int callback = 0; callback < 512 && !battle.NativeEntryReady; callback++)
            {
                battle.Tick();
                restored.Tick();
            }
            Assert.True(battle.NativeEntryReady, $"Formation {formation} did not complete entry.");
            Assert.True(restored.NativeEntryReady, $"Formation {formation} did not complete entry after restore.");
            BattleCombatant enemy = battle.Combatants.First(actor => !actor.IsPlayer && actor.IsActive && actor.CanAct);
            Assert.NotNull(battle.ReadDefinition().Combatants.Single(actor => actor.Id == enemy.Id).NativeAi);
            byte originalTurn = enemy.AiTurnCount;
            foreach (BattleCombatant actor in battle.Combatants)
            {
                actor.Atb = actor == enemy ? BattleSession.AtbMaximum : 0;
                restored.Combatants.Single(candidate => candidate.Id == actor.Id).Atb = actor.Atb;
            }

            for (int callback = 0; callback < 120 && enemy.AiTurnCount == originalTurn; callback++)
            {
                battle.Tick();
                restored.Tick();
            }
            Assert.Equal(unchecked((byte)(originalTurn + 1)), enemy.AiTurnCount);
            BattleSnapshot live = battle.Capture();
            BattleSnapshot continuation = restored.Capture();
            Assert.Equal(live.Frame, continuation.Frame);
            Assert.Equal(JsonSerializer.Serialize(live.Combatants), JsonSerializer.Serialize(continuation.Combatants));
            Assert.Equal(JsonSerializer.Serialize(live.Random), JsonSerializer.Serialize(continuation.Random));
            Assert.Equal(live.NativeAiVariables, continuation.NativeAiVariables);
            Assert.Equal(JsonSerializer.Serialize(live.QueuedActions), JsonSerializer.Serialize(continuation.QueuedActions));
            Assert.Equal(live.QueuedActions.Length, continuation.QueuedActions.Length);
            for (int i = 0; i < live.QueuedActions.Length; i++)
            {
                BattleTargetRequest actual = live.QueuedActions[i].TargetRequest;
                BattleTargetRequest resumed = continuation.QueuedActions[i].TargetRequest;
                Assert.Equal((actual.Eligibility, actual.AllowsKnockedOutAllies, actual.Replacement, actual.DeathCallbackSelf), (resumed.Eligibility, resumed.AllowsKnockedOutAllies, resumed.Replacement, resumed.DeathCallbackSelf));
                Assert.Equal(actual.Selection.GetType(), resumed.Selection.GetType());
                Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(actual.Selection, actual.Selection.GetType()), JsonSerializer.SerializeToElement(resumed.Selection, resumed.Selection.GetType())));
            }
            output.WriteLine($"Field {field.Id}, formation {formation}, expanded {expanded}: first AI turn at frame {battle.Frame}.");
        }
    }

}
