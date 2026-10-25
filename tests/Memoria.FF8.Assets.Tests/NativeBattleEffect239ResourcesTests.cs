using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect239ResourcesTests
{
    [Fact]
    public void Original_registration_supplies_current_named_files_and_default_io_resources()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = Binding(game);
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(239);
        Assert.Equal(0xA15A30u, binding.Entry);
        Assert.Equal(0xA15A00u, binding.Initializer);
        Assert.Equal(0xA1B840u, profile.LoadCallback);
        Assert.Equal(0x233, profile.SourceBase);
        Assert.Equal(0x232, profile.LastSource);
        Assert.False(profile.HasVibrationDirectory);
        ImmutableArray<NativeBattleEffectInitializerAsset> files = NativeBattleEffectInitializerPolicy.Assets(binding);
        Assert.Equal(new[] { "mag238_b.00", "mag238_b.01" }, files.Select(file => file.Name));
        Assert.Equal(new[] { 22984, 170592 }, files.Select(file => file.Data.Length));
        Assert.Equal(new uint[] { 0x27968D4, 0x27968D0 }, NativeBattleEffectInitializerPolicy.Resources(binding).Select(item => item.GlobalAddress));
        NativeBattleCinematicResources resources = game.ReadNativeCinematicResources(239);
        resources.Validate();
        Assert.Same(resources, game.ReadNativeCinematicResources(239));
        Assert.Null(resources.Vibration);
        Assert.Equal(new uint[] { 0x20, 0x50, 0x80, 0x88, 0x90, 0, 0x98 }, Enumerable.Range(0, 7).Select(index => resources.InitializationOffset(index * 4)));
        Assert.Equal(new ushort[] { 363, 364, 365 }, resources.Files.Select(file => file.FileId));
        Assert.Equal(new[] { "ma8def_p.0", "ma8def_p.1", "ma8def_p.2" }, resources.Files.Select(file => file.Name));
        Assert.All(resources.Files, file => Assert.Equal(32768, file.Bytes.Length));
        game.ReadNativeCinematicInitializationDirectories(239).Validate();
        string json = JsonSerializer.Serialize(resources);
        NativeBattleCinematicResources restored = JsonSerializer.Deserialize<NativeBattleCinematicResources>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Throws<InvalidDataException>(() => (restored with { TextureProfile = restored.TextureProfile with { PixelRectangleAddress = restored.TextureProfile.PixelRectangleAddress + 1 } }).Validate());
    }

    [Fact]
    public void Selected_shapes_and_finite_sprite_catalogues_preserve_complete_record_extents()
    {
        using GameAssets game = Open();
        ImmutableArray<byte> bytes = NativeBattleEffectInitializerPolicy.Assets(Binding(game))[0].Data;
        int directory = Dword(bytes, 12);
        Assert.Equal(0xC50, directory);
        Assert.Equal(5, Dword(bytes, directory));
        foreach ((int index, int offset, int vertices, int extent, int kind, int faces, int stride) in new[]
        {
            (3, 0x1300, 91, 3984, 8, 160, 20),
            (4, 0x2290, 132, 5432, 19, 120, 36)
        })
        {
            int shape = directory + Dword(bytes, directory + 4 + 4 * index);
            Assert.Equal(offset, shape);
            Assert.Equal(vertices, Dword(bytes, shape + 0x18));
            Assert.Equal(0, Dword(bytes, shape + 0x20));
            Assert.Equal(extent, Dword(bytes, shape + 0x14) + vertices * 8);
            int primitives = shape + Dword(bytes, shape + 8);
            Assert.Equal(shape + 0x30, primitives);
            Assert.Equal((ushort)kind, Word(bytes, primitives));
            Assert.Equal((ushort)faces, Word(bytes, primitives + 2));
            int terminal = primitives + 4 + faces * stride;
            Assert.Equal(ushort.MaxValue, Word(bytes, terminal + 2));
            Assert.InRange(terminal + 4, shape, shape + extent);
            Assert.Equal(vertices * 8, bytes.AsSpan().Slice(shape + Dword(bytes, shape + 0x14), vertices * 8).Length);
            if (kind == 8)
            {
                for (int face = 0; face < faces; face++)
                {
                    for (int corner = 0; corner < 3; corner++)
                    {
                        ushort vertex = Word(bytes, primitives + 4 + face * stride + 10 + 2 * corner);
                        Assert.Equal(0, vertex & 7);
                        Assert.InRange(vertex, 0, vertices * 8 - 8);
                    }
                }
            }
        }
        foreach ((int index, int sequenceOffset, int frameCount, byte finalCommand) in new[]
        {
            (0, 0xC68, 16, (byte)1),
            (1, 0xE70, 16, (byte)1),
            (2, 0x1078, 20, (byte)2)
        })
        {
            int sequence = directory + Dword(bytes, directory + 4 + 4 * index);
            Assert.Equal(sequenceOffset, sequence);
            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                int record = sequence + frameIndex * 8;
                Assert.Equal((ushort)1024, Word(bytes, record + 4));
                Assert.Equal(index == 2 && frameIndex == 19 ? (byte)2 : (byte)0, bytes[record + 6]);
                int frame = directory + Dword(bytes, record);
                Assert.Equal(1, Dword(bytes, frame) & int.MaxValue);
                Assert.InRange(frame + 24, 0, bytes.Length);
                Assert.Equal(index == 0 ? (ushort)0x3B : index == 1 ? (ushort)0xB6 : (ushort)0x38, Word(bytes, frame + 4 + 0x12));
                Assert.Equal(index == 0 ? (ushort)0x3F14 : index == 1 ? (ushort)0x3FD4 : (ushort)0x3F15, Word(bytes, frame + 4 + 0xE));
            }
            int final = sequence + (index == 2 ? 19 : frameCount) * 8;
            Assert.Equal(finalCommand, bytes[final + 6]);
            if (index != 2)
            {
                Assert.Equal(-1, Dword(bytes, final));
                Assert.Equal(ushort.MaxValue, Word(bytes, final + 4));
            }
        }
    }

    [Fact]
    public void Mixed_texture_sources_retain_mutable_programme_overlap_and_current_arena_uploads()
    {
        using GameAssets game = Open();
        NativeBattleCinematicResources resources = game.ReadNativeCinematicResources(239);
        ImmutableArray<NativeBattleEffectInitializerAsset> files = NativeBattleEffectInitializerPolicy.Assets(Binding(game));
        Assert.Equal(6, resources.TextureProfile.Count);
        Assert.Equal(new byte[] { 0x81, 1, 0x80, 0x81, 1 }, resources.ReadStatic(resources.TextureProfile.PixelSourceMapAddress, 5).ToArray());
        Assert.Equal(new byte[] { 1, 1, 1, 1, 1 }, resources.ReadStatic(resources.TextureProfile.ClutSourceMapAddress, 5).ToArray());
        Assert.Equal(0x60, Dword(files[1].Data, 0x1C));
        const int file0 = 0x1000;
        const int file1 = 0x10000;
        const uint arena = 0x20DFAB8;
        const uint manager = 0x2797690;
        const uint header = 0x2797068;
        NativeBattleWaterExecutionFrame frame = new(NativeBattleScriptedEffectMemory.FromColdProcess(), NativeBattleEffectArena.Clear().Write(file0, files[0].Data.AsSpan()).Write(file1, files[1].Data.AsSpan()), new(0x700000, new byte[0x180].ToImmutableArray(), Enumerable.Repeat(true, 0x180).ToImmutableArray()), new(7));
        frame = Store(frame, 0x27973EC, manager);
        frame = Store(frame, 0x279744C, header);
        frame = Store(frame, 0x27973E8, arena + file1 + 0x60);
        frame = Store(frame, 0x2798A68, arena + file0);
        frame = Store(frame, 0x2798A6C, arena + file1);
        frame = Store(frame, manager + 0x90, arena + file1 + 0x60);
        frame = Store(frame, manager + 0x98, resources.TextureProfile.PixelRectangleAddress);
        frame = Store(frame, manager + 0x9C, resources.TextureProfile.ClutRectangleAddress);
        frame = Store(frame, manager + 0xA4, resources.TextureProfile.PixelSourceMapAddress);
        frame = Store(frame, manager + 0xA8, resources.TextureProfile.ClutSourceMapAddress);
        NativeBattleEffectTextureRectangle[] pixels = [new(576, 256, 128, 256), new(704, 256, 64, 256), new(512, 256, 32, 128), new(384, 256, 128, 256), new(544, 256, 16, 64)];
        int[] pixelOffsets = [0x860, 0x10860, 0x37C8, 0x18860, 0x28860];
        int[] paletteOffsets = [0x29060, 0x29260, 0x29460, 0x29660, 0x29860];
        int[] paletteRows = [253, 252, 254, 255, 252];
        for (int index = 0; index < 5; index++)
        {
            int sourceFile = index == 2 ? 0 : 1;
            NativeBattleCinematicTextureSelection pixel = NativeBattleCinematicTextureMetadata.ResolvePixelB664A0(frame, resources, index);
            NativeBattleCinematicTextureSelection clut = NativeBattleCinematicTextureMetadata.ResolveClutB66560(frame, resources, index);
            Assert.Equal(pixels[index], pixel.Rectangle);
            Assert.Equal(arena + (uint)(sourceFile == 0 ? file0 : file1) + (uint)pixelOffsets[index], pixel.SourceAddress);
            Assert.Equal(arena + file1 + (uint)paletteOffsets[index], clut.SourceAddress);
            Assert.Equal(new NativeBattleEffectTextureRectangle(index == 4 ? (short)336 : (short)320, (short)paletteRows[index], index is 1 or 4 ? (short)16 : (short)256, 1), clut.Rectangle);
            Assert.Equal(pixel.Rectangle.Width * pixel.Rectangle.Height * 2, frame.Arena.Read(checked((int)(pixel.SourceAddress - arena)), pixel.Rectangle.Width * pixel.Rectangle.Height * 2).Length);
        }
        Assert.Equal((byte)0xFF, resources.ReadStatic(resources.TextureProfile.PixelSourceMapAddress + 5, 1)[0]);
        NativeBattleCinematicTextureSelection bodyPalette = NativeBattleCinematicTextureMetadata.ResolveClutB66560(frame, resources, 5);
        Assert.Equal(new NativeBattleEffectTextureRectangle(320, 240, 256, 1), bodyPalette.Rectangle);
        Assert.Equal((byte)0, bodyPalette.SourceMapByte);
        Assert.Equal(arena + file0 + 0x57C8u, bodyPalette.SourceAddress);
        Assert.Equal(resources.TextureProfile.ClutRectangleAddress + 5 * 8u, bodyPalette.RectangleAddress);
        Assert.Equal(22984, 0x57C8 + 256 * 2);
        Assert.Equal(files[0].Data.AsSpan().Slice(0x57C8, 512).ToArray(), frame.Arena.Read(file0 + 0x57C8, 512));
        frame = frame with { Arena = frame.Arena.Write(file0 + 0x57C8, new byte[] { 0x34, 0x12 }) };
        Assert.Equal((ushort)0x1234, BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(checked((int)(NativeBattleCinematicTextureMetadata.ResolveClutB66560(frame, resources, 5).SourceAddress - arena)), 2)));
        frame = frame with { Core = frame.Core.Write(header + 0x38, BitConverter.GetBytes((ushort)8)) };
        frame = Store(frame, 0x2797450, arena + file0 + 0x8C).WriteTemporaryWord(0x14A, 0x7D);
        frame = NativeBattleWaterWorkCommands.Execute(frame, new(file0, files[0].Data.Length, arena + file0));
        Assert.Equal((ushort)32, BinaryPrimitives.ReadUInt16LittleEndian(frame.Core.Read(header + 0x38, 2)));
        Assert.Equal(0x1800, 0x60 + 32 * 0x100 - pixelOffsets[0]);
        for (int slot = 8; slot < 32; slot++)
        {
            int offset = 0x60 + slot * 0x100;
            Assert.Equal(new byte[4], frame.Arena.Read(file1 + offset, 4));
            Assert.Equal(files[1].Data.AsSpan().Slice(offset + 4, 0xFC).ToArray(), frame.Arena.Read(file1 + offset + 4, 0xFC));
        }
        frame = frame with { Arena = frame.Arena.Write(file1 + 0x864, new byte[] { 1, 2, 3, 4 }) };
        NativeBattleWaterSlotBank bank = new(file1, files[1].Data.Length, 32, file1 + 0x60);
        frame = frame with { Arena = NativeBattleWaterSlots.InitializeB65160(frame.Arena, bank, 8, 1, 1, 0) };
        Assert.Equal(new byte[4], frame.Arena.Read(file1 + 0x864, 4));
        NativeBattleCinematicTextureSelection current = NativeBattleCinematicTextureMetadata.ResolvePixelB664A0(frame, resources, 0);
        NativeBattleEffectTextureState queued = NativeBattleCinematicTextureMetadata.Enqueue505DF0(NativeBattleEffectTextureState.FromColdProcess(), frame, current);
        Assert.Equal(file1 + pixelOffsets[0], queued.Slots[0]!.ArenaOffset);
        Assert.Null(queued.Slots[0]!.Source);
        Assert.Equal(files[1].Data.ToArray(), NativeBattleEffectInitializerPolicy.Assets(Binding(game))[1].Data.ToArray());
        Assert.Throws<NotSupportedException>(() => resources.Rectangle(6, false));
    }

    [Fact]
    public void Camera_control_and_single_track_parse_without_forcing_variables()
    {
        using GameAssets game = Open();
        ImmutableArray<byte> bytes = NativeBattleEffectInitializerPolicy.Assets(Binding(game))[0].Data;
        Assert.Equal(0xB50, Dword(bytes, 0x20));
        NativeBattleEffectHeapAllocation camera = new("effect-camera", bytes.AsSpan().Slice(0xB50).ToArray().ToImmutableArray());
        NativeBattleEffectAnimationRegisters animation = NativeBattleEffectTextureState.FromColdProcess().Register5099A0(camera).Animation!;
        Assert.Equal(8, animation.StreamOffset);
        Assert.Equal(0x18, animation.ModelTableOffset);
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
        Assert.Equal(new byte[] { 0, 127 }, variables.Order());
        NativeBattleCameraBank bank = new(camera.Data.AsSpan().Slice(animation.ModelTableOffset).ToArray().ToImmutableArray());
        Assert.Equal(0x18, bank.TrackOffset(0));
        Assert.Equal((ushort)0x3C1, Word(bank.Data, 0x18));
        Assert.Equal(ushort.MaxValue, Word(bank.Data, 0x18 + 96));
        NativeBattleCameraPoseInputs inputs = new() { TransformReference = (_, point) => point };
        NativeBattleCameraState state = (NativeBattleCameraState.Initialize() with { CurrentPose = new(new(17, 31, 47), new(61, 79, 97), 512, 0) }).Start(bank, 0);
        int ticks = 0;
        while (!state.Order.IsEmpty && ticks++ < 2048)
        {
            state = state.Tick(0, inputs);
        }
        Assert.Empty(state.Order);
        Assert.InRange(ticks, 1, 2048);
    }

    private static NativeBattleEffectBinding Binding(GameAssets game) => game.ReadNativeEffectCatalogForEffects([239])[238];
    private static int Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset, 2));
    private static NativeBattleWaterExecutionFrame Store(NativeBattleWaterExecutionFrame frame, uint address, uint value) => frame with { Core = frame.Core.Write(address, BitConverter.GetBytes(value)) };

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select original Effect239 resources.");
        return new(directory!);
    }
}
