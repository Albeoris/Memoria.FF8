using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleHoly175PreviewTests
{
    [Fact]
    public void Installed_Holy_renders_whole_ability_and_continues_a_private_fork()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select original resources for Holy presentation.");
        GameSession session = GameWorld.CreateColdProcess(directory!);
        session.StartNewGame();
        using GameWorld world = new(session, directory!);
        BattleSession active = world.StartEffectPreview(new(1, 0) { AbilityId = 14, CommandId = 2 });
        using BattleAssets assets = new(directory!);
        BattleScenePresentation scene = new(assets, active.Capture().Definition);
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_HOLY175_OUTPUT");
        active.PublishDiagnosticAbilityPreview(2, 14);
        TimeSpan callback = BattlePresentationTiming.ElapsedForCallbacks(1);
        bool captureRestored = false;
        bool flareRestored = false;
        bool entered = false;
        int rendered = 0;
        for (int index = 0; index < 600; index++)
        {
            active.AdvanceNativeGraphicsPresentation(callback);
            BattleSnapshot snapshot = active.CaptureForContinuation();
            entered |= snapshot.ReadFixturePresentation().NativeGraphics?.OriginalEffect?.Binding.Effect == 175;
            bool capturing = snapshot.ReadFixturePresentation().NativeGraphics?.PrimitiveFrame?.LastCommands.Any(command => command.Command.Packet.Bank == NativeBattleHoly175CaptureMemory.PacketBank) == true;
            if (capturing)
            {
                NativeBattlePrimitiveFrame primitive = snapshot.ReadFixturePresentation().NativeGraphics!.PrimitiveFrame!;
                Assert.Contains(primitive.LastCommands, command => command.Pass == 0 && command.Command.Words[0] == 0xE3040240u);
                Assert.Contains(primitive.LastCommands, command => command.Pass == 0 && command.Command.Words[0] == 0xE5080240u);
            }
            if (entered && index % 10 == 0)
            {
                byte[] pixels = scene.Render(active, 320, 224).ToArray();
                rendered++;
                if (output is not null)
                {
                    Directory.CreateDirectory(output);
                    NativeBattleRestorationPresentationTests.Publish(output, $"frame-{index:D3}", snapshot, pixels);
                }
            }
            bool flare = snapshot.ReadFixturePresentation().NativeGraphics?.OriginalEffect?.Pools.SelectMany(pool => pool.Slots).Any(task => (task.Flags & 1) != 0 && task.Callback == 0x5D43B0) == true;
            if (!captureRestored && capturing || !flareRestored && flare)
            {
                BattleSession cold = active.ForkForContinuation();
                cold.AdvanceNativeGraphicsPresentation(callback);
                active.AdvanceNativeGraphicsPresentation(callback);
                BattleSnapshot continued = active.CaptureForContinuation();
                BattleSnapshot restoredNext = cold.CaptureForContinuation();
                // Preview ATB menu suppression is a host policy; compare the saved graphical effect independently of menu admission.
                Assert.Equal(JsonSerializer.Serialize(continued.ReadFixturePresentation().NativeGraphics! with { UiServices = null }), JsonSerializer.Serialize(restoredNext.ReadFixturePresentation().NativeGraphics! with { UiServices = null }));
                Assert.Equal(JsonSerializer.Serialize(continued.ReadFixturePresentation().NativeEffectMemory), JsonSerializer.Serialize(restoredNext.ReadFixturePresentation().NativeEffectMemory));
                Assert.Equal(JsonSerializer.Serialize(continued.ReadFixturePresentation().NativeCamera), JsonSerializer.Serialize(restoredNext.ReadFixturePresentation().NativeCamera));
                Assert.Equal(JsonSerializer.Serialize(continued.ReadFixturePresentation().NativeStage), JsonSerializer.Serialize(restoredNext.ReadFixturePresentation().NativeStage));
                Assert.Equal(scene.Render(active, 320, 224).ToArray(), new BattleScenePresentation(assets, snapshot.Definition).Render(cold, 320, 224).ToArray());
                captureRestored |= capturing;
                flareRestored |= flare;
            }
            if (entered && active.ObserveDiagnosticDrainBlockers().Count == 0)
            {
                Assert.True(captureRestored);
                Assert.True(flareRestored);
                Assert.True(rendered > 5);
                return;
            }
        }
        Assert.Fail("Holy did not finish its complete graphical ability within the diagnostic bound.");
    }
}
