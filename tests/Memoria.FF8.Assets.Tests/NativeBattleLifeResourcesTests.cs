using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleLifeResourcesTests
{
    [Theory]
    [InlineData((ushort)35, 0x8649C0u, 0x8649A0u, 0x26AC780u)]
    [InlineData((ushort)5, 0x8D4450u, 0x8D4430u, 0x27618A0u)]
    public void Original_catalog_and_resources_keep_distinct_entries_and_identical_sprite_payloads(ushort effect, uint entry, uint initializer, uint global)
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalog()[effect - 1];
        Assert.Equal(entry, binding.Entry);
        Assert.Equal(initializer, binding.Initializer);
        NativeBattleEffectInitializerResource initializerResource = binding.InitializerResource ?? throw new InvalidDataException("Original Life-family initializer descriptor is missing.");
        Assert.Equal("mag004-034.tim", initializerResource.Name);
        Assert.Equal(global, initializerResource.GlobalAddress);
        NativeBattleLifeResources resources = game.ReadNativeLifeResources(effect);
        Assert.Same(resources, game.ReadNativeLifeResources(effect));
        Assert.Equal(300021u, resources.Sound);
        NativeBattleLifeResources other = game.ReadNativeLifeResources(effect == 35 ? (ushort)5 : (ushort)35);
        NativeBattleLifeProfile profile = NativeBattleLifeProfile.ForEntry(entry);
        Assert.Equal(new uint[] { 0x15E638C, 0x15E6538, 0x15E66E4, 0x15E6890, 0x15E68EC }.Select(address => address + profile.StaticDelta), resources.Sprites.Select(sprite => sprite.Address));
        Assert.Equal(5, resources.Sprites.Length);
        for (int index = 0; index < resources.Sprites.Length; index++)
        {
            resources.Sprites[index].Validate();
            Assert.Equal(other.Sprites[index].Bytes.ToArray(), resources.Sprites[index].Bytes.ToArray());
            Assert.NotEqual(other.Sprites[index].Address, resources.Sprites[index].Address);
        }
        if (effect == 35)
        {
            Assert.Equal(0x630, resources.Animation!.Data.Length);
        }
        else
        {
            Assert.Null(resources.Animation);
        }
    }

    [Theory]
    [InlineData((ushort)35, (byte)0, true, true)]
    [InlineData((ushort)35, (byte)1, false, false)]
    [InlineData((ushort)5, (byte)0, true, false)]
    [InlineData((ushort)5, (byte)1, true, false)]
    public void Actual_entry_preserves_the_distinct_TIM_and_animation_policy(ushort effectId, byte flags, bool uploads, bool registersAnimation)
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleLifeResources resources = game.ReadNativeLifeResources(effectId);
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(effectId, game.ReadNativeEffectCatalogForEffects([effectId]));
        var initialized = NativeBattleOriginalEffects.Initialize(game.ReadColdProcessNativeBattleMemory().EffectMemory!, selected);
        NativeBattleOriginalEffectState effect = initialized.Effect with { Parameters = new(0, flags, 8, 0, 0, 0) };
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        //Controlled physical header exercises entry upload policy, independently of command/formula admission.
        (packets, _) = packets.Prepare(new(0, 2, 11, 0, 24, effectId, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleLifeFrame frame = NativeBattleLifeController.Initialize8649C0(initialized.Memory, effect, packets, resources);
        NativeBattleLifeProfile profile = NativeBattleLifeProfile.ForEntry(effect.Binding.Entry);
        Assert.Equal(profile.Global(0x26AC868), frame.Effect.ReturnedDescriptor);
        Assert.Equal(new[] { 2, 4, 300, 300, 100 }, frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(profile.Code(0x864AB0), frame.Effect.Pools[0].Slots[0].Callback);
        Assert.Equal((byte)3, frame.Effect.Pools[0].Slots[0].ReadByte(0x2D));
        NativeBattleEffectTextureState textures = frame.Memory.Textures!;
        Assert.Equal(uploads ? 1 : 0, textures.Count);
        if (registersAnimation)
        {
            Assert.Equal(8, textures.Animation!.StreamOffset);
            Assert.Equal(0x144, textures.Animation.ModelTableOffset);
        }
        else
        {
            Assert.Equal(initialized.Memory.Textures!.Animation, textures.Animation);
        }
        NativeBattleEffectTextureDrain drained = textures.Drain505D20(frame.Memory.Arena);
        Assert.Equal(uploads ? 2 : 0, drained.Uploads.Length);
        Assert.Equal(0, drained.State.Count);
        if (effectId == 35 && flags == 1)
        {
            NativeBattleLifeFrame skipped = NativeBattleLifeController.Initialize8649C0(initialized.Memory with { Textures = null, Arena = null }, effect with { ResourcePointers = [] }, packets, resources with { Animation = null });
            Assert.Null(skipped.Memory.Textures);
        }
        if (effectId == 5 && flags == 1)
        {
            Assert.Throws<NotSupportedException>(() => NativeBattleLifeController.Initialize8649C0(initialized.Memory, effect with { ResourcePointers = [] }, packets, resources));
        }
    }

    [Fact]
    public void Unproved_effect_does_not_reuse_a_family_resource_profile()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        Assert.Throws<ArgumentOutOfRangeException>(() => game.ReadNativeLifeResources(1));
    }
}
