using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleCameraScratchMigrationTests
{
    [Fact]
    public void Migrate_only_named_after_callback33_camera_scratch_then_continue_one_callback()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_CAMERA_SCRATCH_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_CAMERA_SCRATCH_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "The exact after-event33 and a distinct migrated output are required.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        byte[] bytes = File.ReadAllBytes(source!);
        const string sourceHash = "E65E6AFD969C73472175DB7B20202792BB36EC6965B8439AB543F3D696FBC00A";
        Assert.Equal(sourceHash, Convert.ToHexString(SHA256.HashData(bytes)));
        BattleSnapshot original = JsonSerializer.Deserialize<BattleSnapshot>(bytes)!;
        Assert.Equal(255L, original.Frame);
        Assert.Equal(545L, original.NativeGraphics!.Callback);
        Assert.Equal((ushort)116, original.NativeGraphics.OriginalEffect!.Binding.Effect);
        Assert.Equal(0u, original.NativeGraphics.OriginalEffect.ReturnedDescriptor);
        Assert.Equal((byte)0, original.NativeEffectMemory!.Active);
        Assert.Null(original.NativeCamera!.ViewTranslationScratch);
        JsonNode originalNode = JsonNode.Parse(bytes)!;
        JsonNode migratedNode = originalNode.DeepClone();
        JsonObject camera = migratedNode["NativeCamera"]!.AsObject();
        Assert.False(camera.ContainsKey("ViewTranslationScratch"));
        camera["ViewTranslationScratch"] = JsonSerializer.SerializeToNode(new NativeBattleCameraViewTranslationScratch(0, 0, 0));
        JsonNode reverted = migratedNode.DeepClone();
        Assert.True(reverted["NativeCamera"]!.AsObject().Remove("ViewTranslationScratch"));
        Assert.True(JsonNode.DeepEquals(originalNode, reverted));
        BattleSnapshot migrated = migratedNode.Deserialize<BattleSnapshot>()!;
        BattleSession first = BattleSession.Restore(migrated);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(migrated))!);
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(first.Capture()));
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        BattleScenePresentation scene = new(assets, migrated.Definition, true);
        byte[] before = scene.Render(first, 320, 224).ToArray();
        Assert.Equal(before, scene.Render(BattleSession.Restore(original), 320, 224).ToArray());
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        first.AdvanceNativeGraphicsPresentation(callback);
        second.AdvanceNativeGraphicsPresentation(callback);
        Assert.Equal(546L, first.NativeGraphics!.Callback);
        Assert.Equal(new NativeBattleCameraViewTranslationScratch(0, 0, 0), first.NativeCamera!.ViewTranslationScratch);
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        Assert.Equal(scene.Render(first, 320, 224).ToArray(), new BattleScenePresentation(assets, migrated.Definition, true).Render(second, 320, 224).ToArray());
        Assert.Equal(0x00464B6Eu, first.NativeGraphics.Models[0].Color40);
        using (FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write))
        {
            JsonSerializer.Serialize(stream, migratedNode);
        }
        Assert.Equal(sourceHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source!))));
        Console.WriteLine($"Exact after-event33 camera scratch migration output={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output!)))}; only liveNativeCamera.ViewTranslationScratch(X/Y/Z=0) added at255/545, original preserved. Unconditional native5033E0 end-tail proves zero at this successful after-callback boundary. Actual callback546, unchanged restored pixels, wholeJSON/independent next render and caster464B6E agree.");
    }
}
