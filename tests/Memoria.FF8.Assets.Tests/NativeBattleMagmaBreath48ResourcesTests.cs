using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleMagmaBreath48Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleMagmaBreath48ResourcesTests
{
    [Fact]
    public void Original_absolute_source_retains_typed_directories_shapes_and_immutable_curves()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeMagmaBreath48Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeMagmaBreath48Resources());
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(resources.Executable.Data.AsSpan()));
        Assert.Equal(new[] { 7, 6, 2, 10 }, Enumerable.Range(0, 4).Select(table => Enumerable.Range(0, 16).Count(index => BinaryPrimitives.ReadUInt32LittleEndian(resources.Executable.Data.AsSpan().Slice(4 + table * 64 + index * 4)) != 0)).ToArray());
        Assert.Equal(5, resources.Sprites.Length);
        Assert.Equal(10, resources.Shapes.Length);
        Assert.All(resources.MeshSeeds, mesh => Assert.Equal(MeshLength, mesh.Bytes.Length));
        Assert.Equal(CameraLength, resources.Camera.Data.Length);
        Assert.Equal(0x641B8u, resources.Sound);
        Assert.Equal(64, resources.ReadStatic(ColorScriptAddress, 64).Length);
        Assert.Equal(110, resources.ReadStatic(0x15B64AC, 110).Length);
        Assert.Throws<InvalidDataException>(() => resources.ReadStatic(ChannelBase, 2));
        Assert.Throws<InvalidDataException>(() => resources.ReadStatic(SourceAddress, SourceLength));
        Assert.Throws<InvalidDataException>(() => resources.ReadStatic(MeshAAddress, MeshLength));
        Assert.Throws<NotSupportedException>(() => resources.ReadResource(MeshAAddress));
    }

    [Theory]
    [InlineData(4, 49)]
    [InlineData(6, 42)]
    public void Original_type4_animated_anchor_reads_first_and_last_native_emitter_frames(int sourceIndex, int lifetime)
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeMagmaBreath48Resources();
        var state = NativeBattleMagmaBreath48State.FromColdProcess().EnsureSources(resources);
        uint sourceAddress = BinaryPrimitives.ReadUInt32LittleEndian(resources.Executable.Data.AsSpan().Slice(4 + sourceIndex * 4));
        byte[] source = resources.ReadStatic(sourceAddress, 344);
        Assert.Equal((byte)4, source[0x11]);
        Assert.Equal(lifetime, source[0x12]);
        foreach (short frame in new short[] { 0, (short)(lifetime - 1) })
        {
            NativeBattleAnimatedAnchorChannels channels = NativeBattleEffectAnimatedAnchor.ReadChannels(source, frame, ReadCurrent);
            Assert.InRange(channels.StartActor, (byte)0, (byte)1);
            Assert.InRange(channels.EndActor, (byte)0, (byte)1);
            Assert.InRange(channels.StartAnchor, (byte)0, (byte)3);
            Assert.InRange(channels.EndAnchor, (byte)0, (byte)3);
        }
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectAnimatedAnchor.ReadChannels(source, (short)lifetime, ReadCurrent));
        var unknown = NativeBattleMagmaBreath48State.FromUnknown();
        unknown.Validate();
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectAnimatedAnchor.ReadChannels(source, 0, (address, length) => address >= ChannelBase ? unknown.Read(address, length) : resources.ReadStatic(address, length)));

        byte[] ReadCurrent(uint address, int length) => address >= ChannelBase ? state.Read(address, length) : resources.ReadStatic(address, length);
    }

    [Fact]
    public void Original_current_source_mesh_and_channel_owners_retain_warm_changes_without_reseeding()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeMagmaBreath48Resources();
        var state = NativeBattleMagmaBreath48State.FromColdProcess().EnsureSources(resources);
        uint vertex = MeshAAddress + 8;
        ushort originalW = state.ReadWord(vertex + 6);
        state = state.Write(vertex, new byte[] { 1, 2, 3, 4, 5, 6 }).WriteWord(ChannelBase, 1234);
        Assert.Equal(originalW, state.ReadWord(vertex + 6));
        var current = state.ReadMesh(resources, MeshAAddress);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, current.Bytes.AsSpan().Slice(8, 6).ToArray());
        Assert.Equal(resources.MeshSeeds[0].Bytes.AsSpan().Slice(14, 2).ToArray(), current.Bytes.AsSpan().Slice(14, 2).ToArray());
        var restored = JsonSerializer.Deserialize<NativeBattleMagmaBreath48State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Same(restored, restored.EnsureSources(resources));
        Assert.Equal((ushort)1234, restored.ReadWord(ChannelBase));
        Assert.Equal(current.Bytes.ToArray(), restored.ReadMesh(resources, MeshAAddress).Bytes.ToArray());
        var restoredResources = JsonSerializer.Deserialize<NativeBattleMagmaBreath48Resources>(JsonSerializer.Serialize(resources))!;
        restoredResources.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restoredResources));
    }

    [Fact]
    public void Truncated_or_inconsistent_original_fronts_fail_before_current_source_binding()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeMagmaBreath48Resources();
        Assert.Throws<InvalidDataException>(() => (resources with { Executable = resources.Executable with { Data = resources.Executable.Data.SetItem(0, 0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { MeshSeeds = resources.MeshSeeds.SetItem(0, resources.MeshSeeds[0] with { Bytes = resources.MeshSeeds[0].Bytes.RemoveAt(0) }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sprites = resources.Sprites.SetItem(0, resources.Sprites[0] with { Bytes = resources.Sprites[0].Bytes.RemoveAt(0) }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Shapes = resources.Shapes.SetItem(0, resources.Shapes[0] with { Bytes = resources.Shapes[0].Bytes.SetItem(4, 0) }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Camera = resources.Camera with { Data = resources.Camera.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { ColorScript = resources.ColorScript.SetItem(15, 0) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sound = 0 }).Validate());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Magma Breath resources.");
        return new(directory!);
    }
}
