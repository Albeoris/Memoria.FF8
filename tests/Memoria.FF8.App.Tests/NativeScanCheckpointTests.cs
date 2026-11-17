using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeScanCheckpointTests
{
    [Fact]
    public void Released_Scan_checkpoint_resumes_exactly_one_graphics_callback_from_two_disk_loads()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_SCAN_CHECKPOINT");
        Assert.SkipUnless(path is not null && File.Exists(path), "Select a completed standalone Scan checkpoint with MEMORIA_FF8_SCAN_CHECKPOINT.");
        BattleSnapshot snapshot = Read();
        AssertReleased(snapshot);
        BattleSession first = BattleSession.Restore(snapshot);
        BattleSession second = BattleSession.Restore(Read());
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(first.Capture()));
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(second.Capture()));
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
        second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
        Assert.Equal(snapshot.ReadFixturePresentation().NativeGraphics!.Callback + 1, first.Presentation!.Graphics.Callback);
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        AssertReleased(first.Capture());
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        byte[] pixels = new BattleScenePresentation(assets, snapshot.Definition).Render(first, 320, 224);
        Assert.Equal(pixels, new BattleScenePresentation(assets, snapshot.Definition).Render(second, 320, 224));
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_SCAN_CHECKPOINT_EVIDENCE");
        if (output is not null)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "scan-next-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff"), first.Capture(), pixels);
        }
        Console.WriteLine($"Released Scan {snapshot.Frame}/{snapshot.ReadFixturePresentation().NativeGraphics!.Callback} continued at {first.Frame}/{first.Presentation!.Graphics.Callback}; whole JSON and RGBA agree.");

        BattleSnapshot Read() => JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(path!))!;
    }

    private static void AssertReleased(BattleSnapshot snapshot)
    {
        NativeBattleGraphicsState graphics = snapshot.ReadFixturePresentation().NativeGraphics!;
        Assert.Equal((ushort)40, graphics.OriginalEffect!.Binding.Effect);
        Assert.Equal(0u, graphics.OriginalEffect.ReturnedDescriptor);
        Assert.Null(snapshot.ActiveAction);
        Assert.Empty(snapshot.QueuedActions);
        Assert.Equal(0u, snapshot.NativeClock!.ActionPause);
        Assert.True(graphics.Descriptors.Queue.IsEmpty);
        Assert.All(graphics.OriginalEffect.Pools.SelectMany(pool => pool.Slots), task => Assert.Equal(0, task.Flags & 1));
        Assert.Equal(0, graphics.UiServices!.Flags942 & 0x200);
        Assert.Equal(0x100, graphics.UiServices.Flags942 & 0x100);
        NativeBattleScanPresentationMemory presentation = snapshot.ReadFixturePresentation().NativeEffectMemory!.Scan!.Presentation;
        for (int index = 0; index < 7; index++)
        {
            if (presentation.Selection269A1BC.ReadDword(index * 4) == 1)
            {
                Assert.Equal(0u, ReadOriginalModelCell(graphics, index).ScaleVectorAddress48);
            }
        }
    }
}
