using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGilgameshModelResourcesTests
{
    [Fact]
    public void Original_model_profile_preserves_complete_stream_slices_and_selected_static_sources()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGilgameshModelResources resources = game.ReadNativeGilgameshModelResources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeGilgameshModelResources());
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_GILGAMESH_MODEL_EXPORT") is { } export)
        {
            using FileStream target = new(export, FileMode.CreateNew);
            NativeBattleEffectOwnedModelResource[] models = [resources.StreamModel746, resources.StreamModel751, .. resources.Stream749Models, .. resources.StaticModels];
            JsonSerializer.Serialize(target, models.Select(model => new
            {
                model.Address,
                Vertices = model.Geometry.Sum(mesh => mesh.Vertices.Length),
                PlainTriangle = model.Geometry.Sum(mesh => mesh.Polygons.Count(polygon => !polygon.Quad && polygon.Bytes.Length == 16)),
                PlainQuad = model.Geometry.Sum(mesh => mesh.Polygons.Count(polygon => polygon.Quad && polygon.Bytes.Length == 20)),
                ColoredTriangle = model.Geometry.Sum(mesh => mesh.Polygons.Count(polygon => !polygon.Quad && polygon.Bytes.Length == 20)),
                ColoredQuad = model.Geometry.Sum(mesh => mesh.Polygons.Count(polygon => polygon.Quad && polygon.Bytes.Length == 24))
            }), new JsonSerializerOptions { WriteIndented = true });
        }
        NativeBattleGilgameshResources files = game.ReadNativeGilgameshResources();
        Assert.Equal(files.Files.Single(file => file.FileId == 746).Bytes.ToArray(), resources.StreamModel746.Bytes.ToArray());
        Assert.Equal(files.Files.Single(file => file.FileId == 751).Bytes.ToArray(), resources.StreamModel751.Bytes.ToArray());
        ImmutableArray<byte> stream = files.Files.Single(file => file.FileId == 749).Bytes;
        int[] offsets = [0, 6044, 12284, 18524, 24764];
        for (int index = 0; index < 5; index++)
        {
            Assert.Equal(stream.AsSpan().Slice(offsets[index], index == 0 ? 6044 : 6240).ToArray(), resources.Stream749Models[index].Bytes.ToArray());
            Assert.Equal(checked(0x2101AB8u + (uint)offsets[index]), resources.Stream749Models[index].Address);
            Assert.Equal(index == 0 ? 3 : 21, resources.Stream749Models[index].PoseSource.BoneCount);
        }
        NativeExecutableMemory memory = new(game.ReadNativeExecutable());
        foreach (NativeBattleEffectOwnedModelResource model in resources.StaticModels)
        {
            Assert.Equal(memory.Read(model.Address, model.Bytes.Length), model.Bytes.ToArray());
            Assert.Equal(new byte[] { 70, 70, 70, 70, 50, 50, 50, 50 }, NativeBattlePoseDecoder.ReadFrameCounts(model.PoseSource.Animations.AsSpan()));
        }
        Assert.Equal(new uint[] { 0xE808D0, 0xE81934, 0xE82808, 0xE839F4 }, resources.StaticModels.Select(model => model.Address));
        NativeBattleGilgameshModelResources restored = RoundTrip(resources);
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        Assert.Throws<InvalidDataException>(() => (resources with { Stream749Models = resources.Stream749Models.RemoveAt(4) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { StaticModels = resources.StaticModels.SetItem(0, resources.StaticModels[1]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { StreamModel746 = resources.StreamModel746 with { Bytes = resources.StreamModel746.Bytes.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Stream749Models = resources.Stream749Models.SetItem(1, resources.Stream749Models[1] with { Address = resources.Stream749Models[0].Address }) }).Validate());
    }

    [Fact]
    public void Original_initializer_uploads_its_own_complete_CLUT_and_image_without_animation_registration()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGilgameshModelResources resources = game.ReadNativeGilgameshModelResources();
        Assert.Equal(167136, resources.InitializerTim.Data.Length);
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x40000, resources.InitializerTim.Data.AsSpan());
        (NativeBattleEffectTextureState queued, int end) = NativeBattleEffectTextureState.FromColdProcess().EnqueueArenaTim505E30(arena, 0x40000);
        Assert.Equal(0x40000 + 167136, end);
        Assert.Null(queued.Animation);
        NativeBattleEffectTextureDrain drain = queued.Drain505D20(arena);
        Assert.Equal(2, drain.Uploads.Length);
        Assert.Equal(new short[] { 320, 250, 272, 6 }, new[] { drain.Uploads[0].X, drain.Uploads[0].Y, drain.Uploads[0].Width, drain.Uploads[0].Height });
        Assert.Equal(new short[] { 384, 256, 320, 256 }, new[] { drain.Uploads[1].X, drain.Uploads[1].Y, drain.Uploads[1].Width, drain.Uploads[1].Height });
        Assert.Equal(resources.InitializerTim.Data.AsSpan().Slice(20, 3264).ToArray(), drain.Uploads[0].Bytes.ToArray());
        Assert.Equal(resources.InitializerTim.Data.AsSpan().Slice(3296).ToArray(), drain.Uploads[1].Bytes.ToArray());
        Assert.Throws<InvalidDataException>(() => (resources with { InitializerTim = resources.InitializerTim with { Data = resources.InitializerTim.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { InitializerTim = resources.InitializerTim with { Data = resources.InitializerTim.Data.SetItem(16, 0) } }).Validate());
    }

    [Fact]
    public void Original_streamed_pose_selectors_decode_first_regular_and_independent_JSON_next()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGilgameshModelResources resources = game.ReadNativeGilgameshModelResources();
        NativeBattleEffectOwnedModelResource[] models = [resources.StreamModel746, resources.StreamModel751, .. resources.Stream749Models];
        foreach (NativeBattleEffectOwnedModelResource model in models)
        {
            ImmutableArray<byte> counts = NativeBattlePoseDecoder.ReadFrameCounts(model.PoseSource.Animations.AsSpan());
            for (int animation = 0; animation < counts.Length; animation++)
            {
                CheckPose(model.PoseSource, animation);
            }
        }
    }

    [Fact]
    public void Original_selected_static_models_initialize_actual_control_and_animation_three_then_advance_after_JSON()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGilgameshModelResources resources = game.ReadNativeGilgameshModelResources();
        for (int index = 0; index < resources.StaticModels.Length; index++)
        {
            NativeBattleEffectOwnedModelResource model = resources.StaticModels[index];
            NativeBattleRawModelBinding binding = new(checked(0x2200D20u + (uint)index * 0x34), 4);
            NativeBattleCactuarRawModelState initialized = NativeBattleCactuarRawModel.Initialize5A8F40(NativeBattleCactuarRawModelState.FromColdProcess(), model, model.Address, 16, binding);
            Assert.Equal(binding.ControlAddress, BinaryPrimitives.ReadUInt32LittleEndian(initialized.Model.AsSpan().Slice(0x84)));
            Assert.Equal(model.Address + (uint)model.Section(0), BinaryPrimitives.ReadUInt32LittleEndian(initialized.Control.AsSpan().Slice(0x0C)));
            Assert.Equal((ushort)16, BinaryPrimitives.ReadUInt16LittleEndian(initialized.Control.AsSpan().Slice(2)));
            NativeBattleCactuarRawModelState selected = NativeBattleCactuarRawModel.Select5A8FB0(initialized, model, 3, binding);
            Assert.Equal((byte)3, selected.Model[0x6C]);
            Assert.Equal((byte)1, selected.Model[0x72]);
            Assert.Equal((byte)70, selected.Model[0x73]);
            Assert.Equal(JsonSerializer.Serialize(model.PoseSource.Sample(3, 1, 0)), JsonSerializer.Serialize(selected.DecodedPose));
            NativeBattleCactuarRawModelStep expected = NativeBattleCactuarRawModel.Advance5A8FD0(selected, model, binding);
            NativeBattleCactuarRawModelStep actual = NativeBattleCactuarRawModel.Advance5A8FD0(RoundTrip(selected), RoundTrip(model), binding);
            Assert.False(actual.Completed);
            Assert.Equal((byte)2, actual.State.Model[0x72]);
            Assert.Equal(JsonSerializer.Serialize(model.PoseSource.Sample(3, 2, 0)), JsonSerializer.Serialize(actual.State.DecodedPose));
            Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
            CheckPose(model.PoseSource, 3);
        }
    }

    private static void CheckPose(NativeBattlePoseSource source, int animation)
    {
        NativeBattlePoseFirstFrame first = NativeBattlePoseDecoder.DecodeFirst509440(source.Animations.AsSpan(), source.BoneCount, animation);
        Assert.Equal(JsonSerializer.Serialize(source.Sample((byte)animation, 1, 0)), JsonSerializer.Serialize(first.Pose));
        NativeBattlePoseFirstFrame second = NativeBattlePoseDecoder.AdvanceRegular508F90(source.Animations.AsSpan(), source.BoneCount, animation, first.Pose, first.ByteOffset, first.BitOffset);
        Assert.Equal(JsonSerializer.Serialize(source.Sample((byte)animation, 2, 0)), JsonSerializer.Serialize(second.Pose));
        NativeBattlePoseFirstFrame saved = RoundTrip(second);
        NativeBattlePoseSource restoredSource = RoundTrip(source);
        NativeBattlePoseFirstFrame expected = NativeBattlePoseDecoder.AdvanceRegular508F90(source.Animations.AsSpan(), source.BoneCount, animation, second.Pose, second.ByteOffset, second.BitOffset);
        NativeBattlePoseFirstFrame actual = NativeBattlePoseDecoder.AdvanceRegular508F90(restoredSource.Animations.AsSpan(), restoredSource.BoneCount, animation, saved.Pose, saved.ByteOffset, saved.BitOffset);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Equal(JsonSerializer.Serialize(source.Sample((byte)animation, 3, 0)), JsonSerializer.Serialize(actual.Pose));
    }

    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
}
