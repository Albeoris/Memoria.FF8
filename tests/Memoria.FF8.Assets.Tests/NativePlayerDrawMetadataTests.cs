using Memoria.FF8.Runtime;
using System.Text.Json;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativePlayerDrawMetadataTests
{
    [Fact]
    public void EveryInstalledSpellSupportsDrawCastWithASeparateRecipientAndStandaloneRestore()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII";
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleActionDefinition[] spells = assets.ActionDefinitions.Where(action => action.Ability == BattleAbility.Magic).ToArray();
        Assert.Equal(56, spells.Length);
        List<string> failures = [];
        foreach (BattleActionDefinition spell in spells)
        {
            try
            {
                BattleSession battle = Create(game, assets, spell);
                string target = spell.NativeAction!.AttackType is 5 or 6 ? "ko" : spell.Allies ? "ally" : "source";
                battle.QueueAction("caster", "source", BattleAbility.Draw, spell.Id, true, drawCastTargetId: target);
                battle.Tick();
                Assert.True(battle.KnownMagic!.Contains(spell.Id));
                BattleSession restored = BattleSession.Restore(battle.Capture());
                for (int frame = 0; frame < 30; frame++)
                {
                    battle.Tick();
                    restored.Tick();
                }
                Assert.Null(battle.ActiveAction);
                Assert.Empty(battle.Combatants[0].MagicStocks);
                Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
                Assert.Contains(battle.Events, item => item.Kind == "NativeText");
            }
            catch (Exception error)
            {
                failures.Add($"Draw Cast{spell.Id} {spell.Name}: {error.GetType().Name}: {error.Message}");
            }
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void OriginalIfritDrawTablePreservesFourPhysicalPairsAndAllThreeLevelBands()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII";
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        using BattleAssets assets = new(directory);
        NativeBattleEnemyTemplate template = assets.ReadEnemyTemplate(61, 6);
        Assert.Equal(3, template.NativeDrawTables!.Length);
        foreach (int level in new[] { 1, 20, 40, 100 })
        {
            NativeBattleDrawSlot[] expected = assets.ReadEnemyDrawSlots(61, level);
            Assert.Equal(4, expected.Length);
            Assert.Equal(expected, template.NativeDrawTables[template.Levels[level].AiLevelBand]);
            Assert.Equal(assets.ReadEnemyDrawSpells(61, level), expected.Where(slot => slot.Id is > 0 and < 64).Select(slot => new BattleDrawSpell(slot.Id, slot.Resistance)));
        }
    }

    private static BattleSession Create(GameAssets game, BattleAssets assets, BattleActionDefinition spell)
    {
        return new(new BattleDefinition
        {
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeFieldBattleRules = 0,
            Name = "Installed Draw Cast component",
            NativeActions = assets.NativeActions.ToArray(), ActionDefinitions = assets.ActionDefinitions.ToArray(),
            NativeKnownMagic = game.ReadNewGameKnownMagic(), NativeScannedModels = game.ReadNewGameScannedModels(), NativeScanAlwaysShow = false,
            NativeSavedBattleFlags = 0, NativeSavedBattleFlagsKnownMask = 255, NativeStatusRules = assets.ReadNativeStatusRules(0), NativeMessageSpeed = 0,
            NativeGlobalMessages = assets.ReadNativeGlobalBattleMessages(), NativeCommandNames = assets.ReadNativeCommandNames(),
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, Level = 100, Magic = 255, MaximumHp = 10000, CurrentHp = 10000, Speed = 1, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Draw], NativeDeathCount = 0, NativeStatusResistances = new byte[40] },
                new() { Id = "ally", Name = "Ally", ModelId = 1, IsPlayer = true, MaximumHp = 10000, CurrentHp = 1000, Speed = 1, NativeDeathCount = 0, NativeStatusResistances = new byte[40] },
                new() { Id = "ko", Name = "KO", ModelId = 2, IsPlayer = true, MaximumHp = 1000, CurrentHp = 0, Speed = 1, NativeStatus0 = 1, NativeDeathCount = 0, NativeStatusResistances = new byte[40] },
                new() { Id = "source", Name = "Source", MaximumHp = 1000000, CurrentHp = 1000000, Speed = 1, DrawSpells = [new(spell.Id, 0)], NativeStatusResistances = new byte[40] }
            ]
        }, BattleAtbMode.TurnBased);
    }
}
