using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect231ResourcesTests
{
    private const int File0Offset = 0x1000;
    private const int File1Offset = 0x10000;
    private const uint ArenaAddress = 0x20DFAB8;
    private const uint Manager = 0x2797690;

    [Fact]
    public void Original_registration_retains_selected_files_directories_camera_and_sound()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = Binding(game);
        Assert.Equal(0xA664A0u, binding.Entry);
        Assert.Equal(0xA66470u, binding.Initializer);
        ImmutableArray<NativeBattleEffectInitializerAsset> files = NativeBattleEffectInitializerPolicy.Assets(binding);
        Assert.Equal(new[] { "mag230_b.00", "mag230_b.01" }, files.Select(file => file.Name));
        Assert.Equal(new[] { 9268, 151608 }, files.Select(file => file.Data.Length));
        NativeBattleCinematicResources resources = game.ReadNativeCinematicResources(231);
        resources.Validate();
        Assert.Same(resources, game.ReadNativeCinematicResources(231));
        Assert.Equal(4, resources.TextureProfile.Count);
        foreach ((int offset, uint value) in new[] { (0, 0x20u), (4, 0x48u), (8, 0x70u), (12, 0x78u), (16, 0x80u), (24, 0x88u) })
        {
            Assert.Equal(value, resources.InitializationOffset(offset));
        }
        game.ReadNativeCinematicInitializationDirectories(231).Validate();
        ImmutableArray<byte> bytes = files[0].Data;
        Assert.Equal(0x58, Dword(bytes, 4));
        Assert.Equal(0x930, Dword(bytes, 0x10));
        Assert.Equal(0x58, Dword(files[1].Data, 0x1C));
        Assert.Equal(400168, Dword(bytes, 0x930 + Dword(bytes, 0x930)));
        Assert.Equal(0, Dword(bytes, 0x24));
        Assert.Null(resources.Vibration);
        Assert.Equal(0x870, Dword(bytes, 0x20));
        NativeBattleEffectHeapAllocation camera = new("effect-camera", bytes.AsSpan().Slice(0x870).ToArray().ToImmutableArray());
        NativeBattleEffectAnimationRegisters animation = NativeBattleEffectTextureState.FromColdProcess().Register5099A0(camera).Animation!;
        Assert.Equal(8, animation.StreamOffset);
        Assert.Equal(0x18, animation.ModelTableOffset);
        NativeBattleCameraBank bank = new(camera.Data.AsSpan().Slice(animation.ModelTableOffset).ToArray().ToImmutableArray());
        Assert.Equal(0x18, bank.TrackOffset(0));
        Assert.Equal(ushort.MaxValue, Word(bank.Data, 0x96));
        Assert.Null(bank.TrackOffset(0x10));
    }

    [Fact]
    public void Four_uploads_and_metadata_select_their_actual_current_sources()
    {
        using GameAssets game = Open();
        ImmutableArray<NativeBattleEffectInitializerAsset> files = NativeBattleEffectInitializerPolicy.Assets(Binding(game));
        NativeBattleCinematicResources resources = game.ReadNativeCinematicResources(231);
        NativeBattleWaterExecutionFrame frame = Frame(files, resources);
        Assert.Equal(new byte[] { 1, 0x81, 0x81, 0x81 }, resources.ReadStatic(resources.TextureProfile.PixelSourceMapAddress, 4).ToArray());
        Assert.Equal(new byte[] { 1, 1, 1, 1 }, resources.ReadStatic(resources.TextureProfile.ClutSourceMapAddress, 4).ToArray());
        NativeBattleEffectTextureRectangle[] pixels = [new(512, 256, 42, 24), new(384, 256, 128, 256), new(576, 256, 128, 256), new(512, 288, 32, 224)];
        NativeBattleEffectTextureRectangle[] palettes = [new(320, 254, 16, 1), new(320, 255, 256, 1), new(320, 253, 256, 1), new(320, 252, 256, 1)];
        int[] pixelOffsets = [0x858, 0x1038, 0x11038, 0x21038];
        int[] paletteOffsets = [0x24838, 0x24A38, 0x24C38, 0x24E38];
        int[] metadataPcs = [0x410, 0x158, 0x35C, 0x2DC];
        ushort[] pages = [0x38, 0xB6, 0xB9, 0xB8];
        ushort[] cluts = [0x3F94, 0x3FD4, 0x3F54, 0x3F14];
        for (int index = 0; index < 4; index++)
        {
            NativeBattleCinematicTextureSelection pixel = NativeBattleCinematicTextureMetadata.ResolvePixelB664A0(frame, resources, index);
            NativeBattleCinematicTextureSelection palette = NativeBattleCinematicTextureMetadata.ResolveClutB66560(frame, resources, index);
            Assert.Equal(pixels[index], pixel.Rectangle);
            Assert.Equal(palettes[index], palette.Rectangle);
            Assert.Equal(ArenaAddress + File1Offset + (uint)pixelOffsets[index], pixel.SourceAddress);
            Assert.Equal(ArenaAddress + File1Offset + (uint)paletteOffsets[index], palette.SourceAddress);
            Assert.InRange(pixelOffsets[index] + pixels[index].Width * pixels[index].Height * 2, 0, files[1].Data.Length);
            Assert.InRange(paletteOffsets[index] + palettes[index].Width * palettes[index].Height * 2, 0, files[1].Data.Length);
            Assert.Equal((ushort)0x52, Word(files[0].Data, metadataPcs[index]));
            Assert.Equal((ushort)index, Word(files[0].Data, metadataPcs[index] + 2));
            NativeBattleWaterExecutionFrame selected = Store(frame, 0x2797450, ArenaAddress + File0Offset + (uint)metadataPcs[index]);
            selected = NativeBattleWaterTextureMetadata.Execute52AA3A60(selected, resources);
            Assert.Equal(pages[index], BinaryPrimitives.ReadUInt16LittleEndian(selected.Arena.Read(File1Offset + 0x58 + 0x92, 2)));
            Assert.Equal(cluts[index], BinaryPrimitives.ReadUInt16LittleEndian(selected.Arena.Read(File1Offset + 0x58 + 0x9A, 2)));
            Assert.Equal(ArenaAddress + File0Offset + (uint)metadataPcs[index] + 4, BinaryPrimitives.ReadUInt32LittleEndian(selected.Core.Read(0x2797450, 4)));
        }
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicTextureMetadata.ResolvePixelB664A0(frame, resources, 4));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicTextureMetadata.ResolveClutB66560(frame, resources, 4));
    }

    [Fact]
    public void Pixel_zero_aliases_slot_eight_and_json_queue_drains_the_latest_arena()
    {
        using GameAssets game = Open();
        ImmutableArray<NativeBattleEffectInitializerAsset> files = NativeBattleEffectInitializerPolicy.Assets(Binding(game));
        NativeBattleCinematicResources resources = game.ReadNativeCinematicResources(231);
        NativeBattleWaterExecutionFrame frame = Frame(files, resources);
        NativeBattleCinematicTextureSelection pixels = NativeBattleCinematicTextureMetadata.ResolvePixelB664A0(frame, resources, 0);
        NativeBattleWaterSlotBank bank = new(File1Offset, files[1].Data.Length, 64, File1Offset + 0x58);
        frame = frame with { Arena = NativeBattleWaterSlots.InitializeB65160(frame.Arena, bank, 8, 128, 128, 0) };
        Assert.Equal(ArenaAddress + (uint)(bank.SlotBaseOffset + 8 * 0x100), pixels.SourceAddress);
        NativeBattleEffectTextureState queued = NativeBattleCinematicTextureMetadata.Enqueue505DF0(NativeBattleEffectTextureState.FromColdProcess(), frame, pixels);
        Assert.Null(queued.Slots[0]!.Source);
        Assert.Equal(File1Offset + 0x858, queued.Slots[0]!.ArenaOffset);
        frame = frame with { Arena = frame.Arena.Write(File1Offset + 0x858, [0x5A, 0xA5]).Write(File1Offset + 0x1036, [0xC3, 0x3C]) };
        NativeBattleEffectTextureState restored = JsonSerializer.Deserialize<NativeBattleEffectTextureState>(JsonSerializer.Serialize(queued))!;
        NativeBattleEffectTextureDrain drain = queued.Drain505D20(frame.Arena);
        Assert.Equal(JsonSerializer.Serialize(drain), JsonSerializer.Serialize(restored.Drain505D20(frame.Arena)));
        NativeBattleEffectVramUpload upload = Assert.Single(drain.Uploads);
        Assert.Equal(2016, upload.Bytes.Length);
        Assert.Equal(frame.Arena.Read(File1Offset + 0x858, 2016), upload.Bytes.ToArray());
        Assert.Equal(files[1].Data.ToArray(), NativeBattleEffectInitializerPolicy.Assets(Binding(game))[1].Data.ToArray());
    }

    [Fact]
    public void Selected_shapes_and_two_particle_sprites_keep_their_actual_record_bounds()
    {
        using GameAssets game = Open();
        ImmutableArray<byte> bytes = NativeBattleEffectInitializerPolicy.Assets(Binding(game))[0].Data;
        int directory = Dword(bytes, 12);
        Assert.Equal(0xA84, directory);
        Assert.Equal(4, Dword(bytes, directory));
        foreach ((int index, int offset, int vertices, int[] kinds, int[] counts) in new[]
        {
            (2, 0xDA8, 81, new[] { 18 }, new[] { 64 }),
            (3, 0x1668, 148, new[] { 18, 19 }, new[] { 68, 4 })
        })
        {
            int shape = directory + Dword(bytes, directory + 4 + 4 * index);
            Assert.Equal(offset, shape);
            Assert.Equal(3, Dword(bytes, shape));
            Assert.Equal(vertices, Dword(bytes, shape + 0x18));
            Assert.Equal(0, Dword(bytes, shape + 0x20));
            Assert.InRange(shape + Dword(bytes, shape + 0x14) + vertices * 8, 0, bytes.Length);
            int cursor = shape + Dword(bytes, shape + 8);
            for (int group = 0; group < kinds.Length; group++)
            {
                Assert.Equal((ushort)kinds[group], Word(bytes, cursor));
                Assert.Equal((ushort)counts[group], Word(bytes, cursor + 2));
                int stride = kinds[group] == 18 ? 24 : 36;
                int indices = kinds[group] == 18 ? 12 : 24;
                for (int face = 0; face < counts[group]; face++)
                {
                    for (int corner = 0; corner < 4; corner++)
                    {
                        int vertex = Word(bytes, cursor + 4 + face * stride + indices + 2 * corner);
                        Assert.Equal(0, vertex & 7);
                        Assert.InRange(vertex / 8, 0, vertices - 1);
                    }
                }
                cursor += 4 + counts[group] * stride;
            }
            Assert.Equal(ushort.MaxValue, Word(bytes, cursor + 2));
        }
        foreach ((int index, int offset, int frames, ushort page) in new[] { (0, 0xA98, 8, (ushort)0x38), (1, 0xBA0, 16, (ushort)0xD6) })
        {
            int sequence = directory + Dword(bytes, directory + 4 + 4 * index);
            Assert.Equal(offset, sequence);
            for (int frameIndex = 0; frameIndex < frames; frameIndex++)
            {
                int record = sequence + frameIndex * 8;
                Assert.Equal((ushort)1024, Word(bytes, record + 4));
                Assert.Equal((byte)0, bytes[record + 6]);
                int frame = directory + Dword(bytes, record);
                Assert.Equal(1, Dword(bytes, frame));
                Assert.InRange(frame + 24, 0, bytes.Length);
                Assert.Equal(page, Word(bytes, frame + 4 + 18));
            }
            Assert.Equal(ushort.MaxValue, Word(bytes, sequence + frames * 8 + 4));
            Assert.Equal((byte)1, bytes[sequence + frames * 8 + 6]);
        }
    }

    private static NativeBattleEffectBinding Binding(GameAssets game) => game.ReadNativeEffectCatalogForEffects([231])[230];
    private static int Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset, 2));
    private static NativeBattleWaterExecutionFrame Store(NativeBattleWaterExecutionFrame frame, uint address, uint value) => frame with { Core = frame.Core.Write(address, BitConverter.GetBytes(value)) };

    private static NativeBattleWaterExecutionFrame Frame(ImmutableArray<NativeBattleEffectInitializerAsset> files, NativeBattleCinematicResources resources)
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(File0Offset, files[0].Data.AsSpan()).Write(File1Offset, files[1].Data.AsSpan());
        NativeBattleWaterSlotBank bank = new(File1Offset, files[1].Data.Length, 64, File1Offset + 0x58);
        arena = NativeBattleWaterSlots.InitializeB65160(arena, bank, 0, 128, 128, 0);
        NativeBattleWaterExecutionFrame frame = new(NativeBattleScriptedEffectMemory.FromColdProcess(), arena, new(0x700000, new byte[0x180].ToImmutableArray(), Enumerable.Repeat(true, 0x180).ToImmutableArray()), new(7));
        frame = Store(frame, 0x27973EC, Manager);
        frame = Store(frame, 0x27973E8, ArenaAddress + File1Offset + 0x58);
        frame = Store(frame, 0x2798A68, ArenaAddress + File0Offset);
        frame = Store(frame, 0x2798A6C, ArenaAddress + File1Offset);
        frame = Store(frame, Manager + 0x98, resources.TextureProfile.PixelRectangleAddress);
        frame = Store(frame, Manager + 0x9C, resources.TextureProfile.ClutRectangleAddress);
        frame = Store(frame, Manager + 0xA4, resources.TextureProfile.PixelSourceMapAddress);
        return Store(frame, Manager + 0xA8, resources.TextureProfile.ClutSourceMapAddress);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select original Effect231 resources.");
        return new(directory!);
    }
}
