using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class Ff7BattleSilkTests
{
    [Fact]
    public void OriginalSilkSpritesAndNativeLinesRasterizeWithColdPacketOrder()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(root), "Select the original FFVII installation.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive battle = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        Ff7BattleRenderer renderer = new(battle, formation.LocationId, magic);
        SceneCameraPlacement camera = new(new(0, -1500, 4500), new(0, -500, 0));
        renderer.Render(gpu, 320, 240, new(camera, []));
        var baseline = GpuTestDevice.Read(gpu.Target);
        int random = 0;
        Ff7BattlePrimaryContext context = Context(() => ++random);
        Ff7BattlePrimaryState state = Ff7BattlePrimaryEffects.ScheduleSilk(new(), 1, 4, 0);
        for (int frame = 0; frame < 50; frame++)
        {
            state = Ff7BattlePrimaryEffects.Advance(state, context);
            if (frame is not (0 or 9 or 18 or 25 or 36 or 39))
            {
                continue;
            }
            renderer.Render(gpu, 320, 240, new(camera, []) { PrimaryEffects = state });
            var original = GpuTestDevice.Read(gpu.Target);
            Assert.True(original.Zip(baseline).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 8, $"Silk has no pixels at callback {frame}.");
            Ff7BattlePrimaryState cold = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(JsonSerializer.Serialize(state))!;
            renderer.Render(gpu, 320, 240, new(camera, []) { PrimaryEffects = cold });
            Assert.Equal(original, GpuTestDevice.Read(gpu.Target));
            if (frame == 18)
            {
                foreach (bool linesOnly in new[] { false, true })
                {
                    Ff7BattlePrimaryState channel = state with { Draws = state.Draws.Where(draw => (draw is Ff7BattlePrimarySilkLineDraw) == linesOnly).ToArray() };
                    renderer.Render(gpu, 320, 240, new(camera, []) { PrimaryEffects = channel });
                    Assert.True(GpuTestDevice.Read(gpu.Target).Zip(baseline).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 8);
                }
            }
        }
    }

    [Fact]
    public void OriginalSilkCallbacksRetainSharedLinesNativePacketsAndColdContinuation()
    {
        int random = 0;
        int coldRandom = 0;
        Ff7BattlePrimaryContext context = Context(() => ++random);
        Ff7BattlePrimaryContext coldContext = Context(() => ++coldRandom);
        Ff7BattlePrimaryState state = Ff7BattlePrimaryEffects.ScheduleSilk(new(), 1, 4, 0);
        Ff7BattlePrimaryState? cold = null;
        using MemoryStream packets = new();
        using BinaryWriter writer = new(packets);
        int lineCount = 0;
        int spriteCount = 0;
        for (int frame = 0; frame < 50; frame++)
        {
            state = Ff7BattlePrimaryEffects.Advance(state, context);
            if (cold is not null)
            {
                cold = Ff7BattlePrimaryEffects.Advance(cold, coldContext);
                Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(cold));
                Assert.Equal(random, coldRandom);
            }
            foreach (Ff7BattlePrimaryDraw draw in state.Draws)
            {
                writer.Write((short)frame);
                writer.Write((short)draw.Slot);
                switch (draw)
                {
                    case Ff7BattlePrimarySilkLineDraw line:
                        writer.Write((byte)0);
                        Point(line.Start);
                        Point(line.End);
                        writer.Write(line.Brightness);
                        lineCount++;
                        break;
                    case Ff7BattlePrimarySilkSpriteDraw sprite:
                        writer.Write((byte)1);
                        writer.Write((byte)sprite.Kind);
                        writer.Write((short)(sprite.Kind == 3 ? sprite.Frame % 2 : sprite.Frame));
                        Point(sprite.Position);
                        writer.Write((short)(sprite.Kind == 3 ? 12288 : 8192));
                        spriteCount++;
                        break;
                    default:
                        throw new InvalidOperationException("Unexpected native Silk packet.");
                }
            }
            Assert.Equal(frame == 36 ? 1 : 0, state.ImpactTargets);
            Assert.Equal(frame == 36 ? 1 : 0, state.Hitmarks.Count);
            if (frame == 36)
            {
                Assert.Equal(new Ff7BattlePrimaryHitmark(0, 0x33), state.Hitmarks[0]);
            }
            if (frame == 0)
            {
                Assert.Equal(20, random);
                Assert.Equal((short)-1, state.SilkLines.Direction);
                Assert.Equal(new Ff7BattleBraverPoint(721, -500, 1443), state.Draws.OfType<Ff7BattlePrimarySilkSpriteDraw>().Single().Position);
            }
            if (frame % 7 == 0)
            {
                cold = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(JsonSerializer.Serialize(state))!;
                coldRandom = random;
            }
        }
        Assert.False(state.HasCallbacks);
        Assert.Equal(76, random);
        Assert.Equal(6900, lineCount);
        Assert.Equal(108, spriteCount);
        Assert.Equal("B3F91AD91951943E67C59211CC0DCD541ADEF10AAA557B4363FCFE908437D879", Convert.ToHexString(SHA256.HashData(packets.ToArray())));

        void Point(Ff7BattleBraverPoint value)
        {
            writer.Write(value.X);
            writer.Write(value.Y);
            writer.Write(value.Z);
        }
    }

    private static Ff7BattlePrimaryContext Context(Func<int> random)
    {
        return new((actor, bone, offset) => Joint(actor, bone) + offset, _ => default, _ => 0, random, JointPoint: Joint, JointSelector: (_, selector) => selector);

        static Vector3 Joint(int actor, int selector) => actor == 4 ? new Vector3(900, -500, 1800) : new Vector3(0, selector == 10 ? -900 : -500, 0);
    }
}
