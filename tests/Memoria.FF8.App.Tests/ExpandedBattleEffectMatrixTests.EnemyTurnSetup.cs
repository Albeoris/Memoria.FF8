using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    [Theory]
    [InlineData(78, 248, HigherEnemyBand)]
    [InlineData(153, 37, HigherEnemyBand)]
    [InlineData(154, 37, HigherEnemyBand)]
    [InlineData(158, 413, HigherEnemyBand)]
    [InlineData(170, 71, HigherEnemyBand)]
    [InlineData(238, 117, HigherEnemyBand)]
    public void Installed_enemy_turn_follows_original_hp_band_and_random_branches(int effectId, int formation, int band)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        NativeBattleEffectBinding effect = Assert.Single(game.ReadNativeEffectCatalog(), binding => binding.Effect == effectId);
        EnemyScenarioScriptedAction[] scripts = ScenarioEnemyDiscovery.Discover(assets).ScriptedActions.Where(script => script.NativeEffectId == effectId).ToArray();
        EnemyRouteResult result = RunDiscoveredEnemyCast(directory, PrepareField(directory), assets, effect, scripts, probeEnemies: true);
        Assert.True(result.Status is "native" or "logical", result.Detail);
        Assert.Contains($"Formation {formation},", result.Detail);
        Assert.Contains($"band {band},", result.Detail);
        if (HpTurnSetup(effectId) is { } setup)
        {
            Assert.Contains(setup.UsesDemi ? "genuine Demi setup" : "genuine Attack setup", result.Detail);
        }
    }

    [Fact]
    public void Installed_Saliva_turn_executes_the_original_dispatch()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        NativeBattleEffectBinding effect = Assert.Single(game.ReadNativeEffectCatalog(), binding => binding.Effect == 138);
        EnemyScenarioScriptedAction[] scripts = ScenarioEnemyDiscovery.Discover(assets).ScriptedActions.Where(script => script.NativeEffectId == effect.Effect).ToArray();
        EnemyRouteResult result = RunExpandedSalivaTurn(directory, PrepareField(directory), effect, scripts);
        Assert.True(result.Status == "native", result.Detail);
        Assert.Contains("Formation 378,", result.Detail);
        Assert.Contains("c0m044", result.Detail);
        Assert.Contains("callback 1, command 8/175, sequence 16, 1 contact(s)", result.Detail);
    }

    private static EnemyRouteResult RunExpandedSalivaTurn(string directory, GameState field, NativeBattleEffectBinding effect, EnemyScenarioScriptedAction[] scripts)
    {
        const ushort formation = 378;
        field = field with { GameplayPreferences = field.GameplayPreferences with { Tuning = field.GameplayPreferences.Tuning with { MaximumDamage = 4 } } };
        GameSession original = null!;
        using GameWorld entry = StartBattle(directory, field, formation, captureSession: session => original = session, nativeSeed: 0);
        BattleSession current = Assert.IsType<BattleSession>(entry.Battle);
        BattleCombatant owner = current.Combatants.First(actor => !actor.IsPlayer && actor.ModelId == 44 && current.GetCombatantDefinition(actor.Id).NativeAi?.LevelBand < HigherEnemyBand);
        int band = current.GetCombatantDefinition(owner.Id).NativeAi!.LevelBand;
        EnemyScenarioScriptedAction[] matching = scripts.Where(script => script.ModelId == owner.ModelId && script.LevelBand == band && script is { Callback: 1, Offset: 0x64, Command: 8, ActionId: 175, Sequence: 16 }).ToArray();
        Assert.NotEmpty(matching);
        int before = current.Events.Count;
        bool entered = false;
        for (int turn = 0; turn < 24 && !entered; turn++)
        {
            if (!current.Combatants.Any(actor => actor.IsPlayer && actor.IsReady))
            {
                entry.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            }
            BattleCombatant? attacker = current.Combatants.FirstOrDefault(actor => actor.IsPlayer && actor.IsReady);
            if (attacker is not null)
            {
                entry.ApplyBattleInput(new(BattleInputKind.Attack) { ActorId = attacker.Id, TargetId = owner.Id });
            }
            for (int frame = 0; frame < 100 && current.Outcome == BattleOutcome.Active; frame++)
            {
                entry.ApplyBattleInput(new(BattleInputKind.Tick));
                entered |= current.ActiveAction is { NativeAiCallbackStage: 2, NativeQueuedCommand: 8, ActionId: 172 } reaction && reaction.ActorId == owner.Id;
                if (current.ActiveAction is null && current.QueuedActions.Count == 0)
                {
                    break;
                }
            }
        }
        Assert.True(entered, $"{owner.Id} never entered its original reaction phase after physical hits.");
        Assert.Null(current.ActiveAction);
        Assert.Empty(current.QueuedActions);
        Assert.True(current.Events.Skip(before).Count(item => item is { Kind: "Damage", Ability: BattleAbility.Attack } && item.TargetId == owner.Id) >= 3);
        GameState state = original.PrepareSnapshot();
        BattleSnapshot snapshot = Assert.IsType<BattleSnapshot>(state.Battle);
        Assert.Equal(1u, snapshot.Combatants.Single(actor => actor.Id == owner.Id).NativeParticipant!.Variables[0]);
        BattleCombatantSnapshot[] combatants = snapshot.Combatants.Select(actor =>
        {
            bool ready = actor.Id == owner.Id;
            NativeBattleAtbState? gauge = actor.NativeAtb;
            return actor with { Atb = ready ? BattleSession.AtbMaximum : 0, NativeAtb = gauge is null ? null : gauge with { Current = ready ? gauge.Maximum : 0 }, LimitTurnCrisis = null };
        }).ToArray();
        // The callback returns when the first byte is divisible by three. Its Saliva branch then needs an even byte.
        NativeBattleRandomState random = NativeBattleRandomState.ForecastPhase(3);
        string checkpoint = TestResources.ArtifactFile($"{Guid.NewGuid():N}.battle.json");
        File.WriteAllText(checkpoint, JsonSerializer.Serialize(state with { Battle = snapshot with { Combatants = combatants, Random = random } }));
        using GameWorld world = new(new(GameSession.Load(checkpoint)), directory);
        world.EnsureField();
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        world.ApplyBattleInput(new(BattleInputKind.SetMode, Mode: BattleAtbMode.Normal));
        EnemyRouteResult result = ObserveEnemyCast(world, battle, effect, new() { [owner.Id] = matching });
        return result with { Detail = $"Formation {formation}, genuine hit reaction, ready actor {owner.Id}, battle RNG phase 3: {result.Detail}" };
    }

    private static EnemyAttackSetup? HpTurnSetup(int effect) => effect switch
    {
        78 => new(248, 49, 0, 5, HigherEnemyBand, UsesDemi: true),
        // c0m017 callback 1 tests the original owner's HP against five tenths before either action.
        153 or 154 => new(37, 17, 0, 5, HigherEnemyBand),
        158 => new(413, 24, 0, 3, HigherEnemyBand, UsesDemi: true),
        170 => new(71, 36, 0, 0, HigherEnemyBand, UsesDemi: true, QuarterHp: true),
        186 => new(247, 57, 0, 5, BlasterEnemyBand, UsesDemi: true),
        _ => null
    };

    private const byte DemiSpellId = 13;

    private static void PrepareDemiStock(NativeCharacterVital[] records)
    {
        NativeCharacterVital owner = records[10];
        Assert.Equal(DemiSpellId, owner.MagicInventory[0].SpellId);
        foreach (int row in new[] { 0, 1, 2, 3, 10 })
        {
            NativeCharacterVital actor = records[row];
            NativeMagicStock[] stock = actor.MagicInventory.ToArray();
            int slot = Array.FindIndex(stock, entry => entry.SpellId == DemiSpellId);
            if (slot < 0)
            {
                slot = Array.FindIndex(stock, entry => entry.SpellId == 0);
            }
            Assert.InRange(slot, 0, stock.Length - 1);
            stock[slot] = new(DemiSpellId, 5);
            records[row] = actor with { Junction = actor.Junction with { Commands = owner.Junction.Commands }, MagicInventory = stock };
        }
    }

    private static int? SeededTurnFormation(int effect) => effect switch
    {
        68 => 377,
        238 => 117,
        345 => 247,
        _ => null
    };

    private static void PrepareThreeLivingParty(NativeCharacterVital[] records)
    {
        for (int index = 3; index < records.Length; index++)
        {
            records[index] = records[index] with { HitPoints = new(0, 0), Status = 1 };
        }
    }
}
