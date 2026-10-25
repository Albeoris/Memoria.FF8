using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect232ResourcesTests
{
    [Fact]
    public void Original_registration_preserves_selected_directories_and_texture_sources()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = Binding(game);
        Assert.Equal(0xA5C2E0u, binding.Entry);
        Assert.Equal(0xA5C2B0u, binding.Initializer);
        ImmutableArray<NativeBattleEffectInitializerAsset> files = NativeBattleEffectInitializerPolicy.Assets(binding);
        Assert.Equal(new[] { "mag231_b.00", "mag231_b.01" }, files.Select(file => file.Name));
        Assert.Equal(new[] { 16816, 153744 }, files.Select(file => file.Data.Length));
        NativeBattleCinematicResources resources = game.ReadNativeCinematicResources(232);
        resources.Validate();
        Assert.Same(resources, game.ReadNativeCinematicResources(232));
        Assert.Equal(4, resources.TextureProfile.Count);
        Assert.Null(resources.Vibration);
        Assert.Equal(new uint[] { 0x20, 0x40, 0x60, 0x68, 0x70, 0, 0x78 }, Enumerable.Range(0, 7).Select(index => resources.InitializationOffset(index * 4)));
        Assert.Equal(new byte[] { 1, 0x81, 0x81, 0x81 }, resources.ReadStatic(resources.TextureProfile.PixelSourceMapAddress, 4).ToArray());
        Assert.All(Enumerable.Range(0, 4), index => Assert.Equal((byte)1, resources.Source(index, false)));
        Assert.All(Enumerable.Range(0, 4), index => Assert.Equal((byte)1, resources.Source(index, true)));
        Assert.Equal(new NativeBattleEffectTextureRectangle(544, 256, 12, 88), resources.Rectangle(0, false));
        Assert.Equal(new NativeBattleEffectTextureRectangle(320, 252, 16, 1), resources.Rectangle(0, true));
        Assert.Throws<NotSupportedException>(() => resources.Rectangle(4, false));
        game.ReadNativeCinematicInitializationDirectories(232).Validate();
        foreach ((int offset, int vertices) in new[] { (0x12C0, 81), (0x1B80, 296), (0x333C, 120) })
        {
            Assert.Equal(vertices, Dword(files[0].Data, offset + 0x18));
            Assert.Equal(0, Dword(files[0].Data, offset + 0x20));
        }
    }

    [Fact]
    public void Selected_camera_tracks_parse_to_their_actual_terminals()
    {
        using GameAssets game = Open();
        ImmutableArray<byte> bytes = NativeBattleEffectInitializerPolicy.Assets(Binding(game))[0].Data;
        Assert.Equal(0x8F0, Dword(bytes, 0x20));
        NativeBattleEffectHeapAllocation camera = new("effect-camera", bytes.AsSpan().Slice(0x8F0).ToArray().ToImmutableArray());
        NativeBattleEffectAnimationRegisters animation = NativeBattleEffectTextureState.FromColdProcess().Register5099A0(camera).Animation!;
        Assert.Equal(8, animation.StreamOffset);
        Assert.Equal(0x28, animation.ModelTableOffset);
        NativeBattleCameraBank bank = new(camera.Data.AsSpan().Slice(animation.ModelTableOffset).ToArray().ToImmutableArray());
        foreach ((byte selector, int offset) in new[] { ((byte)0, 0x18), ((byte)4, 0xC2) })
        {
            Assert.Equal(offset, bank.TrackOffset(selector));
            Assert.Equal((ushort)0x3C0, Word(bank.Data, offset));
            Assert.Equal(ushort.MaxValue, Word(bank.Data, offset + 162));
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
        NativeBattleCinematicResources resources = game.ReadNativeCinematicResources(232);
        ImmutableArray<byte> original = NativeBattleEffectInitializerPolicy.Assets(Binding(game))[1].Data;
        const int fileOffset = 0x10000;
        const uint arenaAddress = 0x20DFAB8;
        const uint manager = 0x2797690;
        NativeBattleWaterExecutionFrame frame = new(NativeBattleScriptedEffectMemory.FromColdProcess(), NativeBattleEffectArena.Clear().Write(fileOffset, original.ToArray()), new(0x700000, new byte[0x180].ToImmutableArray(), Enumerable.Repeat(true, 0x180).ToImmutableArray()), new(7));
        frame = Store(frame, 0x27973EC, manager);
        frame = Store(frame, 0x27973E8, arenaAddress + fileOffset + 0x50);
        frame = Store(frame, 0x2798A6C, arenaAddress + fileOffset);
        frame = Store(frame, manager + 0x98, resources.TextureProfile.PixelRectangleAddress);
        frame = Store(frame, manager + 0x9C, resources.TextureProfile.ClutRectangleAddress);
        frame = Store(frame, manager + 0xA4, resources.TextureProfile.PixelSourceMapAddress);
        frame = Store(frame, manager + 0xA8, resources.TextureProfile.ClutSourceMapAddress);
        NativeBattleCinematicTextureSelection pixels = NativeBattleCinematicTextureMetadata.ResolvePixelB664A0(frame, resources, 0);
        Assert.Equal(arenaAddress + fileOffset + 0x850, pixels.SourceAddress);
        NativeBattleWaterSlotBank slots = new(fileOffset, original.Length, 64, fileOffset + 0x50);
        frame = frame with { Arena = NativeBattleWaterSlots.InitializeB65160(frame.Arena, slots, 8, 1, 1, 0) };
        Assert.False(original.AsSpan().Slice(0x850, 0x100).SequenceEqual(frame.Arena.Read(fileOffset + 0x850, 0x100)));
        NativeBattleEffectTextureState queued = NativeBattleCinematicTextureMetadata.Enqueue505DF0(NativeBattleEffectTextureState.FromColdProcess(), frame, pixels);
        Assert.Equal(fileOffset + 0x850, queued.Slots[0]!.ArenaOffset);
        Assert.Null(queued.Slots[0]!.Source);
        Assert.Equal(original.ToArray(), NativeBattleEffectInitializerPolicy.Assets(Binding(game))[1].Data.ToArray());
    }

    [Fact]
    public void Current_actor_animation_sections_decode_every_selected_frame()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT232_SOURCE_SNAPSHOT");
        Assert.SkipUnless(path is not null, "Select the current Effect232 source snapshot.");
        using GameAssets game = Open();
        BattleSnapshot source = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(path!))!;
        NativeBattleModelCpuResources cpu = Assert.IsType<NativeBattleModelCpuResources>(source.NativeModelCpuResources);
        NativeBattleFileStaging staging = Assert.IsType<NativeBattleFileStaging>(source.NativeFileStaging);
        NativeBattleGraphicsState graphics = Assert.IsType<NativeBattleGraphicsState>(source.NativeGraphics);
        Assert.Equal(0, graphics.Models[3].ActiveResourceRow100);
        Assert.Equal((byte)41, graphics.Models[3].ModelId4);
        NativeBattleModelCpuResourceRow row = cpu.Rows[0];
        Assert.Equal(new NativeBattleModelCpuReference(3, 38924), row.Require(20));
        Assert.Equal(48284, row.Lengths[20]);
        byte[] skeleton = NativeBattleModelEnemyHeap.Read(cpu, staging, row.Require(12), row.Lengths[12]);
        byte[] animations = NativeBattleModelEnemyHeap.Read(cpu, staging, row.Require(20), row.Lengths[20]);
        NativeBattlePoseSource pose = new(skeleton.ToImmutableArray(), animations.ToImmutableArray());
        pose.Validate();
        Assert.Equal(39, pose.BoneCount);
        Assert.Equal(1888, skeleton.Length);
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR")!);
        NativeBattlePoseSource original = Assert.IsType<NativeBattlePoseSource>(assets.ReadEnemyNativeGraphicsResource(25).PoseSource);
        Assert.Equal(original.Skeleton.ToArray(), skeleton);
        Assert.Equal(original.Animations.ToArray(), animations);
        foreach (int selector in new[] { 26, 27, 28 })
        {
            int pointer = BinaryPrimitives.ReadInt32LittleEndian(animations.AsSpan(4 + 4 * selector, 4));
            int frames = animations[pointer];
            Assert.InRange(frames, 1, 255);
            NativeBattlePoseFirstFrame current = NativeBattlePoseDecoder.DecodeFirst509440(animations, 39, selector);
            for (int frame = 1; frame < frames; frame++)
            {
                current = NativeBattlePoseDecoder.AdvanceRegular508F90(animations, 39, selector, current.Pose, current.ByteOffset, current.BitOffset);
            }
            Assert.Equal(JsonSerializer.Serialize(pose.Sample((byte)selector, (byte)frames, 0)), JsonSerializer.Serialize(current.Pose));
            Assert.InRange(pointer + current.ByteOffset, pointer + 1, animations.Length);
        }
    }

    private static NativeBattleEffectBinding Binding(GameAssets game) => game.ReadNativeEffectCatalogForEffects([232])[231];
    private static int Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset, 2));
    private static NativeBattleWaterExecutionFrame Store(NativeBattleWaterExecutionFrame frame, uint address, uint value) => frame with { Core = frame.Core.Write(address, BitConverter.GetBytes(value)) };

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select original Effect232 resources.");
        return new(directory!);
    }
}
