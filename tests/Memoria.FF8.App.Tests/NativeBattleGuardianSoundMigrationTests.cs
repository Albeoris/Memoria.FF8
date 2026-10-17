using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianSoundMigrationTests
{
    [Fact]
    public void Add_authenticated_sound_and_flash_metadata_to_a_separate_named_GF_snapshot()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_SOUND_MIGRATION_SOURCE");
        string? target = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_SOUND_MIGRATION_OUTPUT");
        Assert.SkipUnless(source is not null && target is not null, "Explicit source and distinct output paths are required for the GF sound metadata migration.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(target!), StringComparison.OrdinalIgnoreCase));
        byte[] bytes = File.ReadAllBytes(source!);
        byte[] hash = SHA256.HashData(bytes);
        BattleSnapshot original = JsonSerializer.Deserialize<BattleSnapshot>(bytes)!;
        NativeBattleOriginalEffectState effect = original.NativeGraphics!.OriginalEffect!;
        Assert.Equal((ushort)116, effect.Binding.Effect);
        NativeBattleQuezacotlResources retained = effect.Binding.NativeQuezacotlResources!;
        Assert.NotNull(retained);
        Assert.True(retained.Frame65Sound is null || retained.FlashColors is null, "At least one authenticated resource owner must be missing for this explicit migration.");
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory);
        NativeBattleQuezacotlResources authenticated = game.ReadNativeQuezacotlResources();
        Assert.Equal((uint?)240069, authenticated.Frame65Sound);
        if (retained.Frame65Sound is uint sound)
        {
            Assert.Equal(authenticated.Frame65Sound, sound);
        }
        if (retained.FlashColors is { } colors)
        {
            Assert.Equal(authenticated.FlashColors!.Value, colors);
        }
        Assert.Equal(JsonSerializer.Serialize(retained), JsonSerializer.Serialize(authenticated with { Frame65Sound = retained.Frame65Sound, FlashColors = retained.FlashColors, Ribbon = retained.Ribbon, AnchorVertexIndices11 = retained.AnchorVertexIndices11, Frame209Sound = retained.Frame209Sound, Frame229FlashColor = retained.Frame229FlashColor, Paths = retained.Paths }));
        int index = effect.Binding.Effect - 1;
        Assert.Equal(JsonSerializer.Serialize(retained), JsonSerializer.Serialize(original.Definition.NativeEffectCatalog[index].NativeQuezacotlResources));
        NativeBattleQuezacotlResources migratedResources = retained with { Frame65Sound = authenticated.Frame65Sound, FlashColors = authenticated.FlashColors };
        BattleSnapshot migrated = original with
        {
            Definition = original.Definition with { NativeEffectCatalog = original.Definition.NativeEffectCatalog.SetItem(index, original.Definition.NativeEffectCatalog[index] with { NativeQuezacotlResources = migratedResources }) },
            NativeGraphics = original.NativeGraphics with { OriginalEffect = effect with { Binding = effect.Binding with { NativeQuezacotlResources = migratedResources } } }
        };
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
        Console.WriteLine($"Authenticated sound/flash metadata migration preserves frame{original.Frame}/callback{original.NativeGraphics.Callback}; one-callback result={boundary}");
    }
}
