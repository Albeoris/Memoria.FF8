using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianPaddingMigrationTests
{
    [Fact]
    public void Migrate_only_the_named_GF11_camera_padding_and_validate_one_actual_callback()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_PADDING_MIGRATION_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_PADDING_MIGRATION_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "Exact standalone source and distinct output are required for this camera padding migration.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        byte[] bytes = File.ReadAllBytes(source!);
        byte[] hash = SHA256.HashData(bytes);
        Assert.Equal("F7B53D35083C6062874694E039DF856F498CDAAD7E05C6115BF70DA7DEEBEEE5", Convert.ToHexString(hash));
        BattleSnapshot original = JsonSerializer.Deserialize<BattleSnapshot>(bytes)!;
        Assert.Equal(146L, original.Frame);
        Assert.Equal(226L, original.NativeGraphics!.Callback);
        Assert.Equal((ushort)116, original.NativeGraphics.OriginalEffect!.Binding.Effect);
        Assert.Equal((ushort)86, original.NativeGraphics.OriginalEffect.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12));
        Assert.Equal((ushort)0, original.NativeCamera!.Mask);
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory);
        NativeBattleCameraState cold = game.ReadColdProcessNativeBattleMemory().Camera!;
        Assert.Equal((short?)0, cold.EyePaddingF6);
        Assert.Equal((short?)0, cold.TargetPaddingFE);
        Assert.Equal((short?)0, cold.CapturedEyePadding806);
        Assert.Equal((short?)0, cold.CapturedTargetPadding80E);
        Assert.All(cold.TrackEyePadding, value => Assert.Equal((short?)0, value));
        Assert.All(cold.TrackTargetPadding, value => Assert.Equal((short?)0, value));
        NativeBattleRetainedMemory retained = original.Definition.NativeRetainedMemory!;
        BattleSnapshot migrated = original with
        {
            NativeCamera = Fill(original.NativeCamera),
            Definition = original.Definition with { NativeRetainedMemory = retained with { Camera = Fill(retained.Camera!) } }
        };
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(migrated with { NativeCamera = original.NativeCamera, Definition = original.Definition }));
        BattleSession first = BattleSession.Restore(migrated);
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(first.Capture()));
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(migrated))!);
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        first.AdvanceNativeGraphicsPresentation(callback);
        second.AdvanceNativeGraphicsPresentation(callback);
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        Assert.Equal((ushort)87, first.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12));
        Assert.Equal(227L, first.NativeGraphics.Callback);
        using (FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write))
        {
            JsonSerializer.Serialize(stream, migrated);
        }
        Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(source!)));
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(output!))!).Capture()));
        Console.WriteLine($"GF11 padding migration preserves frame146/callback226; actual callback reaches frame{first.Frame}/callback{first.NativeGraphics.Callback}, phase87. Source{Convert.ToHexString(hash)}; output{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output!)))}.");

        NativeBattleCameraState Fill(NativeBattleCameraState camera)
        {
            Assert.Null(camera.EyePaddingF6);
            Assert.Null(camera.TargetPaddingFE);
            Assert.Null(camera.CapturedEyePadding806);
            Assert.Null(camera.CapturedTargetPadding80E);
            Assert.All(camera.TrackEyePadding, value => Assert.Null(value));
            Assert.All(camera.TrackTargetPadding, value => Assert.Null(value));
            return camera with { EyePaddingF6 = cold.EyePaddingF6, TargetPaddingFE = cold.TargetPaddingFE, CapturedEyePadding806 = cold.CapturedEyePadding806, CapturedTargetPadding80E = cold.CapturedTargetPadding80E, TrackEyePadding = cold.TrackEyePadding, TrackTargetPadding = cold.TrackTargetPadding };
        }
    }
}
