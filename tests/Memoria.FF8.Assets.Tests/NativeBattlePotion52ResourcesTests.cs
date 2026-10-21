using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattlePotion52ResourcesTests
{
    [Fact]
    public void Original_XPotion_resources_keep_actual_addresses_and_fix_up_their_own_complete_node_bank()
    {
        using GameAssets game = OpenGame();
        NativeBattleCuragaResources resources = game.ReadNativePotion52Resources();
        Assert.Equal((ushort)52, resources.Effect);
        Assert.Equal("FF8_EN:15A9218", resources.Animation.Identity);
        Assert.Equal(0xBA0, resources.Animation.Data.Length);
        Assert.Equal(300143u, resources.Sound);
        Assert.Equal(new uint[] { 0x007F7F7F, 0x003F3F3F, 0xFF000000 }, resources.FlashProgram);
        NativeBattleCuragaExecutableSource source = Assert.IsType<NativeBattleCuragaExecutableSource>(resources.Executable);
        Assert.Equal(0x17AB0E8u, source.ResourceAddress);
        Assert.Equal(0x81B4, source.Data.Length);
        NativeBattleCuragaResources curaga = game.ReadNativeCuragaResources();
        Assert.Equal(curaga.Animation.Data.ToArray(), resources.Animation.Data.ToArray());
        Assert.Equal(curaga.Executable!.Data.ToArray(), source.Data.ToArray());
        NativeBattleCuragaExecutableState initialized = NativeBattleCuragaExecutableState.FromColdProcess(52).Initialize87EA20(source);
        for (int index = 0; index < 16; index++)
        {
            uint original = BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(4 + 4 * index));
            Assert.Equal(0x6C34u + (uint)index * 0x158u, original);
            uint actual = BinaryPrimitives.ReadUInt32LittleEndian(initialized.Read(source.ResourceAddress + 4u + 4u * (uint)index, 4));
            Assert.Equal(source.ResourceAddress + original, actual);
            for (int field = 0xC8; field <= 0x144; field += 4)
            {
                uint channel = BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(checked((int)original + field)));
                Assert.Equal(unchecked(channel + source.ResourceAddress), BinaryPrimitives.ReadUInt32LittleEndian(initialized.Read(actual + (uint)field, 4)));
            }
        }
    }

    [Fact]
    public void Original_XPotion_initializer_registers_its_camera_TIM_and_distinct_cold_executable_owner()
    {
        using GameAssets game = OpenGame();
        var catalog = game.ReadNativeEffectCatalogForEffects([52]);
        NativeBattleEffectBinding binding = catalog[51];
        Assert.Equal(0x817410u, binding.Entry);
        Assert.Equal(0x8173F0u, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag027-051.tim", 0x2619494), binding.InitializerResource);
        Assert.Equal((ushort)52, binding.CuragaResources!.Effect);
        NativeBattleEffectMemory memory = game.ReadColdProcessNativeBattleMemory().EffectMemory!;
        NativeBattleCuragaExecutableState retained = Assert.IsType<NativeBattleCuragaExecutableState>(memory.Potion52Executable);
        Assert.Equal(0x17AB0E8u, retained.ResourceAddress);
        Assert.Equal(0u, retained.InitializationFlag);
        Assert.Null(retained.Data);
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(52, catalog);
        var initialized = NativeBattleOriginalEffects.Initialize(memory, selected);
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 4, 0, 0, 0, 52, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleCuragaFrame frame = NativeBattleCuragaController.Initialize87DB10(initialized.Memory, initialized.Effect with { Parameters = new(0, 0, 8, 0, 0, 0) }, packets, game.ReadNativePotion52Resources());
        Assert.Equal(0x2619588u, frame.Effect.ReturnedDescriptor);
        Assert.Equal(0x817540u, frame.Effect.Pools[0].Slots[0].Callback);
        Assert.Equal(4, frame.Effect.Pools.Length);
        Assert.Equal(frame.State.SourceArenaOffset + 0xC000, frame.State.AuxiliaryCursor);
        Assert.Equal(8, frame.Memory.Textures!.Animation!.StreamOffset);
        Assert.Equal(2, frame.Memory.Textures.Drain505D20(frame.Memory.Arena).Uploads.Length);
    }

    private static GameAssets OpenGame()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check the XPotion resource contract.");
        return new(directory!);
    }
}
