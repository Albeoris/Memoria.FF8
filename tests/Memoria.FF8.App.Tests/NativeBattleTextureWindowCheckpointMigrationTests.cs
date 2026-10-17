using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleTextureWindowCheckpointMigrationTests
{
    [Fact]
    public void Explicitly_add_the_proved_zero_window_to_a_separate_pre_E2_diagnostic_save()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEXTURE_WINDOW_MIGRATION_SOURCE");
        string? target = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEXTURE_WINDOW_MIGRATION_OUTPUT");
        Assert.SkipUnless(source is not null && target is not null, "An exact published pre-E2 diagnostic source and distinct migration output are required.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(target!), StringComparison.OrdinalIgnoreCase));
        byte[] bytes = File.ReadAllBytes(source!);
        string hash = Convert.ToHexString(SHA256.HashData(bytes));
        Assert.Contains(hash, new[]
        {
            //These exact cold diagnostic routes reach only the ordinary module/Cure/Quezacotl first window, before any native E2 call.
            "036E4EFD025AB94D56734BD404D43A3A4EB143E7278BAE2BF127568C4EB0A28E",
            "68746A5E6E4A0EE23F55F455997248D464A8FD070661D4B3A31AA83E6146B104",
            "A43CD00990A3770F0D43A3D2C0D7B2B7805BB529CABE7A744415DF03EE8EAF01"
        });
        BattleSnapshot original = JsonSerializer.Deserialize<BattleSnapshot>(bytes)!;
        NativeBattleEffectMemory memory = original.NativeEffectMemory!;
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess!;
        Assert.Null(process.TextureWindowBits);
        NativeBattleGraphicsState graphics = original.NativeGraphics!;
        NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame!;
        Assert.DoesNotContain(frame.LastCommands, command => command.Command.Opcode == 0xE2);
        Assert.All(frame.LastCommands, command => Assert.Null(command.DrawMode.TextureWindowBits));
        Assert.Null(process.TextureWindowCache);
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory);
        Assert.Equal((uint?)0, game.ReadColdProcessNativeBattleMemory().EffectMemory!.PrimitiveProcess!.TextureWindowBits);
        //4647A0 clears renderer resource rows, preserving unknown prior UV scales. Rebuild only this saved OT, with no simulation/travel replay.
        NativeBattlePcTextureWindowCache reset = NativeBattlePcTextureWindowCache.Reset4647A0(process.TextureWindowCache);
        Assert.Null(reset.ScaleU);
        Assert.Null(reset.ScaleV);
        var selected = NativeBattleLogicalPasses.SelectTextures(frame.LastCommands.Select(command => command with { DrawMode = command.DrawMode with { TextureWindowBits = 0 } }).ToImmutableArray(), reset);
        BattleSnapshot migrated = original with
        {
            NativeEffectMemory = memory with { PrimitiveProcess = process with { TextureWindowBits = 0, TextureWindowCache = selected.Cache } },
            NativeGraphics = graphics with { PrimitiveFrame = frame with { LastCommands = selected.Commands } }
        };
        BattleSession first = BattleSession.Restore(migrated);
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(first.Capture()));
        using BattleAssets assets = new(game.GameDirectory);
        Assert.Equal(320 * 224 * 4, new BattleScenePresentation(assets, migrated.Definition, true).Render(first, 320, 224).Length);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(migrated))!);
        string boundary = NativeBattleGuardianCheckpointTests.Advance(first);
        if (graphics.OriginalEffect?.Binding.Effect != 116)
        {
            Assert.Equal("continued", boundary);
        }
        Assert.Equal(boundary, NativeBattleGuardianCheckpointTests.Advance(second));
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        using (FileStream output = new(target!, FileMode.CreateNew, FileAccess.Write))
        {
            JsonSerializer.Serialize(output, migrated);
        }
        Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source!))));
        Console.WriteLine($"Explicit window migration preserves frame{original.Frame}/callback{graphics.Callback}; one-callback result={boundary}");
    }
}
