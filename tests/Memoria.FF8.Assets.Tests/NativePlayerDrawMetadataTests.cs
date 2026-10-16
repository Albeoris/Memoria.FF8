using Memoria.FF8.Runtime;
using System.Text.Json;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativePlayerDrawMetadataTests
{
    [Fact]
    public void EveryInstalledSpellSupportsDrawCastWithASeparateRecipientAndStandaloneRestore()
    {
        string directory = TestResources.GameDirectory;
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
                for (int frame = 0; frame < 30 && battle.Combatants[0].IsBusy; frame++)
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

    [Theory]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(65, false)]
    public void InstalledStockAndFailurePublishTheOriginalFactualPacketExactlyOnceAcrossRestore(int selection, bool failed)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleActionDefinition spell = assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == 1);
        BattleDefinition definition = Create(game, assets, spell).Capture().Definition;
        BattleCombatantDefinition[] actors = definition.Combatants.ToArray();
        actors[0] = actors[0] with { GuardianForceCompatibility = Enumerable.Repeat((ushort)4000, 16).ToArray() };
        actors[3] = actors[3] with { DrawSpells = [], NativeDrawSlots = [new(checked((byte)selection), failed ? (byte)255 : (byte)0), new(0, 0), new(0, 0), new(0, 0)] };
        BattleSession battle = new(definition with { Combatants = actors, NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(), GuardianForces = [new(1, 1000, 100, false)] }, BattleAtbMode.TurnBased);
        int[] hp = battle.Combatants.Select(actor => actor.CurrentHp).ToArray();
        battle.QueueAction("caster", "source", BattleAbility.Draw, selection);
        battle.Tick();
        BattleNativeDrawContext result = Assert.IsType<BattleNativeDrawContext>(battle.ActiveAction!.NativeDraw);
        Assert.Equal(failed, result.Failed);
        Assert.Equal((byte)1, battle.NativeActionPackets.ActionCount);
        Assert.Equal((byte)1, battle.NativeActionPackets.ContactCount);
        NativeBattleActionHeaderSlot header = battle.NativeActionPackets.Headers[0];
        Assert.Equal(failed ? (byte)0 : (byte)6, header.ReadByte(1));
        Assert.Equal(failed ? (byte)10 : game.ReadNativeBattleCommandSequences()[10], header.ReadByte(2));
        Assert.Equal(failed ? (ushort)9 : checked((ushort)selection), NativeBattleActionGraphics.Word(header, 4));
        Assert.Equal(failed ? (ushort)0 : (ushort)15, NativeBattleActionGraphics.Word(header, 6));
        Assert.Equal(failed ? (byte)0 : (byte)1, header.ReadByte(16));
        Assert.Equal(0x47EBD0u, header.Data!.ProducerAddress);
        Assert.Equal((ushort)6, header.Data.Argument);
        NativeBattleContactHalf contact = battle.NativeActionPackets.ReadContact(0).Primary;
        Assert.Equal((byte)3, contact.ActorIndex);
        Assert.Equal((ushort)0, contact.Amount6);
        Assert.Equal(hp, battle.Combatants.Select(actor => actor.CurrentHp));
        Assert.Equal(Enumerable.Repeat((ushort)4000, 16), battle.Capture().Combatants[0].GuardianForceCompatibility);
        Assert.DoesNotContain(battle.Events, item => item.Kind == "NativeText");
        int stock = battle.Combatants[0].MagicStocks.Sum(stock => stock.Count);
        Assert.Equal(selection < 64 ? result.Added : 0, stock);
        Assert.Equal(selection >= 64, Assert.Single(battle.GuardianForces).Acquired);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        for (int frame = 0; frame < 40 && battle.Combatants[0].IsBusy; frame++)
        {
            battle.Tick();
            restored.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        }
        Assert.Null(battle.ActiveAction);
        BattleEvent message = Assert.Single(battle.Events, item => item.Kind == "NativeText");
        Assert.True(message.Frame > 1);
        Assert.Equal(stock, battle.Combatants[0].MagicStocks.Sum(stock => stock.Count));
        Assert.Equal(hp, battle.Combatants.Select(actor => actor.CurrentHp));
    }

    [Fact]
    public void OriginalIfritDrawTablePreservesFourPhysicalPairsAndAllThreeLevelBands()
    {
        string directory = TestResources.GameDirectory;
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
