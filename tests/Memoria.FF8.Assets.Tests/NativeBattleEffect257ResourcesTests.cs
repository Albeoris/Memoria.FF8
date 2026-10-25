using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect257ResourcesTests
{
    private const int File0Offset = 0x1000;
    private const int File1Offset = 0x10000;
    private const uint ArenaAddress = 0x20DFAB8;
    private const uint Manager = 0x2797690;

    [Fact]
    public void Original_registration_retains_both_resources_and_three_complete_common_files()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([257])[256];
        Assert.Equal(0x96F290u, binding.Entry);
        Assert.Equal(0x96F260u, binding.Initializer);
        ImmutableArray<NativeBattleEffectInitializerAsset> files = NativeBattleEffectInitializerPolicy.Assets(binding);
        Assert.Equal(new[] { "mag256_b.00", "mag256_b.01" }, files.Select(file => file.Name));
        Assert.Equal(new[] { 26092, 227440 }, files.Select(file => file.Data.Length));
        NativeBattleCinematicResources resources = game.ReadNativeCinematicResources(257);
        resources.Validate();
        Assert.Same(resources, game.ReadNativeCinematicResources(257));
        Assert.Null(resources.Vibration);
        Assert.Equal(8, resources.TextureProfile.Count);
        Assert.Equal(new uint[] { 0x20, 0x60, 0xA0, 0xAC, 0xB8, 0, 0xC8 }, Enumerable.Range(0, 7).Select(index => resources.InitializationOffset(index * 4)));
        Assert.Equal(new ushort[] { 363, 364, 365 }, resources.Files.Select(file => file.FileId));
        Assert.Equal(new[] { "ma8def_p.0", "ma8def_p.1", "ma8def_p.2" }, resources.Files.Select(file => file.Name));
        Assert.All(resources.Files, file => Assert.Equal(32768, file.Bytes.Length));
        Assert.Equal(0x112C, Dword(files[0].Data, 12));
        Assert.Equal(14, Dword(files[0].Data, 0x112C));
        Assert.Equal(0, Dword(files[0].Data, 0x24));
        game.ReadNativeCinematicInitializationDirectories(257).Validate();
    }

    [Fact]
    public void Texture_metadata_preserves_three_row_palette_and_deferred_current_arena_bytes()
    {
        using GameAssets game = Open();
        ImmutableArray<NativeBattleEffectInitializerAsset> files = NativeBattleEffectInitializerPolicy.Assets(game.ReadNativeEffectCatalogForEffects([257])[256]);
        NativeBattleCinematicResources resources = game.ReadNativeCinematicResources(257);
        NativeBattleWaterExecutionFrame frame = Frame(files, resources);
        Assert.Equal(new byte[] { 0x81, 1, 1, 0x81, 0x81, 0x81, 0x81, 0x81 }, resources.ReadStatic(resources.TextureProfile.PixelSourceMapAddress, 8).ToArray());
        Assert.Equal(Enumerable.Repeat((byte)1, 8), resources.ReadStatic(resources.TextureProfile.ClutSourceMapAddress, 8).ToArray());
        int[] pixelOffsets = [2160, 19056, 52336, 85616, 102512, 135792, 201840, 218736];
        int[] paletteOffsets = [18544, 51824, 85104, 102000, 135280, 201328, 218224, 226928];
        for (int index = 0; index < 8; index++)
        {
            NativeBattleCinematicTextureSelection pixel = NativeBattleCinematicTextureMetadata.ResolvePixelB664A0(frame, resources, index);
            NativeBattleCinematicTextureSelection palette = NativeBattleCinematicTextureMetadata.ResolveClutB66560(frame, resources, index);
            Assert.Equal(ArenaAddress + File1Offset + (uint)pixelOffsets[index], pixel.SourceAddress);
            Assert.Equal(ArenaAddress + File1Offset + (uint)paletteOffsets[index], palette.SourceAddress);
            Assert.InRange(pixelOffsets[index] + pixel.Rectangle.Width * pixel.Rectangle.Height * 2, 0, files[1].Data.Length);
        }
        NativeBattleCinematicTextureSelection selected = NativeBattleCinematicTextureMetadata.ResolveClutB66560(frame, resources, 1);
        Assert.Equal(new NativeBattleEffectTextureRectangle(576, 240, 16, 3), selected.Rectangle);
        NativeBattleEffectTextureState queued = NativeBattleCinematicTextureMetadata.Enqueue505DF0(NativeBattleEffectTextureState.FromColdProcess(), frame, selected);
        int source = File1Offset + paletteOffsets[1];
        frame = frame with { Arena = frame.Arena.Write(source, new byte[] { 0x5A, 0xA5 }).Write(source + 94, new byte[] { 0xC3, 0x3C }) };
        NativeBattleEffectTextureState restored = JsonSerializer.Deserialize<NativeBattleEffectTextureState>(JsonSerializer.Serialize(queued))!;
        NativeBattleEffectTextureDrain drain = queued.Drain505D20(frame.Arena);
        Assert.Equal(JsonSerializer.Serialize(drain), JsonSerializer.Serialize(restored.Drain505D20(frame.Arena)));
        NativeBattleEffectVramUpload upload = Assert.Single(drain.Uploads);
        Assert.Equal(frame.Arena.Read(source, 96), upload.Bytes.ToArray());
        Assert.Equal(96, upload.Bytes.Length);
        Assert.Equal(files[1].Data.ToArray(), NativeBattleEffectInitializerPolicy.Assets(game.ReadNativeEffectCatalogForEffects([257])[256])[1].Data.ToArray());
        Assert.Throws<NotSupportedException>(() => resources.Rectangle(8, false));
    }

    [Fact]
    public void Special_file_requests_copy_whole_files_without_replacing_either_resource_alias()
    {
        using GameAssets game = Open();
        ImmutableArray<NativeBattleEffectInitializerAsset> files = NativeBattleEffectInitializerPolicy.Assets(game.ReadNativeEffectCatalogForEffects([257])[256]);
        NativeBattleCinematicResources resources = game.ReadNativeCinematicResources(257);
        NativeBattleWaterExecutionFrame frame = Frame(files, resources);
        const int destinationOffset = File1Offset + 0x28070;
        uint destination = ArenaAddress + destinationOffset;
        frame = Store(frame, Manager + 0xB8, destination);
        NativeBattleWaterScriptProgram program = new(File0Offset, files[0].Data.Length, ArenaAddress + File0Offset);
        for (int index = 0; index < 3; index++)
        {
            int pc = 0xB12 + 0x10 * index;
            ushort instruction = BinaryPrimitives.ReadUInt16LittleEndian(files[0].Data.AsSpan().Slice(pc, 2));
            Assert.Equal(unchecked((ushort)(0xC006 + (index << 9))), instruction);
            frame = Store(frame, 0x2797450, ArenaAddress + File0Offset + (uint)pc).WriteTemporaryWord(0x14A, instruction);
            frame = frame with { Core = frame.Core.Write(0x2798219, [0]) };
            NativeBattleCinematicIoStep prepared = NativeBattleCinematicIoCommands.Execute(frame, program, resources, 0x974E80);
            NativeBattleWaterFileReadRequest request = Assert.IsType<NativeBattleWaterFileReadRequest>(prepared.Request);
            Assert.Equal((short)(363 + index), request.FileId);
            Assert.Equal(destination, request.DestinationAddress);
            Assert.Equal(0x974E80u, request.CompletionCallback);
            NativeBattleEffectMemory memory = new(prepared.Frame.Arena, 0, 0, [], 0, 0, 0, 0) { ScriptedEffects = prepared.Frame.Core };
            NativeBattleWaterIoQueued copied = NativeBattleCinematicIoCommands.CopyAndQueue(memory, NativeBattleFileIoState.FromColdProcess(), request, resources);
            frame = prepared.Frame with { Arena = copied.Memory.Arena!, Core = copied.Memory.ScriptedEffects! };
            Assert.Equal(resources.Files[index].Bytes.ToArray(), frame.Arena.Read(destinationOffset, 32768));
            Assert.Equal(ArenaAddress + File0Offset, BinaryPrimitives.ReadUInt32LittleEndian(frame.Core.Read(0x2798A68, 4)));
            Assert.Equal(ArenaAddress + File1Offset, BinaryPrimitives.ReadUInt32LittleEndian(frame.Core.Read(0x2798A6C, 4)));
            Assert.Equal((byte)0xFF, frame.Core.Read(0x2798219, 1)[0]);
        }
        Assert.Equal(0x7800, files[1].Data.Length - 0x30070);
    }

    private static NativeBattleWaterExecutionFrame Frame(ImmutableArray<NativeBattleEffectInitializerAsset> files, NativeBattleCinematicResources resources)
    {
        NativeBattleWaterExecutionFrame frame = new(NativeBattleScriptedEffectMemory.FromColdProcess(), NativeBattleEffectArena.Clear().Write(File0Offset, files[0].Data.AsSpan()).Write(File1Offset, files[1].Data.AsSpan()), new(0x700000, new byte[0x180].ToImmutableArray(), Enumerable.Repeat(true, 0x180).ToImmutableArray()), new(7));
        frame = Store(frame, 0x27973EC, Manager);
        frame = Store(frame, 0x27973E8, ArenaAddress + File1Offset + 0x70);
        frame = Store(frame, 0x2798A68, ArenaAddress + File0Offset);
        frame = Store(frame, 0x2798A6C, ArenaAddress + File1Offset);
        frame = Store(frame, Manager + 0x98, resources.TextureProfile.PixelRectangleAddress);
        frame = Store(frame, Manager + 0x9C, resources.TextureProfile.ClutRectangleAddress);
        frame = Store(frame, Manager + 0xA4, resources.TextureProfile.PixelSourceMapAddress);
        frame = Store(frame, Manager + 0xA8, resources.TextureProfile.ClutSourceMapAddress);
        return frame;
    }

    private static int Dword(ImmutableArray<byte> bytes, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    }

    private static NativeBattleWaterExecutionFrame Store(NativeBattleWaterExecutionFrame frame, uint address, uint value)
    {
        return frame with { Core = frame.Core.Write(address, BitConverter.GetBytes(value)) };
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select original Effect257 resources.");
        return new(directory!);
    }
}
