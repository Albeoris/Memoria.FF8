using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianProfileMigrationTests
{
    [Fact]
    public void Explicitly_declare_the_existing_PC_primitive_profile_in_a_separate_named_GF_snapshot()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_PROFILE_MIGRATION_SOURCE");
        string? target = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_PROFILE_MIGRATION_OUTPUT");
        Assert.SkipUnless(source is not null && target is not null, "Explicit source and distinct output paths are required for the selected PC profile migration.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(target!), StringComparison.OrdinalIgnoreCase));
        byte[] bytes = File.ReadAllBytes(source!);
        byte[] hash = SHA256.HashData(bytes);
        BattleSnapshot original = JsonSerializer.Deserialize<BattleSnapshot>(bytes)!;
        NativeBattleGraphicsDefinition graphics = original.Definition.NativeGraphics!;
        Assert.NotNull(graphics);
        Assert.Null(graphics.PrimitiveCapabilities);
        Assert.NotNull(graphics.Rules.ShadowSource);
        Assert.Equal((ushort)116, original.NativeGraphics!.OriginalEffect!.Binding.Effect);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(BattleSession.Restore(original).Capture()));
        //The earlier renderer already used the PC alpha-palette RGBA path. Declare its supported Gouraud RGB/alpha capability;
        //do not infer a historical native device, hidden bounds or the rolling WORD146 minimum from this snapshot.
        BattleSnapshot migrated = original with { Definition = original.Definition with { NativeGraphics = graphics with { PrimitiveCapabilities = NativeBattlePrimitiveCapabilities.PrimaryRgba8() } } };
        BattleSession first = BattleSession.Restore(migrated);
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(first.Capture()));
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(migrated))!);
        string boundary = NativeBattleGuardianCheckpointTests.Advance(first);
        Assert.Equal(boundary, NativeBattleGuardianCheckpointTests.Advance(second));
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        using (FileStream output = new(target!, FileMode.CreateNew, FileAccess.Write))
        {
            JsonSerializer.Serialize(output, migrated);
        }
        Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(source!)));
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(target!))!).Capture()));
        Console.WriteLine($"Profile migration preserves frame{original.Frame}/callback{original.NativeGraphics.Callback}; one-callback result={boundary}");
    }
}
