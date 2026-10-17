using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianPhase86MigrationTests
{
    [Fact]
    public void Migrate_only_named_GF11_resources_and_continue_actual86_and87_to_fresh88()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF11_RESOURCE_MIGRATION_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF11_RESOURCE_MIGRATION_OUTPUT");
        string? boundaryOutput = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF11_RESOURCE_MIGRATION_BOUNDARY");
        Assert.SkipUnless(source is not null && output is not null && boundaryOutput is not null, "Exact GF11 input, separate metadata migration and distinct new phase88 boundary outputs are required.");
        Assert.Equal(3, new[] { source!, output!, boundaryOutput! }.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.False(File.Exists(output!));
        Assert.False(File.Exists(boundaryOutput!));
        byte[] bytes = File.ReadAllBytes(source!);
        string sourceHash = Convert.ToHexString(SHA256.HashData(bytes));
        Assert.Equal("FDFAE8A4F57B47B5EC6DDDAB21A49F4EA15EEF1CE57A3E1189D5B6E71F7FAD8E", sourceHash);
        BattleSnapshot original = JsonSerializer.Deserialize<BattleSnapshot>(bytes)!;
        Assert.Equal(146L, original.Frame);
        Assert.Equal(226L, original.NativeGraphics!.Callback);
        NativeBattleOriginalEffectState effect = original.NativeGraphics.OriginalEffect!;
        Assert.Equal((ushort)116, effect.Binding.Effect);
        Assert.Equal((ushort)86, effect.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12));
        NativeBattleQuezacotlResources retained = effect.Binding.NativeQuezacotlResources!;
        Assert.Null(retained.Ribbon);
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory);
        using BattleAssets presentationAssets = new(game.GameDirectory);
        NativeBattleQuezacotlResources authenticated = game.ReadNativeQuezacotlResources();
        Assert.NotNull(authenticated.Ribbon);
        Assert.NotNull(authenticated.Ribbon.ParticleScaleWords4096);
        authenticated.Ribbon.Validate();
        Assert.Equal(JsonSerializer.Serialize(retained), JsonSerializer.Serialize(authenticated with { Ribbon = null, AnchorVertexIndices11 = retained.AnchorVertexIndices11 }));
        int index = effect.Binding.Effect - 1;
        NativeBattleEffectBinding catalog = original.Definition.NativeEffectCatalog[index];
        Assert.Equal(JsonSerializer.Serialize(retained), JsonSerializer.Serialize(catalog.NativeQuezacotlResources));
        NativeBattleQuezacotlResources resources = retained with { Ribbon = authenticated.Ribbon };
        BattleSnapshot migrated = original with
        {
            Definition = original.Definition with { NativeEffectCatalog = original.Definition.NativeEffectCatalog.SetItem(index, catalog with { NativeQuezacotlResources = resources }) },
            NativeGraphics = original.NativeGraphics with { OriginalEffect = effect with { Binding = effect.Binding with { NativeQuezacotlResources = resources } } }
        };
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(migrated with { Definition = original.Definition, NativeGraphics = original.NativeGraphics }));
        BattleSession first = BattleSession.Restore(migrated);
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(first.Capture()));
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(migrated))!);
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        for (int phase = 86; phase <= 87; phase++)
        {
            first.AdvanceNativeGraphicsPresentation(callback);
            second.AdvanceNativeGraphicsPresentation(callback);
            Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
            Assert.Equal((byte)(phase - 84), first.NativeEffectMemory!.Arena!.Read(0x9A, 1)[0]);
            if (phase == 86)
            {
                BattleSnapshot before87 = first.Capture();
                ushort random = before87.NativeGraphics!.OriginalEffect!.Quezacotl!.Random17A0;
                BattleSession paused = BattleSession.Restore(before87 with { NativeEffectMemory = before87.NativeEffectMemory! with { QuezacotlPause = 1 } });
                paused.AdvanceNativeGraphicsPresentation(callback);
                Assert.Equal((ushort)88, paused.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12));
                Assert.Null(paused.NativeGraphics.OriginalEffect.Pools.Single(pool => pool.DescriptorAddress == 0x2521728).Head);
                Assert.Equal(random, paused.NativeGraphics.OriginalEffect.Quezacotl!.Random17A0);
                Assert.Equal((byte)2, paused.NativeEffectMemory!.Arena!.Read(0x9A, 1)[0]);
            }
        }
        BattleSnapshot boundary = first.Capture();
        Assert.Equal(148L, boundary.Frame);
        Assert.Equal(228L, boundary.NativeGraphics!.Callback);
        Assert.Equal((ushort)88, boundary.NativeGraphics.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12));
        BattleScenePresentation scene = new(presentationAssets, migrated.Definition, true);
        Assert.Equal(scene.Render(first, 320, 224), scene.Render(second, 320, 224));
        BattleSession boundaryClone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(boundary))!);
        BattleSession boundaryClone2 = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(boundary))!);
        string message = NativeBattleGuardianCheckpointTests.Advance(boundaryClone);
        Assert.Contains("phase88", message);
        Assert.Equal(message, NativeBattleGuardianCheckpointTests.Advance(boundaryClone2));
        Assert.Equal(JsonSerializer.Serialize(boundaryClone.Capture()), JsonSerializer.Serialize(boundaryClone2.Capture()));
        Write(output!, migrated);
        Write(boundaryOutput!, boundary);
        Assert.Equal(sourceHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source!))));
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(output!))!).Capture()));
        Assert.Equal(JsonSerializer.Serialize(boundary), JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(boundaryOutput!))!).Capture()));
        Console.WriteLine($"Exact11 metadata-only migration source={sourceHash}; migrated={Hash(output!)} remains146/226; corrected86+87 boundary={Hash(boundaryOutput!)} at148/228 phase88, pose3. Original15 historical packets were not retrofitted.");

        static void Write(string path, BattleSnapshot snapshot)
        {
            using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write);
            JsonSerializer.Serialize(stream, snapshot);
        }

        static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    }
}
