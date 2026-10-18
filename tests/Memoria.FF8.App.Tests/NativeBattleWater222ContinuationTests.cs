using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleWater222ContinuationTests
{
    [Fact]
    public void Explicit_named_Water_checkpoint_continues_independently_and_publishes_its_actual_release()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_WATER222_RESUME");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(input), "An explicit named Water checkpoint is required.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_WATER222_OUTPUT") ?? throw new InvalidDataException("A distinct output is required.");
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any());
        Directory.CreateDirectory(output);
        BattleSnapshot saved = NativeBattleEffectFamilyRunner.LoadSnapshot(input!);
        Assert.Equal((ushort)222, saved.NativeGraphics!.OriginalEffect!.Binding.Effect);
        BattleSession first = BattleSession.Restore(saved);
        BattleSession second = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(input!));
        int callbacks = Environment.GetEnvironmentVariable("MEMORIA_FF8_WATER222_RESUME_CALLBACKS") is { } text ? int.Parse(text) : 1;
        Assert.InRange(callbacks, 1, 256);
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        BattleScenePresentation scene = new(assets, saved.Definition, true);
        BattleScenePresentation independent = new(assets, saved.Definition, true);
        for (int index = 0; index < callbacks; index++)
        {
            TimeSpan tick = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
            first.AdvanceNativeGraphicsPresentation(tick);
            second.AdvanceNativeGraphicsPresentation(tick);
            Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
            Assert.Equal(scene.Render(first, 320, 224).ToArray(), independent.Render(second, 320, 224).ToArray());
        }
        Assert.Equal(0u, first.NativeGraphics!.OriginalEffect!.ReturnedDescriptor);
        NativeBattleRestorationPresentationTests.Publish(output, "continued", first.Capture(), scene.Render(first, 320, 224));
    }
}
