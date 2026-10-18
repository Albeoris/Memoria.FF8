using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeEnemyResourceSectionsTests
{
    [Fact]
    public void Shared_packed_geometry_reader_preserves_existing_enemy_vertices_bone_groups_and_original_polygon_bytes()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        NativeBattleModelDatSource source = assets.ReadNativeModelDatSource(142, false, 0);
        NativeBattleGraphicsResource resource = assets.ReadEnemyNativeGraphicsResource(126);
        byte[] geometry = source.Bytes.Slice(source.Section(1), source.Section(2) - source.Section(1)).ToArray();
        var decoded = NativeBattleRawGeometryReader.Read(geometry, resource.PoseSource!.BoneCount);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(resource.Geometry!.Value), System.Text.Json.JsonSerializer.Serialize(decoded));
        Assert.Throws<InvalidDataException>(() => NativeBattleRawGeometryReader.Read(geometry, 0));
    }

    [Fact]
    public void Original_enemy_with_distinct_motion_and_sequence_retains_actual_resource_sections()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        NativeBattleModelDatSource source = assets.ReadNativeModelDatSource(142, false, 0);
        NativeBattleGraphicsResource resource = assets.ReadEnemyNativeGraphicsResource(126);
        Assert.True(source.Section(3) < source.Section(4));
        Assert.Equal(source.Bytes.Slice(source.Section(3), source.Section(4) - source.Section(3)).ToArray(), resource.TextureAnimations!.Value.ToArray());
        Assert.Equal(source.Bytes.Slice(source.Section(4), source.Section(5) - source.Section(4)).ToArray(), resource.SequenceSection.ToArray());
        Assert.Equal(source.Bytes.Slice(source.Section(5), source.Section(6) - source.Section(5)).ToArray(), resource.ActionCameraBank!.Data.ToArray());
        Assert.Equal(source.Bytes.Slice(source.Section(2), source.Section(3) - source.Section(2)).ToArray(), resource.PoseSource!.Animations.ToArray());
        BattleModel decoded = assets.LoadEnemy(126);
        Assert.Equal(resource.SequenceSection.ToArray(), decoded.Sequences!.NativeResource.SequenceSection.ToArray());
        Assert.Equal(resource.ActionCameraBank.Data.ToArray(), decoded.NativeActionCameraBank!.Data.ToArray());
        NativeBattleGraphicsResource separate = assets.ReadEnemyNativeGraphicsResource(127);
        Assert.Empty(separate.Geometry!.Value);
        Assert.Equal(resource.SequenceSection.ToArray(), separate.SequenceSection.ToArray());
        Assert.Equal(2, System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(separate.ModelLoadSources!.Value.Single().Bytes.AsSpan()));
    }
}
