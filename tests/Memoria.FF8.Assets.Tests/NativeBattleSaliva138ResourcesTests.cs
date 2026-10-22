using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleSaliva138Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleSaliva138ResourcesTests
{
    [Fact]
    public void Original_ACTs_keep_direct_144_vertices_distinct_from_interpolated_48()
    {
        using GameAssets game = Open();
        NativeBattleSaliva138Resources resources = game.ReadNativeSaliva138Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeSaliva138Resources());
        AssertShapes(resources.MainAct, [144], [1]);
        AssertShapes(resources.SecondaryAct, [48], [3]);
        AssertShapes(resources.ImpactAct, [33, 49], [2, 1]);
        AssertWorkspace(resources.MainAct, 0xA8, 10);
        AssertWorkspace(resources.SecondaryAct, 0x194, 14);
        AssertWorkspace(resources.ImpactAct, 0x118, 19);
        Assert.True(144 * 8 > MorphBytes);
        Assert.True(48 * 8 <= MorphBytes);
        Assert.Equal(2049, resources.DeformationTable.Length);
        Assert.Equal(0x4000, resources.DistanceTable.Length);
        Assert.Equal(new short[] { 0, 0, 0, 1, 4, 9, 16, 25, 36, 49, 64, 72, 72, 64, 49, 36, 25, 16 }, resources.AgeDrift.ToArray());
        Assert.Equal((short)13, BinaryPrimitives.ReadInt16LittleEndian(resources.BoneAnchor.AsSpan().Slice(6)));
        Assert.Equal(0x641B5u, resources.Sound);
    }

    [Fact]
    public void Mutable_PE_sources_seed_once_and_keep_mesh_W_and_matrix_changes_after_JSON()
    {
        using GameAssets game = Open();
        NativeBattleSaliva138Resources resources = game.ReadNativeSaliva138Resources();
        NativeBattleSaliva138State state = NativeBattleSaliva138State.FromColdProcess().SeedColdSources(resources);
        Assert.Equal(resources.InitialMesh.Bytes.ToArray(), state.ReadMesh().ToArray());
        Assert.Equal(resources.InitialScaleMatrix32.Bytes.ToArray(), state.Read(MatrixAddress, 32));
        byte[] mesh = state.ReadMesh().ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(14), 4096);
        state = state.WriteMesh(mesh.ToImmutableArray()).WriteWord(MatrixAddress + 16, 1234);
        NativeBattleSaliva138State restored = JsonSerializer.Deserialize<NativeBattleSaliva138State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Same(state, state.SeedColdSources(resources));
        Assert.Same(restored, restored.SeedColdSources(resources));
        Assert.Equal((short)4096, BinaryPrimitives.ReadInt16LittleEndian(restored.ReadMesh().AsSpan().Slice(14)));
        Assert.Equal((ushort)1234, restored.ReadWord(MatrixAddress + 16));
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
        Assert.Throws<NotSupportedException>(() => NativeBattleSaliva138State.FromUnknown().SeedColdSources(resources));
    }

    [Fact]
    public void Resource_roundtrip_and_strict_extents_reject_missing_inclusive_deformation_entry()
    {
        using GameAssets game = Open();
        NativeBattleSaliva138Resources resources = game.ReadNativeSaliva138Resources();
        NativeBattleSaliva138Resources restored = JsonSerializer.Deserialize<NativeBattleSaliva138Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        Assert.Same(restored.MainAct, restored.Act(MainActAddress));
        Assert.Same(restored.SpriteB, restored.Sprite(SpriteBAddress));
        Assert.Throws<NotSupportedException>(() => restored.Act(MeshAddress));
        Assert.Throws<NotSupportedException>(() => restored.Sprite(MainActAddress));
        Assert.Throws<InvalidDataException>(() => (restored with { DeformationTable = restored.DeformationTable.Take(2048).ToImmutableArray() }).Validate());
    }

    private static void AssertShapes(NativeBattleGfAnimationSource source, int[] vertices, int[] variants)
    {
        ReadOnlySpan<byte> bytes = source.Data.AsSpan();
        Assert.Equal(vertices.Length, BinaryPrimitives.ReadInt32LittleEndian(bytes));
        for (int index = 0; index < vertices.Length; index++)
        {
            int offset = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(8 + index * 4));
            Assert.Equal(vertices[index], BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 4)));
            Assert.Equal(variants[index], BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 8)));
        }
    }
    private static void AssertWorkspace(NativeBattleGfAnimationSource source, int size, int duration)
    {
        NativeBattleGfAnimationWorkspace workspace = new(new byte[size].ToImmutableArray());
        workspace = workspace.Initialize7016B0(source, size);
        int emitted = 0;
        for (int frame = 0; frame < duration; frame++)
        {
            NativeBattleGfAnimationAdvance next = workspace.Advance701970(source, 0, (current, node) => current);
            emitted += next.EmittedNodes;
            workspace = next.Workspace;
        }
        Assert.True(emitted > 0);
        Assert.Equal(duration, workspace.Frame);
        Assert.Equal(0, workspace.Advance701970(source, 0, (current, node) => current).EmittedNodes);
    }
    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Saliva resources.");
        return new(directory!);
    }
}
