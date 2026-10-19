using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDiablos325SecondStreamContinuationTests
{
    [Fact]
    public void Named_completed_file_poll_replaces_stream_and_acknowledges_timeline_without_replaying_audio()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_DIABLOS325_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_DIABLOS325_EVIDENCE");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(input is not null && File.Exists(input) && !string.IsNullOrWhiteSpace(evidence) && directory is not null && Directory.Exists(directory), "Select installed resources, a before-second-stream Diablos standalone and a distinct evidence directory.");
        string original = File.ReadAllText(input!);
        BattleSnapshot source = JsonSerializer.Deserialize<BattleSnapshot>(original)!;
        Assert.Equal((ushort)89, Root(source).ReadWord(0xC));
        Assert.Equal((byte)1, Root(source).ReadByte(0xF));
        string output = Path.Combine(evidence!, "diablos-second-stream-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine($"Diablos second stream continuation evidence: {output}");
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
            for (ushort counter = 87; counter <= 89; counter++)
            {
                before = first.Capture();
                first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                BattleSnapshot current = first.Capture();
                Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(second.Capture()));
                byte[] pixels = scene.Render(first, 320, 224).ToArray();
                Assert.Equal(pixels, scene.Render(second, 320, 224).ToArray());
                NativeBattleModulePoolTask task = current.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x25051A0).Slots[0];
                NativeBattleRestorationPresentationTests.Publish(output, counter == 89 ? "before-second-camera-reset" : "timeline-" + counter, current, pixels);
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
                Assert.Equal(new NativeBattleSummonStreamState(690162, 0x20DFAB8u + 0x1685C), current.NativeEffectMemory.SummonStream);
                Assert.Equal((byte)1, task.ReadByte(0xF));
                Assert.DoesNotContain(current.Events.Skip(before.Events.Length), item => item.NativeModuleSound is not null);
                Assert.Equal(new byte[] { 0, 0, 1, 0, 0 }, current.NativeEffectMemory.Arena!.Read(0x4C14, 5));
                if (counter == 89)
                {
                    Assert.Equal(new NativeBattleCameraPose(new(1734, -10908, -829), new(-1314, -2156, -5327), 288, 0), current.NativeCamera!.CurrentPose);
                }
                rows.Add(new { current.Frame, NativeCallback = current.NativeGraphics.Callback, Timeline = counter + 1, JsonSha256 = Hash(JsonSerializer.Serialize(current)), PixelsSha256 = Convert.ToHexString(SHA256.HashData(pixels)), CommonCursor = current.NativeEffectMemory.PrimitiveProcess!.ByteCursor });
            }
            BattleSnapshot boundary = first.Capture();
            File.WriteAllText(Path.Combine(output, "callback-proofs.json"), JsonSerializer.Serialize(rows));
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "second-stream-validated", boundary.Frame, NativeCallback = boundary.NativeGraphics!.Callback, Boundary = "before-second-camera-reset" }));
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
