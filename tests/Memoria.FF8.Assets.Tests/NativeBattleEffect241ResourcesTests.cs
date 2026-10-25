using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect241ResourcesTests
{
    [Fact]
    public void Original_registration_preserves_named_files_and_counted_generic_directory()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = Binding(game);
        Assert.Equal(0xA01E10u, binding.Entry);
        Assert.Equal(0xA01DE0u, binding.Initializer);
        ImmutableArray<NativeBattleEffectInitializerAsset> files = NativeBattleEffectInitializerPolicy.Assets(binding);
        Assert.Equal(new[] { "mag240_b.00", "mag240_b.01" }, files.Select(file => file.Name));
        Assert.Equal(new[] { 7756, 143960 }, files.Select(file => file.Data.Length));
        NativeBattleCinematicResources resources = game.ReadNativeCinematicResources(241);
        resources.Validate();
        Assert.Same(resources, game.ReadNativeCinematicResources(241));
        Assert.Equal(5, resources.TextureProfile.Count);
        Assert.Null(resources.Vibration);
        Assert.Equal(new uint[] { 0x20, 0x48, 0x70, 0x78, 0x80, 0, 0x88 }, Enumerable.Range(0, 7).Select(index => resources.InitializationOffset(index * 4)));
        ImmutableArray<byte> bytes = files[0].Data;
        int directory = Dword(bytes, 12);
        Assert.Equal(0x800, directory);
        Assert.Equal(4, Dword(bytes, directory));
        Assert.Equal(new[] { 0x14, 0x294, 0xE78, 0x1398 }, Enumerable.Range(0, 4).Select(index => Dword(bytes, directory + 4 + 4 * index)));
        Assert.Equal(new byte[4], resources.ReadStatic(resources.TextureProfile.InitializationAddress + resources.InitializationOffset(0x10), 4).ToArray());
        game.ReadNativeCinematicInitializationDirectories(241).Validate();
    }

    [Fact]
    public void Selected_shapes_and_sprite_preserve_complete_record_domains()
    {
        using GameAssets game = Open();
        ImmutableArray<byte> bytes = NativeBattleEffectInitializerPolicy.Assets(Binding(game))[0].Data;
        NativeBattleCinematicResources resources = game.ReadNativeCinematicResources(241);
        int directory = Dword(bytes, 12);
        foreach ((int index, int expectedOffset, int vertices, int extent, int[] types, int[] counts) in new[]
        {
            (1, 0xA94, 97, 3044, new[] { 8, 18 }, new[] { 24, 72 }),
            (2, 0x1678, 49, 1312, new[] { 18 }, new[] { 36 }),
            (3, 0x1B98, 33, 692, new[] { 8, 18 }, new[] { 4, 12 })
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
                int stride = type == 8 ? 20 : 24;
                int corners = type == 8 ? 3 : 4;
                int indices = type == 8 ? 10 : 12;
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
        int sequence = directory + Dword(bytes, directory + 4);
        Assert.Equal(0x814, sequence);
        int maximumRectangles = 0;
        HashSet<(byte Selector, ushort Clut, ushort Page)> materials = [];
        for (int index = 0; index < 16; index++)
        {
            int record = sequence + index * 8;
            Assert.Equal((ushort)1024, Word(bytes, record + 4));
            Assert.Equal((byte)0, bytes[record + 6]);
            int frame = directory + Dword(bytes, record);
            int rectangles = Dword(bytes, frame) & int.MaxValue;
            Assert.InRange(rectangles, 1, 2);
            Assert.InRange(frame, 0x89C, 0xA94 - 4 - 20 * rectangles);
            maximumRectangles = Math.Max(maximumRectangles, rectangles);
            for (int rectangle = 0; rectangle < rectangles; rectangle++)
            {
                int offset = frame + 4 + 20 * rectangle;
                materials.Add((bytes[offset + 0x10], Word(bytes, offset + 0xE), Word(bytes, offset + 0x12)));
            }
        }
        Assert.Equal(2, maximumRectangles);
        Assert.Equal(new (byte, ushort, ushort)[] { (0x0D, 0x3FD4, 0xB6), (0x20, 0x3FD4, 0xB6), (0x40, 0x3FD4, 0xB6), (0x60, 0x3FD4, 0xB6), (0x80, 0x3F14, 0xB8), (0x80, 0x3FD4, 0xB6) }, materials.OrderBy(item => item.Selector).ThenBy(item => item.Clut).ThenBy(item => item.Page));
        Assert.Equal(-1, Dword(bytes, sequence + 16 * 8));
        Assert.Equal(ushort.MaxValue, Word(bytes, sequence + 16 * 8 + 4));
        Assert.Equal((byte)1, bytes[sequence + 16 * 8 + 6]);
    }

    [Fact]
    public void Five_texture_pairs_use_current_bytes_across_workspace_growth()
    {
        using GameAssets game = Open();
        NativeBattleCinematicResources resources = game.ReadNativeCinematicResources(241);
        ImmutableArray<NativeBattleEffectInitializerAsset> files = NativeBattleEffectInitializerPolicy.Assets(Binding(game));
        ImmutableArray<byte> original = files[1].Data;
        Assert.Equal(new byte[] { 0x81, 0x81, 0x81, 0x81, 0x81 }, resources.ReadStatic(resources.TextureProfile.PixelSourceMapAddress, 5).ToArray());
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
        NativeBattleEffectTextureRectangle[] pixels = [new(512, 256, 128, 256), new(384, 256, 64, 128), new(384, 384, 64, 128), new(640, 256, 32, 128), new(448, 256, 64, 256)];
        int[] pixelOffsets = [0x858, 0x10A58, 0x14C58, 0x18E58, 0x1B058];
        int[] paletteOffsets = [0x10858, 0x14A58, 0x18C58, 0x1AE58, 0x23058];
        int[] paletteRows = [252, 255, 254, 251, 253];
        for (int index = 0; index < 5; index++)
        {
            NativeBattleCinematicTextureSelection pixel = NativeBattleCinematicTextureMetadata.ResolvePixelB664A0(frame, resources, index);
            NativeBattleCinematicTextureSelection palette = NativeBattleCinematicTextureMetadata.ResolveClutB66560(frame, resources, index);
            Assert.Equal(pixels[index], pixel.Rectangle);
            Assert.Equal(new NativeBattleEffectTextureRectangle(320, checked((short)paletteRows[index]), 256, 1), palette.Rectangle);
            Assert.Equal(arenaAddress + file1Offset + (uint)pixelOffsets[index], pixel.SourceAddress);
            Assert.Equal(arenaAddress + file1Offset + (uint)paletteOffsets[index], palette.SourceAddress);
            Assert.Equal((byte)0x81, pixel.SourceMapByte);
            Assert.Equal((byte)1, palette.SourceMapByte);
            Assert.InRange(pixelOffsets[index] + pixel.Rectangle.Width * pixel.Rectangle.Height * 2, 0, original.Length);
        }
        Assert.Equal(original.Length, paletteOffsets[4] + 512);
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
        Assert.Equal(original.ToArray(), NativeBattleEffectInitializerPolicy.Assets(Binding(game))[1].Data.ToArray());
        Assert.Throws<NotSupportedException>(() => resources.Rectangle(5, false));
    }

    [Fact]
    public void Camera_catalogue_parses_static_tracks_without_forcing_control_variables()
    {
        using GameAssets game = Open();
        ImmutableArray<byte> bytes = NativeBattleEffectInitializerPolicy.Assets(Binding(game))[0].Data;
        Assert.Equal(0x6F0, Dword(bytes, 0x20));
        NativeBattleEffectHeapAllocation camera = new("effect-camera", bytes.AsSpan().Slice(0x6F0).ToArray().ToImmutableArray());
        NativeBattleEffectAnimationRegisters animation = NativeBattleEffectTextureState.FromColdProcess().Register5099A0(camera).Animation!;
        Assert.Equal(8, animation.StreamOffset);
        Assert.Equal(0x28, animation.ModelTableOffset);
        HashSet<byte> variables = [];
        int cursor = animation.StreamOffset;
        while (camera.Data[cursor] >= 0xC0)
        {
            byte opcode = camera.Data[cursor++];
            if (opcode < 0xE4)
            {
                if ((opcode & 3) == 3)
                {
                    variables.Add(camera.Data[cursor]);
                }
                cursor += (opcode & 3) == 0 ? 2 : 1;
            }
            else
            {
                Assert.InRange(opcode, (byte)0xE5, (byte)0xF3);
                cursor += opcode < 0xED ? 1 : 2;
            }
        }
        Assert.Equal(new byte[] { 0, 22, 127 }, variables.Order());
        NativeBattleCameraBank bank = new(camera.Data.AsSpan().Slice(animation.ModelTableOffset).ToArray().ToImmutableArray());
        foreach ((byte selector, int offset) in new[] { ((byte)0, 0x18), ((byte)4, 0x80) })
        {
            Assert.Equal(offset, bank.TrackOffset(selector));
            Assert.Equal((ushort)0x3C1, Word(bank.Data, offset));
            Assert.Equal(ushort.MaxValue, Word(bank.Data, offset + 96));
            NativeBattleCameraPoseInputs inputs = new() { TransformReference = (_, point) => point };
            NativeBattleCameraState state = (NativeBattleCameraState.Initialize() with { CurrentPose = new(new(17, 31, 47), new(61, 79, 97), 512, 0) }).Start(bank, selector);
            int ticks = 0;
            while (!state.Order.IsEmpty && ticks++ < 2048)
            {
                state = state.Tick(0, inputs);
            }
            Assert.Empty(state.Order);
            Assert.InRange(ticks, 1, 2048);
            Assert.NotNull(state.CurrentPose);
        }
        Assert.Null(bank.TrackOffset(0x10));
    }

    private static NativeBattleEffectBinding Binding(GameAssets game) => game.ReadNativeEffectCatalogForEffects([241])[240];
    private static int Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset, 2));
    private static NativeBattleWaterExecutionFrame Store(NativeBattleWaterExecutionFrame frame, uint address, uint value) => frame with { Core = frame.Core.Write(address, BitConverter.GetBytes(value)) };

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select original Effect241 resources.");
        return new(directory!);
    }
}
