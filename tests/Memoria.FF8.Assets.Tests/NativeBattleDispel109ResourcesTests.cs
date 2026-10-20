using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDispel109ResourcesTests
{
    [Fact]
    public void Original_resources_include_complete_animation_camera_sprite_and_sound_banks()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleDispel109Resources resources = game.ReadNativeDispel109Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([109])[108];
        Assert.Equal(0x6CF4A0u, binding.Entry);
        Assert.Equal(0x6CF4B0u, binding.Initializer);
        Assert.Same(resources, binding.Dispel109Resources);
        NativeExecutableMemory memory = new(game.ReadNativeExecutable());
        Assert.Equal(memory.Read(0x12E3600, 0x2E10), resources.Animation.Data.ToArray());
        Assert.Equal(memory.Read(0x12E6410, 0x828), resources.Camera.Data.ToArray());
        Assert.Equal(memory.Read(0x12E337C, 0xC4), resources.Swirl.Bytes.ToArray());
        Assert.Equal(memory.Read(0x12E3440, 0x1C0), resources.Spark.Bytes.ToArray());
        Assert.Equal(0x4941Eu, resources.Sound);
        Assert.Equal((ushort)7, BinaryPrimitives.ReadUInt16LittleEndian(resources.Swirl.Bytes.AsSpan().Slice(8)));
        Assert.Equal((ushort)16, BinaryPrimitives.ReadUInt16LittleEndian(resources.Spark.Bytes.AsSpan().Slice(8)));
        Assert.Equal(3, BinaryPrimitives.ReadInt32LittleEndian(resources.Animation.Data.AsSpan()));
        int table = BinaryPrimitives.ReadInt32LittleEndian(resources.Animation.Data.AsSpan().Slice(4));
        Assert.Equal(10, BinaryPrimitives.ReadInt32LittleEndian(resources.Animation.Data.AsSpan().Slice(table)));
        Assert.Equal(46, BinaryPrimitives.ReadInt32LittleEndian(resources.Animation.Data.AsSpan().Slice(table + 4)));
        for (int phase = 0; phase < 16; phase++)
        {
            Assert.NotEmpty(resources.Spark.ProjectedFrame571C80(phase).Quads);
            if (phase < 7)
            {
                Assert.NotEmpty(resources.Swirl.ProjectedFrame571C80(phase).Quads);
            }
        }
    }

    [Fact]
    public void Original_animation_fits_exact_808_byte_workspace_and_all_geometry_shape_accesses()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleDispel109Resources resources = game.ReadNativeDispel109Resources();
        NativeBattleGfAnimationWorkspace workspace = new(new byte[808].ToImmutableArray());
        workspace = workspace.Initialize7016B0(resources.Animation, 808);
        int callbacks = 0;
        HashSet<ushort> geometries = [];
        for (int age = 0; age < 73; age++)
        {
            workspace = workspace.Advance701970(resources.Animation, 0, (current, node) =>
            {
                callbacks++;
                geometries.Add(node.GeometryIndex);
                int geometry = BinaryPrimitives.ReadInt32LittleEndian(resources.Animation.Data.AsSpan().Slice(8 + node.GeometryIndex * 4));
                int count = BinaryPrimitives.ReadInt32LittleEndian(resources.Animation.Data.AsSpan().Slice(geometry + 4));
                foreach (short shape in new[] { node.Word40, node.Word42 })
                {
                    Assert.Equal(count * 8, resources.Animation.Data.AsSpan().Slice(geometry + 12 + shape * count * 8, count * 8).Length);
                }
                return current;
            }).Workspace;
        }
        Assert.Equal(46, workspace.Frame);
        Assert.Equal(460, callbacks);
        Assert.Equal(new ushort[] { 0, 1, 2 }, geometries.Order().ToArray());
    }
}
