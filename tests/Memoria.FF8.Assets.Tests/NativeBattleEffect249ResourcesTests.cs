using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect249ResourcesTests
{
    [Fact]
    public void Original_registration_preserves_selected_directories_and_texture_sources()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = Binding(game);
        Assert.Equal(0x9B7380u, binding.Entry);
        Assert.Equal(0x9B7350u, binding.Initializer);
        ImmutableArray<NativeBattleEffectInitializerAsset> files = NativeBattleEffectInitializerPolicy.Assets(binding);
        Assert.Equal(new[] { "mag248_b.00", "mag248_b.01" }, files.Select(file => file.Name));
        Assert.Equal(new[] { 7764, 143960 }, files.Select(file => file.Data.Length));
        NativeBattleCinematicResources resources = game.ReadNativeCinematicResources(249);
        resources.Validate();
        Assert.Same(resources, game.ReadNativeCinematicResources(249));
        Assert.Equal(5, resources.TextureProfile.Count);
        Assert.Null(resources.Vibration);
        Assert.Equal(new uint[] { 0x20, 0x48, 0x70, 0x78, 0x80, 0, 0x88 }, Enumerable.Range(0, 7).Select(index => resources.InitializationOffset(index * 4)));
        Assert.Equal(new byte[] { 0x81, 0x81, 0x81, 0x81, 0x81 }, resources.ReadStatic(resources.TextureProfile.PixelSourceMapAddress, 5).ToArray());
        Assert.All(Enumerable.Range(0, 5), index => Assert.Equal((byte)1, resources.Source(index, false)));
        Assert.All(Enumerable.Range(0, 5), index => Assert.Equal((byte)1, resources.Source(index, true)));
        Assert.Equal(new NativeBattleEffectTextureRectangle(512, 256, 128, 256), resources.Rectangle(0, false));
        Assert.Equal(new NativeBattleEffectTextureRectangle(320, 252, 256, 1), resources.Rectangle(0, true));
        Assert.Throws<NotSupportedException>(() => resources.Rectangle(5, false));
        game.ReadNativeCinematicInitializationDirectories(249).Validate();
    }

    [Fact]
    public void Selected_camera_tracks_parse_to_their_actual_terminals()
    {
        using GameAssets game = Open();
        ImmutableArray<byte> bytes = NativeBattleEffectInitializerPolicy.Assets(Binding(game))[0].Data;
        Assert.Equal(0x6F8, Dword(bytes, 0x20));
        NativeBattleEffectHeapAllocation camera = new("effect-camera", bytes.AsSpan().Slice(0x6F8).ToArray().ToImmutableArray());
        NativeBattleEffectAnimationRegisters animation = NativeBattleEffectTextureState.FromColdProcess().Register5099A0(camera).Animation!;
        Assert.Equal(8, animation.StreamOffset);
        Assert.Equal(0x28, animation.ModelTableOffset);
        NativeBattleCameraBank bank = new(camera.Data.AsSpan().Slice(animation.ModelTableOffset).ToArray().ToImmutableArray());
        foreach ((byte selector, int offset) in new[] { ((byte)0, 0x18), ((byte)4, 0x80) })
        {
            Assert.Equal(offset, bank.TrackOffset(selector));
            Assert.Equal((ushort)0x3C1, Word(bank.Data, offset));
            Assert.Equal(ushort.MaxValue, Word(bank.Data, offset + 96));
            HashSet<byte> references = [];
            NativeBattleCameraPoseInputs inputs = new()
            {
                TransformReference = (reference, point) =>
                {
                    references.Add(reference);
                    return point;
                }
            };
            NativeBattleCameraState state = (NativeBattleCameraState.Initialize() with { CurrentPose = new(new(17, 31, 47), new(61, 79, 97), 512, 0) }).Start(bank, selector);
            int ticks = 0;
            while (!state.Order.IsEmpty && ticks++ < 2048)
            {
                state = state.Tick(0, inputs);
            }
            Assert.Empty(state.Order);
            Assert.InRange(ticks, 1, 2048);
            Assert.NotEmpty(references);
            Assert.NotNull(state.CurrentPose);
        }
        Assert.Null(bank.TrackOffset(0x10));
    }

    [Fact]
    public void Texture_upload_retains_the_current_mutable_slot_alias()
    {
        using GameAssets game = Open();
        NativeBattleCinematicResources resources = game.ReadNativeCinematicResources(249);
        ImmutableArray<byte> original = NativeBattleEffectInitializerPolicy.Assets(Binding(game))[1].Data;
        const int fileOffset = 0x10000;
        const uint arenaAddress = 0x20DFAB8;
        const uint manager = 0x2797690;
        NativeBattleWaterExecutionFrame frame = new(NativeBattleScriptedEffectMemory.FromColdProcess(), NativeBattleEffectArena.Clear().Write(fileOffset, original.ToArray()), new(0x700000, new byte[0x180].ToImmutableArray(), Enumerable.Repeat(true, 0x180).ToImmutableArray()), new(7));
        frame = Store(frame, 0x27973EC, manager);
        frame = Store(frame, 0x27973E8, arenaAddress + fileOffset + 0x58);
        frame = Store(frame, 0x2798A6C, arenaAddress + fileOffset);
        frame = Store(frame, manager + 0x98, resources.TextureProfile.PixelRectangleAddress);
        frame = Store(frame, manager + 0x9C, resources.TextureProfile.ClutRectangleAddress);
        frame = Store(frame, manager + 0xA4, resources.TextureProfile.PixelSourceMapAddress);
        frame = Store(frame, manager + 0xA8, resources.TextureProfile.ClutSourceMapAddress);
        NativeBattleCinematicTextureSelection pixels = NativeBattleCinematicTextureMetadata.ResolvePixelB664A0(frame, resources, 0);
        Assert.Equal(arenaAddress + fileOffset + 0x858, pixels.SourceAddress);
        NativeBattleWaterSlotBank slots = new(fileOffset, original.Length, 64, fileOffset + 0x58);
        frame = frame with { Arena = NativeBattleWaterSlots.InitializeB65160(frame.Arena, slots, 8, 1, 1, 0) };
        Assert.False(original.AsSpan().Slice(0x858, 0x100).SequenceEqual(frame.Arena.Read(fileOffset + 0x858, 0x100)));
        NativeBattleEffectTextureState queued = NativeBattleCinematicTextureMetadata.Enqueue505DF0(NativeBattleEffectTextureState.FromColdProcess(), frame, pixels);
        Assert.Equal(fileOffset + 0x858, queued.Slots[0]!.ArenaOffset);
        Assert.Null(queued.Slots[0]!.Source);
        Assert.Equal(original.ToArray(), NativeBattleEffectInitializerPolicy.Assets(Binding(game))[1].Data.ToArray());
    }

    [Fact]
    public void Selected_shapes_and_sprite_preserve_their_actual_record_domains()
    {
        using GameAssets game = Open();
        ImmutableArray<byte> bytes = NativeBattleEffectInitializerPolicy.Assets(Binding(game))[0].Data;
        int directory = Dword(bytes, 12);
        Assert.Equal(0x808, directory);
        foreach ((int index, int expectedOffset, int vertices, int extent, int[] types, int[] counts) in new[]
        {
            (1, 0xA9C, 97, 3044, new[] { 8, 18 }, new[] { 24, 72 }),
            (2, 0x1680, 49, 1312, new[] { 18 }, new[] { 36 }),
            (3, 0x1BA0, 33, 692, new[] { 8, 18 }, new[] { 4, 12 })
        })
        {
            int shape = directory + Dword(bytes, directory + 4 + 4 * index);
            Assert.Equal(expectedOffset, shape);
            Assert.Equal(3, Dword(bytes, shape));
            Assert.Equal(vertices, Dword(bytes, shape + 0x18));
            Assert.Equal(0, Dword(bytes, shape + 0x20));
            Assert.Equal(extent, Dword(bytes, shape + 0x14) + vertices * 8);
            int cursor = shape + Dword(bytes, shape + 8);
            for (int group = 0; group < types.Length; group++)
            {
                Assert.Equal((ushort)types[group], Word(bytes, cursor));
                Assert.Equal((ushort)counts[group], Word(bytes, cursor + 2));
                cursor += 4 + counts[group] * (types[group] == 8 ? 20 : 24);
            }
            Assert.Equal(ushort.MaxValue, Word(bytes, cursor + 2));
        }
        int sequence = directory + Dword(bytes, directory + 4);
        Assert.Equal(0x81C, sequence);
        int maximumRectangles = 0;
        HashSet<ushort> pages = [];
        for (int index = 0; index < 16; index++)
        {
            int record = sequence + index * 8;
            Assert.Equal((ushort)1024, Word(bytes, record + 4));
            Assert.Equal((byte)0, bytes[record + 6]);
            int frame = directory + Dword(bytes, record);
            int rectangles = Dword(bytes, frame);
            Assert.InRange(rectangles, 1, 2);
            maximumRectangles = Math.Max(maximumRectangles, rectangles);
            for (int rectangle = 0; rectangle < rectangles; rectangle++)
            {
                int offset = frame + 4 + 20 * rectangle;
                pages.Add(Word(bytes, offset + 18));
                Assert.True(offset + 20 <= bytes.Length);
            }
        }
        Assert.Equal(2, maximumRectangles);
        Assert.Equal(new ushort[] { 0xB6, 0xB8 }, pages.Order());
        Assert.Equal(-1, Dword(bytes, sequence + 16 * 8));
        Assert.Equal((byte)1, bytes[sequence + 16 * 8 + 6]);
    }

    private static NativeBattleEffectBinding Binding(GameAssets game) => game.ReadNativeEffectCatalogForEffects([249])[248];
    private static int Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset, 2));
    private static NativeBattleWaterExecutionFrame Store(NativeBattleWaterExecutionFrame frame, uint address, uint value) => frame with { Core = frame.Core.Write(address, BitConverter.GetBytes(value)) };

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select original Effect249 resources.");
        return new(directory!);
    }
}
