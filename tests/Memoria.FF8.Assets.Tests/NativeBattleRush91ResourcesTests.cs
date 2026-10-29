using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRush91ResourcesTests
{
    [Fact]
    public void Original_Rush_retains_its_independent_model_and_complete_raw_resource_owners()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([91])[90];
        Assert.Equal(0x759370u, binding.Entry);
        Assert.Equal(0x759350u, binding.Initializer);
        Assert.Equal("mag090.tim", binding.InitializerAsset!.Name);
        NativeBattleRush91Resources resources = Assert.IsType<NativeBattleRush91Resources>(binding.Rush91Resources);
        Assert.Same(resources, game.ReadNativeRush91Resources());
        Assert.Equal("FF8_EN:154328C", resources.Animation.Identity);
        Assert.Equal(0x5F4, resources.Animation.Data.Length);
        Assert.Equal(0x1543880u, resources.Model.Address);
        Assert.Equal(0x38B4, resources.Model.Bytes.Length);
        Assert.Equal(34, resources.Model.PoseSource.BoneCount);
        Assert.Equal(new byte[] { 6, 2, 2, 4 }, NativeBattlePoseDecoder.ReadFrameCounts(resources.Model.PoseSource.Animations.AsSpan()).ToArray());
        Assert.Equal(302, Assert.Single(resources.Model.Geometry).Vertices.Length);
        Assert.Equal(game.ReadNativeRecover93Resources().Model.Bytes.AsSpan().Slice(0x688, 0x2CBC).ToArray(), resources.Model.Bytes.AsSpan().Slice(0x688, 0x2CBC).ToArray());
        Assert.Equal("FF8_EN:16CF8B0", resources.ModelTim.Identity);
        Assert.Equal(0x8420, resources.ModelTim.Data.Length);
        Assert.Equal(0x32808080u, resources.DefaultColor44);
        Assert.Equal(0x15430E8u, resources.Sprite12.Address);
        Assert.Equal(0x144, resources.Sprite12.Bytes.Length);
        Assert.Equal(12, BinaryPrimitives.ReadUInt16LittleEndian(resources.Sprite12.Bytes.AsSpan().Slice(8)));
        Assert.Equal(0x154322Cu, resources.ParticleMesh.Address);
        Assert.Equal(0x60, resources.ParticleMesh.Bytes.Length);
        Assert.Equal(4u, U32(resources.ParticleMesh.Bytes, 4));
        Assert.Equal(0x10, resources.HeightProgram.Data.Length);
        Assert.Equal("FF8_EN:1547134", resources.HeightProgram.Identity);
        Assert.Equal(5, resources.OverlayProgram.Commands.Length);
        Assert.Equal(0x1547144u, resources.OverlayProgram.Address);
        Assert.Equal(game.ReadNativeRushOverlayProgram().Commands.ToArray(), resources.OverlayProgram.Commands.ToArray());
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().Register5099A0(resources.Animation);
        var upload = textures.EnqueueTim505E30(resources.ModelTim);
        Assert.Equal(0x8420, upload.NextOffset);
        Assert.NotEmpty(upload.State.Drain505D20().Uploads);
        string json = NativeBattleSnapshotJson.Serialize(resources);
        Assert.Equal(json, NativeBattleSnapshotJson.Serialize(NativeBattleSnapshotJson.Deserialize<NativeBattleRush91Resources>(json)!));
        Assert.Null(game.ReadNativeEffectCatalog()[90].Rush91Resources);
        Assert.Throws<NotSupportedException>(() => NativeBattleRush91ResourcesReader.Read(new byte[128]));
    }

    [Fact]
    public void Original_Rush_neutral_bank_preserves_complete_cold_directories_without_constructing_mutable_state()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleEffectHeapAllocation resource = game.ReadNativeRush91Resources().Nodes;
        Assert.Equal("FF8_EN:16CB3FC", resource.Identity);
        ImmutableArray<byte> bytes = resource.Data;
        Assert.Equal(0xEF0, bytes.Length);
        Assert.Equal(0u, U32(bytes, 0));
        Assert.Equal(new uint[] { 0xAE8, 0xC40, 0xD98 }, Enumerable.Range(0, 3).Select(index => U32(bytes, 4 + index * 4)).ToArray());
        Assert.Equal((uint)bytes.Length, U32(bytes, 12) + 0x158);
        Assert.Equal(0x104u, U32(bytes, 0x44));
        foreach (int meshOffset in new int[] { 0x298, 0x368 })
        {
            Assert.Equal(9u, U32(bytes, meshOffset + 4));
            for (int group = 0; group < 8; group++)
            {
                Assert.Equal(group == 3 ? 4 : 0, NativeBattleIceMeshVertices.Group(bytes.AsSpan().Slice(meshOffset), group).Count);
            }
        }
        Assert.All(Enumerable.Range(0, 16), index => Assert.Equal(0u, U32(bytes, 0xC4 + index * 4)));
    }

    [Fact]
    public void Original_Rush_non_direct_three_shape_program_advances_and_cold_restores_without_a_draw_adapter()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGfAnimationSource source = game.ReadNativeRush91Resources().ProgramSource;
        Assert.Equal(0x16CC2ECu, source.Address);
        Assert.Equal(0x35C4, source.Data.Length);
        Assert.Equal(2u, U32(source.Data, 0x33F8));
        Assert.Equal(48u, U32(source.Data, 0x33FC));
        Assert.Equal(0u, U32(source.Data, 0x3408));
        Assert.Equal(1u, U32(source.Data, 0x3534));
        NativeBattleGfAnimationWorkspace workspace = new(new byte[0x70].ToImmutableArray());
        workspace = workspace.Initialize7016B0(source, 0x70);
        int nodes = 0;
        for (int frame = 0; frame < 48; frame++)
        {
            string? workspaceCheckpoint = frame == 23 ? NativeBattleSnapshotJson.Serialize(workspace) : null;
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (current, node) =>
            {
                Assert.Equal(0x3010u, node.AnimationFlags);
                Assert.Equal(0u, node.Header & 0x200);
                return current;
            });
            Assert.Equal(47 - frame, advanced.Remaining);
            if (frame == 23)
            {
                NativeBattleGfAnimationWorkspace restored = NativeBattleSnapshotJson.Deserialize<NativeBattleGfAnimationWorkspace>(workspaceCheckpoint!)!;
                NativeBattleGfAnimationAdvance cold = restored.Advance701970(source, 0, (current, _) => current);
                Assert.Equal(NativeBattleSnapshotJson.Serialize(advanced), NativeBattleSnapshotJson.Serialize(cold));
            }
            nodes += advanced.EmittedNodes;
            workspace = advanced.Workspace;
        }
        Assert.Equal(48, workspace.Frame);
        Assert.True(nodes > 0);
    }

    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
}
