using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleHoly175PreviewTests
{
    [Fact]
    public void Installed_Holy_renders_whole_ability_and_continues_a_serialized_live_frame()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select original resources for Holy presentation.");
        GameSession session = GameWorld.CreateColdProcess(directory!);
        session.StartNewGame();
        using GameWorld world = new(session, directory!);
        BattleSession active = world.StartEffectPreview(new(1, 0) { AbilityId = 14, CommandId = 2 });
        using BattleAssets assets = new(directory!);
        BattleScenePresentation scene = new(assets, active.Capture().Definition, true);
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_HOLY175_OUTPUT");
        active.PublishDiagnosticAbilityPreview(2, 14);
        TimeSpan callback = NativeBattleGraphicsPresentationClock.ElapsedForCallbacks(1) + TimeSpan.FromTicks(1);
        bool captureRestored = false;
        bool flareRestored = false;
        bool entered = false;
        int rendered = 0;
        for (int index = 0; index < 600; index++)
        {
            active.AdvanceNativeGraphicsPresentation(callback);
            BattleSnapshot snapshot = active.Capture();
            entered |= snapshot.NativeGraphics?.OriginalEffect?.Binding.Effect == 175;
            bool capturing = snapshot.NativeGraphics?.PrimitiveFrame?.LastCommands.Any(command => command.Command.Packet.Bank == NativeBattleHoly175CaptureMemory.PacketBank) == true;
            if (capturing)
            {
                NativeBattlePrimitiveFrame primitive = snapshot.NativeGraphics!.PrimitiveFrame!;
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
            bool flare = snapshot.NativeGraphics?.OriginalEffect?.Pools.SelectMany(pool => pool.Slots).Any(task => (task.Flags & 1) != 0 && task.Callback == 0x5D43B0) == true;
            if (!captureRestored && capturing || !flareRestored && flare)
            {
                BattleSession cold = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
                cold.AdvanceNativeGraphicsPresentation(callback);
                active.AdvanceNativeGraphicsPresentation(callback);
                BattleSnapshot continued = active.Capture();
                BattleSnapshot restoredNext = cold.Capture();
                // Preview ATB menu suppression is a host policy; compare the saved graphical effect independently of menu admission.
                Assert.Equal(JsonSerializer.Serialize(continued.NativeGraphics! with { UiServices = null }), JsonSerializer.Serialize(restoredNext.NativeGraphics! with { UiServices = null }));
                Assert.Equal(JsonSerializer.Serialize(continued.NativeEffectMemory), JsonSerializer.Serialize(restoredNext.NativeEffectMemory));
                Assert.Equal(JsonSerializer.Serialize(continued.NativeCamera), JsonSerializer.Serialize(restoredNext.NativeCamera));
                Assert.Equal(JsonSerializer.Serialize(continued.NativeStage), JsonSerializer.Serialize(restoredNext.NativeStage));
                Assert.Equal(scene.Render(active, 320, 224).ToArray(), new BattleScenePresentation(assets, snapshot.Definition, true).Render(cold, 320, 224).ToArray());
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
