using System.Buffers.Binary;
using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Mods.Tests;

public sealed class ModelModExternalCatalogTests
{
    [Fact]
    public void InstalledShotWeaponSlotUsesItsIndependentRigAndLeavesBodyBankUntouched()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(game), "Set MEMORIA_FF8_GAME_DIR to run the optional installed resource check.");
        using GameAssets discovery = new(game!);
        ModelCatalogEntry selected = discovery.EnumerateModelCatalog().First(entry => entry.ResourcePath.StartsWith("battle/d2c", StringComparison.OrdinalIgnoreCase));
        ModelAssetDiscoveryItem[] discovered = discovery.DiscoverModelAssets(selected, TestContext.Current.CancellationToken).ToArray();
        ModelAssetDiscoveryItem weapon = discovered.First(item => item.Kind == ModelAssetDiscoveryKind.Object && item.AlternativeGroup is not null);
        ModelAssetDiscoveryItem clip = discovered.First(item => item.Kind == ModelAssetDiscoveryKind.Clip && item.OwnerId == weapon.Id && item.Bank == "shot-188");
        EditableModel original = clip.Load!();
        EditableAnimation animation = original.Animations[clip.ClipIndex];
        EditableAnimation changed = animation with { Channels = animation.Channels.Select(channel => channel.Path == "translation" && original.Bones[channel.Bone].Parent < 0 ? channel with { Values = channel.Values.Select(value => value + new Vector4(0.25f, 0, 0, 0)).ToArray() } : channel).ToArray() };
        ModelModReplacement replacement = new(weapon.Id, original, false, [new(clip.Bank, clip.ClipIndex, changed)]);
        _ = ModelMods.Preview(original, replacement);
        ModelModInstallPlan plan = ModelModInstaller.Plan([replacement]);
        ModFile[] files = plan.Files.Select(file => new ModFile("sample", ModFormat.Memoria, file.RelativePath, 1, () => file.Content)).ToArray();
        Assert.True(ModelMods.TryPrepare(new(1, [], files), out ModelMods mods, out _));
        using BattleAssets assets = new(game!, modelOverrides: mods);
        int weaponId = assets.WeaponDefinitions.First(item => item.CharacterId == 2).Id;
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = 2, Costume = 0, WeaponId = weaponId });
        NativeBattleAnimationBankOverride bank = Assert.Single(resource.AnimationBankOverrides);
        Assert.True(bank.Linked);
        NativeBattlePoseSource source = System.Text.Json.JsonSerializer.Deserialize<NativeBattlePoseSource>(original.NativeMetadata.GetProperty("NativePoseSource"))!;
        NativeBattleGraphicsResource resolved = (resource with { LinkedPoseSource = source }).ApplyAnimationBankOverrides(null, bank.SourceDirectory);
        Assert.Same(resource.PoseSource, resolved.PoseSource);
        Assert.NotEqual(source.Sample(0, 1, 0).Translation, resolved.LinkedPoseSource!.Sample(0, 1, 0).Translation);
        Assert.Equal(source.BoneCount, resolved.LinkedPoseSource.BoneCount);
        Assert.Equal(source.Sample(1, 1, 0).Translation, resolved.LinkedPoseSource.Sample(1, 1, 0).Translation);
    }

    [Fact]
    public void InstalledExplicitActorBankDirectoriesMatchDeclaredSourceContracts()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(game), "Set MEMORIA_FF8_GAME_DIR to run the optional installed resource check.");
        using var source = GameDataSourceFactory.Open(game!);
        ResourceService resources = new(source);
        IGameNativeMemorySource memory = GameDataResources.ReadNativeMemory(resources);
        List<string> errors = [];
        foreach (ModelExternalBankSource bank in ModelExternalBankCatalog.Sources)
        {
            int actual = BinaryPrimitives.ReadInt32LittleEndian(memory.Read(bank.Descriptor.Address, 4));
            if (actual != bank.SectionCount)
            {
                errors.Add($"{bank.Descriptor.Bank}: declared {bank.SectionCount}, actual {actual}");
                continue;
            }
            byte[] bytes = ModelAssetDiscoveryBindings.ReadExternalBank(memory, bank.Descriptor.Address, bank.SectionCount);
            Assert.True(bytes.Length > 8 + bank.SectionCount * 4);
        }
        Assert.True(errors.Count == 0, string.Join("; ", errors));
    }

    [Fact]
    public void InstalledActionOwnershipKeepsDuelShotAndGuestBanksInTheirOwnModelContexts()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(game), "Set MEMORIA_FF8_GAME_DIR to run the optional installed resource check.");
        using BattleAssets assets = new(game!);
        var duel = ModelExternalBankCatalog.ForCharacter(1, assets.NativeActions);
        var shot = ModelExternalBankCatalog.ForCharacter(2, assets.NativeActions);
        Assert.Contains(duel, bank => bank.Descriptor.Bank == "duel-112");
        Assert.Contains(shot, bank => bank.Descriptor.Bank == "shot-188");
        Assert.DoesNotContain(duel, bank => bank.Descriptor.Bank.StartsWith("shot-", StringComparison.Ordinal));
        Assert.DoesNotContain(shot, bank => bank.Descriptor.Bank.StartsWith("duel-", StringComparison.Ordinal));
        Assert.Contains(ModelExternalBankCatalog.ForCharacter(8, assets.NativeActions), bank => bank.Descriptor.Bank == "actor-261");
        Assert.DoesNotContain(ModelExternalBankCatalog.ForCharacter(8, assets.NativeActions), bank => bank.Descriptor.Bank == "actor-263");
        Assert.Contains(ModelExternalBankCatalog.ForCharacter(10, assets.NativeActions), bank => bank.Descriptor.Bank == "actor-263");
        Assert.DoesNotContain(ModelExternalBankCatalog.ForCharacter(10, assets.NativeActions), bank => bank.Descriptor.Bank == "actor-261");
    }
}
