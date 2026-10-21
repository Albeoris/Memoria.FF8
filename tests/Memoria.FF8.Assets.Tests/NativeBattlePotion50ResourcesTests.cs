using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattlePotion50ResourcesTests
{
    [Fact]
    public void Original_Potion_resources_keep_their_own_addresses_and_complete_frame_directories()
    {
        using GameAssets game = OpenGame();
        NativeBattleCureResources resources = game.ReadNativePotion50Resources();
        Assert.Equal((ushort)50, resources.Effect);
        Assert.Equal("FF8_EN:15ACD7C", resources.Animation.Identity);
        Assert.Equal(0x7B0, resources.Animation.Data.Length);
        Assert.Equal(300018u, resources.Sound);
        Assert.Equal(new uint[] { 0x15AD52C, 0x15AD580, 0x15AD610, 0x15AD7EC, 0x15ADAB8 }, resources.Sprites.Select(sprite => sprite.Address));
        Assert.Equal(new[] { 84, 144, 476, 716, 40 }, resources.Sprites.Select(sprite => sprite.Bytes.Length));
        foreach (NativeBattleModuleResource sprite in resources.Sprites)
        {
            sprite.Validate();
        }
        NativeBattleCureResources cure = game.ReadNativeCureResources();
        Assert.Equal(cure.Animation.Data.ToArray(), resources.Animation.Data.ToArray());
        for (int index = 0; index < resources.Sprites.Length; index++)
        {
            Assert.Equal(cure.Sprites[index].Bytes.ToArray(), resources.Sprites[index].Bytes.ToArray());
            Assert.NotEqual(cure.Sprites[index].Address, resources.Sprites[index].Address);
        }
    }

    [Fact]
    public void Original_Potion_initializer_registers_camera_and_uploads_its_actual_TIM_with_distinct_cold_context()
    {
        using GameAssets game = OpenGame();
        NativeBattleCureResources resources = game.ReadNativePotion50Resources();
        var catalog = game.ReadNativeEffectCatalogForEffects([50]);
        NativeBattleEffectBinding binding = catalog[49];
        Assert.Equal(0x81FA10u, binding.Entry);
        Assert.Equal(0x81F9F0u, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag000-049.tim", 0x2634AB8), binding.InitializerResource);
        Assert.Equal((ushort)50, binding.CureResources!.Effect);
        NativeBattleEffectMemory memory = game.ReadColdProcessNativeBattleMemory().EffectMemory!;
        Assert.Equal((short?)0, memory.Potion50ContextF4);
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(50, catalog);
        var initialized = NativeBattleOriginalEffects.Initialize(memory, selected);
        NativeBattleOriginalEffectState effect = initialized.Effect with { Parameters = new(0, 0, 8, 0, 0, 0) };
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 4, 0, 0, 0, 50, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleCureFrame frame = NativeBattleCureController.Initialize8D6A00(initialized.Memory, effect, packets, resources);
        Assert.Equal(0x2634BB8u, frame.Effect.ReturnedDescriptor);
        Assert.Equal(0x81FB40u, frame.Effect.Pools[0].Slots[0].Callback);
        Assert.Equal((short?)0, frame.Memory.Potion50ContextF4);
        NativeBattleEffectTextureState textures = Assert.IsType<NativeBattleEffectTextureState>(frame.Memory.Textures);
        Assert.Equal(1, textures.Count);
        Assert.Equal(8, textures.Animation!.StreamOffset);
        Assert.Equal(2, textures.Drain505D20(frame.Memory.Arena).Uploads.Length);
    }

    private static GameAssets OpenGame()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check the Potion50 resource contract.");
        return new(directory!);
    }
}
