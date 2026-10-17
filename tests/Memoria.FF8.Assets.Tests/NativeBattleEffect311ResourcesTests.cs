using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect311ResourcesTests
{
    [Fact]
    public void Original311KeepsItsDirectSpriteCameraTimAndFullHierarchyAcrossSnapshot()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeBattleEffectBinding binding = assets.ReadNativeEffectCatalogForEffects([311])[310];
        Assert.Equal(0x6652C0u, binding.Entry);
        Assert.Equal(0x6652D0u, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag213-310.tim", 0x25055E4), binding.InitializerResource);
        NativeBattleEffect311Resources resources = binding.Effect311Resources!;
        Assert.Equal(0x642C2u, resources.Sound);
        Assert.Equal(0x324, resources.Animation.Data.Length);
        Assert.Equal(6, resources.Sprite.Kind);
        Assert.Equal(50, BinaryPrimitives.ReadUInt16LittleEndian(resources.Sprite.Bytes.AsSpan().Slice(8)));
        Assert.NotEmpty(resources.Sprite.ProjectedFrame571C80(1).Quads);
        Assert.Throws<InvalidDataException>(() => resources.Sprite.SpriteFrame(1));
        NativeBattleGfAnimationSource hierarchy = resources.Hierarchy;
        int table = BinaryPrimitives.ReadInt32LittleEndian(hierarchy.Data.AsSpan().Slice(4));
        Assert.Equal(11, BinaryPrimitives.ReadInt32LittleEndian(hierarchy.Data.AsSpan().Slice(table)));
        Assert.Equal(74, BinaryPrimitives.ReadInt32LittleEndian(hierarchy.Data.AsSpan().Slice(table + 4)));
        NativeBattleGfAnimationWorkspace workspace = new(Enumerable.Repeat((byte)0xAB, 756).ToImmutableArray());
        workspace = workspace.Initialize7016B0(hierarchy, 756);
        for (int frame = 0; frame < 74; frame++)
        {
            NativeBattleGfAnimationWorkspace restored = JsonSerializer.Deserialize<NativeBattleGfAnimationWorkspace>(JsonSerializer.Serialize(workspace))!;
            List<string> firstNodes = [];
            List<string> secondNodes = [];
            NativeBattleGfAnimationAdvance first = workspace.Advance701970(hierarchy, 0, (current, node) =>
            {
                firstNodes.Add(JsonSerializer.Serialize(node));
                return current;
            });
            NativeBattleGfAnimationAdvance second = restored.Advance701970(hierarchy, 0, (current, node) =>
            {
                secondNodes.Add(JsonSerializer.Serialize(node));
                return current;
            });
            Assert.Equal(firstNodes, secondNodes);
            Assert.Equal(11, first.EmittedNodes);
            Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
            workspace = first.Workspace;
        }
        Assert.Equal(0, workspace.Advance701970(hierarchy, 0, (current, _) => throw new InvalidOperationException("Expired hierarchy emitted again.")).EmittedNodes);
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().Register5099A0(resources.Animation);
        textures = textures.EnqueueTim505E30(new(binding.InitializerAsset!.Name, binding.InitializerAsset.Data)).State;
        Assert.NotEmpty(textures.Drain505D20().Uploads);
    }
}
