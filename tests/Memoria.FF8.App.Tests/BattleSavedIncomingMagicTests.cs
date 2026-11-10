using System.Reflection;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleSavedIncomingMagicTests(ITestOutputHelper output)
{
    [Fact]
    public void SavedTonberryCounterTargetsCasterAfterMulticastAndJsonRestore()
    {
        string input = TestResources.FixturePath("App.Tests.BattleSavedIncomingMagicTests.1");
        GameSession session = new(GameplaySaveSessionLoader.Load(input, TestResources.GameDirectory));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        Assert.NotNull(world.Field!.RandomEncounters);
        foreach (ushort formation in world.Field.RandomEncounters.Formations.Distinct())
        {
            BattleDefinition definition = world.CreateEncounterBattle(formation) with { NativeGraphics = null, NativeClockRules = null };
            Assert.All(definition.Combatants.Where(actor => actor.IsPlayer), actor => Assert.Equal(0u, actor.NativePlayerStatRules!.NativeStatusPassiveFlags & 8));
            BattleNativeAction grudge = Assert.Single(definition.NativeActions, action => action.Command == 8 && action.ActionId == 223);
            Assert.Equal((byte)34, grudge.AttackType);
            Assert.Equal(0u, grudge.Statuses1 & 0x8000);
            foreach ((uint status, int casts) in new (uint, int)[] { (0, 1), (0x20000, 1), (0x40000, 1), (0x20000, 2), (0x40000, 3) })
            {
                BattleDefinition variant = definition with { Combatants = definition.Combatants.Select(actor => actor with { InitialAtb = actor.Id == "party-3" ? BattleSession.AtbMaximum : 0 }).ToArray() };
                BattleSession battle = new(variant, BattleAtbMode.Normal);
                if (status != 0)
                {
                    typeof(BattleSession).GetMethod("SetNativeAiStatus", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [battle.Combatants.Single(actor => actor.Id == "party-3"), status == 0x20000 ? 33 : 34, 1]);
                }
                BattleActionDefinition spell = definition.ActionDefinitions.First(action => action.Ability == BattleAbility.Magic && action.NativeAction?.AttackType == 2 && battle.GetAvailableMagicCasts("party-3", action.Id) >= 3);
                Assert.Contains(casts, battle.GetNativeMagicCastCounts("party-3", spell.Id));
                battle.QueueAction("party-3", "enemy-1", BattleAbility.Magic, spell.Id, magicTargets: casts == 1 ? null : Enumerable.Repeat("enemy-1", casts).ToArray());
                BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
                for (int frame = 0; frame < 100; frame++)
                {
                    battle.Tick();
                    restored.Tick();
                }
                output.WriteLine(JsonSerializer.Serialize(new { Formation = formation, Status = status, Casts = casts, Damage = battle.Events.Where(entry => entry.Kind == "Damage").Select(entry => new { entry.ActorId, entry.TargetId, entry.Amount }) }));
                Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
                Assert.Equal(casts, battle.Events.Count(entry => entry.Kind == "Damage" && entry.ActorId == "party-3" && entry.TargetId == "enemy-1"));
                Assert.DoesNotContain(battle.Events, entry => entry.Kind == "Damage" && entry.ActorId == "enemy-1" && entry.TargetId == "enemy-1");
                if (casts == 1)
                {
                    Assert.Contains(battle.Events, entry => entry.Kind == "Damage" && entry.ActorId == "enemy-1" && entry.TargetId == "party-3" && entry.Amount == 1220);
                }
            }
        }
    }
}
