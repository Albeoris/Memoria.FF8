using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ScenarioRevivalRecipeTests
{
    [Fact]
    public void InstalledRevivalRowsUseOriginalCommandsAndADistinctExplicitKoRecipient()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for original revival recipes.");
        using BattleAssets assets = new(directory);
        using GameAssets game = new(directory);
        NativeCharacterVitals initial = game.ReadNewGameCharacterVitals();
        string template = Path.GetTempFileName();
        try
        {
            File.WriteAllText(template, """{"fixture":{"characters":[{"row":0,"character_id":0,"stocks":[],"guardian_forces":[],"magic_command":{"slot":0,"ability":20}}]},"driver":{"input":{}}}""");
            BattleNativeAction[] actions = assets.NativeActions.Where(action => action.KernelSection is 1 or 7 && action.Command is 2 or 4 && action.AttackType is 5 or 6 && action.MagicId is > 0 and <= 400).ToArray();
            Assert.NotEmpty(actions);
            foreach (BattleNativeAction native in actions)
            {
                Assert.True(ScenarioDiscovery.TryPrepareRevivalRecipe(assets, initial, native, template, out JsonObject? result, out string? blocker), blocker);
                JsonObject fixture = result!["fixture"]!.AsObject();
                JsonNode caster = fixture["characters"]![0]!;
                JsonNode target = fixture["characters"]![1]!;
                int targetRow = target["row"]!.GetValue<int>();
                Assert.NotEqual(0, targetRow);
                Assert.Equal(initial.Records[targetRow].CharacterId, target["character_id"]!.GetValue<byte>());
                Assert.Equal(0, target["hit_points"]!.GetValue<int>());
                Assert.True(target["knocked_out"]!.GetValue<bool>());
                Assert.True(caster["hit_points"]!.GetValue<ushort>() > 0);
                Assert.Equal($"party-{targetRow}", result["driver"]!["input"]!["target_id"]!.GetValue<string>());
                Assert.Equal(native.Command, result["driver"]!["expected_native_command"]!.GetValue<byte>());
                Assert.Equal(native.ActionId, result["driver"]!["expected_native_action"]!.GetValue<ushort>());
                Assert.Equal(native.MagicId, result["driver"]!["expected_effect"]!.GetValue<ushort>());
                Assert.Null(result["driver"]!["repairs"]);
            }
        }
        finally
        {
            File.Delete(template);
        }
    }
}
