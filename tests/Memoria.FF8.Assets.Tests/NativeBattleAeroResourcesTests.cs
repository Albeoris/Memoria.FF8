using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleAeroResourcesTests
{
    [Fact]
    public void Original_Aero_binds_both_twelve_byte_geometry_sources_and_separate_particle_mesh()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([118])[117];
        Assert.Equal(0x6C0C80u, binding.Entry);
        Assert.Equal(0x6C0C90u, binding.Initializer);
        Assert.Equal("mag117.tim", binding.InitializerAsset!.Name);
        NativeBattleAeroResources resources = Assert.IsType<NativeBattleAeroResources>(binding.AeroResources);
        Assert.Equal(0xD14, resources.Animation.Data.Length);
        Assert.Equal(0x4944Eu, resources.Sound);
        Assert.Equal(0x1287DBCu, resources.SourceA.Address);
        Assert.Equal(0x4354, resources.SourceA.Data.Length);
        Assert.Equal(0x128C110u, resources.SourceB.Address);
        Assert.Equal(0x149C, resources.SourceB.Data.Length);
        AssertSource(resources.SourceA, 0x3488, [108, 92, 64, 44, 64, 20], [1, 3, 1, 1, 1, 1], [0, 0, 0x101, 0x101, 0x101, 0x102, 0x102, 0x102, 0x103, 0x103, 0x103, 0, 4, 0x1005, 0x1005], 0x4EC);
        AssertSource(resources.SourceB, 0xFD0, [84, 33], [2, 1], [0x8000, 0x8000, 0x8000, 0x8000, 0x1001, 0x1001], 0x184);
        Assert.Equal(0x1287C44u, resources.ParticleMesh.Address);
        Assert.Equal(0x178, resources.ParticleMesh.Bytes.Length);
        for (int group = 0; group < 8; group++)
        {
            Assert.Equal(group == 0 ? 20 : 0, NativeBattleIceMeshVertices.Group(resources.ParticleMesh.Bytes.AsSpan(), group).Count);
        }
        NativeBattleEffectTextureState textures = NativeBattleEffectMemory.FromColdProcess().Textures!.Register5099A0(resources.Animation);
        Assert.Equal(resources.Animation, textures.Animation!.Source);
        Assert.Same(resources, game.ReadNativeAeroResources());
        Assert.Null(game.ReadNativeEffectCatalog()[117].AeroResources);
        string json = JsonSerializer.Serialize(binding);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleEffectBinding>(json)!));
        Assert.Throws<NotSupportedException>(() => NativeBattleAeroResourcesReader.Read(new byte[128]));
    }

    private static void AssertSource(NativeBattleGfAnimationSource source, int table, int[] vertices, int[] shapes, uint[] headers, int workspaceSize)
    {
        uint U32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(offset));
        Assert.Equal((uint)vertices.Length, U32(0));
        Assert.Equal((uint)table, U32(4));
        for (int index = 0; index < vertices.Length; index++)
        {
            int geometry = checked((int)U32(8 + index * 4));
            Assert.Equal((uint)(12 + vertices[index] * shapes[index] * 8), U32(geometry));
            Assert.Equal((uint)vertices[index], U32(geometry + 4));
            Assert.Equal((uint)shapes[index], U32(geometry + 8));
        }
        Assert.Equal((uint)headers.Length, U32(table));
        Assert.Equal(38u, U32(table + 4));
        Assert.Equal(headers, Enumerable.Range(0, headers.Length).Select(index => U32(table + checked((int)U32(table + 8 + index * 4)))).ToArray());
        NativeBattleGfAnimationWorkspace workspace = new(new byte[workspaceSize].ToImmutableArray());
        workspace = workspace.Initialize7016B0(source, workspaceSize);
        for (int frame = 0; frame < 38; frame++)
        {
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (current, _) => current);
            Assert.Equal(37 - frame, advanced.Remaining);
            workspace = advanced.Workspace;
        }
        Assert.Equal(38, workspace.Frame);
    }
}
