using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleBlizzaraResourcesTests
{
    [Fact]
    public void Original_Blizzara_wrapper_loads_original_mesh_pair_sprite_animation_and_TIM()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleActionDefinition action = assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == 5);
        Assert.Equal((ushort)103, action.NativeAction!.MagicId);
        Assert.Equal((ushort)5, action.NativeAction.ActionId);
        var catalog = game.ReadNativeEffectCatalogForEffects([103]);
        NativeBattleEffectBinding binding = catalog[102];
        Assert.Equal(0x6DA300u, binding.Entry);
        Assert.Equal(0x6DA2E0u, binding.Initializer);
        Assert.Equal("mag102.tim", binding.InitializerAsset!.Name);
        NativeBattleBlizzaraResources resources = Assert.IsType<NativeBattleBlizzaraResources>(binding.BlizzaraResources);
        Assert.Equal(0xD50, resources.Animation.Data.Length);
        Assert.Equal(300015u, resources.Sound);
        Assert.Equal(16, BinaryPrimitives.ReadUInt16LittleEndian(resources.Sprite16.Bytes.AsSpan().Slice(8)));
        Assert.Equal(0x13197E4u, resources.Sprite16.Address);
        Assert.Equal(new uint[] { 0x1319990, 0x131A678, 0x131ABB0, 0x131C86C }, new[] { resources.SkyMesh.Address, resources.RingMesh.Address, resources.PrimaryMesh.Address, resources.PartnerMesh.Address });
        Assert.Equal(0x1CBC, resources.PrimaryMesh.Bytes.Length);
        Assert.Equal(0x1194, resources.PartnerMesh.Bytes.Length);
        Assert.Equal(134, BinaryPrimitives.ReadInt32LittleEndian(resources.PrimaryMesh.Bytes.AsSpan().Slice(4)));
        Assert.Same(resources, game.ReadNativeBlizzaraResources());
        Assert.Null(game.ReadNativeEffectCatalog()[102].BlizzaraResources);
        Assert.Throws<NotSupportedException>(() => NativeBattleBlizzaraResourcesReader.Read(new byte[128]));
        var initialized = NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), NativeBattleOriginalEffects.Select(103, catalog));
        NativeBattleEffectTextureState textures = initialized.Memory.Textures!.Register5099A0(resources.Animation).EnqueueArenaTim505E30(initialized.Memory.Arena!, 0).State;
        Assert.NotEmpty(textures.Drain505D20(initialized.Memory.Arena).Uploads);
        string json = JsonSerializer.Serialize(initialized.Effect);
        NativeBattleOriginalEffectState restored = JsonSerializer.Deserialize<NativeBattleOriginalEffectState>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(resources.PrimaryMesh.Bytes.ToArray(), restored.Binding.BlizzaraResources!.PrimaryMesh.Bytes.ToArray());
    }
}
