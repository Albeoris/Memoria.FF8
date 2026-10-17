using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianRibbonMigrationTests
{
    [Fact]
    public void Migrate_only_named_GF14_ribbon_resources_and_validate_one_actual_callback()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_RIBBON_MIGRATION_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_RIBBON_MIGRATION_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "The exact standalone GF14 source and a distinct output are required.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        byte[] bytes = File.ReadAllBytes(source!);
        byte[] hash = SHA256.HashData(bytes);
        Assert.Equal("C98B20F2C30A3D6AA5898573068CD2107637E07081BFB26DA55553E9E6A85347", Convert.ToHexString(hash));
        BattleSnapshot original = JsonSerializer.Deserialize<BattleSnapshot>(bytes)!;
        Assert.Equal(147L, original.Frame);
        Assert.Equal(227L, original.NativeGraphics!.Callback);
        NativeBattleOriginalEffectState effect = original.NativeGraphics.OriginalEffect!;
        Assert.Equal((ushort)116, effect.Binding.Effect);
        Assert.Equal((ushort)87, effect.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12));
        NativeBattleQuezacotlResources retained = effect.Binding.NativeQuezacotlResources!;
        Assert.Null(retained.Ribbon);
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory);
        NativeBattleQuezacotlResources authenticated = game.ReadNativeQuezacotlResources();
        Assert.NotNull(authenticated.Ribbon);
        authenticated.Ribbon.Validate();
        Assert.Equal(JsonSerializer.Serialize(retained), JsonSerializer.Serialize(authenticated with { Ribbon = null }));
        int index = effect.Binding.Effect - 1;
        NativeBattleEffectBinding catalog = original.Definition.NativeEffectCatalog[index];
        Assert.Equal(JsonSerializer.Serialize(retained), JsonSerializer.Serialize(catalog.NativeQuezacotlResources));
        NativeBattleQuezacotlResources migratedResources = retained with { Ribbon = authenticated.Ribbon };
        BattleSnapshot migrated = original with
        {
            Definition = original.Definition with { NativeEffectCatalog = original.Definition.NativeEffectCatalog.SetItem(index, catalog with { NativeQuezacotlResources = migratedResources }) },
            NativeGraphics = original.NativeGraphics with { OriginalEffect = effect with { Binding = effect.Binding with { NativeQuezacotlResources = migratedResources } } }
        };
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(migrated with { Definition = original.Definition, NativeGraphics = original.NativeGraphics }));
        BattleSession first = BattleSession.Restore(migrated);
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(first.Capture()));
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(migrated))!);
        using BattleAssets presentationAssets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory);
        BattleScenePresentation scene = new(presentationAssets, migrated.Definition, true);
        Assert.Equal(320 * 224 * 4, scene.Render(first, 320, 224).Length);
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        first.AdvanceNativeGraphicsPresentation(callback);
        second.AdvanceNativeGraphicsPresentation(callback);
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        Assert.Equal(148L, first.Frame);
        Assert.Equal(228L, first.NativeGraphics!.Callback);
        Assert.Equal((ushort)88, first.NativeGraphics.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12));
        Assert.Equal(scene.Render(first, 320, 224), scene.Render(second, 320, 224));
        Assert.Contains(first.NativeGraphics.PrimitiveFrame!.LastCommands, command => command.Pass > 0 && command.Command.Packet.Bank is "off_B6D070" or "off_B6D094");
        using (FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write))
        {
            JsonSerializer.Serialize(stream, migrated);
        }
        Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(source!)));
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(output!))!).Capture()));
        Console.WriteLine($"GF14 ribbon metadata: source={Convert.ToHexString(hash)}, output={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output!)))}; saved147/227 remains unchanged; two independent callbacks reach148/228 phase88 with equal rendering.");
    }
}
