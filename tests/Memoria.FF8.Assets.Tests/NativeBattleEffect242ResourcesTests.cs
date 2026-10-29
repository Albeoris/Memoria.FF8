using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect242ResourcesTests
{
    [Fact]
    public void Original_registration_preserves_named_files_and_counted_generic_directory()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = Binding(game);
        Assert.Equal(0x9F7D10u, binding.Entry);
        Assert.Equal(0x9F7CE0u, binding.Initializer);
        ImmutableArray<NativeBattleEffectInitializerAsset> files = NativeBattleEffectInitializerPolicy.Assets(binding);
        Assert.Equal(new[] { "mag241_b.00", "mag241_b.01" }, files.Select(file => file.Name));
        Assert.Equal(new[] { 29736, 129624 }, files.Select(file => file.Data.Length));
        NativeBattleCinematicResources resources = game.ReadNativeCinematicResources(242);
        resources.Validate();
        Assert.Same(resources, game.ReadNativeCinematicResources(242));
        Assert.Equal(5, resources.TextureProfile.Count);
        Assert.Null(resources.Vibration);
        Assert.Equal(new uint[] { 0x20, 0x48, 0x70, 0x78, 0x80, 0, 0x8C }, Enumerable.Range(0, 7).Select(index => resources.InitializationOffset(index * 4)));
        ImmutableArray<byte> bytes = files[0].Data;
        int directory = Dword(bytes, 12);
        Assert.Equal(0xEC0, directory);
        Assert.Equal(9, Dword(bytes, directory));
        Assert.Equal(new[] { 0x28, 0x230, 0x438, 0x2B48, 0x372C, 0x4A84, 0x4FA4, 0x5588, 0x5870 }, Enumerable.Range(0, 9).Select(index => Dword(bytes, directory + 4 + 4 * index)));
        Assert.Equal(new byte[4], resources.ReadStatic(resources.TextureProfile.InitializationAddress + resources.InitializationOffset(0x10), 4).ToArray());
        game.ReadNativeCinematicInitializationDirectories(242).Validate();
    }

    [Fact]
    public void Selected_meshes_preserve_all_face_vertex_bounds_and_terminal_markers()
    {
        using GameAssets game = Open();
        ImmutableArray<byte> bytes = NativeBattleEffectInitializerPolicy.Assets(Binding(game))[0].Data;
        int directory = Dword(bytes, 12);
        foreach ((int index, int expectedOffset, int vertices, int extent, int[] types, int[] counts) in new[]
        {
            (2, 0x12F8, 366, 10000, new[] { 6, 7, 17 }, new[] { 4, 12, 280 }),
            (3, 0x3A08, 73, 3044, new[] { 9, 19 }, new[] { 24, 48 }),
            (4, 0x45EC, 180, 4952, new[] { 18 }, new[] { 144 }),
            (5, 0x5944, 49, 1312, new[] { 18 }, new[] { 36 }),
            (6, 0x5E64, 49, 1508, new[] { 7, 17 }, new[] { 24, 24 }),
            (7, 0x6448, 25, 744, new[] { 6, 7, 17 }, new[] { 4, 12, 8 }),
            (8, 0x6730, 120, 3320, new[] { 17 }, new[] { 96 })
        })
        {
            int shape = directory + Dword(bytes, directory + 4 + 4 * index);
            Assert.Equal(expectedOffset, shape);
            Assert.Equal(vertices, Dword(bytes, shape + 0x18));
            Assert.Equal(0, Dword(bytes, shape + 0x20));
            Assert.Equal(extent, Dword(bytes, shape + 0x14) + vertices * 8);
            Assert.Equal(0x30, Dword(bytes, shape + 8));
            int cursor = shape + Dword(bytes, shape + 8);
            for (int group = 0; group < types.Length; group++)
            {
                int type = types[group];
                Assert.Equal((ushort)type, Word(bytes, cursor));
                Assert.Equal((ushort)counts[group], Word(bytes, cursor + 2));
                (int stride, int corners, int indices) = type switch
                {
                    6 => (12, 3, 4),
                    7 => (20, 3, 12),
                    9 => (28, 3, 18),
                    17 => (24, 4, 16),
                    18 => (24, 4, 12),
                    19 => (36, 4, 24),
                    _ => throw new InvalidDataException("Unsupported selected primitive.")
                };
                for (int face = 0; face < counts[group]; face++)
                {
                    int record = cursor + 4 + face * stride;
                    Assert.InRange(record + stride, shape, shape + extent);
                    for (int corner = 0; corner < corners; corner++)
                    {
                        ushort vertex = Word(bytes, record + indices + 2 * corner);
                        Assert.Equal(0, vertex & 7);
                        Assert.InRange(vertex, 0, vertices * 8 - 8);
                    }
                }
                cursor += 4 + counts[group] * stride;
            }
            Assert.Equal(ushort.MaxValue, Word(bytes, cursor + 2));
        }
    }

    [Fact]
    public void Both_sprite_sequences_end_before_dereferencing_the_terminal_pointer()
    {
        using GameAssets game = Open();
        ImmutableArray<byte> bytes = NativeBattleEffectInitializerPolicy.Assets(Binding(game))[0].Data;
        int directory = Dword(bytes, 12);
        foreach ((int index, int origin, ushort page, ushort clut) in new[] { (0, 0xEE8, (ushort)0xB8, (ushort)0x3F14), (1, 0x10F0, (ushort)0x36, (ushort)0x3FE4) })
        {
            int sequence = directory + Dword(bytes, directory + 4 + 4 * index);
            Assert.Equal(origin, sequence);
            for (int indexInSequence = 0; indexInSequence < 16; indexInSequence++)
            {
                int record = sequence + indexInSequence * 8;
                Assert.Equal((byte)0, bytes[record + 6]);
                Assert.Equal((ushort)1024, Word(bytes, record + 4));
                int frame = directory + Dword(bytes, record);
                Assert.Equal(1, Dword(bytes, frame));
                Assert.InRange(frame + 24, 0, bytes.Length);
                Assert.Equal(clut, Word(bytes, frame + 4 + 0xE));
                Assert.Equal((byte)0x80, bytes[frame + 4 + 0x10]);
                Assert.Equal(page, Word(bytes, frame + 4 + 0x12));
            }
            int terminal = sequence + 16 * 8;
            Assert.Equal((byte)1, bytes[terminal + 6]);
            Assert.Equal(ushort.MaxValue, Word(bytes, terminal + 4));
        }
    }

    [Fact]
    public void Five_texture_pairs_use_current_bytes_across_workspace_growth()
    {
        using GameAssets game = Open();
        NativeBattleCinematicResources resources = game.ReadNativeCinematicResources(242);
        ImmutableArray<NativeBattleEffectInitializerAsset> files = NativeBattleEffectInitializerPolicy.Assets(Binding(game));
        ImmutableArray<byte> original = files[1].Data;
        Assert.Equal(new byte[] { 0x81, 0x81, 0x81, 0x81, 0x01 }, resources.ReadStatic(resources.TextureProfile.PixelSourceMapAddress, 5).ToArray());
        Assert.Equal(new byte[] { 1, 1, 1, 1, 1 }, resources.ReadStatic(resources.TextureProfile.ClutSourceMapAddress, 5).ToArray());
        Assert.Equal(0x30, Dword(original, 0x14));
        Assert.Equal(0x44, Dword(original, 8));
        const int file0Offset = 0x1000;
        const int file1Offset = 0x10000;
        const uint arenaAddress = 0x20DFAB8;
        const uint manager = 0x2797690;
        const uint header = 0x2797068;
        NativeBattleWaterExecutionFrame frame = new(NativeBattleScriptedEffectMemory.FromColdProcess(), NativeBattleEffectArena.Clear().Write(file0Offset, files[0].Data.ToArray()).Write(file1Offset, original.ToArray()), new(0x700000, new byte[0x180].ToImmutableArray(), Enumerable.Repeat(true, 0x180).ToImmutableArray()), new(7));
        frame = Store(frame, 0x27973EC, manager);
        frame = Store(frame, 0x279744C, header);
        frame = Store(frame, 0x27973E8, arenaAddress + file1Offset + 0x58);
        frame = Store(frame, 0x2798A6C, arenaAddress + file1Offset);
        frame = Store(frame, manager + 0x90, arenaAddress + file1Offset + 0x58);
        frame = Store(frame, manager + 0x98, resources.TextureProfile.PixelRectangleAddress);
        frame = Store(frame, manager + 0x9C, resources.TextureProfile.ClutRectangleAddress);
        frame = Store(frame, manager + 0xA4, resources.TextureProfile.PixelSourceMapAddress);
        frame = Store(frame, manager + 0xA8, resources.TextureProfile.ClutSourceMapAddress);
        NativeBattleEffectTextureRectangle[] pixels = [new(384, 256, 32, 128), new(512, 256, 128, 256), new(448, 256, 64, 256), new(384, 384, 64, 128), new(416, 256, 16, 64)];
        int[] pixelOffsets = [0x858, 0x2a58, 0x12c58, 0x1ae58, 0x1f058];
        int[] paletteOffsets = [0x2858, 0x12a58, 0x1ac58, 0x1ee58, 0x1f858];
        int[] paletteRows = [255, 252, 254, 253, 255];
        for (int index = 0; index < 5; index++)
        {
            NativeBattleCinematicTextureSelection pixel = NativeBattleCinematicTextureMetadata.ResolvePixelB664A0(frame, resources, index);
            NativeBattleCinematicTextureSelection palette = NativeBattleCinematicTextureMetadata.ResolveClutB66560(frame, resources, index);
            Assert.Equal(pixels[index], pixel.Rectangle);
            Assert.Equal(new NativeBattleEffectTextureRectangle(index == 4 ? (short)576 : (short)320, checked((short)paletteRows[index]), index == 4 ? (short)16 : (short)256, 1), palette.Rectangle);
            Assert.Equal(arenaAddress + file1Offset + (uint)pixelOffsets[index], pixel.SourceAddress);
            Assert.Equal(arenaAddress + file1Offset + (uint)paletteOffsets[index], palette.SourceAddress);
            Assert.Equal(index == 4 ? (byte)1 : (byte)0x81, pixel.SourceMapByte);
            Assert.Equal((byte)1, palette.SourceMapByte);
            Assert.InRange(pixelOffsets[index] + pixel.Rectangle.Width * pixel.Rectangle.Height * 2, 0, original.Length);
        }
        Assert.Equal(480, original.Length - paletteOffsets[4] - 32);
        frame = frame with { Core = frame.Core.Write(header + 0x38, BitConverter.GetBytes((ushort)8)) };
        frame = Store(frame, 0x2797450, arenaAddress + file0Offset + 0x84).WriteTemporaryWord(0x14A, 0x7D);
        frame = NativeBattleWaterWorkCommands.Execute(frame, new(file0Offset, files[0].Data.Length, arenaAddress + file0Offset));
        Assert.Equal((ushort)64, BinaryPrimitives.ReadUInt16LittleEndian(frame.Core.Read(header + 0x38, 2)));
        for (int slot = 8; slot < 64; slot++)
        {
            int offset = 0x58 + slot * 0x100;
            Assert.Equal(new byte[4], frame.Arena.Read(file1Offset + offset, 4));
            Assert.Equal(original.AsSpan().Slice(offset + 4, 0xFC).ToArray(), frame.Arena.Read(file1Offset + offset + 4, 0xFC));
        }
        Assert.Equal(0x3800, 0x58 + 64 * 0x100 - pixelOffsets[0]);
        NativeBattleWaterSlotBank slots = new(file1Offset, original.Length, 64, file1Offset + 0x58);
        frame = frame with { Arena = NativeBattleWaterSlots.InitializeB65160(frame.Arena, slots, 8, 1, 1, 0) };
        NativeBattleCinematicTextureSelection current = NativeBattleCinematicTextureMetadata.ResolvePixelB664A0(frame, resources, 0);
        NativeBattleEffectTextureState queued = NativeBattleCinematicTextureMetadata.Enqueue505DF0(NativeBattleEffectTextureState.FromColdProcess(), frame, current);
        Assert.Equal(file1Offset + pixelOffsets[0], queued.Slots[0]!.ArenaOffset);
        Assert.Null(queued.Slots[0]!.Source);
        int mutableSource = file1Offset + pixelOffsets[0];
        frame = frame with { Arena = frame.Arena.Write(mutableSource, new byte[] { 0x5A, 0xA5 }).Write(mutableSource + 8190, new byte[] { 0xC3, 0x3C }) };
        NativeBattleEffectTextureState restoredQueue = NativeBattleSnapshotJson.Deserialize<NativeBattleEffectTextureState>(NativeBattleSnapshotJson.Serialize(queued))!;
        NativeBattleWaterExecutionFrame restoredFrame = NativeBattleSnapshotJson.Deserialize<NativeBattleWaterExecutionFrame>(NativeBattleSnapshotJson.Serialize(frame))!;
        NativeBattleEffectTextureDrain drain = queued.Drain505D20(frame.Arena);
        NativeBattleEffectTextureDrain restoredDrain = restoredQueue.Drain505D20(restoredFrame.Arena);
        NativeBattleEffectVramUpload upload = Assert.Single(drain.Uploads);
        Assert.Equal(frame.Arena.Read(mutableSource, 8192), upload.Bytes.ToArray());
        Assert.Equal(NativeBattleSnapshotJson.Serialize(drain), NativeBattleSnapshotJson.Serialize(restoredDrain));
        Assert.Equal(0, drain.State.Count);
        Assert.Equal(new byte[] { 0x5A, 0xA5 }, upload.Bytes.Take(2));
        Assert.Equal(new byte[] { 0xC3, 0x3C }, upload.Bytes.TakeLast(2));
        Assert.Equal(original.ToArray(), NativeBattleEffectInitializerPolicy.Assets(Binding(game))[1].Data.ToArray());
        Assert.Throws<NotSupportedException>(() => resources.Rectangle(5, false));
    }

    private static NativeBattleEffectBinding Binding(GameAssets game) => game.ReadNativeEffectCatalogForEffects([242])[241];
    private static int Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset, 2));
    private static NativeBattleWaterExecutionFrame Store(NativeBattleWaterExecutionFrame frame, uint address, uint value) => frame with { Core = frame.Core.Write(address, BitConverter.GetBytes(value)) };

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select original Effect242 resources.");
        return new(directory!);
    }
}
