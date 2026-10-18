using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGilgamesh330ContinuationTests
{
    [Fact]
    public void External_completed_standalone_restores_and_continues_one_native_callback()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_GILGAMESH330_COMPLETED");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(path), "Select a complete external Excalipoor standalone snapshot.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_GILGAMESH330_RESUME_OUTPUT") ?? throw new InvalidDataException("Select a distinct empty continuation output directory.");
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any());
        Directory.CreateDirectory(output);
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        using BattleAssets assets = new(directory);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        Assert.Null(source.ActiveAction);
        Assert.Equal(0u, source.NativeClock!.ActionPause);
        Assert.Equal((ushort)330, source.NativeGraphics!.OriginalEffect!.Binding.Effect);
        Assert.Equal(0u, source.NativeGraphics.OriginalEffect.ReturnedDescriptor);
        Assert.All(source.NativeGraphics.OriginalEffect.Pools, pool => Assert.DoesNotContain(pool.Slots, task => (task.Flags & 1) != 0));
        Assert.DoesNotContain(source.NativeGraphics.Descriptors.Queue, index => source.NativeGraphics.Descriptors.Packets[index].Command == 104);
        BattleSession first = BattleSession.Restore(source);
        BattleSession second = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(path!));
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(first.Capture()));
        BattleScenePresentation firstScene = new(assets, source.Definition, true);
        BattleScenePresentation secondScene = new(assets, source.Definition, true);
        Assert.Equal(firstScene.Render(first, 320, 224), secondScene.Render(second, 320, 224));
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        first.AdvanceNativeGraphicsPresentation(callback);
        second.AdvanceNativeGraphicsPresentation(callback);
        Assert.Equal(source.NativeGraphics.Callback + 1, first.NativeGraphics!.Callback);
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        Assert.Equal(firstScene.Render(first, 320, 224), secondScene.Render(second, 320, 224));
        NativeBattleRestorationPresentationTests.Publish(output, "continued", first.Capture(), firstScene.Render(first, 320, 224));
    }
}
