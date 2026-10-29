using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleBreath129Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleBreath129ResourcesTests
{
    [Fact]
    public void Original_four_ACT_workspaces_sprite_camera_and_exact_control_tables_are_complete()
    {
        using GameAssets game = Open();
        NativeBattleBreath129Resources resources = game.ReadNativeBreath129Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeBreath129Resources());
        uint[] addresses = [FirstActAddress, SecondActAddress, ThirdActAddress, FourthActAddress];
        int[] lengths = [0x1AB0, 0xC30, 0x7F4, 0xA50];
        int[] workspaces = [0x108, 0x110, 0x68, 0x188];
        for (int index = 0; index < addresses.Length; index++)
        {
            NativeBattleGfAnimationSource act = resources.Act(addresses[index]);
            Assert.Equal(lengths[index], act.Data.Length);
            NativeBattleGfAnimationWorkspace workspace = new(new byte[workspaces[index]].ToImmutableArray());
            _ = workspace.Initialize7016B0(act, workspaces[index]);
        }
        Assert.Equal(0x1EF0, resources.SourceSeeds.Data.Length);
        Assert.Equal(0x128, resources.Camera.Data.Length);
        Assert.Equal(0x6F0, resources.ControlTriangles.Length);
        Assert.Equal(0x500, resources.Streams.Length);
        Assert.Equal(8192, resources.TrigonometricWords8192.Length);
        Assert.Equal(0x641DAu, resources.Sound);
        Assert.Equal(7, BinaryPrimitives.ReadInt16LittleEndian(resources.ControlPairs.AsSpan().Slice(30)));
        Assert.Throws<NotSupportedException>(() => resources.Read(IndexedVerticesAddress + 8, 8));
        Assert.Throws<NotSupportedException>(() => resources.Act(MeshAddress));
    }

    [Fact]
    public void Original_preprocessing_respects_both_exact_extents_and_real_camera_tail_read()
    {
        using GameAssets game = Open();
        NativeBattleBreath129Resources resources = game.ReadNativeBreath129Resources();
        NativeBattleBreath129State state = NativeBattleBreath129State.FromColdProcess().RequireSeeds(resources);
        NativeBattleBreath129Frame frame = new(NativeBattleEffectMemory.FromColdProcess(), new(129, 128, new(129, Entry, Initializer), null, 0), new(0), state);
        NativeBattleBreath129Frame next = NativeBattleBreath129Preprocessing.Initialize(frame, resources);
        short[] first = Enumerable.Range(0, VertexCount).Select(index => unchecked((short)next.State.ReadWord(MeshAddress + 14 + (uint)(index * 8)))).ToArray();
        short[] second = Enumerable.Range(0, 88).Select(index => unchecked((short)next.State.ReadWord(IndexedVerticesAddress + 14 + (uint)(index * 8)))).ToArray();
        Assert.Equal(-4096, first.Min());
        Assert.Equal(0, first.Max());
        Assert.Equal(-4075, second.Min());
        Assert.Equal(0, second.Max());
        Assert.Equal(32, BinaryPrimitives.ReadInt16LittleEndian(resources.Read(CameraAddress + 4, 2).AsSpan()));
        Assert.Equal(resources.SourceSeeds.Data.AsSpan(8, 6).ToArray(), next.State.Read(MeshAddress + 8, 6));
        Assert.Same(next.State, next.State.RequireSeeds(resources));
        NativeBattleBreath129State restored = NativeBattleSnapshotJson.Deserialize<NativeBattleBreath129State>(NativeBattleSnapshotJson.Serialize(next.State))!;
        restored.Validate();
        Assert.Equal(NativeBattleSnapshotJson.Serialize(next.State), NativeBattleSnapshotJson.Serialize(restored));
    }

    [Fact]
    public void Immutable_resource_roundtrip_rejects_changed_shapes_table_aliases_and_pair_ranges()
    {
        using GameAssets game = Open();
        NativeBattleBreath129Resources resources = game.ReadNativeBreath129Resources();
        NativeBattleBreath129Resources restored = NativeBattleSnapshotJson.Deserialize<NativeBattleBreath129Resources>(NativeBattleSnapshotJson.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(NativeBattleSnapshotJson.Serialize(resources), NativeBattleSnapshotJson.Serialize(restored));
        Assert.Throws<InvalidDataException>(() => (resources with { SourceSeeds = resources.SourceSeeds with { Data = resources.SourceSeeds.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { SqrtWords256 = resources.SqrtWords256.SetItem(0, (ushort)(resources.SqrtWords256[0] ^ 1)) }).Validate());
        byte[] pairs = resources.ControlPairs.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(pairs.AsSpan(28), 74);
        Assert.Throws<InvalidDataException>(() => (resources with { ControlPairs = pairs.ToImmutableArray() }).Validate());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Breath resources.");
        return new(directory!);
    }
}
