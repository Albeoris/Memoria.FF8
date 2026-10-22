using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleOilBlast137ResourcesTests
{
    [Fact]
    public void Original_impact_geometry_zero_with_GT3_is_visible_during_the_actual_caller_interval()
    {
        using GameAssets game = Open();
        NativeBattleOilBlast137Resources resources = game.ReadNativeOilBlast137Resources();
        NativeBattleGfAnimationSource source = resources.ImpactAct;
        int geometry = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(8));
        int cursor = geometry + BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(geometry));
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 6; group++)
        {
            cursor += 4 + BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(cursor)) * strides[group];
        }
        Assert.Equal(10, BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(cursor)));
        NativeBattleGfAnimationWorkspace workspace = new(new byte[0x2FC].ToImmutableArray());
        workspace = workspace.Initialize7016B0(source, 0x2FC);
        List<(int Age, ushort Node, uint Header)> reached = [];
        for (int frame = 0; frame < 26; frame++)
        {
            int callerAge = 28 + frame;
            NativeBattleGfAnimationAdvance step = workspace.Advance701970(source, 0, (current, node) =>
            {
                if (node.GeometryIndex == 0 && NativeBattleActMeshRendering.Visible(node))
                {
                    reached.Add((callerAge, node.NodeIndex, node.Header));
                }
                return current;
            });
            workspace = step.Workspace;
        }
        Assert.NotEmpty(reached);
        Assert.All(reached, item =>
        {
            Assert.InRange(item.Age, 28, 53);
            Assert.Equal(0u, item.Header);
        });
        Assert.True(reached.Select(item => item.Node).Distinct().Count() >= 2);
        Assert.Equal(26, workspace.Frame);
    }

    [Fact]
    public void Original_sources_keep_all_selected_geometry_groups_and_exact_morph_capacity()
    {
        using GameAssets game = Open();
        NativeBattleOilBlast137Resources resources = game.ReadNativeOilBlast137Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeOilBlast137Resources());
        Assert.Equal(0x641B2u, resources.Sound);
        Assert.Equal(0x254, resources.Camera.Data.Length);
        AssertShapes(resources.OpeningAct, [49], [1]);
        AssertShapes(resources.MainAct, [49, 52, 30, 30], [1, 2, 1, 1]);
        AssertShapes(resources.ImpactAct, [89, 68, 44, 30, 30, 49], [2, 1, 1, 2, 1, 1]);
        Assert.Equal(89 * 8, NativeBattleOilBlast137Profile.MorphBytes);
        AssertWorkspace(resources.OpeningAct, 0x40, 9);
        AssertWorkspace(resources.MainAct, 0x1A0, 20);
        AssertWorkspace(resources.ImpactAct, 0x2FC, 26);
        AssertMesh(resources.CustomMesh, [0, 0, 0, 48, 16, 32, 0, 72]);
        AssertMesh(resources.StaticMesh, [1, 0, 0, 0, 11, 0, 0, 0]);
    }

    [Fact]
    public void Actual_mutable_matrix_seed_is_once_only_and_warm_JSON_never_recopies_it()
    {
        using GameAssets game = Open();
        NativeBattleOilBlast137Resources resources = game.ReadNativeOilBlast137Resources();
        NativeBattleOilBlast137State state = NativeBattleOilBlast137State.FromColdProcess().SeedColdSource(resources);
        Assert.False(state.ColdMatrixSourcePending);
        Assert.Equal(resources.InitialMatrix32.Bytes.ToArray(), state.Read(NativeBattleOilBlast137Profile.MatrixAddress, 32));
        Assert.Equal((ushort)4096, state.ReadWord(NativeBattleOilBlast137Profile.MatrixAddress));
        Assert.Equal((ushort)0, state.ReadWord(NativeBattleOilBlast137Profile.MatrixAddress + 18));
        state = state.WriteWord(NativeBattleOilBlast137Profile.MatrixAddress, 123).WriteWord(NativeBattleOilBlast137Profile.MatrixAddress + 18, 456);
        NativeBattleOilBlast137State restored = JsonSerializer.Deserialize<NativeBattleOilBlast137State>(JsonSerializer.Serialize(state))!;
        Assert.Same(state, state.SeedColdSource(resources));
        Assert.Same(restored, restored.SeedColdSource(resources));
        Assert.Equal((ushort)123, restored.ReadWord(NativeBattleOilBlast137Profile.MatrixAddress));
        Assert.Equal((ushort)456, restored.ReadWord(NativeBattleOilBlast137Profile.MatrixAddress + 18));
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
        Assert.Throws<NotSupportedException>(() => NativeBattleOilBlast137State.FromUnknown().SeedColdSource(resources));
    }

    [Fact]
    public void Resource_JSON_and_strict_resolver_preserve_three_independent_sources()
    {
        using GameAssets game = Open();
        NativeBattleOilBlast137Resources source = game.ReadNativeOilBlast137Resources();
        NativeBattleOilBlast137Resources restored = JsonSerializer.Deserialize<NativeBattleOilBlast137Resources>(JsonSerializer.Serialize(source))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(restored));
        Assert.Same(restored.OpeningAct, restored.Act(NativeBattleOilBlast137Profile.OpeningActAddress));
        Assert.Same(restored.MainAct, restored.Act(NativeBattleOilBlast137Profile.MainActAddress));
        Assert.Same(restored.ImpactAct, restored.Act(NativeBattleOilBlast137Profile.ImpactActAddress));
        Assert.Throws<NotSupportedException>(() => restored.Act(NativeBattleOilBlast137Profile.StaticMeshAddress));
        Assert.Throws<InvalidDataException>(() => (restored with { InitialMatrix32 = restored.InitialMatrix32 with { Bytes = restored.InitialMatrix32.Bytes.Take(30).ToImmutableArray() } }).Validate());
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
        for (int index = 0; index < duration; index++)
        {
            NativeBattleGfAnimationAdvance next = workspace.Advance701970(source, 0, (current, node) => current);
            emitted += next.EmittedNodes;
            workspace = next.Workspace;
        }
        Assert.True(emitted > 0);
        Assert.Equal(duration, workspace.Frame);
        Assert.Equal(0, workspace.Advance701970(source, 0, (current, node) => current).EmittedNodes);
    }

    private static void AssertMesh(NativeBattleStaticMeshResource mesh, int[] groups)
    {
        ReadOnlySpan<byte> bytes = mesh.Bytes.AsSpan();
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int index = 0; index < 8; index++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(cursor));
            Assert.Equal(groups[index], count);
            cursor += 4 + count * strides[index];
        }
        Assert.Equal(bytes.Length, cursor);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Oil Blast resources.");
        return new(directory!);
    }
}
