using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDoomtrainMatrixMigrationTests
{
    [Fact]
    public void Migrate_only_named_Doomtrain_Euler_padding_knownness_and_continue_the_actual_flash_boundary()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_DOOMTRAIN_MATRIX_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_DOOMTRAIN_MATRIX_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "Select a standalone Doomtrain boundary and a distinct migrated output.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        byte[] bytes = File.ReadAllBytes(source!);
        string hash = Convert.ToHexString(SHA256.HashData(bytes));
        JsonNode original = JsonNode.Parse(bytes)!;
        JsonNode migrated = original.DeepClone();
        JsonArray known = migrated["NativeEffectMemory"]!["Doomtrain191"]!["BaseMatrix"]!["Known"]!.AsArray();
        Assert.True(known[18]!.GetValue<bool>());
        Assert.True(known[19]!.GetValue<bool>());
        known[18] = false;
        known[19] = false;
        JsonNode reverted = migrated.DeepClone();
        reverted["NativeEffectMemory"]!["Doomtrain191"]!["BaseMatrix"]!["Known"]![18] = true;
        reverted["NativeEffectMemory"]!["Doomtrain191"]!["BaseMatrix"]!["Known"]![19] = true;
        Assert.True(JsonNode.DeepEquals(original, reverted));
        BattleSnapshot snapshot = migrated.Deserialize<BattleSnapshot>()!;
        Assert.Equal((ushort)191, snapshot.NativeGraphics!.OriginalEffect!.RequestedEffect);
        BattleSession first = BattleSession.Restore(snapshot);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(first.Capture()));
        string directory = Path.GetDirectoryName(Path.GetFullPath(output!))!;
        Directory.CreateDirectory(directory);
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        try
        {
            TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
            first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            Assert.Equal(snapshot.Frame + 1, first.Frame);
            Assert.Equal(snapshot.NativeGraphics!.Callback + 1, first.NativeGraphics!.Callback);
            Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
            byte[] pixels = new BattleScenePresentation(assets, snapshot.Definition, true).Render(first, 320, 224).ToArray();
            Assert.Equal(pixels, new BattleScenePresentation(assets, snapshot.Definition, true).Render(second, 320, 224).ToArray());
            Assert.False(first.NativeEffectMemory!.Doomtrain191!.BaseMatrix.Known[18]);
            Assert.False(first.NativeEffectMemory!.Doomtrain191!.BaseMatrix.Known[19]);
            Assert.Equal(snapshot.NativeEffectMemory!.Doomtrain191!.BaseMatrix.Bytes, first.NativeEffectMemory.Doomtrain191.BaseMatrix.Bytes);
            using (FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write))
            {
                JsonSerializer.Serialize(stream, migrated);
            }
            NativeBattleRestorationPresentationTests.Publish(directory, Path.GetFileNameWithoutExtension(output!) + ".after-matrix", first.Capture(), pixels);
            Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source!))));
            File.WriteAllText(output! + ".migration.json", JsonSerializer.Serialize(new { SourceHash = hash, SourceFrame = snapshot.Frame, ResultFrame = first.Frame, ChangedKnownBits = new[] { 18, 19 }, MatrixBytesUnchanged = true, IndependentJsonAndRgba = true }));
        }
        catch
        {
            NativeBattleRestorationPresentationTests.Publish(directory, Path.GetFileNameWithoutExtension(output!) + ".before-boundary", snapshot, null);
            NativeBattleRestorationPresentationTests.Publish(directory, Path.GetFileNameWithoutExtension(output!) + ".failed-live", first.Capture(), null);
            throw;
        }
    }
}
