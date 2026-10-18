using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleTargetPackageResourcesTests
{
    [Theory]
    [InlineData("Scan", 2, 50, 1, 50, 40, 0x84D030u, 0x84D010u)]
    [InlineData("Cure", 2, 21, 1, 21, 1, 0x8D6A00u, 0x8D69E0u)]
    [InlineData("Shiva", 254, 65, 2, 1, 185, 0x5C0D50u, 0x5C0D30u)]
    [InlineData("Ifrit", 254, 66, 2, 2, 201, 0xB25780u, 0xB25750u)]
    public void Installed_kernel_rows_and_executable_initializers_identify_the_target_resources(string name, byte command, ushort actionId, int section, int index, ushort effectId, uint entry, uint initializer)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets battle = new(game.GameDirectory);
        BattleNativeAction action = battle.NativeActions.Single(action => action.Command == command && action.ActionId == actionId);
        Assert.Equal(section, action.KernelSection);
        Assert.Equal(index, action.KernelIndex);
        Assert.Equal(effectId, action.MagicId);
        BattleNativeKernelRecord row = battle.NativeKernelRecords.Single(row => row.Section == section && row.Index == index);
        Assert.Equal(row.Bytes.ToArray(), action.RawRecord.ToArray());
        Assert.Equal(effectId, BinaryPrimitives.ReadUInt16LittleEndian(row.Bytes.AsSpan().Slice(4, 2)));
        BattleActionDefinition definition = battle.ActionDefinitions.Single(action => action.Ability == (command == 2 ? BattleAbility.Magic : BattleAbility.GuardianForce) && action.Id == index);
        Assert.Equal(action.Name, definition.Name);
        if (command == 2)
        {
            Assert.Equal(name, definition.Name);
        }
        Assert.Equal(action, definition.NativeAction);

        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalog()[effectId - 1];
        Assert.Equal(effectId, binding.Effect);
        Assert.Equal(entry, binding.Entry);
        Assert.Equal(initializer, binding.Initializer);
        Assert.Equal(false, binding.InitializerIsRet);
        Assert.Equal(ExpectedResources(effectId), NativeBattleEffectInitializerPolicy.Resources(binding).ToArray());
    }

    [Theory]
    [InlineData((ushort)40)]
    [InlineData((ushort)1)]
    [InlineData((ushort)185)]
    [InlineData((ushort)201)]
    public void Generic_initializer_retains_original_archive_bytes_and_ordered_arena_publications_across_serialization(ushort effectId)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using IFlArchive archive = FlArchive.OpenForRead(Path.Combine(game.GameDirectory, "Data", "lang-en", "magic.fs"), FlArchiveRepresentation.Files);
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(effectId, game.ReadNativeEffectCatalogForEffects([effectId]));
        NativeBattleEffectInitializerResource[] resources = ExpectedResources(effectId);
        Assert.Equal(resources, NativeBattleEffectInitializerPolicy.Resources(selected.Binding).ToArray());
        NativeBattleEffectInitializerAsset[] assets = NativeBattleEffectInitializerPolicy.Assets(selected.Binding).ToArray();
        Assert.Equal(resources.Length, assets.Length);
        byte[][] originals = resources.Select(resource => ReadArchiveResource(archive, resource.Name)).ToArray();
        for (int resourceIndex = 0; resourceIndex < resources.Length; resourceIndex++)
        {
            Assert.Equal(resources[resourceIndex].Name, assets[resourceIndex].Name);
            Assert.NotEmpty(originals[resourceIndex]);
            Assert.Equal(originals[resourceIndex], assets[resourceIndex].Data.ToArray());
        }

        var initialized = NativeBattleOriginalEffects.Initialize(game.ReadColdProcessNativeBattleMemory().EffectMemory!, selected);
        Assert.Equal(effectId - 1, initialized.Effect.SelectedIndex);
        Assert.Null(initialized.Effect.ReturnedDescriptor);
        Assert.Empty(initialized.Effect.Pools);
        Assert.Equal(resources.Length, initialized.Effect.ResourcePointers.Length);
        NativeBattleEffectMemory restoredMemory = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(initialized.Memory))!;
        NativeBattleOriginalEffectState restoredEffect = JsonSerializer.Deserialize<NativeBattleOriginalEffectState>(JsonSerializer.Serialize(initialized.Effect))!;
        restoredMemory.Validate();
        restoredEffect.Validate();
        Assert.Equal(resources, NativeBattleEffectInitializerPolicy.Resources(restoredEffect.Binding).ToArray());
        Assert.Equal(initialized.Effect.ResourcePointers.ToArray(), restoredEffect.ResourcePointers.ToArray());
        int offset = 0;
        for (int resourceIndex = 0; resourceIndex < resources.Length; resourceIndex++)
        {
            Assert.Equal(new NativeBattleEffectResourcePointer(resources[resourceIndex].GlobalAddress, offset), initialized.Effect.ResourcePointers[resourceIndex]);
            Assert.Equal(originals[resourceIndex], initialized.Memory.Arena!.Read(offset, originals[resourceIndex].Length));
            Assert.Equal(originals[resourceIndex], restoredMemory.Arena!.Read(offset, originals[resourceIndex].Length));
            Assert.Equal(originals[resourceIndex], NativeBattleEffectInitializerPolicy.Assets(restoredEffect.Binding)[resourceIndex].Data.ToArray());
            offset += originals[resourceIndex].Length;
        }
        Assert.Equal(offset, initialized.Memory.Cursor);
        Assert.Equal(offset, restoredMemory.Cursor);
    }

    private static NativeBattleEffectInitializerResource[] ExpectedResources(ushort effectId)
    {
        return effectId switch
        {
            40 => [new("mag039-067.tim", 0x269A160)],
            1 => [new("mag000-049.tim", 0x277AEC0)],
            185 => [new("mag184.tim", 0x22BC150)],
            201 => [new("mag200_b.00", 0x2796E4C), new("mag200_b.01", 0x2796E48)],
            _ => throw new ArgumentOutOfRangeException(nameof(effectId))
        };
    }

    private static byte[] ReadArchiveResource(IFlArchive archive, string name)
    {
        using Stream stream = archive.Entries.Single(entry => entry.RelativePath.Replace('\\', '/').Split('/')[^1].Equals(name, StringComparison.OrdinalIgnoreCase)).OpenForRead();
        using MemoryStream content = new();
        stream.CopyTo(content);
        return content.ToArray();
    }
}
