using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect213Profile;
using static Memoria.FF8.Runtime.NativeBattleEffect213Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect213ResourceTests
{
    [Fact]
    public void Original_selected_sources_have_bounded_shapes_frames_and_workspaces()
    {
        using GameAssets game = Open();
        NativeBattleEffect213Resources resources = game.ReadNativeEffect213Resources();
        resources.Validate();
        Assert.Equal(137248, resources.MainTim.Data.Length);
        Assert.Equal(392, resources.Camera.Data.Length);
        Assert.Equal(2464, resources.OriginalMutableVertices.Length);
        for (int index = 0; index < ActAddresses.Length; index++)
        {
            Assert.Equal(ActLengths[index], resources.Act(ActAddresses[index]).Data.Length);
            Assert.Equal(ActWorkspaceLengths[index], resources.WorkspaceLength(ActAddresses[index]));
        }
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            Assert.Equal(MeshLengths[index], resources.ReadMesh(MeshAddresses[index]).Bytes.Length);
        }
        Assert.Equal(40, resources.ReadStatic(0x118102C, 40).Length);
        Assert.Equal(12, resources.ReadStatic(0x1181054, 12).Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0x1181054, 13));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(MutableStreamAddress, MutableStreamLength));
    }

    [Fact]
    public void Node_material_tables_reach_every_publisher_index_and_stop_before_the_next_owner()
    {
        using GameAssets game = Open();
        NativeBattleEffect213Resources resources = game.ReadNativeEffect213Resources();
        foreach ((uint address, int nodes, uint end) in new[] { (0x118102Cu, 10, 0x1181054u), (0x1181054u, 3, 0x1181060u) })
        {
            Assert.Equal(end, address + checked((uint)(nodes * 4)));
            for (int node = 0; node < nodes; node++)
            {
                byte[] flags = resources.ReadStatic(address + checked((uint)(node * 4)), 4);
                Assert.Equal(0x30u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(flags));
            }
            Assert.Equal(nodes * 4, resources.StaticFronts[address].Length);
            Assert.Throws<NotSupportedException>(() => resources.ReadStatic(address, nodes * 4 + 1));
        }
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0x1181060, 1));
    }

    [Fact]
    public void Original_resource_JSON_and_current_stream_preserve_distinct_owners()
    {
        using GameAssets game = Open();
        NativeBattleEffect213Resources resources = JsonSerializer.Deserialize<NativeBattleEffect213Resources>(JsonSerializer.Serialize(game.ReadNativeEffect213Resources()))!;
        resources.Validate();
        var bound = NativeBattleEffect213State.FromColdProcess().BindSources(NativeBattleFileStaging.Unknown(), resources);
        NativeBattleFileStaging staging = bound.Staging.Write(MutableStreamAddress, BitConverter.GetBytes((short)1234));
        Assert.Equal(BitConverter.GetBytes((short)1234), resources.ReadCurrentMesh(staging, 0x117ADD8).Bytes.AsSpan(8, 2).ToArray());
        Assert.Equal(resources.SourceStream.Bytes.AsSpan(0, 8).ToArray(), resources.ReadStatic(staging, 0x117ADD8, 8));
        Assert.Equal(resources.SourceStream.Bytes.AsSpan().Slice(8 + MutableStreamLength).ToArray(), resources.ReadStatic(staging, MutableStreamAddress + MutableStreamLength, resources.SourceStream.Bytes.Length - 8 - MutableStreamLength));
        NativeBattleCameraBank bank = new(resources.Camera.Data.AsSpan().Slice(32).ToArray().ToImmutableArray());
        for (byte selector = 0; selector < 8; selector++)
        {
            NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess().Start(bank, selector);
            for (int callback = 0; callback < 2000 && !camera.Order.IsEmpty; callback++)
            {
                camera = camera.Tick(0, new() { TransformReference = (_, point) => point });
            }
            Assert.Empty(camera.Order);
        }
    }

    [Fact]
    public void Selected_catalog_hydrates_same_resource_instance()
    {
        using GameAssets game = Open();
        NativeBattleEffect213Resources resources = game.ReadNativeEffect213Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([213])[212];
        Assert.Same(resources, binding.Effect213Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag212.tim", InitializerGlobal), binding.InitializerResource);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed effect213 resources.");
        return new(directory!);
    }
}
