using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattlePotion51ResourcesTests
{
    [Fact]
    public void Original_HiPotion_resources_retain_their_own_addresses_and_complete_Cura_directories()
    {
        using GameAssets game = OpenGame();
        NativeBattleCuraResources resources = game.ReadNativePotion51Resources();
        Assert.Equal((ushort)51, resources.Effect);
        Assert.Equal("FF8_EN:15AA734", resources.Animation.Identity);
        Assert.Equal(0x848, resources.Animation.Data.Length);
        Assert.Equal(300077u, resources.Sound);
        Assert.Equal(new uint[] { 0x15A9DCC, 0x15A9F78, 0x15A9FEC, 0x15AA040, 0x15AA104, 0x15AA230, 0x15AA3DC, 0x15AA588 }, resources.Sprites.Select(sprite => sprite.Address));
        Assert.Equal(new[] { 16, 4, 2, 7, 11, 16, 16, 16 }, resources.Sprites.Select(sprite => sprite.Bytes[8] | sprite.Bytes[9] << 8));
        Assert.Equal(new uint[] { 0x15AAF7C, 0x15AB250 }, resources.Scenes.Select(scene => scene.Address));
        Assert.Equal(new[] { 724, 6756 }, resources.Scenes.Select(scene => scene.Data.Length));
        Assert.Equal(new[] { 30, 30, 30 }, resources.EmissionSchedules.Select(schedule => schedule.Length));
        NativeBattleCuraResources cura = game.ReadNativeCuraResources();
        Assert.Equal(cura.Animation.Data.ToArray(), resources.Animation.Data.ToArray());
        for (int index = 0; index < resources.Sprites.Length; index++)
        {
            resources.Sprites[index].Validate();
            Assert.Equal(cura.Sprites[index].Bytes.ToArray(), resources.Sprites[index].Bytes.ToArray());
            Assert.NotEqual(cura.Sprites[index].Address, resources.Sprites[index].Address);
        }
        for (int index = 0; index < resources.Scenes.Length; index++)
        {
            Assert.Equal(cura.Scenes[index].Data.ToArray(), resources.Scenes[index].Data.ToArray());
        }
        for (int index = 0; index < resources.EmissionSchedules.Length; index++)
        {
            Assert.Equal(cura.EmissionSchedules[index].ToArray(), resources.EmissionSchedules[index].ToArray());
        }
    }

    [Fact]
    public void Original_HiPotion_entry_uploads_its_TIM_and_retains_distinct_cold_module_owners()
    {
        using GameAssets game = OpenGame();
        NativeBattleCuraResources resources = game.ReadNativePotion51Resources();
        var catalog = game.ReadNativeEffectCatalogForEffects([51]);
        NativeBattleEffectBinding binding = catalog[50];
        Assert.Equal(0x81C800u, binding.Entry);
        Assert.Equal(0x81C7E0u, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag024-050.tim", 0x2619F50), binding.InitializerResource);
        Assert.Equal((ushort)51, binding.CuraResources!.Effect);
        NativeBattleEffectMemory memory = game.ReadColdProcessNativeBattleMemory().EffectMemory!;
        Assert.Equal((short?)0, memory.Potion51ContextF4);
        Assert.Equal((int?)0, memory.Potion51SceneFade);
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(51, catalog);
        var initialized = NativeBattleOriginalEffects.Initialize(memory, selected);
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 4, 0, 0, 0, 51, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleCuraFrame frame = NativeBattleCuraController.Initialize889D60(initialized.Memory, initialized.Effect with { Parameters = new(0, 0, 8, 0, 0, 0) }, packets, resources);
        Assert.Equal(0x261A048u, frame.Effect.ReturnedDescriptor);
        Assert.Equal(0x81C8E0u, frame.Effect.Pools[0].Slots[0].Callback);
        Assert.Equal(0, frame.State.OddDrawBase26FD114);
        Assert.Equal(0x12000, frame.State.EvenDrawBase26FD110);
        Assert.Equal((short?)0, frame.Memory.Potion51ContextF4);
        Assert.Equal((int?)0, frame.Memory.Potion51SceneFade);
        NativeBattleEffectTextureState textures = Assert.IsType<NativeBattleEffectTextureState>(frame.Memory.Textures);
        Assert.Equal(1, textures.Count);
        Assert.Equal(8, textures.Animation!.StreamOffset);
        Assert.Equal(2, textures.Drain505D20(frame.Memory.Arena).Uploads.Length);
    }

    private static GameAssets OpenGame()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check the HiPotion resource contract.");
        return new(directory!);
    }
}
