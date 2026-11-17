using Memoria.FF8.Assets;
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
    public void Victory_fade_darkens_the_whole_published_viewport_and_continues_after_private_fork(bool hardware, int width, int height)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: game);
        ExpandedBattleInstalledEntry.Prepare(world, session, _ => { }, allies: 3, additionalEnemies: 0);
        BattleSession battle = ExpandedBattleInstalledEntry.Start(world);
        ExpandedBattleInstalledEntry.AdmitDebugVictory(world);
        Assert.Equal((byte)4, battle.Presentation!.Graphics.NativeOutcome!.ResultByte6E7);
        bool checkedDarkening = false;
        for (int callback = 0; callback < 1200 && !battle.CanReturnToField; callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            NativeBattlePrimitiveFrame frame = battle.Presentation!.Graphics.PrimitiveFrame!;
            if (!frame.LastCommands.Any(IsFade))
            {
                continue;
            }
            NativeBattleLogicalResolvedCommand fade = ReadFade(frame);
            Assert.Equal((ushort)1, fade.Command.Flags20);
            Assert.Equal(new uint[] { 0, 320, 216u << 16, (216u << 16) | 320 }, fade.Command.Words.Skip(1));
            byte shade = (byte)fade.Command.Words[0];
            if (shade < 64)
            {
                continue;
            }
            byte[] faded = Render(battle, frame, hardware, width, height);
            byte[] unfaded = Render(battle, frame with { LastCommands = NativeBattleResolvedCommandList.From(frame.LastCommands.Where(command => command != fade)) }, hardware, width, height);
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
        Assert.True(checkedDarkening, "Victory fade must reach a visible shade before terminal release.");

        BattleSession restored = battle.ForkForContinuation();
        world.ApplyBattleInput(new(BattleInputKind.Tick));
        restored.Tick();
        NativeBattlePrimitiveFrame next = battle.Presentation!.Graphics.PrimitiveFrame!;
        NativeBattlePrimitiveFrame restoredNext = restored.Presentation!.Graphics.PrimitiveFrame!;
        Assert.Equal(battle.Presentation!.Graphics.Callback, restored.Presentation!.Graphics.Callback);
        Assert.Equal(ReadFade(next).Command.Words.ToArray(), ReadFade(restoredNext).Command.Words.ToArray());
        Assert.Equal(ReadFade(next).Command.Flags20, ReadFade(restoredNext).Command.Flags20);
        Assert.Equal(Render(battle, next, hardware, width, height), Render(restored, restoredNext, hardware, width, height));
    }

    private static NativeBattleLogicalResolvedCommand ReadFade(NativeBattlePrimitiveFrame frame)
    {
        return Assert.Single(frame.LastCommands, IsFade);
    }

    private static bool IsFade(NativeBattleLogicalResolvedCommand command)
    {
        return command.Command.Opcode == 0x2A && command.Command.OrderingBucket == 1 && command.Pass != 0;
    }

    private static byte[] Render(BattleSession battle, NativeBattlePrimitiveFrame frame, bool hardware, int width, int height)
    {
        NativeBattleEffectMemory memory = battle.Presentation!.Memory;
        using GpuTestDevice? device = hardware ? new() : null;
        using GpuTriangleRenderer? gpu = device is null ? null : new(device.Device);
        BattleRenderer renderer = new(width, height, ViewportLayout.Fit(320, 240, width, height)) { TriangleRenderer = gpu };
        byte[] pixels = renderer.RenderNativePrimitiveFrame(frame, memory.Textures, capabilities: battle.CaptureForContinuation().ReadFixtureGraphicsDefinition()!.PrimitiveCapabilities, textureWindowCache: memory.PrimitiveProcess!.TextureWindowCache);
        return gpu is null ? pixels : GpuTestDevice.Read(gpu.Target).SelectMany(color => new byte[] { color.R, color.G, color.B, color.A }).ToArray();
    }
}
