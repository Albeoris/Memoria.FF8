using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect256ResourcesTests
{
    [Fact]
    public void Original_registration_supplies_ordered_initializers_and_cached_addressed_resources()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = Binding(game);
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(256);
        Assert.Equal(0x979400u, binding.Entry);
        Assert.Equal(0x9793D0u, binding.Initializer);
        Assert.Equal(profile.Entry, binding.Entry);
        Assert.Equal(profile.Initializer, binding.Initializer);
        ImmutableArray<NativeBattleEffectInitializerResource> operands = NativeBattleEffectInitializerPolicy.Resources(binding);
        ImmutableArray<NativeBattleEffectInitializerAsset> files = NativeBattleEffectInitializerPolicy.Assets(binding);
        Assert.Equal(new[] { "mag255_b.00", "mag255_b.01" }, files.Select(file => file.Name).ToArray());
        Assert.Equal(new uint[] { 0x2796558, 0x2796540 }, operands.Select(operand => operand.GlobalAddress).ToArray());
        Assert.Equal(new[] { 2764, 68152 }, files.Select(file => file.Data.Length).ToArray());
        NativeBattleCinematicResources resources = game.ReadNativeCinematicResources(256);
        resources.Validate();
        Assert.Same(resources, game.ReadNativeCinematicResources(256));
        Assert.Equal(1, resources.TextureProfile.Count);
        Assert.Null(resources.Vibration);
        Assert.Equal(new ushort[] { 0x16B, 0x16C, 0x16D }, resources.Files.Select(file => file.FileId).ToArray());
        Assert.Equal(new[] { "ma8def_p.0", "ma8def_p.1", "ma8def_p.2" }, resources.Files.Select(file => file.Name).ToArray());
        Assert.All(resources.Files, file => Assert.Equal(32768, file.Bytes.Length));
        NativeBattleScriptedInitializationDirectories directories = game.ReadNativeCinematicInitializationDirectories(256);
        directories.Validate();
        Assert.Equal(0x1C, directories.InitializationDirectory.Data.Length);
        Assert.Equal(0x14, directories.OwnerDirectory.Data.Length);
        NativeBattleCinematicResources restored = JsonSerializer.Deserialize<NativeBattleCinematicResources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        Assert.Throws<InvalidDataException>(() => (restored with { TextureProfile = restored.TextureProfile with { PixelRectangleAddress = restored.TextureProfile.PixelRectangleAddress + 1 } }).Validate());
    }

    [Fact]
    public void Original_camera_and_sprite_directories_have_complete_selected_tracks_and_frames()
    {
        using GameAssets game = Open();
        ImmutableArray<NativeBattleEffectInitializerAsset> files = NativeBattleEffectInitializerPolicy.Assets(Binding(game));
        ImmutableArray<byte> bytes = files[0].Data;
        int cameraOffset = Dword(bytes, 0x20);
        Assert.Equal(0x84C, cameraOffset);
        NativeBattleEffectHeapAllocation camera = new("effect-camera", bytes.AsSpan().Slice(cameraOffset).ToArray().ToImmutableArray());
        Assert.Equal(640, camera.Data.Length);
        NativeBattleEffectAnimationRegisters animation = NativeBattleEffectTextureState.FromColdProcess().Register5099A0(camera).Animation!;
        Assert.Equal(8, animation.StreamOffset);
        Assert.Equal(24, animation.ModelTableOffset);
        NativeBattleCameraBank bank = new(camera.Data.AsSpan().Slice(animation.ModelTableOffset).ToArray().ToImmutableArray());
        Assert.Equal(24, bank.TrackOffset(0));
        Assert.Null(bank.TrackOffset(0x10));
        Assert.Equal((ushort)0x3C1, Word(bank.Data, 24));
        Assert.Equal(ushort.MaxValue, Word(bank.Data, 72));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectTextureState.FromColdProcess().Register5099A0(camera with { Data = camera.Data.Take(5).ToImmutableArray() }));

        NativeBattleCinematicResources resources = game.ReadNativeCinematicResources(256);
        uint map = resources.TextureProfile.InitializationAddress + resources.InitializationOffset(0x10);
        Assert.Equal((byte)0, resources.ReadStatic(map, 1)[0]);
        int directory = Dword(bytes, 12);
        int sequence = directory + Dword(bytes, directory + 4);
        Assert.Equal(0x8BC, directory);
        Assert.Equal(0x8C4, sequence);
        for (int index = 0; index < 16; index++)
        {
            int record = sequence + index * 8;
            Assert.Equal((ushort)1024, Word(bytes, record + 4));
            Assert.Equal((byte)0, bytes[record + 6]);
            int frame = directory + Dword(bytes, record);
            Assert.Equal(0x94C + index * 24, frame);
            Assert.Equal(1, Dword(bytes, frame));
            Assert.True(frame + 24 <= bytes.Length);
            Assert.Equal((byte)0x80, bytes[frame + 4 + 0x10]);
            Assert.Equal((ushort)0xB6, Word(bytes, frame + 4 + 0x12));
        }
        int terminal = sequence + 16 * 8;
        Assert.Equal(uint.MaxValue, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(terminal, 4)));
        Assert.Equal((byte)1, bytes[terminal + 6]);
        Assert.Equal(0x94C + 16 * 24, bytes.Length);
    }

    [Fact]
    public void Selected_texture_uploads_keep_the_current_programme_and_pixel_alias()
    {
        using GameAssets game = Open();
        NativeBattleCinematicResources resources = game.ReadNativeCinematicResources(256);
        ImmutableArray<byte> original = NativeBattleEffectInitializerPolicy.Assets(Binding(game))[1].Data;
        Assert.Equal(new NativeBattleEffectTextureRectangle(384, 256, 128, 256), resources.Rectangle(0, false));
        Assert.Equal(new NativeBattleEffectTextureRectangle(320, 255, 256, 1), resources.Rectangle(0, true));
        Assert.Equal((byte)1, resources.Source(0, false));
        Assert.Equal((byte)1, resources.Source(0, true));
        Assert.Throws<NotSupportedException>(() => resources.Rectangle(1, false));
        const int fileOffset = 0x10000;
        const uint arenaAddress = 0x20DFAB8;
        const uint manager = 0x2797690;
        NativeBattleWaterExecutionFrame frame = new(NativeBattleScriptedEffectMemory.FromColdProcess(), NativeBattleEffectArena.Clear().Write(fileOffset, original.ToArray()), new(0x700000, new byte[0x180].ToImmutableArray(), Enumerable.Repeat(true, 0x180).ToImmutableArray()), new(7));
        frame = Dword(frame, 0x27973EC, manager);
        frame = Dword(frame, 0x27973E8, arenaAddress + fileOffset + 0x38);
        frame = Dword(frame, 0x2798A6C, arenaAddress + fileOffset);
        frame = Dword(frame, manager + 0x98, resources.TextureProfile.PixelRectangleAddress);
        frame = Dword(frame, manager + 0x9C, resources.TextureProfile.ClutRectangleAddress);
        frame = Dword(frame, manager + 0xA4, resources.TextureProfile.PixelSourceMapAddress);
        frame = Dword(frame, manager + 0xA8, resources.TextureProfile.ClutSourceMapAddress);
        NativeBattleCinematicTextureSelection pixels = NativeBattleCinematicTextureMetadata.ResolvePixelB664A0(frame, resources, 0);
        NativeBattleCinematicTextureSelection clut = NativeBattleCinematicTextureMetadata.ResolveClutB66560(frame, resources, 0);
        Assert.Equal(arenaAddress + fileOffset + 0x838, pixels.SourceAddress);
        Assert.Equal(arenaAddress + fileOffset + 0x10838, clut.SourceAddress);
        Assert.Equal(0x10838 + 512, original.Length);
        NativeBattleWaterSlotBank slots = new(fileOffset, original.Length, 16, fileOffset + 0x38);
        frame = frame with { Arena = NativeBattleWaterSlots.InitializeB65160(frame.Arena, slots, 8, 1, 1, 0) };
        Assert.False(original.AsSpan().Slice(0x838, 0x100).SequenceEqual(frame.Arena.Read(fileOffset + 0x838, 0x100)));
        NativeBattleEffectTextureState queued = NativeBattleCinematicTextureMetadata.Enqueue505DF0(NativeBattleEffectTextureState.FromColdProcess(), frame, pixels);
        Assert.Equal(fileOffset + 0x838, queued.Slots[0]!.ArenaOffset);
        Assert.Null(queued.Slots[0]!.Source);
        Assert.Equal(65536u, clut.SourceAddress - pixels.SourceAddress);
        Assert.Equal(original.ToArray(), NativeBattleEffectInitializerPolicy.Assets(Binding(game))[1].Data.ToArray());
    }

    private static NativeBattleEffectBinding Binding(GameAssets game) => game.ReadNativeEffectCatalogForEffects([256])[255];

    private static int Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));

    private static ushort Word(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset, 2));

    private static NativeBattleWaterExecutionFrame Dword(NativeBattleWaterExecutionFrame frame, uint address, uint value) => frame with { Core = frame.Core.Write(address, BitConverter.GetBytes(value)) };

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select original Effect256 resources.");
        return new(directory!);
    }
}
