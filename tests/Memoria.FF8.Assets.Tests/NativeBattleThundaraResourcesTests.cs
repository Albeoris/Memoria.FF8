using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleThundaraResourcesTests
{
    [Fact]
    public void Original_Thundara_binds_all_sprite_frames_meshes_sound_and_private_flash_RGB()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleActionDefinition action = assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == 8);
        Assert.Equal((ushort)102, action.NativeAction!.MagicId);
        Assert.Equal((ushort)8, action.NativeAction.ActionId);
        var catalog = game.ReadNativeEffectCatalogForEffects([102]);
        NativeBattleEffectBinding binding = catalog[101];
        Assert.Equal(0x6DC2D0u, binding.Entry);
        Assert.Equal(0x6DC2B0u, binding.Initializer);
        Assert.Equal("mag101.tim", binding.InitializerAsset!.Name);
        NativeBattleThundaraResources resources = Assert.IsType<NativeBattleThundaraResources>(binding.ThundaraResources);
        Assert.Equal(0x650, resources.Animation.Data.Length);
        Assert.Equal(300009u, resources.Sound);
        Assert.Equal(0x808080u, resources.FlashColor);
        Assert.Equal(new uint[] { 0x131E760, 0x131E878, 0x131EABC, 0x131ECB0, 0x131EECC, 0x131F0E8, 0x131F244, 0x131F458, 0x131F5B4, 0x131FA68, 0x131FF74 }, resources.Sprites.Select(sprite => sprite.Address).ToArray());
        Assert.Equal(new ushort[] { 8, 8, 8, 8, 8, 9, 20, 9, 9, 10, 9 }, resources.Sprites.Select(sprite => BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8))).ToArray());
        Assert.Equal(new uint[] { 0x13200D0, 0x13207B8, 0x1320EA0 }, new[] { resources.Mesh0.Address, resources.Mesh1.Address, resources.Mesh2.Address });
        Assert.Same(resources.Sprites[6], resources.Sprite(0x131F244));
        Assert.Throws<NotSupportedException>(() => resources.Sprite(0));
        Assert.Same(resources, game.ReadNativeThundaraResources());
        Assert.Null(game.ReadNativeEffectCatalog()[101].ThundaraResources);
        Assert.Throws<NotSupportedException>(() => NativeBattleThundaraResourcesReader.Read(new byte[128]));
        NativeBattleOriginalEffectState effect = NativeBattleOriginalEffects.Select(102, catalog);
        var initialized = NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), effect);
        NativeBattleEffectTextureState textures = initialized.Memory.Textures!.Register5099A0(resources.Animation).EnqueueArenaTim505E30(initialized.Memory.Arena!, 0).State;
        Assert.NotEmpty(textures.Drain505D20(initialized.Memory.Arena).Uploads);
        string json = JsonSerializer.Serialize(initialized.Effect);
        NativeBattleOriginalEffectState restored = JsonSerializer.Deserialize<NativeBattleOriginalEffectState>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(resources.Animation.Data.ToArray(), restored.Binding.ThundaraResources!.Animation.Data.ToArray());
    }
}
