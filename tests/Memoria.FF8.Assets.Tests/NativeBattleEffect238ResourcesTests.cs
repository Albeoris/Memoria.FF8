using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect238ResourcesTests
{
    [Fact]
    public void Original_binding_keeps_ordered_files_and_selected_static_directories()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = Binding(game);
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(238);
        Assert.Equal(0xA1FDC0u, binding.Entry);
        Assert.Equal(0xA1FD90u, binding.Initializer);
        Assert.Equal(profile.Entry, binding.Entry);
        Assert.Equal(profile.Initializer, binding.Initializer);
        Assert.Equal(new[] { "mag237_b.00", "mag237_b.01" }, NativeBattleEffectInitializerPolicy.Assets(binding).Select(file => file.Name).ToArray());
        Assert.Equal(new[] { 23564, 204392 }, NativeBattleEffectInitializerPolicy.Assets(binding).Select(file => file.Data.Length).ToArray());
        Assert.Equal(new uint[] { 0x279690C, 0x2796908 }, NativeBattleEffectInitializerPolicy.Resources(binding).Select(resource => resource.GlobalAddress).ToArray());
        NativeBattleCinematicResources resources = game.ReadNativeCinematicResources(238);
        resources.Validate();
        Assert.Same(resources, game.ReadNativeCinematicResources(238));
        Assert.Equal(7, resources.TextureProfile.Count);
        Assert.Equal(0x9F8, profile.StaticLength);
        Assert.Equal(0x18652A0u, profile.BoneTable);
        Assert.Equal(0x1865630u, profile.InstructionTable);
        Assert.Null(resources.Vibration);
        Assert.Equal(new ushort[] { 0x16B, 0x16C, 0x16D }, resources.Files.Select(file => file.FileId).ToArray());
        Assert.All(resources.Files, file => Assert.Equal(32768, file.Bytes.Length));
        NativeBattleScriptedInitializationDirectories directories = game.ReadNativeCinematicInitializationDirectories(238);
        directories.Validate();
        Assert.Equal(0x1C, directories.InitializationDirectory.Data.Length);
        Assert.Equal(0x14, directories.OwnerDirectory.Data.Length);
        NativeBattleCinematicResources restored = JsonSerializer.Deserialize<NativeBattleCinematicResources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        Assert.Throws<InvalidDataException>(() => (restored with { TextureProfile = restored.TextureProfile with { PixelRectangleAddress = restored.TextureProfile.PixelRectangleAddress + 1 } }).Validate());
    }

    [Fact]
    public void Original_camera_and_sprite_records_cover_both_tracks_and_selected_frames()
    {
        using GameAssets game = Open();
        ImmutableArray<byte> bytes = NativeBattleEffectInitializerPolicy.Assets(Binding(game))[0].Data;
        Assert.Equal(0x68, Dword(bytes, 4));
        Assert.Equal((ushort)0x003B, Word(bytes, 0x68));
        Assert.Equal(0, Dword(bytes, 0x24));
        Assert.Equal(0xBD4, Dword(bytes, 0x20));
        NativeBattleEffectHeapAllocation camera = new("effect-camera", bytes.AsSpan()[0xBD4..].ToArray().ToImmutableArray());
        Assert.Equal(20536, camera.Data.Length);
        NativeBattleEffectAnimationRegisters animation = NativeBattleEffectTextureState.FromColdProcess().Register5099A0(camera).Animation!;
        Assert.Equal(8, animation.StreamOffset);
        Assert.Equal(32, animation.ModelTableOffset);
        NativeBattleCameraBank bank = new(camera.Data.AsSpan()[animation.ModelTableOffset..].ToArray().ToImmutableArray());
        Assert.Equal(24, bank.TrackOffset(0));
        Assert.Equal(152, bank.TrackOffset(1));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectTextureState.FromColdProcess().Register5099A0(camera with { Data = camera.Data.Take(5).ToImmutableArray() }));
        int directory = Dword(bytes, 12);
        Assert.Equal(0xD18, directory);
        int[] frameCounts = [16, 11, 16, 16];
        int[] maximumRectangles = [3, 1, 2, 1];
        int[] sequences = [0xD3C, 0xFE4, 0x114C, 0x13A4];
        for (int sprite = 0; sprite < frameCounts.Length; sprite++)
        {
            int sequence = directory + Dword(bytes, directory + 4 + sprite * 4);
            Assert.Equal(sequences[sprite], sequence);
            int largest = 0;
            for (int index = 0; index < frameCounts[sprite]; index++)
            {
                int record = sequence + index * 8;
                Assert.Equal((ushort)1024, Word(bytes, record + 4));
                Assert.Equal((byte)0, bytes[record + 6]);
                int frame = directory + Dword(bytes, record);
                int rectangles = Dword(bytes, frame);
                Assert.InRange(rectangles, 1, maximumRectangles[sprite]);
                Assert.True(frame + 4 + rectangles * 20 <= bytes.Length);
                largest = Math.Max(largest, rectangles);
            }
            Assert.Equal(maximumRectangles[sprite], largest);
            int terminal = sequence + frameCounts[sprite] * 8;
            Assert.Equal(-1, Dword(bytes, terminal));
            Assert.Equal((byte)1, bytes[terminal + 6]);
        }
    }

    [Fact]
    public void Expanded_programme_and_texture_uploads_share_the_current_arena()
    {
        using GameAssets game = Open();
        NativeBattleCinematicResources resources = game.ReadNativeCinematicResources(238);
        ImmutableArray<byte> original = NativeBattleEffectInitializerPolicy.Assets(Binding(game))[1].Data;
        Assert.Equal(0x68, Dword(original, 0x1C));
        Assert.Equal(new NativeBattleEffectTextureRectangle(384, 256, 96, 256), resources.Rectangle(1, false));
        Assert.Equal(new NativeBattleEffectTextureRectangle(320, 255, 256, 1), resources.Rectangle(1, true));
        Assert.Equal((byte)1, resources.Source(1, false));
        Assert.Equal((byte)1, resources.Source(1, true));
        Assert.Throws<NotSupportedException>(() => resources.Rectangle(7, false));
        const int fileOffset = 0x10000;
        const uint arenaAddress = 0x20DFAB8;
        const uint manager = 0x2797690;
        NativeBattleWaterExecutionFrame frame = new(NativeBattleScriptedEffectMemory.FromColdProcess(), NativeBattleEffectArena.Clear().Write(fileOffset, original.ToArray()), new(0x700000, new byte[0x180].ToImmutableArray(), Enumerable.Repeat(true, 0x180).ToImmutableArray()), new(7));
        frame = Dword(frame, 0x27973EC, manager);
        frame = Dword(frame, 0x27973E8, arenaAddress + fileOffset + 0x68);
        frame = Dword(frame, 0x2798A6C, arenaAddress + fileOffset);
        frame = Dword(frame, manager + 0x98, resources.TextureProfile.PixelRectangleAddress);
        frame = Dword(frame, manager + 0x9C, resources.TextureProfile.ClutRectangleAddress);
        frame = Dword(frame, manager + 0xA4, resources.TextureProfile.PixelSourceMapAddress);
        frame = Dword(frame, manager + 0xA8, resources.TextureProfile.ClutSourceMapAddress);
        NativeBattleCinematicTextureSelection pixels = NativeBattleCinematicTextureMetadata.ResolvePixelB664A0(frame, resources, 1);
        NativeBattleCinematicTextureSelection clut = NativeBattleCinematicTextureMetadata.ResolveClutB66560(frame, resources, 1);
        Assert.Equal(arenaAddress + fileOffset + 0x868, pixels.SourceAddress);
        Assert.Equal(arenaAddress + fileOffset + 201320, clut.SourceAddress);
        NativeBattleWaterSlotBank slots = new(fileOffset, original.Length, 64, fileOffset + 0x68);
        frame = frame with { Arena = NativeBattleWaterSlots.InitializeB65160(frame.Arena, slots, 8, 1, 1, 0) };
        Assert.False(original.AsSpan().Slice(0x868, 0x100).SequenceEqual(frame.Arena.Read(fileOffset + 0x868, 0x100)));
        NativeBattleEffectTextureState queued = NativeBattleCinematicTextureMetadata.Enqueue505DF0(NativeBattleEffectTextureState.FromColdProcess(), frame, pixels);
        Assert.Equal(fileOffset + 0x868, queued.Slots[0]!.ArenaOffset);
        Assert.Null(queued.Slots[0]!.Source);
        NativeBattleEffectTextureDrain drained = queued.Drain505D20(frame.Arena);
        Assert.Equal(frame.Arena.Read(fileOffset + 0x868, 49152), Assert.Single(drained.Uploads).Bytes.ToArray());
    }

    private static NativeBattleEffectBinding Binding(GameAssets game) => game.ReadNativeEffectCatalogForEffects([238])[237];
    private static int Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset, 2));
    private static NativeBattleWaterExecutionFrame Dword(NativeBattleWaterExecutionFrame frame, uint address, uint value) => frame with { Core = frame.Core.Write(address, BitConverter.GetBytes(value)) };

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select original Effect238 resources.");
        return new(directory!);
    }
}
