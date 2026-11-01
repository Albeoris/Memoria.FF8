using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleWaterRepeatAbortTests
{
    [Theory]
    [InlineData(4, 2, true)]
    [InlineData(5, 3, true)]
    [InlineData(61, 2, false)]
    public void Lethal_slots_water_excludes_the_aborted_header_and_completes_after_enemy_release(int enemyModel, int repetitions, bool cards)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Select the installed original game resources.");
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleDefinition original = NativeBattleGuestLimitCastTests.CreateDefinition(game, assets, 5, 222);
        BattleCombatantDefinition[] actors =
        [
            original.Combatants[0],
            original.Combatants[1] with { ModelId = enemyModel, Name = "Enemy", MaximumHp = 1, CurrentHp = 1, NativeCombatantFlags = assets.ReadEnemyNativeCombatantFlags(enemyModel), NativeStatusResistances = assets.ReadEnemyNativeStatusData(enemyModel).Resistances.ToArray(), Information246 = assets.ReadEnemyNativeInformation246(enemyModel) }
        ];
        BattleDefinition definition = original with { SelphieCardSlots = cards, Combatants = actors, NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), assets.LoadEncounter(94)) };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        Attach(battle);
        for (int callback = 0; callback < 400 && !battle.CanChoosePlayerCommand("party"); callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.CanChoosePlayerCommand("party"));
        BattleNativeSlotsRoll roll = new(10, checked((byte)repetitions));
        battle = Restore(battle.Capture() with { NativeLimits = [new("party", 4) { Slots = cards ? null : roll }] });
        if (cards)
        {
            BattleNativeSlotsDeal deal = battle.BeginNativeSlotsDeal("party");
            battle = Restore(battle.Capture() with { PendingSlotsDeal = deal with { Candidates = deal.Candidates.SetItem(0, roll) } });
            Assert.True(battle.SelectNativeSlotsCard(deal.Id, 0));
            Assert.True(battle.SubmitNativeSlotsCard(deal.Id));
        }
        else
        {
            battle.QueueNativePlayerCommand("party", "enemy", 16, 10);
        }
        string? outputRoot = Environment.GetEnvironmentVariable("MEMORIA_FF8_REPEAT_ABORT_EVIDENCE");
        string? output = outputRoot is null ? null : Path.Combine(outputRoot, $"water-{enemyModel}-{repetitions}-{cards}");
        if (output is not null)
        {
            Directory.CreateDirectory(output);
            Save("accepted", battle.Capture());
        }
        bool produced = false;
        bool waterSeen = false;
        bool restoredMidpoint = false;
        BattleSnapshot before = battle.Capture();
        try
        {
            for (int callback = 0; callback < 2048; callback++)
            {
                before = battle.Capture();
                battle.Tick();
                BattleSnapshot current = battle.Capture();
                if (!produced && current.NativeActionPackets!.ActionCount != 0)
                {
                    Assert.Equal(2, current.NativeActionPackets.ActionCount);
                    Assert.Equal(1, current.NativeActionPackets.ContactCount);
                    Assert.Equal(0, current.NativeActionPackets.Headers[0].ReadByte(17));
                    Assert.Equal(0, battle.Combatants.Single(actor => actor.Id == "enemy").CurrentHp);
                    produced = true;
                    Save("produced", current);
                }
                if (current.NativeGraphics!.OriginalEffect is { ReturnedDescriptor: not 0, Binding.Effect: 222 })
                {
                    waterSeen = true;
                    if (!restoredMidpoint && callback > 120 && current.NativeGraphics.OriginalEffect.Pools.Any(pool => pool.Slots.Any(task => task.Callback == NativeBattleWater222Controller.Callback && (task.Flags & 1) != 0)))
                    {
                        CompareNext("midpoint", current);
                        restoredMidpoint = true;
                    }
                }
                if (waterSeen && current.ActiveAction is null && current.QueuedActions.Length == 0 && current.NativeGraphics.OriginalEffect is { ReturnedDescriptor: 0 } && current.NativeGraphics.Descriptors.Queue.IsEmpty)
                {
                    Assert.True(produced);
                    Assert.True(restoredMidpoint);
                    if (enemyModel is 4 or 5)
                    {
                        Assert.Equal(0, current.NativeGraphics.Models[3].Flags0 & 2);
                    }
                    CompareNext("completed", current);
                    Console.WriteLine($"Water repeat abort: enemy {enemyModel}, casts {repetitions}, cards {cards}, frame {current.Frame}, native callback {current.NativeGraphics.Callback}.");
                    return;
                }
            }
            Assert.Fail("Water did not release its action and descriptors within the bounded continuation.");
        }
        catch
        {
            Save("before-boundary", before);
            throw;
        }

        void Attach(BattleSession session)
        {
            session.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        }

        BattleSession Restore(BattleSnapshot snapshot)
        {
            BattleSession restored = BattleSession.Restore(snapshot);
            Attach(restored);
            return restored;
        }

        void Save(string name, BattleSnapshot snapshot)
        {
            if (output is not null)
            {
                File.WriteAllBytes(Path.Combine(output, name + ".battle.json"), JsonSerializer.SerializeToUtf8Bytes(snapshot));
            }
        }

        void CompareNext(string name, BattleSnapshot snapshot)
        {
            Save(name, snapshot);
            BattleSession direct = Restore(snapshot);
            BattleSession cold = Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
            direct.Tick();
            cold.Tick();
            Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
            BattleScenePresentation scene = new(assets, definition, true);
            byte[] pixels = scene.Render(direct, 320, 224).ToArray();
            Assert.Equal(pixels, new BattleScenePresentation(assets, definition, true).Render(cold, 320, 224).ToArray());
            Save(name + "-next", cold.Capture());
            if (output is not null)
            {
                using var bitmap = Memoria.FF8.GameUi.BattlePreviewCanvas.BitmapFromRgba(pixels, 320, 224);
                bitmap.Save(Path.Combine(output, name + ".png"));
            }
        }
    }
}
