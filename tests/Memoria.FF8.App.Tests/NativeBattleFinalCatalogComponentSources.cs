using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleFinalCatalogComponentSources
{
    internal static bool Supports(ushort effect) => effect is 141 or 159 or 160 or 161 or 259 or 331 or 332 or 333 or 334 or 335 or 336 or 337 or 339 or 340 or 341 or 342 or 343;

    internal static BattleSnapshot? Prepare(ushort effect, string gameDirectory, string outputDirectory)
    {
        if (effect == 331)
        {
            NativeEffectFixtureRequest request = new("component331", 331, 8, 313, 0, 800, 240) { EncounterId = 749, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true };
            NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, gameDirectory, Path.Combine(outputDirectory, "fixture"));
            Assert.NotEqual("blocked", exported.Status);
            return NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(outputDirectory, "fixture", "queued-action104.battle.json"));
        }
        int model = effect switch
        {
            159 => 92,
            161 => 91,
            334 => 67,
            335 => 77,
            336 => 90,
            337 => 117,
            339 => 112,
            340 => 126,
            341 => 84,
            342 => 124,
            343 => 122,
            _ => -1
        };
        if (model < 0 && effect is not (141 or 160 or 259 or 332 or 333))
        {
            return null;
        }
        using GameAssets game = new(gameDirectory);
        using BattleAssets assets = new(gameDirectory);
        byte crisis = effect switch { 141 or 160 => 1, 259 => 2, 332 => 3, _ => 4 };
        BattleDefinition definition;
        if (model >= 0)
        {
            definition = NativeBattleRenzokukenSpecialCastTests.CreateDefinition(game, assets, effect, model);
        }
        else
        {
            BattleDefinition original = NativeBattleGuestLimitCastTests.CreateDefinition(game, assets, 0, effect);
            definition = original with { NativeRenzokukenResources = game.ReadNativeRenzokukenResources(), Combatants = [original.Combatants[0], original.Combatants[1] with { NativeRenzokukenMagicIds = assets.ReadNativeRenzokukenMagicIds(61) }] };
        }
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        battle.AttachNativeEffectResourceResolver(selected => game.ReadNativeEffectCatalogForEffects([selected])[selected - 1]);
        for (int callback = 0; callback < 400 && !battle.CanChoosePlayerCommand("party"); callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        Assert.True(battle.CanChoosePlayerCommand("party"));
        // These are the existing standalone tests' declared crisis/resource inputs, outside ordinary admission coverage.
        BattleCombatantDefinition target = definition.Combatants.Single(actor => actor.Id == "enemy");
        NativeBattleRandomState random = Enumerable.Range(0, 256).Select(seed => NativeBattleRandomState.Initialize((byte)seed)).First(candidate =>
        {
            candidate = candidate.Draw(out byte resource).Draw(out byte finisher);
            return target.NativeRenzokukenMagicIds[crisis + 1 + resource % 2] == effect && !NativeBattleRenzokukenRules.HasFinisher(crisis, finisher);
        });
        BattleSnapshot ready = battle.Capture();
        battle = BattleSession.Restore(ready with { NativeLimits = [new("party", crisis)], Random = random, NativeStage = ready.NativeStage! with { ModelColors = ready.NativeStage.ModelColors! with { Color40 = 0x00312617 } } });
        battle.AttachNativeEffectResourceResolver(selected => game.ReadNativeEffectCatalogForEffects([selected])[selected - 1]);
        battle.QueueNativePlayerCommand("party", "enemy", 5, 0);
        BattleSnapshot queued = NativeBattleLimitComponentSources.AwaitQueued(battle, effect);
        Assert.Equal(effect, battle.GetNativeRenzokuken("party")!.MagicId);
        return queued;
    }
}
