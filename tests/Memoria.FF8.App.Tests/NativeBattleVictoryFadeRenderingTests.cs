using System.Collections.Immutable;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class NativeBattleVictoryFadeRenderingTests
{
    [Theory]
    [InlineData(false, 320, 240)]
    [InlineData(true, 320, 240)]
    [InlineData(false, 480, 240)]
    [InlineData(true, 480, 240)]
    [InlineData(false, 320, 400)]
    [InlineData(true, 320, 400)]
    public void Victory_fade_darkens_the_whole_published_viewport_and_continues_after_compact_restore(bool hardware, int width, int height)
    {
        string source = TestResources.FixturePath("battle-victory-fade");
        string directory = TestResources.GameDirectory;
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = NativeBattleProductionEffectAdmissionTests.RestoreWorld(session, directory);
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        Assert.Equal((byte)4, battle.NativeGraphics!.NativeOutcome!.ResultByte6E7);
        Assert.Equal((byte)128, battle.NativeEffectMemory!.PrimitiveProcess!.ScreenFadeEB8);
        TimeSpan elapsed = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        bool checkedDarkening = false;
        for (int callback = 0; callback < 14; callback++)
        {
            battle.AdvanceNativeGraphicsPresentation(elapsed, TimeSpan.Zero);
            world.SynchronizeBattlePresentation();
            NativeBattlePrimitiveFrame frame = battle.NativeGraphics!.PrimitiveFrame!;
            NativeBattleLogicalResolvedCommand fade = ReadFade(frame);
            Assert.Equal((ushort)1, fade.Command.Flags20);
            Assert.Equal(new uint[] { 0, 320, 216u << 16, (216u << 16) | 320 }, fade.Command.Words.Skip(1));
            byte shade = (byte)fade.Command.Words[0];
            if (shade < 64)
            {
                continue;
            }
            byte[] faded = Render(battle, frame, hardware, width, height);
            byte[] unfaded = Render(battle, frame with { LastCommands = frame.LastCommands.Where(command => command != fade).ToImmutableArray() }, hardware, width, height);
            int distantChannels = 0;
            for (int pixel = 0; pixel < width * height; pixel++)
            {
                for (int channel = 0; channel < 3; channel++)
                {
                    int offset = pixel * 4 + channel;
                    if (unfaded[offset] == 0)
                    {
                        continue;
                    }
                    Assert.True(faded[offset] < unfaded[offset], $"Fade did not darken pixel ({pixel % width}, {pixel / width}), channel {channel}.");
                    if (pixel % width >= width / 2 && pixel / width >= height / 2)
                    {
                        distantChannels++;
                    }
                }
            }
            Assert.True(distantChannels > 0, "The retained scene must include visible pixels beyond the top-left region.");
            checkedDarkening = true;
            break;
        }
        Assert.True(checkedDarkening, "Victory fade must reach a visible shade within fourteen native callbacks.");

        world.CaptureBattleState();
        session.SetEventState(world.Events.Capture());
        string saved = TestResources.ArtifactFile("victory-fade-" + Guid.NewGuid().ToString("N") + ".json");
        session.Save(saved, compact: true);
        using GameWorld restored = NativeBattleProductionEffectAdmissionTests.RestoreWorld(new GameSession(GameSession.Load(saved)), directory);
        battle.AdvanceNativeGraphicsPresentation(elapsed, TimeSpan.Zero);
        restored.Battle!.AdvanceNativeGraphicsPresentation(elapsed, TimeSpan.Zero);
        world.SynchronizeBattlePresentation();
        restored.SynchronizeBattlePresentation();
        NativeBattlePrimitiveFrame next = battle.NativeGraphics!.PrimitiveFrame!;
        NativeBattlePrimitiveFrame restoredNext = restored.Battle.NativeGraphics!.PrimitiveFrame!;
        Assert.Equal(battle.NativeGraphics.Callback, restored.Battle.NativeGraphics.Callback);
        Assert.Equal(ReadFade(next).Command.Words.ToArray(), ReadFade(restoredNext).Command.Words.ToArray());
        Assert.Equal(ReadFade(next).Command.Flags20, ReadFade(restoredNext).Command.Flags20);
        Assert.Equal(Render(battle, next, hardware, width, height), Render(restored.Battle, restoredNext, hardware, width, height));
    }

    private static NativeBattleLogicalResolvedCommand ReadFade(NativeBattlePrimitiveFrame frame)
    {
        return Assert.Single(frame.LastCommands, command => command.Command.Opcode == 0x2A && command.Command.OrderingBucket == 1 && command.Pass != 0);
    }

    private static byte[] Render(BattleSession battle, NativeBattlePrimitiveFrame frame, bool hardware, int width, int height)
    {
        NativeBattleEffectMemory memory = battle.NativeEffectMemory!;
        using GpuTestDevice? device = hardware ? new() : null;
        using GpuTriangleRenderer? gpu = device is null ? null : new(device.Device);
        BattleRenderer renderer = new(width, height, ViewportLayout.Fit(320, 240, width, height)) { TriangleRenderer = gpu };
        byte[] pixels = renderer.RenderNativePrimitiveFrame(frame, memory.Textures, capabilities: battle.Capture().Definition.NativeGraphics!.PrimitiveCapabilities, textureWindowCache: memory.PrimitiveProcess!.TextureWindowCache);
        return gpu is null ? pixels : GpuTestDevice.Read(gpu.Target).SelectMany(color => new byte[] { color.R, color.G, color.B, color.A }).ToArray();
    }
}
