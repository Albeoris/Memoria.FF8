using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleFiragaResourcesTests
{
    [Fact]
    public void Original_Firaga_resources_materialize_without_neighboring_ASCII_or_embedded_arrays()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleActionDefinition action = assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == 3);
        Assert.Equal((ushort)143, action.NativeAction!.MagicId);
        var catalog = game.ReadNativeEffectCatalogForEffects([143]);
        NativeBattleEffectBinding binding = catalog[142];
        Assert.Equal("mag142.tim", binding.InitializerAsset!.Name);
        NativeBattleFiragaResources resources = Assert.IsType<NativeBattleFiragaResources>(binding.FiragaResources);
        Assert.Equal(0xB48, resources.Animation.Data.Length);
        Assert.Equal(300056u, resources.Sound);
        Assert.Equal(16, BinaryPrimitives.ReadUInt16LittleEndian(resources.Sprite16.Bytes.AsSpan().Slice(8)));
        Assert.Equal(0xDDA594u, resources.Sprite16.Address);
        Assert.Equal(new uint[] { 0xDDA740, 0xDDC578, 0xDDCE48, 0xDDD208 }, new[] { resources.FlareMesh.Address, resources.PortalMesh.Address, resources.OrbitMesh.Address, resources.RingMesh.Address });
        Assert.Same(resources, game.ReadNativeFiragaResources());
        Assert.Null(game.ReadNativeEffectCatalog()[142].FiragaResources);
        Assert.Throws<NotSupportedException>(() => NativeBattleFiragaResourcesReader.Read(new byte[128]));
        NativeBattleOriginalEffectState effect = NativeBattleOriginalEffects.Select(143, catalog);
        var initialized = NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), effect);
        Assert.True(binding.InitializerAsset.Data.AsSpan().SequenceEqual(initialized.Memory.Arena!.Read(0, binding.InitializerAsset.Data.Length)));
        NativeBattleEffectTextureState textures = initialized.Memory.Textures!.Register5099A0(resources.Animation).EnqueueArenaTim505E30(initialized.Memory.Arena!, 0).State;
        Assert.NotEmpty(textures.Drain505D20(initialized.Memory.Arena).Uploads);
        string json = JsonSerializer.Serialize(initialized.Effect);
        NativeBattleOriginalEffectState restored = JsonSerializer.Deserialize<NativeBattleOriginalEffectState>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(resources.Animation.Data.ToArray(), restored.Binding.FiragaResources!.Animation.Data.ToArray());
    }
}
