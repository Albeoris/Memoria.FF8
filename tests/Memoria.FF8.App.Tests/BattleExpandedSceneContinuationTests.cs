using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using Memoria.FF8.Rendering;
using System.Collections.Immutable;

namespace Memoria.FF8.App.Tests;

public sealed class BattleExpandedSceneContinuationTests
{
    [Fact]
    public void Standalone_scene_keeps_full_coordinates_while_waiting_and_after_snapshot_restore()
    {
        GameState state = GameSession.Load(TestResources.FixturePath("battle-expanded-scene"));
        BattleSnapshot source = Assert.IsType<BattleSnapshot>(state.Battle);
        GameSession session = new(state);
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EXPANDED_SCENE_OUTPUT");
        bool extended = false;
        bool packedDifference = false;
        long firstCallback = battle.NativeGraphics!.Callback;
        for (int callback = 0; callback < 180; callback++)
        {
            battle.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 30 + 1));
            world.SynchronizeBattlePresentation();
            foreach (NativeBattleLogicalCommand command in battle.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Opcode is 0x24 or 0x2C && command.Packet.Bank is "off_B6D070" or "off_B6D094"))
            {
                Assert.True(command.ProjectedCoordinates.HasValue, $"Callback {battle.NativeGraphics.Callback} lost full scene coordinates.");
                var points = command.ProjectedCoordinates!.Value;
                Assert.Equal(command.Opcode == 0x2C ? 4 : 3, points.Length);
                Assert.True(Winding(points[0], points[1], points[2]) < 0);
                extended |= points.Any(point => point.X < short.MinValue || point.X > short.MaxValue || point.Y < short.MinValue || point.Y > short.MaxValue);
            }
            if (output is not null && callback % 30 == 0)
            {
                Directory.CreateDirectory(output);
                byte[] pixels = scene.Render(battle, 1280, 720);
                using var bitmap = Memoria.FF8.GameUi.BattlePreviewCanvas.BitmapFromRgba(pixels, 1280, 720);
                bitmap.Save(Path.Combine(output, $"callback-{battle.NativeGraphics.Callback}.png"));
            }
            if (output is not null && !packedDifference)
            {
                NativeBattlePrimitiveFrame frame = battle.NativeGraphics.PrimitiveFrame!;
                NativeBattlePrimitiveFrame packed = frame with { LastCommands = frame.LastCommands.Select(item => item with { Command = item.Command with { ProjectedCoordinates = null } }).ToImmutableArray() };
                BattleRenderer raster = new(640, 360, ViewportLayout.Fit(320, 240, 640, 360));
                var memory = battle.NativeEffectMemory!;
                byte[] fullPixels = raster.RenderNativePrimitiveFrame(frame, memory.Textures, capabilities: source.Definition.NativeGraphics!.PrimitiveCapabilities, textureWindowCache: memory.PrimitiveProcess!.TextureWindowCache).ToArray();
                byte[] packedPixels = raster.RenderNativePrimitiveFrame(packed, memory.Textures, capabilities: source.Definition.NativeGraphics.PrimitiveCapabilities, textureWindowCache: memory.PrimitiveProcess.TextureWindowCache);
                if (!fullPixels.SequenceEqual(packedPixels))
                {
                    packedDifference = true;
                    using var fullImage = Memoria.FF8.GameUi.BattlePreviewCanvas.BitmapFromRgba(fullPixels, 640, 360);
                    using var packedImage = Memoria.FF8.GameUi.BattlePreviewCanvas.BitmapFromRgba(packedPixels, 640, 360);
                    fullImage.Save(Path.Combine(output, "full-coordinate-frame.png"));
                    packedImage.Save(Path.Combine(output, "packed-coordinate-frame.png"));
                    Console.WriteLine($"Wrapped packet raster differs at callback {battle.NativeGraphics.Callback}.");
                }
            }
        }
        Assert.True(extended, "The supplied component must exercise projected coordinates outside signed WORD range.");
        Assert.True(battle.NativeGraphics!.Callback >= firstCallback + 180);
        string checkpointPath = output is null ? TestResources.ArtifactFile("expanded-scene-standalone.json") : Path.Combine(output, "continued-standalone.json");
        world.CaptureBattleState();
        session.Save(checkpointPath, compact: true);
        BattleSession restored = BattleSession.Restore(GameSession.Load(checkpointPath).Battle!);
        battle.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 30 + 1));
        restored.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 30 + 1));
        Assert.Equal(battle.NativeGraphics.Callback, restored.NativeGraphics!.Callback);
        Assert.Equal(JsonSerializer.Serialize(battle.NativeGraphics.PrimitiveFrame!.LastCommands), JsonSerializer.Serialize(restored.NativeGraphics.PrimitiveFrame!.LastCommands));
        Assert.Equal(scene.Render(battle, 1280, 720), new BattleScenePresentation(assets, source.Definition, true).Render(restored, 1280, 720));
        Console.WriteLine($"Expanded scene frame {battle.Frame}, callbacks {firstCallback}..{battle.NativeGraphics.Callback}, standalone {checkpointPath}.");
    }

    private static long Winding(BattleSceneScreenPoint a, BattleSceneScreenPoint b, BattleSceneScreenPoint c)
    {
        return ((long)b.Y - c.Y) * ((long)b.X - a.X) - ((long)b.Y - a.Y) * ((long)b.X - c.X);
    }
}
