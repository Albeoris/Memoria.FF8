using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleShivaResourcesTests
{
    [Fact]
    public void Authenticated_cold_process_keeps_both_complete_vectors_and_the_current_W_alias()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeExecutableMemory original = new(File.ReadAllBytes(Path.Combine(game.GameDirectory, "FF8_EN.exe")));
        NativeBattleShivaProcess cold = game.ReadColdProcessNativeBattleMemory().EffectMemory!.Shiva!;
        cold.Validate();
        Assert.Equal(ReadVector(0x22BC190), cold.Geometry.Current);
        Assert.Equal(ReadVector(0x22BC128), cold.Geometry.Previous);
        Assert.Equal(ReadVector(0x22BCFC8), cold.LocalAnchor22BCFC8);
        Assert.Equal(ReadVector(0x22BCFC0), cold.WorldAnchor22BCFC0);
        Assert.Equal(original.ReadLoader(0x22BD018, 0x90), cold.RawModel.Model.ToArray());
        Assert.Equal(original.ReadLoader(0x22BCFD0, 0x34), cold.RawModel.Control.ToArray());
        Assert.Null(cold.RawModel.ProjectedScratch);
        Assert.Equal(original.ReadLoader(0x22BD0E8, 32), cold.BillboardMatrix22BD0E8.ToArray());
        Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(original.ReadLoader(0xD32508, 4)), cold.SpawnedChildD32508);
        Assert.Equal(BinaryPrimitives.ReadInt16LittleEndian(original.ReadLoader(0x22BC196, 2)), cold.Geometry.Current.W);
        Assert.Equal(BinaryPrimitives.ReadInt32LittleEndian(original.ReadLoader(0x22BD0E0, 4)), cold.VibrationHandle);
        Assert.Equal(original.ReadLoader(0x22BD0E4, 1)[0], cold.SoundReady);
        Assert.Equal(NativeBattleShivaProcess.FromColdProcess().Geometry, cold.Geometry);
        Assert.Null(cold.Entry);
        Assert.Null(cold.WorldView);
        Assert.Null(cold.CameraTask);

        NativeBattleShivaRetainedVector ReadVector(uint address)
        {
            byte[] bytes = original.ReadLoader(address, 8);
            return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)));
        }
    }

    [Fact]
    public void Original_resources_cache_the_six_numeric_stream_owners_and_keep_actual_audio_descriptors()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets battle = new(game.GameDirectory);
        NativeBattleShivaResources resources = game.ReadNativeShivaResources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeShivaResources());
        Assert.Equal(new short[] { 543, 544, 545, 546, 547, 548 }, resources.Files.Select(file => file.FileId));
        Assert.Equal(new[] { "MAG184_D.DAT", "MAG184_E.DAT", "MAG184_F.DAT", "MAG184_G.DAT", "MAG184_H.DAT", "MAG184_I.DAT" }, resources.Files.Select(file => file.Name));
        foreach (NativeBattleEffectFileAsset file in resources.Files)
        {
            NativeBattleArchiveFile original = battle.ReadNativeBattleFile(checked((ushort)file.FileId));
            Assert.Equal(original.Name, file.Name.ToLowerInvariant());
            Assert.Equal(original.Bytes.ToArray(), file.Data.ToArray());
            Assert.NotEmpty(file.Data);
        }
        Assert.Equal(124u, resources.InitialAudioSelector);
        Assert.Equal(new uint[] { 240042, 240045, 240048, 240051 }, resources.Sounds.ToArray());
        NativeExecutableMemory originalImage = new(File.ReadAllBytes(Path.Combine(game.GameDirectory, "FF8_EN.exe")));
        Assert.Equal(Enumerable.Range(0, 6).Select(index => BinaryPrimitives.ReadInt32LittleEndian(originalImage.Read(checked(0xD3250Cu + (uint)(index * 4)), 4))), resources.TextureValuesD3250C);
        Assert.Equal(Enumerable.Range(0, 56).Select(index => BinaryPrimitives.ReadInt32LittleEndian(originalImage.Read(checked(0xD32524u + (uint)(index * 4)), 4))), resources.ClippingVertexSelectorsD32524);
    }

    [Fact]
    public void Original_first_geometry_and_vibration_keep_their_full_reached_source_extents()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleShivaResources resources = game.ReadNativeShivaResources();
        Assert.Equal("FF8_EN:D20FC4", resources.InitialVertexPairs.Identity);
        Assert.Equal(37 * 2 * 8, resources.InitialVertexPairs.Data.Length);
        Assert.Equal("FF8_EN:D324B8", resources.BurstRowsD324B8.Identity);
        Assert.Equal(4 * 16, resources.BurstRowsD324B8.Data.Length);
        Assert.Equal(0xD215C4u, resources.InitialMesh.Address);
        Assert.Equal(74u, BinaryPrimitives.ReadUInt32LittleEndian(resources.InitialMesh.Bytes.AsSpan().Slice(4)));
        int cursor = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(resources.InitialMesh.Bytes.AsSpan()));
        Assert.Equal(0x258, cursor);
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        int[] counts = new int[8];
        for (int index = 0; index < counts.Length; index++)
        {
            counts[index] = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(resources.InitialMesh.Bytes.AsSpan().Slice(cursor)));
            cursor += 4 + counts[index] * strides[index];
        }
        Assert.Equal(new[] { 0, 0, 0, 36, 0, 0, 0, 0 }, counts);
        Assert.Equal(0x5D8, cursor);
        Assert.Equal(cursor, resources.InitialMesh.Bytes.Length);
        Assert.Equal(0xD32344u, resources.VibrationAddress);
        Assert.Equal(64, resources.Vibration.Offsets.Length);
        Assert.Equal(new[] { 0x100, 0x108 }, resources.Vibration.Offsets.Take(2));
        Assert.All(resources.Vibration.Offsets.Skip(2), offset => Assert.Equal(0, offset));
        Assert.Equal(0x144, resources.Vibration.Bytes.Length);
        Assert.Equal((ushort)54, resources.Vibration.ReadWord(0x108));
        Assert.Equal((ushort)0, resources.Vibration.ReadWord(0x10A));
        NativeVibrationEnqueue queued = NativeVibrationProcessState.FromColdProcess().Enqueue(resources.Vibration, 1, 1);
        NativeVibrationProcessState vibration = queued.State;
        for (int callback = 0; callback < 100; callback++)
        {
            vibration = vibration.Advance(4);
        }
        vibration.Validate();
    }

    [Fact]
    public void Original_burst_shard_and_sprite_sources_keep_their_distinct_complete_directories()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleShivaResources resources = game.ReadNativeShivaResources();
        Assert.Equal(0xD3138Cu, resources.BurstMeshD3138C.Address);
        Assert.Equal(0x6E8, resources.BurstMeshD3138C.Bytes.Length);
        Assert.Equal(new[] { 0, 0, 0, 48, 0, 0, 0, 0 }, MeshGroups(resources.BurstMeshD3138C));
        Assert.Equal(0xD2F1C4u, resources.ShardMeshD2F1C4.Address);
        Assert.Equal(0x12C8, resources.ShardMeshD2F1C4.Bytes.Length);
        Assert.Equal(new[] { 0, 0, 102, 0, 0, 0, 68, 0 }, MeshGroups(resources.ShardMeshD2F1C4));
        Assert.Equal(0xD209F4u, resources.FirstSpriteD209F4.Address);
        Assert.Equal(0xDC, resources.FirstSpriteD209F4.Bytes.Length);
        Assert.Equal((ushort)8, BinaryPrimitives.ReadUInt16LittleEndian(resources.FirstSpriteD209F4.Bytes.AsSpan().Slice(8)));
        Assert.Equal(0xD20C7Cu, resources.SecondSpriteD20C7C.Address);
        Assert.Equal(0x194, resources.SecondSpriteD20C7C.Bytes.Length);
        Assert.Equal((ushort)15, BinaryPrimitives.ReadUInt16LittleEndian(resources.SecondSpriteD20C7C.Bytes.AsSpan().Slice(8)));
        for (int frame = 0; frame < 8; frame++)
        {
            Assert.NotEmpty(resources.FirstSpriteD209F4.SpriteFrame(frame).Quads);
        }
        for (int frame = 0; frame < 15; frame++)
        {
            Assert.NotEmpty(resources.SecondSpriteD20C7C.SpriteFrame(frame).Quads);
        }
    }

    [Fact]
    public void Original_particle_sprite_and_loaded544_model_use_the_existing_frame_pose_and_geometry_readers()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleShivaResources resources = game.ReadNativeShivaResources();
        NativeBattleModuleResource sprite = resources.ParticleSpriteD20AD0;
        Assert.Equal(0xD20AD0u, sprite.Address);
        Assert.Equal(0x1AC, sprite.Bytes.Length);
        Assert.Equal((ushort)16, BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)));
        for (int frame = 0; frame < 16; frame++)
        {
            Assert.NotEmpty(sprite.SpriteFrame(frame).Quads);
        }
        NativeBattleEffectOwnedModelResource model = resources.Model544;
        Assert.Equal(0x20DFAB8u, model.Address);
        Assert.Equal(resources.Files.Single(file => file.FileId == 544).Data.ToArray(), model.Bytes.ToArray());
        Assert.Same(model, game.ReadNativeShivaResources().Model544);
        Assert.True(model.PoseSource.BoneCount > 0);
        Assert.NotEmpty(model.Geometry);
        NativeBattleDecodedPose first = model.PoseSource.Sample(0, 1, 0);
        Assert.Equal(model.PoseSource.BoneCount, first.Bones.Length);
        Assert.All(model.Geometry, mesh =>
        {
            mesh.Validate();
            Assert.NotEmpty(mesh.Vertices);
            Assert.NotEmpty(mesh.Polygons);
        });
    }

    [Fact]
    public void Original_later_model_particles_keep_all_named_mesh_groups_sprite_frames_and_signed_spawn_rows()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleShivaResources resources = game.ReadNativeShivaResources();
        (NativeBattleStaticMeshResource Mesh, uint Vertices, int[] Groups)[] meshes =
        [
            (resources.AnchorMeshD31A74, 73, [0, 0, 0, 0, 24, 48, 0, 0]),
            (resources.RingMeshD3048C, 50, [0, 0, 0, 0, 0, 36, 0, 0]),
            (resources.RingMeshD309A4, 96, [0, 0, 0, 0, 0, 72, 0, 0]),
            (resources.ChildMeshD21B9C, 433, [0, 0, 0, 288, 0, 0, 24, 120]),
            (resources.ScrollMeshD2A50C, 157, [0, 0, 0, 80, 0, 0, 12, 52]),
            (resources.FinalMeshD257CC, 231, [0, 0, 400, 0, 0, 0, 0, 0]),
            (resources.FinalMeshD27E6C, 231, [0, 0, 400, 0, 0, 0, 0, 0]),
            (resources.SecondShatterMeshD2BA3C, 166, [0, 0, 287, 0, 0, 0, 0, 0]),
            (resources.SecondShatterMeshD2D600, 166, [0, 0, 287, 0, 0, 0, 0, 0])
        ];
        foreach (var selected in meshes)
        {
            Assert.Equal(selected.Vertices, BinaryPrimitives.ReadUInt32LittleEndian(selected.Mesh.Bytes.AsSpan().Slice(4)));
            Assert.Equal(selected.Groups, MeshGroups(selected.Mesh));
        }
        Assert.Equal(0xD208B0u, resources.ModelParticleSpriteD208B0.Address);
        Assert.Equal(0x144, resources.ModelParticleSpriteD208B0.Bytes.Length);
        Assert.Equal((ushort)5, BinaryPrimitives.ReadUInt16LittleEndian(resources.ModelParticleSpriteD208B0.Bytes.AsSpan().Slice(8)));
        for (int frame = 0; frame < 5; frame++)
        {
            Assert.NotEmpty(resources.ModelParticleSpriteD208B0.SpriteFrame(frame).Quads);
        }
        Assert.Equal(new short[] { 6, -7900, 8, -6100, 10, -3700, 14, -1700 }, Enumerable.Range(0, 8).Select(index => BinaryPrimitives.ReadInt16LittleEndian(resources.SpawnRowsD324F8.Data.AsSpan().Slice(index * 2))));
        Assert.Equal(0xD257CCu, resources.FinalMeshD257CC.Address);
        Assert.Equal(0xD27E6Cu, resources.FinalMeshD27E6C.Address);
        Assert.Equal(0x26A0, resources.FinalMeshD257CC.Bytes.Length);
        Assert.Equal(0x26A0, resources.FinalMeshD27E6C.Bytes.Length);
        Assert.Equal(0xD2BA3Cu, resources.SecondShatterMeshD2BA3C.Address);
        Assert.Equal(0xD2D600u, resources.SecondShatterMeshD2D600.Address);
        Assert.Equal(0x1BC4, resources.SecondShatterMeshD2BA3C.Bytes.Length);
        Assert.Equal(0x1BC4, resources.SecondShatterMeshD2D600.Bytes.Length);
        Assert.Equal(0xD20E10u, resources.LateParticleSpriteD20E10.Address);
        Assert.Equal(0x1AC, resources.LateParticleSpriteD20E10.Bytes.Length);
        Assert.Equal((ushort)16, BinaryPrimitives.ReadUInt16LittleEndian(resources.LateParticleSpriteD20E10.Bytes.AsSpan().Slice(8)));
        for (int frame = 0; frame < 16; frame++)
        {
            Assert.NotEmpty(resources.LateParticleSpriteD20E10.SpriteFrame(frame).Quads);
        }
    }

    [Fact]
    public void Original_camera_executes_its_complete_program_and_restores_the_next_callback_with_the_actual_random_divisor()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleShivaResources resources = game.ReadNativeShivaResources();
        Assert.Equal("FF8_EN:D32604", resources.CameraProgram.Identity);
        Assert.Equal(0x13E, resources.CameraProgram.Data.Length);
        Assert.Equal(48, resources.CameraRandomDivisor);
        NativeBattleGuardianCameraTask task = NativeBattleGuardianCameraProgram.Begin63E960(NativeBattleGuardianCameraProgram.FromColdProcess(), resources.CameraProgram.Data, resources.CameraRandomDivisor, () => 1);
        Assert.Equal(-1, task.Process.Sign);
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleCameraPose pose = new(new(0, 0, 0), new(0, 0, 0), 288, 0);
        bool restored = false;
        int publications = 0;
        for (int callback = 0; callback < 2048 && !task.Complete; callback++)
        {
            NativeBattleGuardianCameraStep step = NativeBattleGuardianCameraProgram.Advance63E9C0(task, matrix, pose, false);
            if (callback == 23)
            {
                NativeBattleGuardianCameraTask clone = JsonSerializer.Deserialize<NativeBattleGuardianCameraTask>(JsonSerializer.Serialize(task))!;
                NativeBattleGuardianCameraStep independent = NativeBattleGuardianCameraProgram.Advance63E9C0(clone, matrix, pose, false);
                Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(independent));
                restored = true;
            }
            task = step.Task;
            pose = step.Pose;
            if (step.Published)
            {
                publications++;
            }
        }
        Assert.True(restored);
        Assert.True(publications > 23);
        Assert.True(task.Complete);
        Assert.Equal(0x13E, task.Offset);
    }

    private static int[] MeshGroups(NativeBattleStaticMeshResource mesh)
    {
        int cursor = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()));
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        int[] counts = new int[8];
        for (int index = 0; index < counts.Length; index++)
        {
            counts[index] = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor)));
            cursor += 4 + counts[index] * strides[index];
        }
        Assert.Equal(mesh.Bytes.Length, cursor);
        return counts;
    }
}
