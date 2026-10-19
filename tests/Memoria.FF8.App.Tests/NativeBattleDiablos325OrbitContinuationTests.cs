using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDiablos325OrbitContinuationTests
{
    [Fact]
    public void Named_orbit_transition_preserves_native_camera_puffs_and_random_clock_until_second_stream()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_DIABLOS325_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_DIABLOS325_EVIDENCE");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(input is not null && File.Exists(input) && !string.IsNullOrWhiteSpace(evidence) && directory is not null && Directory.Exists(directory), "Select installed resources, a before-orbit Diablos standalone and a distinct evidence directory.");
        string original = File.ReadAllText(input!);
        BattleSnapshot source = JsonSerializer.Deserialize<BattleSnapshot>(original)!;
        Assert.Equal((ushort)62, Root(source).ReadWord(0xC));
        Assert.Equal((byte)1, Root(source).ReadByte(0xF));
        string output = Path.Combine(evidence!, "diablos-orbit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine($"Diablos orbit continuation evidence: {output}");
        BattleSession first = BattleSession.Restore(source);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(original)!);
        using BattleAssets assets = new(directory!);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        NativeBattleRestorationPresentationTests.Publish(output, "source", source, null);
        List<object> rows = [];
        BattleSnapshot before = source;
        try
        {
            for (ushort counter = 60; counter <= 86; counter++)
            {
                before = first.Capture();
                first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                BattleSnapshot current = first.Capture();
                Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(second.Capture()));
                byte[] pixels = scene.Render(first, 320, 224).ToArray();
                Assert.Equal(pixels, scene.Render(second, 320, 224).ToArray());
                NativeBattleModulePoolTask task = current.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x25051A0).Slots[0];
                NativeBattleRestorationPresentationTests.Publish(output, counter == 86 ? "before-second-stream" : "timeline-" + counter, current, pixels);
                Assert.Equal((ushort)(counter + 1), task.ReadWord(0xC));
                Assert.Equal((uint)(counter - 8), task.ReadDword(0x268));
                Assert.Equal(4096u, current.NativeEffectMemory!.Diablos325!.Flash51E8);
                Assert.Equal(4096u, current.NativeEffectMemory.Diablos325.PreviousFlash51EC);
                Assert.All(current.NativeStage!.Groups, group =>
                {
                    Assert.Equal((short)4096, group.ColorDepth);
                    Assert.Equal(0, group.Flags & 2);
                });
                Assert.Equal(source.NativeCrtRandom, current.NativeCrtRandom);
                if (counter == 60)
                {
                    Assert.Equal(new NativeBattleCameraPose(new(-393, -10852, -390), new(2241, -1625, -4222), 288, 0), current.NativeCamera!.CurrentPose);
                }
                if (counter == 86)
                {
                    Assert.Equal(new NativeBattleCameraPose(new(1514, -10902, -784), new(-946, -2101, -5213), 288, 0), current.NativeCamera!.CurrentPose);
                }
                rows.Add(new { current.Frame, NativeCallback = current.NativeGraphics.Callback, Timeline = counter + 1, JsonSha256 = Hash(JsonSerializer.Serialize(current)), PixelsSha256 = Convert.ToHexString(SHA256.HashData(pixels)), CommonCursor = current.NativeEffectMemory.PrimitiveProcess!.ByteCursor });
            }
            BattleSnapshot boundary = first.Capture();
            File.WriteAllText(Path.Combine(output, "callback-proofs.json"), JsonSerializer.Serialize(rows));
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "orbit-camera-validated", boundary.Frame, NativeCallback = boundary.NativeGraphics!.Callback, Boundary = "before-second-stream" }));
        }
        catch
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(output, "failed-live", first.Capture(), null);
            throw;
        }
        Assert.Equal(original, File.ReadAllText(input!));
    }

    private static NativeBattleModulePoolTask Root(BattleSnapshot snapshot) => snapshot.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x25051B0).Slots[0];
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
}
