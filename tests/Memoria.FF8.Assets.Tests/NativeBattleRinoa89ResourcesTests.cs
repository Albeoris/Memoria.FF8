using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRinoa89ResourcesTests
{
    [Fact]
    public void Original_catalog_binds_the_authenticated_empty_initializer_and_scene_files()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([89])[88];
        Assert.Equal(0x6F9CF0u, binding.Entry);
        Assert.Equal(0x5755E0u, binding.Initializer);
        Assert.True(binding.InitializerIsRet);
        Assert.Null(binding.InitializerResource);
        NativeBattleRinoa89Resources resources = Assert.IsType<NativeBattleRinoa89Resources>(binding.Rinoa89Resources);
        Assert.Equal(new short[] { 758, 759, 760, 761, 762, 763, 764, 360, 361, 362 }, resources.Files.Select(file => file.FileId));
        Assert.All(resources.Files, file => Assert.False(NativeBattleStorageSnapshots.Capture(file).Data.IsDefaultOrEmpty));
    }

    [Fact]
    public void Original_body_textures_archive_names_and_loader_storage_have_exact_owners()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleRinoa89Resources resources = game.ReadNativeRinoa89Resources();
        Assert.Equal(0x14D0740 - 0x14913FC, NativeBattleStorageSnapshots.Capture(resources.StaticImage).Data.Length);
        Assert.Equal(new[] { 33824, 101408 }, resources.InitialTextures.Select(texture => NativeBattleStorageSnapshots.Capture(texture).Data.Length));
        Assert.Equal(new short[] { 758, 759, 760, 761, 762, 763, 764, 360, 361, 362 }, resources.Files.Select(file => file.FileId));
        NativeBattleEffectOwnedModelResource body = Assert.IsType<NativeBattleEffectOwnedModelResource>(resources.Body);
        Assert.Equal(34, body.PoseSource.BoneCount);
        Assert.Equal(302, body.Geometry.Max(mesh => mesh.Vertices.Length));
        Assert.Equal(16692, body.Bytes.Length);
    }

    [Fact]
    public void Original_shard_mesh_initializes_all120_records_from_actual_centroids_and_retained_rotation_tables()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleRinoa89Resources resources = game.ReadNativeRinoa89Resources();
        NativeBattleRinoa89Frame frame = Frame(resources);
        frame = NativeBattleRinoa89Controller.InitializeShardRotations6FA320(frame);
        frame = NativeBattleRinoa89Controller.InitializeShards6FA3E0(frame);
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        NativeCrtRandomState expected = new(1);
        for (int draw = 0; draw < 376; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
        for (int index = 0; index < 120; index++)
        {
            byte[] shard = arena.Read(24228 + index * 52, 52);
            Assert.Equal((ushort)16, BinaryPrimitives.ReadUInt16LittleEndian(shard.AsSpan(6)));
            uint rotation = BinaryPrimitives.ReadUInt32LittleEndian(shard.AsSpan(16));
            Assert.InRange(rotation, 0x20DFAB8u + 30468, 0x20DFAB8u + 30468 + 63 * 20);
            Assert.Equal(0u, (rotation - 0x20DFAB8u - 30468) % 20);
        }
        Assert.Equal(120, NativeBattleRinoa89ShardDrawing.Draw6FCFD0(arena).Attempts.Count(attempt => attempt.Primitive is not null));
        NativeBattleRinoa89Frame restored = NativeBattleSnapshotJson.Deserialize<NativeBattleRinoa89Frame>(NativeBattleSnapshotJson.Serialize(frame))!;
        Assert.Equal(NativeBattleSnapshotJson.Serialize(NativeBattleRinoa89Controller.AdvanceShards6FD100(frame, 2560)), NativeBattleSnapshotJson.Serialize(NativeBattleRinoa89Controller.AdvanceShards6FD100(restored, 2560)));
    }

    [Fact]
    public void Original_embedded_model_pose_updates_publish_mutable_skeleton_and_independently_restore()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleRinoa89Resources resources = game.ReadNativeRinoa89Resources();
        NativeBattleEffectOwnedModelResource body = Assert.IsType<NativeBattleEffectOwnedModelResource>(resources.Body);
        NativeBattleRinoa89Frame frame = Frame(resources);
        NativeBattleModulePoolTask task = new(1, 0, null, 0x6FA780, new byte[1040].ToImmutableArray());
        task = NativeBattleRinoa89Model.Initialize6EC060(task, 0x20DFAB8 + 23104, body);
        NativeBattleRinoa89ModelStep selected = NativeBattleRinoa89Model.Select6574D0(frame, task, body, 3);
        NativeBattleRinoa89ModelStep restored = NativeBattleSnapshotJson.Deserialize<NativeBattleRinoa89ModelStep>(NativeBattleSnapshotJson.Serialize(selected))!;
        for (int advance = 0; advance < 8; advance++)
        {
            selected = NativeBattleRinoa89Model.Advance6FBDB0(selected.Frame, selected.Task, body);
            restored = NativeBattleRinoa89Model.Advance6FBDB0(restored.Frame, restored.Task, body);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(selected), NativeBattleSnapshotJson.Serialize(restored));
        }
    }

    private static NativeBattleRinoa89Frame Frame(NativeBattleRinoa89Resources resources)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() };
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown().Write(NativeBattleRinoa89Resources.StaticAddress, NativeBattleStorageSnapshots.Capture(resources.StaticImage).Data.AsSpan()));
        return new(memory, null!, staging, new(1));
    }
}
