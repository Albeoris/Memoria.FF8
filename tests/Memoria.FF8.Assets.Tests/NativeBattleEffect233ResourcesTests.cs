using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect233ResourcesTests
{
    private const int File0Offset = 0x1000;
    private const int File1Offset = 0x10000;
    private const uint ArenaAddress = 0x20DFAB8;
    private const uint Manager = 0x2797690;

    [Fact]
    public void Original_registration_retains_selected_files_directories_and_camera_bank()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = Binding(game);
        Assert.Equal(0xA51F40u, binding.Entry);
        Assert.Equal(0xA51F10u, binding.Initializer);
        ImmutableArray<NativeBattleEffectInitializerAsset> files = NativeBattleEffectInitializerPolicy.Assets(binding);
        Assert.Equal(new[] { "mag232_b.00", "mag232_b.01" }, files.Select(file => file.Name));
        Assert.Equal(new[] { 12288, 134208 }, files.Select(file => file.Data.Length));
        NativeBattleCinematicResources resources = game.ReadNativeCinematicResources(233);
        resources.Validate();
        Assert.Same(resources, game.ReadNativeCinematicResources(233));
        Assert.Equal(2, resources.TextureProfile.Count);
        foreach ((int offset, uint value) in new[] { (0, 0x20u), (4, 0x30u), (8, 0x40u), (12, 0x44u), (16, 0x48u), (24, 0x50u) })
        {
            Assert.Equal(value, resources.InitializationOffset(offset));
        }
        game.ReadNativeCinematicInitializationDirectories(233).Validate();
        ImmutableArray<byte> bytes = files[0].Data;
        Assert.Equal(0x40, Dword(bytes, 4));
        Assert.Equal(0x890, Dword(bytes, 16));
        Assert.Equal(0x40, Dword(files[1].Data, 28));
        Assert.Equal(400174, Dword(bytes, 0x890 + Dword(bytes, 0x890)));
        Assert.Equal(0xA28, Dword(bytes, 0x20));
        NativeBattleEffectHeapAllocation camera = new("effect-camera", bytes.AsSpan().Slice(0xA28).ToArray().ToImmutableArray());
        NativeBattleEffectAnimationRegisters animation = NativeBattleEffectTextureState.FromColdProcess().Register5099A0(camera).Animation!;
        Assert.Equal(8, animation.StreamOffset);
        Assert.Equal(0x20, animation.ModelTableOffset);
        NativeBattleCameraBank bank = new(camera.Data.AsSpan().Slice(animation.ModelTableOffset).ToArray().ToImmutableArray());
        Assert.Equal(0x18, bank.TrackOffset(0));
        Assert.Equal(0xAA, bank.TrackOffset(1));
        Assert.Equal(ushort.MaxValue, Word(bank.Data, 0xA8));
        Assert.Equal(ushort.MaxValue, Word(bank.Data, 0x13A));
        Assert.Null(bank.TrackOffset(0x10));
    }

    [Fact]
    public void Metadata52_index_two_is_scalar_only_and_uploads_read_the_latest_arena()
    {
        using GameAssets game = Open();
        ImmutableArray<NativeBattleEffectInitializerAsset> files = NativeBattleEffectInitializerPolicy.Assets(Binding(game));
        NativeBattleCinematicResources resources = game.ReadNativeCinematicResources(233);
        NativeBattleWaterExecutionFrame frame = Frame(files, resources);
        Assert.Equal(new byte[] { 0x81, 0x81 }, resources.ReadStatic(resources.TextureProfile.PixelSourceMapAddress, 2).ToArray());
        Assert.Equal(new byte[] { 1, 1 }, resources.ReadStatic(resources.TextureProfile.ClutSourceMapAddress, 2).ToArray());
        Assert.Equal(new NativeBattleEffectTextureRectangle(512, 256, 128, 256), resources.Rectangle(0, false));
        Assert.Equal(new NativeBattleEffectTextureRectangle(384, 256, 128, 256), resources.Rectangle(1, false));
        Assert.Equal((byte)0xFF, resources.ReadStatic(resources.TextureProfile.PixelSourceMapAddress + 2, 1)[0]);
        frame = Store(frame, 0x2797450, ArenaAddress + File0Offset + 0x200);
        NativeBattleWaterExecutionFrame selected = NativeBattleWaterTextureMetadata.Execute52AA3A60(frame, resources);
        int slot = File1Offset + 0x40;
        Assert.Equal((ushort)0x85, BinaryPrimitives.ReadUInt16LittleEndian(selected.Arena.Read(slot + 0x92, 2)));
        Assert.Equal((ushort)0x7FD8, BinaryPrimitives.ReadUInt16LittleEndian(selected.Arena.Read(slot + 0x9A, 2)));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(selected.Arena.Read(slot + 0x9E, 2)));
        Assert.Equal(ArenaAddress + File0Offset + 0x204, BinaryPrimitives.ReadUInt32LittleEndian(selected.Core.Read(0x2797450, 4)));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicTextureMetadata.ResolvePixelB664A0(frame, resources, 2));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicTextureMetadata.ResolveClutB66560(frame, resources, 2));
        for (int index = 0; index < 2; index++)
        {
            NativeBattleCinematicTextureSelection pixel = NativeBattleCinematicTextureMetadata.ResolvePixelB664A0(frame, resources, index);
            NativeBattleCinematicTextureSelection palette = NativeBattleCinematicTextureMetadata.ResolveClutB66560(frame, resources, index);
            Assert.Equal(ArenaAddress + File1Offset + 0x840u + (uint)index * 0x10000, pixel.SourceAddress);
            Assert.Equal(ArenaAddress + File1Offset + 0x20840u + (uint)index * 0x200, palette.SourceAddress);
            Assert.Equal(new NativeBattleEffectTextureRectangle(320, (short)(254 + index), 256, 1), palette.Rectangle);
        }
        NativeBattleCinematicTextureSelection pixels = NativeBattleCinematicTextureMetadata.ResolvePixelB664A0(frame, resources, 0);
        NativeBattleWaterSlotBank bank = new(File1Offset, files[1].Data.Length, 64, File1Offset + 0x40);
        frame = frame with { Arena = NativeBattleWaterSlots.InitializeB65160(frame.Arena, bank, 8, 1, 1, 0) };
        Assert.Equal(ArenaAddress + (uint)(bank.SlotBaseOffset + 8 * 0x100), pixels.SourceAddress);
        NativeBattleEffectTextureState queued = NativeBattleCinematicTextureMetadata.Enqueue505DF0(NativeBattleEffectTextureState.FromColdProcess(), frame, pixels);
        Assert.Null(queued.Slots[0]!.Source);
        Assert.Equal(File1Offset + 0x840, queued.Slots[0]!.ArenaOffset);
        frame = frame with { Arena = frame.Arena.Write(File1Offset + 0x840, [0x5A, 0xA5]).Write(File1Offset + 0x1083E, [0xC3, 0x3C]) };
        NativeBattleEffectTextureState restored = NativeBattleSnapshotJson.Deserialize<NativeBattleEffectTextureState>(NativeBattleSnapshotJson.Serialize(queued))!;
        NativeBattleEffectTextureDrain drain = queued.Drain505D20(frame.Arena);
        Assert.Equal(NativeBattleSnapshotJson.Serialize(drain), NativeBattleSnapshotJson.Serialize(restored.Drain505D20(frame.Arena)));
        NativeBattleEffectVramUpload upload = Assert.Single(drain.Uploads);
        Assert.Equal(frame.Arena.Read(File1Offset + 0x840, 65536), upload.Bytes.ToArray());
        Assert.Equal(files[1].Data.ToArray(), NativeBattleEffectInitializerPolicy.Assets(Binding(game))[1].Data.ToArray());
    }

    [Fact]
    public void Selected_shapes_and_particle_sprite_keep_bounded_indices_and_terminal_records()
    {
        using GameAssets game = Open();
        ImmutableArray<byte> bytes = NativeBattleEffectInitializerPolicy.Assets(Binding(game))[0].Data;
        int directory = Dword(bytes, 12);
        Assert.Equal(0xB90, directory);
        Assert.Equal(5, Dword(bytes, directory));
        foreach ((int index, int expectedOffset, int vertices, int type, int faces, int stride, int corners, int indices) in new[]
        {
            (1, 0xDB0, 72, 17, 48, 24, 4, 16),
            (2, 0x14A8, 74, 9, 144, 28, 3, 18),
            (3, 0x26F0, 26, 9, 48, 28, 3, 18),
            (4, 0x2D38, 12, 9, 20, 28, 3, 18)
        })
        {
            int shape = directory + Dword(bytes, directory + 4 + 4 * index);
            Assert.Equal(expectedOffset, shape);
            Assert.Equal(3, Dword(bytes, shape));
            Assert.Equal(vertices, Dword(bytes, shape + 0x18));
            Assert.Equal(0, Dword(bytes, shape + 0x20));
            Assert.InRange(shape + Dword(bytes, shape + 0x14) + vertices * 8, 0, bytes.Length);
            int group = shape + Dword(bytes, shape + 8);
            Assert.Equal((ushort)type, Word(bytes, group));
            Assert.Equal((ushort)faces, Word(bytes, group + 2));
            for (int face = 0; face < faces; face++)
            {
                for (int corner = 0; corner < corners; corner++)
                {
                    int offset = Word(bytes, group + 4 + face * stride + indices + 2 * corner);
                    Assert.Equal(0, offset & 7);
                    Assert.InRange(offset / 8, 0, vertices - 1);
                }
            }
            Assert.Equal(ushort.MaxValue, Word(bytes, group + 4 + faces * stride + 2));
        }
        int sequence = directory + Dword(bytes, directory + 4);
        Assert.Equal(0xBA8, sequence);
        for (int index = 0; index < 16; index++)
        {
            int record = sequence + index * 8;
            Assert.Equal((ushort)1024, Word(bytes, record + 4));
            Assert.Equal((byte)0, bytes[record + 6]);
            int frame = directory + Dword(bytes, record);
            Assert.Equal(1, Dword(bytes, frame));
            Assert.InRange(frame + 24, 0, bytes.Length);
        }
        Assert.Equal(ushort.MaxValue, Word(bytes, sequence + 16 * 8 + 4));
        Assert.Equal((byte)1, bytes[sequence + 16 * 8 + 6]);
    }

    [Fact]
    public void Common_requests_copy_complete_files_and_preserve_both_current_aliases()
    {
        using GameAssets game = Open();
        ImmutableArray<NativeBattleEffectInitializerAsset> files = NativeBattleEffectInitializerPolicy.Assets(Binding(game));
        NativeBattleCinematicResources resources = game.ReadNativeCinematicResources(233);
        Assert.Equal(new ushort[] { 363, 364, 365 }, resources.Files.Select(file => file.FileId));
        Assert.Equal(new[] { "ma8def_p.0", "ma8def_p.1", "ma8def_p.2" }, resources.Files.Select(file => file.Name));
        Assert.All(resources.Files, file => Assert.Equal(32768, file.Bytes.Length));
        NativeBattleWaterExecutionFrame frame = Frame(files, resources);
        Assert.Equal(0x24000, Dword(files[0].Data, 0x4C));
        int destinationOffset = File1Offset + Dword(files[1].Data, 0x1C) + Dword(files[0].Data, 0x4C);
        uint destination = ArenaAddress + (uint)destinationOffset;
        frame = Store(frame, Manager + 0xB8, destination);
        NativeBattleWaterScriptProgram program = new(File0Offset, files[0].Data.Length, ArenaAddress + File0Offset);
        for (int index = 0; index < 3; index++)
        {
            int pc = 0x7F0 + 0x10 * index;
            ushort instruction = Word(files[0].Data, pc);
            Assert.Equal(unchecked((ushort)(0xC006 + (index << 9))), instruction);
            frame = Store(frame, 0x2797450, ArenaAddress + File0Offset + (uint)pc).WriteTemporaryWord(0x14A, instruction);
            frame = frame with { Core = frame.Core.Write(0x2798219, [0]) };
            NativeBattleCinematicIoStep prepared = NativeBattleCinematicIoCommands.Execute(frame, program, resources, 0xA57CA0);
            NativeBattleWaterFileReadRequest request = Assert.IsType<NativeBattleWaterFileReadRequest>(prepared.Request);
            Assert.Equal((short)(363 + index), request.FileId);
            Assert.Equal(destination, request.DestinationAddress);
            Assert.Equal(0xA57CA0u, request.CompletionCallback);
            NativeBattleEffectMemory memory = new(prepared.Frame.Arena, 0, 0, [], 0, 0, 0, 0) { ScriptedEffects = prepared.Frame.Core };
            NativeBattleWaterIoQueued copied = NativeBattleCinematicIoCommands.CopyAndQueue(memory, NativeBattleFileIoState.FromColdProcess(), request, resources);
            frame = prepared.Frame with { Arena = copied.Memory.Arena!, Core = copied.Memory.ScriptedEffects! };
            Assert.Equal(resources.Files[index].Bytes.ToArray(), frame.Arena.Read(destinationOffset, 32768));
            Assert.Equal(ArenaAddress + File0Offset, BinaryPrimitives.ReadUInt32LittleEndian(frame.Core.Read(0x2798A68, 4)));
            Assert.Equal(ArenaAddress + File1Offset, BinaryPrimitives.ReadUInt32LittleEndian(frame.Core.Read(0x2798A6C, 4)));
            Assert.Equal((byte)0xFF, frame.Core.Read(0x2798219, 1)[0]);
        }
    }

    private static NativeBattleEffectBinding Binding(GameAssets game) => game.ReadNativeEffectCatalogForEffects([233])[232];
    private static int Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset, 2));
    private static NativeBattleWaterExecutionFrame Store(NativeBattleWaterExecutionFrame frame, uint address, uint value) => frame with { Core = frame.Core.Write(address, BitConverter.GetBytes(value)) };

    private static NativeBattleWaterExecutionFrame Frame(ImmutableArray<NativeBattleEffectInitializerAsset> files, NativeBattleCinematicResources resources)
    {
        NativeBattleWaterExecutionFrame frame = new(NativeBattleScriptedEffectMemory.FromColdProcess(), NativeBattleEffectArena.Clear().Write(File0Offset, files[0].Data.AsSpan()).Write(File1Offset, files[1].Data.AsSpan()), new(0x700000, new byte[0x180].ToImmutableArray(), Enumerable.Repeat(true, 0x180).ToImmutableArray()), new(7));
        frame = Store(frame, 0x27973EC, Manager);
        frame = Store(frame, 0x27973E8, ArenaAddress + File1Offset + 0x40);
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
        Assert.SkipUnless(directory is not null, "Select original Effect233 resources.");
        return new(directory!);
    }
}
