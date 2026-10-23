using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleRenzokukenFinisherDeathContinuationTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void Lethal_contacts_finalize_enemy_death_through_the_selected_finisher(int hitPoints)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select the installed original game resources.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleNativeAction finisher = assets.NativeActions.Single(action => action.Command == 249 && action.MagicId == 163);
        byte finisherIndex = checked((byte)finisher.ActionId);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        byte weapon = Enumerable.Range(0, 7).Select(value => checked((byte)value)).First(value =>
        {
            NativeCharacterVitals equipped = vitals with { Records = vitals.Records.Select((record, index) => index == 0 ? record with { Weapon = value } : record).ToArray() };
            return (assets.ReadPlayerNativeStatRules(equipped, 0).WeaponRecord[2] & (1 << finisherIndex)) != 0;
        });
        vitals = vitals with { Records = vitals.Records.Select((record, index) => index == 0 ? record with { Weapon = weapon } : record).ToArray() };
        BattleNativePlayerStatRules rules = assets.ReadPlayerNativeStatRules(vitals, 0);
        NativeCharacterVital character = vitals.Records[0];
        BattleNativeCharacterGrowth growth = new(character.HitPointBonus, character.StatBonuses.ToImmutableArray());
        BattleNativePlayerStatistics statistics = rules.Project(character.MagicInventory, growth);
        BattleDefinition source = NativeBattleGuestLimitCastTests.CreateDefinition(game, assets, 0, 163);
        BattleCombatantDefinition actor = source.Combatants[0] with { Name = "Squall", WeaponId = weapon, NativePlayerStatRules = rules, NativeAttack = assets.ReadPlayerNativeAttackData(vitals, 0), NativeGrowth = growth, Attack = statistics.Strength, Defense = statistics.Vitality, Magic = statistics.Magic, Spirit = statistics.Spirit, Speed = statistics.Speed, Luck = statistics.Luck, Hit = statistics.Hit, Evasion = statistics.Evasion };
        BattleEncounter encounter = assets.LoadEncounter(1);
        BattleEnemyPlacement soldier = encounter.Enemies.First();
        BattleCombatantDefinition enemy = source.Combatants[1] with { ModelId = soldier.ModelId, SceneSlot = soldier.Slot, Information246 = assets.ReadEnemyNativeInformation246(soldier.ModelId), NativeStatusResistances = assets.ReadEnemyNativeStatusData(soldier.ModelId).Resistances.ToArray(), CurrentHp = hitPoints, NativeRenzokukenMagicIds = assets.ReadNativeRenzokukenMagicIds(soldier.ModelId) };
        BattleCombatantDefinition[] actors = [actor, enemy];
        BattleDefinition definition = source with { Combatants = actors, NativeRenzokukenResources = game.ReadNativeRenzokukenResources(), NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), encounter), NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([160, 163]) };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        BattleScenePresentation scene = new(assets, definition, true);
        string output = Path.Combine(Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_EFFECT_EVIDENCE") ?? Path.GetTempPath(), "rough-divide-163-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine("Rough Divide evidence: " + output);
        BattleSnapshot before = battle.Capture();
        bool initial = false;
        bool selected = false;
        bool midpoint = false;
        try
        {
            for (int callback = 0; callback < 400 && !battle.CanChoosePlayerCommand("party"); callback++)
            {
                before = battle.Capture();
                battle.Tick();
            }
            Assert.True(battle.CanChoosePlayerCommand("party"));
            const byte crisis = 1;
            NativeBattleRandomState random = Enumerable.Range(0, 256).Select(seed => NativeBattleRandomState.Initialize((byte)seed)).First(candidate =>
            {
                candidate = candidate.Draw(out byte resource).Draw(out byte finish).Draw(out byte selection);
                return actors[1].NativeRenzokukenMagicIds[crisis + 1 + resource % 2] == 160 && NativeBattleRenzokukenRules.HasFinisher(crisis, finish) && NativeBattleRenzokukenRules.Finisher(rules.WeaponRecord[2], selection) == finisherIndex;
            });
            battle = BattleSession.Restore(battle.Capture() with { NativeLimits = [new("party", crisis)], Random = random });
            Publish("before-command");
            battle.QueueNativePlayerCommand("party", "enemy", 5, 0);
            CompareNext("queued");
            for (int callback = 0; callback < 4096; callback++)
            {
                before = battle.Capture();
                battle.Tick();
                NativeBattleGraphicsState graphics = battle.NativeGraphics!;
                if (!initial && graphics.OriginalEffect is { Binding.Effect: 160, ReturnedDescriptor: > 0 })
                {
                    Assert.True(battle.Capture().NativeEffectMemory!.Renzokuken160!.InitialControl);
                    Assert.Equal(finisherIndex, battle.GetNativeRenzokuken("party")!.FinisherActionId);
                    CompareNext("initial-control");
                    initial = true;
                }
                bool active = graphics.OriginalEffect is { Binding.Effect: 163, ReturnedDescriptor: > 0 };
                if (active)
                {
                    selected = true;
                    NativeBattleActionPacketsState packets = battle.Capture().NativeActionPackets!;
                    NativeBattleActionHeaderSlot header = packets.Headers.Single(header => header.FirstContactIndex is not null && header.ReadByte(1) == 250);
                    NativeBattleContactHalf contact = packets.ReadContact(header.FirstContactIndex!.Value).Primary;
                    Assert.Equal((byte)3, contact.Sequence);
                    Assert.Equal(8, contact.Flags2 & 8);
                    Assert.Equal(1, contact.SavedStatuses4 & 1);
                    if (!midpoint && graphics.OriginalEffect!.Pools[1].Slots[0].ReadWord(12) >= 50)
                    {
                        CompareNext("midpoint");
                        midpoint = true;
                    }
                }
                if (selected && battle.GetNativeRenzokuken("party") is null && battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.NativeGraphics!.OriginalEffect is { ReturnedDescriptor: 0 })
                {
                    Assert.True(initial);
                    Assert.Equal(0, battle.Combatants.Single(actor => !actor.IsPlayer).CurrentHp);
                    Assert.Equal((byte)3, battle.NativeGraphics!.Models[3].Sequence);
                    Assert.Equal((byte)6, battle.NativeGraphics.Models[3].Kind5);
                    CompareNext("death-removal");
                    for (int removalCallback = 0; removalCallback < 128 && (battle.NativeGraphics.Models[3].Flags0 & 2) != 0; removalCallback++)
                    {
                        battle.Tick();
                    }
                    Assert.Equal(0, battle.NativeGraphics!.Models[3].Flags0 & 2);
                    Assert.True(midpoint);
                    Assert.Equal(4 + finisher.HitCount, battle.Events.Count(entry => entry.Kind == "Damage" && entry.ActorId == "party" && entry.TargetId == "enemy"));
                    int initialDamage = battle.Events.Where(entry => entry.Kind == "Damage" && entry.ActorId == "party" && entry.TargetId == "enemy").Take(4).Sum(entry => entry.Amount);
                    Assert.Equal(hitPoints == 1, initialDamage == hitPoints);
                    Assert.Null(battle.Capture().NativeTimedAction);
                    Assert.Equal((byte)0, battle.Capture().NativeClock!.ActionPause);
                    CompareNext("completed");
                    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "passed", weapon, finisherIndex, battle.Frame, NativeCallback = battle.NativeGraphics.Callback, initial, selected, midpoint, hitPoints }));
                    return;
                }
            }
            Assert.Fail("Rough Divide did not release its original task and command owners within the diagnostic bound.");
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(output, "failed-live", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "failed", before.Frame, NativeCallback = before.NativeGraphics?.Callback, Error = error.ToString() }));
            throw;
        }

        void Publish(string name)
        {
            NativeBattleRestorationPresentationTests.Publish(output, name, battle.Capture(), scene.Render(battle, 320, 224).ToArray());
        }

        void CompareNext(string name)
        {
            Publish(name);
            BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(Path.Combine(output, name + ".battle.json")))!);
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
            before = battle.Capture();
            battle.Tick();
            clone.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, definition, true).Render(clone, 320, 224).ToArray());
            Publish(name + "-next");
        }
    }
}
