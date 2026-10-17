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
        using GameAssets game = new(TestResources.GameDirectory);
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

    [Fact]
    public void OriginalFile513ThirdWorkspaceUsesIts158ByteCacheAndPreservesThePreviousSuffix()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleQuezacotlResources resources = game.ReadNativeQuezacotlResources();
        NativeBattleEffectFileAsset file = resources.Files.Single(file => file.FileId == 513);
        Assert.Equal("MAG115_H.08", file.Name);
        Assert.Equal(8096, file.Data.Length);
        NativeBattleEffectMemory memory = game.ReadColdProcessNativeBattleMemory().EffectMemory!.BeginLoad(116);
        memory = memory with { QuezacotlExecutableMemory = memory.QuezacotlExecutableMemory!.Write(NativeBattleQuezacotlExecutableMemory.Address, file.Data.AsSpan()), Arena = memory.Arena!.Write(NativeBattleQuezacotlWorkspace.Offset, Enumerable.Repeat((byte)0xA5, NativeBattleQuezacotlWorkspace.SecondLength).ToArray()) };
        NativeBattleGfAnimationSource source = NativeBattleQuezacotlWorkspace.Source(memory);
        NativeBattleGfAnimationWorkspace prefix = NativeBattleQuezacotlWorkspace.Read(memory, NativeBattleQuezacotlWorkspace.ThirdLength).Initialize7016B0(source, NativeBattleQuezacotlWorkspace.ThirdLength);
        NativeBattleGfAnimationWorkspace larger = NativeBattleQuezacotlWorkspace.Read(memory, NativeBattleQuezacotlWorkspace.SecondLength).Initialize7016B0(source, NativeBattleQuezacotlWorkspace.ThirdLength);
        List<byte[]> smallNodes = [];
        List<byte[]> largeNodes = [];
        NativeBattleGfAnimationAdvance small = prefix.Advance701970(source, 0, (current, node) =>
        {
            smallNodes.Add(node.Data.ToArray());
            return current;
        });
        NativeBattleGfAnimationAdvance large = larger.Advance701970(source, 0, (current, node) =>
        {
            largeNodes.Add(node.Data.ToArray());
            return current;
        });
        Assert.Equal(3, small.EmittedNodes);
        Assert.Equal(63, small.Remaining);
        Assert.Equal(small.Workspace.Data.ToArray(), large.Workspace.Data.AsSpan().Slice(0, NativeBattleQuezacotlWorkspace.ThirdLength).ToArray());
        Assert.Equal(smallNodes.Count, largeNodes.Count);
        for (int index = 0; index < smallNodes.Count; index++)
        {
            Assert.Equal(smallNodes[index], largeNodes[index]);
        }
        Assert.All(large.Workspace.Data.Skip(NativeBattleQuezacotlWorkspace.ThirdLength), value => Assert.Equal((byte)0xA5, value));
        NativeBattleEffectTaskPool child = NativeBattleEffectTaskPool.Initialize(0x2521738, 0x20DFAB8, 0x8D0, 1);
        (child, _) = child.Append(0x6C3940);
        NativeBattleOriginalEffectState effect = new(116, 115, new(116, 0x6C3550, 0x6C3560), null, null) { Pools = [child] };
        NativeBattleQuezacotlWorkspaceWrite persisted = NativeBattleQuezacotlWorkspace.Write(memory, effect, small.Workspace);
        Assert.Equal(small.Workspace.Data.ToArray(), persisted.Memory.Arena!.Read(NativeBattleQuezacotlWorkspace.Offset, NativeBattleQuezacotlWorkspace.ThirdLength));
        Assert.All(persisted.Memory.Arena.Read(NativeBattleQuezacotlWorkspace.Offset + NativeBattleQuezacotlWorkspace.ThirdLength, NativeBattleQuezacotlWorkspace.SecondLength - NativeBattleQuezacotlWorkspace.ThirdLength), value => Assert.Equal((byte)0xA5, value));
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
