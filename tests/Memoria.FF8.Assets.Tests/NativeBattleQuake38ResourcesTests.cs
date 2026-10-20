using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleQuake38ResourcesTests
{
    [Fact]
    public void Original_Quake_stage_ground_keeps_the_actual_header_group_and_packed_vertex_count()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleStage stage = assets.LoadStage(31);
        var ground = stage.NativeSource!.Geometry!.Value.Where(mesh => mesh.StageGroup == 1).ToArray();
        Assert.Equal(new[] { 54, 48, 34 }, ground.Select(mesh => mesh.Vertices.Length).ToArray());
        foreach (NativeBattleRawMesh mesh in ground)
        {
            NativeBattleRawVertexGroup group = Assert.Single(mesh.PackedVertexGroups ?? throw new InvalidDataException("Original stage vertex groups are missing."));
            Assert.Equal((ushort)1, group.Bone);
            Assert.Equal(mesh.Vertices.Length, group.Count);
            Assert.Equal(mesh.Vertices.Length * 6 + 6, group.Bytes.Length);
            Assert.Equal(new short[] { 1 }, mesh.BoneGroups!.Value.ToArray());
            for (int index = 0; index < mesh.Vertices.Length; index++)
            {
                NativeBattleRawVertex vertex = mesh.Vertices[index];
                Assert.Equal(group.Bone, vertex.Bone);
                Assert.Equal(vertex.Position.X, BinaryPrimitives.ReadInt16LittleEndian(group.Bytes.AsSpan().Slice(index * 6)));
                Assert.Equal(vertex.Position.Y, BinaryPrimitives.ReadInt16LittleEndian(group.Bytes.AsSpan().Slice(index * 6 + 2)));
                Assert.Equal(vertex.Position.Z, BinaryPrimitives.ReadInt16LittleEndian(group.Bytes.AsSpan().Slice(index * 6 + 4)));
            }
        }
    }

    [Fact]
    public void Original_Quake_retains_embedded_texture_camera_sprites_tables_and_exact_cold_fields()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleQuake38Resources resources = game.ReadNativeQuake38Resources();
        NativeBattleEffectBinding materialized = game.ReadNativeEffectCatalogForEffects([38])[37];
        Assert.Equal(0x8D8510u, materialized.Entry);
        Assert.True(materialized.InitializerIsRet);
        Assert.Same(resources, materialized.Quake38Resources);
        NativeExecutableMemory image = new(game.ReadNativeExecutable());
        NativeBattleQuake38State cold = NativeBattleQuake38State.FromColdProcess();
        cold.Validate();
        Assert.Equal(image.ReadLoader(0x278C8E0, 0xD8), cold.Header.ToArray());
        Assert.Equal(image.ReadLoader(0x279299C, 0x34), cold.Middle.ToArray());
        Assert.Equal(image.ReadLoader(0x2792B40, 0x10), cold.Pointers.ToArray());
        Assert.Equal(image.ReadLoader(0x2792D68, 0x10), cold.Tail.ToArray());
        Assert.Equal(image.ReadLoader(0x2792E04, 0x5C), cold.End.ToArray());
        for (int actor = 0; actor < 7; actor++)
        {
            NativeBattleQuake38SavedActor saved = cold.SavedActors[actor];
            byte[] expected = image.ReadLoader(checked(0x2792D78u + (uint)actor * 20), 20);
            Assert.Equal(BinaryPrimitives.ReadInt16LittleEndian(expected), saved.Rotation12);
            Assert.Equal(BinaryPrimitives.ReadInt16LittleEndian(expected.AsSpan(2)), saved.Rotation14);
            Assert.Equal(BinaryPrimitives.ReadInt16LittleEndian(expected.AsSpan(4)), saved.Rotation16);
            Assert.Equal(BinaryPrimitives.ReadUInt16LittleEndian(expected.AsSpan(6)), saved.RotationPadding18);
            Assert.Equal(new NativeBattleVector(BinaryPrimitives.ReadInt16LittleEndian(expected.AsSpan(8)), BinaryPrimitives.ReadInt16LittleEndian(expected.AsSpan(10)), BinaryPrimitives.ReadInt16LittleEndian(expected.AsSpan(12))), saved.Position28);
            Assert.Equal(BinaryPrimitives.ReadInt16LittleEndian(expected.AsSpan(14)), saved.Vertical34);
            Assert.Equal(BinaryPrimitives.ReadInt16LittleEndian(expected.AsSpan(16)), saved.BouncePhase);
            Assert.Equal(BinaryPrimitives.ReadInt16LittleEndian(expected.AsSpan(18)), saved.Velocity);
        }
        Assert.Equal(0x29020, resources.Texture.Data.Length);
        Assert.Equal(16u, BinaryPrimitives.ReadUInt32LittleEndian(resources.Texture.Data.AsSpan()));
        Assert.Equal(9u, BinaryPrimitives.ReadUInt32LittleEndian(resources.Texture.Data.AsSpan().Slice(4)));
        Assert.Equal(0x4A8, resources.Camera.Data.Length);
        Assert.Equal((ushort)12, BinaryPrimitives.ReadUInt16LittleEndian(resources.Chip.Bytes.AsSpan().Slice(8)));
        Assert.Equal((ushort)12, BinaryPrimitives.ReadUInt16LittleEndian(resources.Dust.Bytes.AsSpan().Slice(8)));
        Assert.Equal((ushort)16, BinaryPrimitives.ReadUInt16LittleEndian(resources.Rock.Bytes.AsSpan().Slice(8)));
        Assert.Equal(1024, resources.ZoneMap.Length);
        Assert.Equal(1024, resources.DelayMap.Length);
        Assert.All(resources.ZoneMap, zone => Assert.InRange(zone, (byte)0, (byte)9));
        Assert.Equal(0x4946Cu, resources.RumbleSound);
        Assert.Equal(0x494E4u, resources.QuakeSound);
    }
}
