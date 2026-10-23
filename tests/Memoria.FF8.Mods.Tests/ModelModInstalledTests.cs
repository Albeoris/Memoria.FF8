using System.Collections.Immutable;
using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Mods.Tests;

public sealed class ModelModInstalledTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void InstalledEmbeddedAndExternalOverridesReachNativeGameplayResourceBoundaries(int character)
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(game), "Set MEMORIA_FF8_GAME_DIR to run the optional installed resource check.");
        using GameAssets discovery = new(game!);
        ModelCatalogEntry selected = discovery.EnumerateModelCatalog().First(entry => entry.ResourcePath.StartsWith($"battle/d{character:x}c", StringComparison.OrdinalIgnoreCase));
        ModelAssetDiscoveryItem[] clips = discovery.DiscoverModelAssets(selected, TestContext.Current.CancellationToken).Where(item => item.Kind == ModelAssetDiscoveryKind.Clip && item.OwnerId == selected.Id && item.Error is null).ToArray();
        ModelAssetDiscoveryItem[] banks = clips.GroupBy(item => item.Bank).Select(group => group.First()).ToArray();
        if (character == 0)
        {
            Assert.Contains(banks, bank => bank.Bank == "renzokuken-160");
            Assert.Contains(banks, bank => bank.Bank == "finisher-165");
        }
        else
        {
            Assert.Contains(banks, bank => bank.Bank.StartsWith("victory-", StringComparison.Ordinal));
        }
        List<ModelModReplacement> replacements = [];
        Dictionary<string, EditableModel> originals = [];
        foreach (ModelAssetDiscoveryItem bank in banks)
        {
            EditableModel model = bank.Load!();
            originals.Add(bank.Bank, model);
            EditableAnimation animation = model.Animations[bank.ClipIndex];
            EditableAnimation changed = animation with { Channels = animation.Channels.Select(channel => channel.Path == "translation" && model.Bones[channel.Bone].Parent < 0 ? channel with { Values = channel.Values.Select(value => value + new Vector4(0.25f, 0, 0, 0)).ToArray() } : channel).ToArray() };
            ModelModReplacement replacement = new(selected.Id, model, false, [new(bank.Bank, bank.ClipIndex, changed)]);
            _ = ModelMods.Preview(model, replacement);
            replacements.Add(replacement);
        }
        ModelModInstallPlan plan = ModelModInstaller.Plan(replacements);
        ModFile[] files = plan.Files.Select(file => new ModFile("sample", ModFormat.Memoria, file.RelativePath, 1, () => file.Content)).ToArray();
        Assert.True(ModelMods.TryPrepare(new(1, [], files), out ModelMods mods, out var diagnostics), string.Join("; ", diagnostics.Select(item => item.Message)));
        using BattleAssets assets = new(game!, modelOverrides: mods);
        using BattleAssets originalAssets = new(game!);
        BattleCombatantDefinition actor = new() { IsPlayer = true, ModelId = character, WeaponId = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == character).Id, Costume = 0 };
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(actor);
        NativeBattleGraphicsResource originalResource = originalAssets.ReadNativeGraphicsResource(actor);
        Assert.NotEqual(originalResource.PoseSource!.Sample(0, 1, 0).Translation, resource.PoseSource!.Sample(0, 1, 0).Translation);
        Assert.Equal(banks.Length - 1, resource.AnimationBankOverrides.Length);
        foreach (NativeBattleAnimationBankOverride bank in resource.AnimationBankOverrides)
        {
            NativeBattlePoseSource source = System.Text.Json.JsonSerializer.Deserialize<NativeBattlePoseSource>(originals[bank.Bank].NativeMetadata.GetProperty("NativePoseSource"))!;
            NativeBattleGraphicsResource resolved = (resource with { PoseSource = source }).ApplyAnimationBankOverrides(bank.Bank);
            Assert.NotEqual(source.Sample(0, 1, 0).Translation, resolved.PoseSource!.Sample(0, 1, 0).Translation);
        }
        NativeBattleModelDatSource staged = assets.ReadNativeModelDatSource((byte)character, false, 0);
        NativeBattleModelDatSource originalStaged = originalAssets.ReadNativeModelDatSource((byte)character, false, 0);
        Assert.NotEqual(originalStaged.Bytes.Length, staged.Bytes.Length);
        Assert.Equal(resource.PoseSource.Animations.ToArray(), staged.Bytes.AsSpan().Slice(staged.Section(2), staged.Section(3) - staged.Section(2)).ToArray());
    }
}
