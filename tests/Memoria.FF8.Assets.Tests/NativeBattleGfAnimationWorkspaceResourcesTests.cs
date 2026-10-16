using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGfAnimationWorkspaceResourcesTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false, 0x1298C68u, 0x7D4, 29, 76)]
    [InlineData(true, 0x12A9664u, 0x7FC, 23, 24)]
    public void Original_Quezacotl_workspaces_decode_all_first_frame_nodes_and_resume_identically(bool second, uint address, int clearSize, int expectedCount, int expectedDuration)
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleQuezacotlResources resources = Assert.IsType<NativeBattleQuezacotlResources>(game.ReadNativeEffectCatalogForEffects([116])[115].NativeQuezacotlResources);
        ImmutableArray<byte> original = second ? resources.WorkSeed1.Data : resources.WorkSeed0.Data;
        NativeBattleGfAnimationSource source = new(address, original);
        int table = BinaryPrimitives.ReadInt32LittleEndian(original.AsSpan().Slice(4));
        Assert.Equal(expectedCount, BinaryPrimitives.ReadInt32LittleEndian(original.AsSpan().Slice(table)));
        Assert.Equal(expectedDuration, BinaryPrimitives.ReadInt32LittleEndian(original.AsSpan().Slice(table + 4)));
        NativeBattleGfAnimationWorkspace prior = new(Enumerable.Repeat((byte)0xA5, clearSize + 4).ToImmutableArray());
        NativeBattleGfAnimationWorkspace workspace = prior.Initialize7016B0(source, clearSize);
        Assert.Equal(new byte[] { 0xA5, 0xA5, 0xA5, 0xA5 }, workspace.Data.AsSpan().Slice(clearSize).ToArray());
        List<NativeBattleGfAnimationNode> firstNodes = [];
        NativeBattleGfAnimationAdvance first = workspace.Advance701970(source, 0, (current, node) =>
        {
            Assert.Equal(0, current.Frame);
            firstNodes.Add(node);
            return current;
        });
        Assert.Equal(expectedCount, first.EmittedNodes);
        Assert.Equal(Enumerable.Range(0, expectedCount).Select(index => (ushort)index).ToArray(), firstNodes.Select(node => node.NodeIndex).ToArray());
        Assert.All(firstNodes, node => Assert.Equal(44, node.Data.Length));
        NativeBattleGfAnimationNode? firstDrawable = firstNodes.FirstOrDefault(node => !Skips6C6060(node));
        int drawableCount = firstNodes.Count(node => !Skips6C6060(node));
        Report($"Source={address:X8}; frame=0; nodes={firstNodes.Count}; drawable={drawableCount}; skipped={firstNodes.Count - drawableCount}");
        if (firstDrawable is not null)
        {
            Assert.All(firstNodes.Take(firstDrawable.NodeIndex), node => Assert.True(Skips6C6060(node)));
            Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan().Slice(firstDrawable.ResourceOffset)), firstDrawable.Header);
            Report($"First drawable node={firstDrawable.NodeIndex}; geometry={firstDrawable.GeometryIndex}; header={firstDrawable.Header:X8}; scale={firstDrawable.Vector24}; scalar36={firstDrawable.Scalar36}; color32={firstDrawable.Color32:X8}; morph={firstDrawable.Word40}/{firstDrawable.Word42}@{firstDrawable.Scalar38}");
        }
        else
        {
            Assert.All(firstNodes, node => Assert.True(Skips6C6060(node)));
            Report("No first-frame node passes native6C6060 early-out predicates.");
        }
        Assert.Equal(1, first.Workspace.Frame);
        Assert.Equal(expectedDuration - 1, first.Remaining);
        NativeBattleGfAnimationWorkspace restored = JsonSerializer.Deserialize<NativeBattleGfAnimationWorkspace>(JsonSerializer.Serialize(first.Workspace))!;
        List<byte[]> left = [];
        List<byte[]> right = [];
        NativeBattleGfAnimationAdvance expected = first.Workspace.Advance701970(source, 0, (current, node) =>
        {
            left.Add(node.Data.ToArray());
            return current;
        });
        NativeBattleGfAnimationAdvance actual = restored.Advance701970(source, 0, (current, node) =>
        {
            right.Add(node.Data.ToArray());
            return current;
        });
        Assert.Equal(expected.Workspace.Data.ToArray(), actual.Workspace.Data.ToArray());
        Assert.Equal(expected.Remaining, actual.Remaining);
        Assert.Equal(left.Count, right.Count);
        for (int index = 0; index < left.Count; index++)
        {
            Assert.Equal(left[index], right[index]);
        }
        Assert.Equal(original.ToArray(), source.Data.ToArray());
        Assert.All(prior.Data, value => Assert.Equal((byte)0xA5, value));
    }

    private static bool Skips6C6060(NativeBattleGfAnimationNode node)
    {
        NativeBattleVector scale = node.Vector24;
        return (scale.X == 0 && scale.Y == 0 && scale.Z == 0) || (node.Scalar36 >= 4096 && node.Color32 == 0);
    }

    private void Report(string value)
    {
        output.WriteLine(value);
        Console.WriteLine(value);
    }
}
