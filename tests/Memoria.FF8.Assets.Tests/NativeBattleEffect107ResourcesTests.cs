using System.Buffers.Binary;
using System.Text.Json;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect107ResourcesTests
{
    [Fact]
    public void Original107MaterializesItsActualTimCameraSixSpritesHierarchyAndInclusiveLookupBytes()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        Assert.Null(assets.ReadNativeEffectCatalog()[106].Effect107Resources);
        NativeBattleEffectBinding binding = assets.ReadNativeEffectCatalogForEffects([107])[106];
        Assert.Equal(0x6D1310u, binding.Entry);
        Assert.Equal(0x6D1320u, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag106.tim", 0x2521B30), binding.InitializerResource);
        Assert.Equal("mag106.tim", binding.InitializerAsset!.Name);
        NativeBattleEffect107Resources resources = binding.Effect107Resources!;
        Assert.Equal(0x49421u, resources.Sound);
        Assert.Equal(0x90C, resources.Animation.Data.Length);
        Assert.Equal(new[] { 6, 8, 11, 13, 13, 9 }, resources.Sprites.Select(sprite => sprite.Bytes[8] | sprite.Bytes[9] << 8));
        Assert.Single(resources.Read(0x12F5978 + (128 << 7) + 128, 1));
        NativeBattleGfAnimationSource hierarchy = resources.Hierarchy;
        int table = BinaryPrimitives.ReadInt32LittleEndian(hierarchy.Data.AsSpan().Slice(4));
        Assert.Equal(12, BinaryPrimitives.ReadInt32LittleEndian(hierarchy.Data.AsSpan().Slice(table)));
        Assert.Equal(76, BinaryPrimitives.ReadInt32LittleEndian(hierarchy.Data.AsSpan().Slice(table + 4)));
        NativeBattleGfAnimationWorkspace workspace = new(Enumerable.Repeat((byte)0xAB, 632).ToImmutableArray());
        workspace = workspace.Initialize7016B0(hierarchy, 632);
        List<int> firstNodeFrames = [];
        for (int frame = 0; frame < 76; frame++)
        {
            NativeBattleGfAnimationWorkspace restored = JsonSerializer.Deserialize<NativeBattleGfAnimationWorkspace>(JsonSerializer.Serialize(workspace))!;
            List<string> emitted = [];
            NativeBattleGfAnimationAdvance first = workspace.Advance701970(hierarchy, 0, (current, node) =>
            {
                if (node.NodeIndex == 0)
                {
                    firstNodeFrames.Add(current.Frame);
                }
                emitted.Add(JsonSerializer.Serialize(node));
                return current;
            });
            List<string> cloneEmitted = [];
            NativeBattleGfAnimationAdvance second = restored.Advance701970(hierarchy, 0, (current, node) =>
            {
                cloneEmitted.Add(JsonSerializer.Serialize(node));
                return current;
            });
            Assert.Equal(emitted, cloneEmitted);
            Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
            Assert.Equal(12, first.EmittedNodes);
            workspace = first.Workspace;
        }
        Assert.Equal(Enumerable.Range(0, 76), firstNodeFrames);
        Assert.Equal(0, workspace.Advance701970(hierarchy, 0, (current, _) => throw new InvalidOperationException("Expired hierarchy emitted again.")).EmittedNodes);
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().Register5099A0(resources.Animation);
        Assert.NotNull(textures.Animation);
        textures = textures.EnqueueTim505E30(new(binding.InitializerAsset.Name, binding.InitializerAsset.Data)).State;
        Assert.NotEmpty(textures.Drain505D20().Uploads);
    }
}
