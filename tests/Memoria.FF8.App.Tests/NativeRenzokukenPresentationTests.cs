using System.Collections.Immutable;

using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeRenzokukenPresentationTests
{
    [Fact]
    public void Renzokuken_renders_embedded_body_and_weapon_poses_and_restores_the_same_process_pose()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        NativeCharacterVital character = vitals.Records[0];
        BattleNativePlayerStatRules rules = assets.ReadPlayerNativeStatRules(vitals, 0);
        BattleEncounter encounter = assets.LoadEncounter(94);
        BattleEnemyPlacement ifrit = encounter.Enemies.Single(enemy => enemy.ModelId == 61);
        BattleDefinition definition = new()
        {
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            EncounterId = 94,
            StageId = encounter.StageId,
            NativeActions = assets.NativeActions.ToArray(),
            NativeRenzokukenResources = game.ReadNativeRenzokukenResources(),
            NativeStatusRules = assets.ReadNativeStatusRules(0),
            NativeLimitsDisabled = false,
            LimitProgress = game.ReadNewGameLimitProgress(),
            Combatants =
            [
                new() { Id = "squall", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = character.Weapon, Attack = 80, MaximumHp = 486, CurrentHp = 400, InitialAtb = BattleSession.AtbMaximum, NativeLimit = assets.ReadNativeLimitDefinition(0), NativePlayerStatRules = rules, NativeStatusResistances = rules.ProjectStatusResistances(character.MagicInventory).ToArray(), NativeAttack = assets.ReadPlayerNativeAttackData(vitals, 0), NativeGrowth = new(character.HitPointBonus, character.StatBonuses.ToImmutableArray()) },
                new() { Id = "ifrit", Name = "Ifrit", ModelId = 61, SceneSlot = ifrit.Slot, Speed = 1, MaximumHp = 50000, CurrentHp = 50000, NativeStatusResistances = new byte[40], NativeRenzokukenMagicIds = assets.ReadNativeRenzokukenMagicIds(61) }
            ]
        };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        battle = BattleSession.Restore(battle.Capture() with { NativeLimits = [new("squall", 1)] });
        BattleScenePresentation scene = new(assets, definition, true);
        byte[] before = scene.Render(battle, 640, 448).ToArray();
        battle.QueueNativePlayerCommand("squall", "ifrit", 5, 0);
        battle.Tick();
        battle.AdvanceNativeRenzokukenPresentation(TimeSpan.FromMilliseconds(500));
        long frame = battle.Frame;
        string checkpoint = JsonSerializer.Serialize(battle.Capture());

        byte[] during = scene.Render(battle, 640, 448).ToArray();

        Assert.False(before.SequenceEqual(during));
        Assert.Equal(frame, battle.Frame);
        Assert.Equal(checkpoint, JsonSerializer.Serialize(battle.Capture()));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(checkpoint)!);
        BattleScenePresentation freshOriginal = new(assets, battle.Capture().Definition, true);
        BattleScenePresentation restoredScene = new(assets, restored.Capture().Definition, true);
        Assert.Equal(freshOriginal.Render(battle, 640, 448), restoredScene.Render(restored, 640, 448));
        for (int callback = 0; callback < 5000 && restored.GetNativeRenzokuken("squall") is not null; callback++)
        {
            restored.AdvanceNativeRenzokukenPresentation(TimeSpan.FromTicks(83334));
        }
        Assert.Null(restored.GetNativeRenzokuken("squall"));
        Assert.NotEmpty(restoredScene.Render(restored, 640, 448));
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_RENZ_EVIDENCE") is string evidence)
        {
            Directory.CreateDirectory(evidence);
            using var initial = BattlePreviewCanvas.BitmapFromRgba(before, 640, 448);
            initial.SavePng(TestResources.ArtifactFile("App.Tests.NativeRenzokukenPresentationTests.1.png"));
            using var active = BattlePreviewCanvas.BitmapFromRgba(during, 640, 448);
            active.SavePng(TestResources.ArtifactFile("App.Tests.NativeRenzokukenPresentationTests.2.png"));
        }
    }
}
