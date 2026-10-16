using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCureResourcesTests
{
    [Theory]
    [InlineData("Cure", 21, 1, 0x8D6A00u, 0x8D69E0u, "mag000-049.tim")]
    [InlineData("Cura", 22, 25, 0x889D60u, 0x889D40u, "mag024-050.tim")]
    [InlineData("Curaga", 23, 28, 0x87DB10u, 0x87DAF0u, "mag027-051.tim")]
    public void Healing_actions_use_the_installed_kernel_effect_and_executable_entry(string name, int actionId, ushort effectId, uint entry, uint initializer, string texture)
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleActionDefinition action = assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == actionId);
        Assert.Equal(name, action.Name);
        Assert.Equal(effectId, action.NativeAction!.MagicId);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalog()[effectId - 1];
        Assert.Equal(entry, binding.Entry);
        Assert.Equal(initializer, binding.Initializer);
        Assert.Equal(texture, binding.InitializerResource!.Name);
    }

    [Fact]
    public void Cure_entry_registers_its_original_animation_and_uploads_the_loaded_TIM_before_its_first_callback()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleCureResources resources = game.ReadNativeCureResources();
        Assert.Equal(1968, resources.Animation.Data.Length);
        Assert.Equal(0x493F2u, resources.Sound);
        Assert.Equal(new uint[] { 0x1641F78, 0x1641FCC, 0x164205C, 0x1642238, 0x1642504 }, resources.Sprites.Select(sprite => sprite.Address));
        Assert.Equal(new[] { 2, 5, 17, 27, 1 }, resources.Sprites.Select(sprite => sprite.Bytes[8] | sprite.Bytes[9] << 8));
        Assert.Equal(new[] { 84, 144, 476, 716, 40 }, resources.Sprites.Select(sprite => sprite.Bytes.Length));
        foreach (NativeBattleModuleResource resource in resources.Sprites)
        {
            resource.Validate();
        }
        var catalog = game.ReadNativeEffectCatalogForEffects([1]);
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(1, catalog);
        var initialized = NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), selected);
        NativeBattleOriginalEffectState effect = initialized.Effect with { Parameters = new(0, 0, 8, 0, 0, 0) };
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 2, 11, 0, 21, 1, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleCureFrame frame = NativeBattleCureController.Initialize8D6A00(initialized.Memory, effect, packets, resources);
        Assert.Equal(0, frame.State.SourceArenaOffset);
        Assert.Equal(0x277AFC0u, frame.Effect.ReturnedDescriptor);
        NativeBattleEffectTextureState textures = frame.Memory.Textures!;
        Assert.Equal(1, textures.Count);
        Assert.Equal(8, textures.Animation!.StreamOffset);
        Assert.Equal(0x144, textures.Animation.ModelTableOffset);
        NativeBattleEffectTextureDrain drained = textures.Drain505D20(frame.Memory.Arena);
        Assert.Equal(2, drained.Uploads.Length);
        Assert.Equal(0, drained.State.Count);
        Assert.Equal(0x8D6B30u, frame.Effect.Pools[0].Slots[0].Callback);
    }
}
