using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect54Profile;
using static Memoria.FF8.Runtime.NativeBattleEffect54Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect54ResourceTests
{
    [Fact]
    public void Original_initializer_camera_directory_and_selected_descriptor_extents_are_exact()
    {
        using GameAssets game = Open();
        NativeBattleEffect54Resources resources = game.ReadNativeEffect54Resources();
        resources.Validate();
        Assert.Equal(165408, resources.MainTim.Data.Length);
        Assert.Equal(176, resources.Camera.Data.Length);
        Assert.Equal(SourceLength, resources.OriginalSourceBank.Data.Length);
        (_, NativeBattleFileStaging staging) = NativeBattleEffect54State.FromColdProcess().BindSources(NativeBattleFileStaging.Unknown(), resources);
        Assert.Equal(DirectoryLength, resources.ReadResource(staging, SourceAddress).Length);
        foreach (uint descriptor in DescriptorAddresses)
        {
            Assert.Equal(DescriptorLength, resources.ReadResource(staging, descriptor).Length);
        }
        Assert.Throws<NotSupportedException>(() => resources.ReadResource(staging, DescriptorAddresses[0] + 1));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(SourceAddress + SourceLength, 1));
    }

    [Fact]
    public void Original_sprite_frames_have_positive_prefix_and_finite_palette_transform_domains()
    {
        using GameAssets game = Open();
        NativeBattleEffect54Resources resources = game.ReadNativeEffect54Resources();
        for (int spriteIndex = 0; spriteIndex < SpriteAddresses.Length; spriteIndex++)
        {
            NativeBattleModuleResource source = resources.Sprite(SpriteAddresses[spriteIndex]);
            Assert.Equal(SpriteLengths[spriteIndex], source.Bytes.Length);
            for (int phase = 0; phase < SpriteFrameCounts[spriteIndex]; phase++)
            {
                NativeBattleModuleSpriteFrame frame = source.ProjectedFrame571C80(phase);
                Assert.False(frame.ExtendedPacket);
                Assert.InRange(frame.Quads.Length / 20, 1, spriteIndex == 0 ? 2 : 1);
                for (int quad = 0; quad < frame.Quads.Length; quad += 20)
                {
                    ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(frame.Quads.AsSpan(quad + 14, 2));
                    Assert.InRange(flags >> 12, 0, 1);
                    Assert.Equal(0x400, flags & 0xC00);
                }
            }
            Assert.True(source.ProjectedFrame571C80(SpriteFrameCounts[spriteIndex] - 1).NextOffset < 0);
        }
    }

    [Fact]
    public void Lazy_relocation_publishes_only_69_current_cells_and_JSON_never_reloads_them()
    {
        using GameAssets game = Open();
        NativeBattleEffect54Resources resources = JsonSerializer.Deserialize<NativeBattleEffect54Resources>(JsonSerializer.Serialize(game.ReadNativeEffect54Resources()))!;
        resources.Validate();
        (NativeBattleEffect54State state, NativeBattleFileStaging staging) = NativeBattleEffect54State.FromColdProcess().BindSources(NativeBattleFileStaging.Unknown(), resources);
        byte[] relocated = resources.ReadBank(staging).ToArray();
        foreach (uint address in MutableDwordAddresses)
        {
            int offset = checked((int)(address - SourceAddress));
            uint original = BinaryPrimitives.ReadUInt32LittleEndian(relocated.AsSpan(offset, 4));
            BinaryPrimitives.WriteUInt32LittleEndian(relocated.AsSpan(offset, 4), address == SourceAddress ? 1u : unchecked(original + SourceAddress));
        }
        staging = resources.PublishBank(staging, relocated.ToImmutableArray());
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(SourceAddress, 4)));
        Assert.Equal(DescriptorAddresses[0], BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadResource(staging, SourceAddress + 4, 4)));
        Assert.Equal(FirstSpriteAddress, BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadResource(staging, SourceAddress + 0x44, 4)));
        Assert.All(staging.ReadSnapshot(SourceAddress + 12, 4).Known, known => Assert.False(known));
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect54State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        staging = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleFileStaging>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(staging)))!);
        (state, staging) = state.BindSources(staging, resources);
        Assert.Equal(relocated, resources.ReadBank(staging).ToArray());
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(DescriptorAddresses[0] + 0xC8, 4));
        foreach ((uint address, int length) in StaticRanges)
        {
            Assert.Equal(length, resources.ReadStatic(address, length).Length);
        }
    }

    [Fact]
    public void Selected_catalog_hydrates_the_same_original_resource_binding()
    {
        using GameAssets game = Open();
        NativeBattleEffect54Resources resources = game.ReadNativeEffect54Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([54])[53];
        Assert.Same(resources, binding.Effect54Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.False(binding.InitializerIsRet);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag053.tim", InitializerGlobal), binding.InitializerResource);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed effect54 resources.");
        return new(directory!);
    }
}
