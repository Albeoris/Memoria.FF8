using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleOilShot136Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleOilShot136ResourcesTests
{
    [Fact]
    public void Original_ACTs_keep_all_six_typed_fronts_and_workspaces()
    {
        using GameAssets game = Open();
        NativeBattleOilShot136Resources resources = game.ReadNativeOilShot136Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeOilShot136Resources());
        AssertShapes(resources.MainAct, [64], [2]);
        AssertShapes(resources.SecondaryAct, [63, 63], [3, 2]);
        AssertShapes(resources.ImpactAct, [64], [2]);
        AssertShapes(resources.EmitterAct, [4, 4], [1, 1]);
        AssertShapes(resources.LargeParticleAct, [4, 4], [1, 1]);
        AssertShapes(resources.LargeImpactAct, [64], [1]);
        AssertWorkspace(resources.MainAct, 0xC8, 8);
        AssertWorkspace(resources.SecondaryAct, 0x1F4, 26);
        AssertWorkspace(resources.ImpactAct, 0x184, 38);
        AssertWorkspace(resources.EmitterAct, 0xA8, 17);
        AssertWorkspace(resources.LargeParticleAct, 0x50, 50);
        AssertWorkspace(resources.LargeImpactAct, 0xA8, 14);
        Assert.Equal(108 * 8, MorphBytes);
        Assert.Equal(2049, resources.DeformationTable.Length);
        Assert.Equal((short)27, BinaryPrimitives.ReadInt16LittleEndian(resources.BoneAnchor.AsSpan().Slice(6)));
        Assert.Equal(0x641E0u, resources.Sound);
    }

    [Fact]
    public void Mutable_PE_sources_seed_once_and_keep_mesh_W_and_matrix_changes_after_JSON()
    {
        using GameAssets game = Open();
        NativeBattleOilShot136Resources resources = game.ReadNativeOilShot136Resources();
        NativeBattleOilShot136State state = NativeBattleOilShot136State.FromColdProcess().SeedColdSources(resources);
        Assert.Equal(resources.InitialMesh.Bytes.ToArray(), state.ReadMesh().ToArray());
        Assert.Equal(resources.InitialScaleMatrix32.Bytes.ToArray(), state.Read(MatrixAddress, 32));
        byte[] mesh = state.ReadMesh().ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(14), 4096);
        state = state.WriteMesh(mesh.ToImmutableArray()).WriteWord(MatrixAddress + 16, 1234);
        NativeBattleOilShot136State restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleOilShot136State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        restored.Validate();
        Assert.Same(state, state.SeedColdSources(resources));
        Assert.Same(restored, restored.SeedColdSources(resources));
        Assert.Equal((short)4096, BinaryPrimitives.ReadInt16LittleEndian(restored.ReadMesh().AsSpan().Slice(14)));
        Assert.Equal((ushort)1234, restored.ReadWord(MatrixAddress + 16));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
        Assert.Throws<NotSupportedException>(() => NativeBattleOilShot136State.FromUnknown().SeedColdSources(resources));
    }

    [Fact]
    public void Resource_roundtrip_and_strict_extents_reject_missing_inclusive_deformation_entry()
    {
        using GameAssets game = Open();
        NativeBattleOilShot136Resources resources = game.ReadNativeOilShot136Resources();
        NativeBattleOilShot136Resources restored = JsonSerializer.Deserialize<NativeBattleOilShot136Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        Assert.Same(restored.MainAct, restored.Act(MainActAddress));
        Assert.Same(restored.SpriteB, restored.Sprite(SpriteBAddress));
        Assert.Throws<NotSupportedException>(() => restored.Act(MeshAddress));
        Assert.Throws<NotSupportedException>(() => restored.Sprite(MainActAddress));
        Assert.Throws<InvalidDataException>(() => (restored with { DeformationTable = restored.DeformationTable.Take(2048).ToImmutableArray() }).Validate());
        Assert.Throws<InvalidDataException>(() => (restored with { LargeImpactAct = restored.LargeImpactAct with { Data = restored.LargeImpactAct.Data.Take(0x887).ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (restored with { InitialMesh = restored.InitialMesh with { Bytes = restored.InitialMesh.Bytes.Take(MeshBytes - 1).ToImmutableArray() } }).Validate());
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
        Assert.SkipUnless(directory is not null, "Select installed original Oil Shot resources.");
        return new(directory!);
    }
}
