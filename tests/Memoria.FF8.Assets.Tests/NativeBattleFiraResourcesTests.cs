using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleFiraResourcesTests
{
    [Fact]
    public void OriginalFiraActionBindsItsInitializerAndTypedStaticResources()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleActionDefinition action = assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == 2);
        Assert.Equal((ushort)142, action.NativeAction!.MagicId);
        var catalog = game.ReadNativeEffectCatalogForEffects([142]);
        NativeBattleEffectBinding binding = catalog[141];
        Assert.Equal("mag141.tim", binding.InitializerAsset!.Name);
        NativeBattleFiraResources resources = Assert.IsType<NativeBattleFiraResources>(binding.FiraResources);
        Assert.Equal(0x7D4, resources.Animation.Data.Length);
        Assert.Equal(300003u, resources.Sound);
        Assert.Equal(16, resources.UvOffsets.Length);
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(resources.Sprite0.Bytes.AsSpan().Slice(8)));
        Assert.Equal(16, BinaryPrimitives.ReadUInt16LittleEndian(resources.AnimatedSprite.Bytes.AsSpan().Slice(8)));
        Assert.Equal(new uint[] { 0xDDE0D8, 0xDDE100 }, new[] { resources.Sprite0.Address, resources.AnimatedSprite.Address });
        Assert.Equal(new uint[] { 0xDDE2AC, 0xDDE6C4, 0xDDEF14 }, new[] { resources.Mesh0.Address, resources.Mesh1.Address, resources.Mesh2.Address });
        Assert.Same(resources, game.ReadNativeFiraResources());
        Assert.Null(game.ReadNativeEffectCatalog()[141].FiraResources);
        Assert.Throws<NotSupportedException>(() => NativeBattleFiraResourcesReader.Read(new byte[128]));
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(142, game.ReadNativeEffectCatalog());
        NativeBattleOriginalEffectState materialized = NativeBattleEffectResourceResolver.MaterializeSelected(selected, effect => catalog[effect - 1]);
        Assert.Same(resources, materialized.Binding.FiraResources);
        var initialized = NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), materialized);
        Assert.Same(resources, initialized.Effect.Binding.FiraResources);
        Assert.Equal(binding.InitializerResource!.GlobalAddress, Assert.Single(initialized.Effect.ResourcePointers).GlobalAddress);
        Assert.True(binding.InitializerAsset.Data.AsSpan().SequenceEqual(initialized.Memory.Arena!.Read(0, binding.InitializerAsset.Data.Length)));
        NativeBattleEffectTextureState textures = initialized.Memory.Textures!.Register5099A0(resources.Animation).EnqueueArenaTim505E30(initialized.Memory.Arena!, 0).State;
        Assert.NotEmpty(textures.Drain505D20(initialized.Memory.Arena).Uploads);
        string json = JsonSerializer.Serialize(new Saved(initialized.Memory, initialized.Effect));
        Saved restored = JsonSerializer.Deserialize<Saved>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(resources.UvOffsets.ToArray(), restored.Effect.Binding.FiraResources!.UvOffsets.ToArray());
        Assert.Equal(resources.Animation.Data.ToArray(), restored.Effect.Binding.FiraResources.Animation.Data.ToArray());
        Assert.Same(restored.Effect, NativeBattleEffectResourceResolver.MaterializeSelected(restored.Effect, null));
    }

    private sealed record Saved(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect);
}
