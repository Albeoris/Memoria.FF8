using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffectCheckpointTests
{
    [Fact]
    public void Resume_the_named_supported_native_effect_checkpoint_for_one_rendered_callback()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT_COMPONENT_SNAPSHOT");
        Assert.SkipUnless(path is not null, "A concrete standalone supported native-effect checkpoint is required.");
        BattleSnapshot before = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(path!))!;
        BattleSession battle = BattleSession.Restore(before);
        string json = JsonSerializer.Serialize(before);
        Assert.Equal(json, JsonSerializer.Serialize(battle.Capture()));
        BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        BattleScenePresentation first = new(assets, before.Definition, true);
        BattleScenePresentation second = new(assets, before.Definition, true);
        Assert.Equal(first.Render(battle, 320, 224).ToArray(), second.Render(clone, 320, 224));
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        battle.AdvanceNativeGraphicsPresentation(callback);
        clone.AdvanceNativeGraphicsPresentation(callback);
        Assert.Equal(before.NativeGraphics!.Callback + 1, battle.NativeGraphics!.Callback);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
        Assert.Equal(first.Render(battle, 320, 224).ToArray(), second.Render(clone, 320, 224));
        Console.WriteLine($"Supported effect checkpoint continued frame{before.Frame}/callback{before.NativeGraphics.Callback} to frame{battle.Frame}/callback{battle.NativeGraphics.Callback}.");
    }
}
