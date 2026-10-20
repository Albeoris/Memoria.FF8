using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRapture288ResourcesTests
{
    [Fact]
    public void Original_Rapture_two_layouts_retain_their_native_workspace_and_midpoint_continuation()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleRapture288Resources resources = game.ReadNativeRapture288Resources();
        Assert.Equal(350099u, resources.Sound);
        foreach (var item in new[] { (Source: resources.First, Size: 252, Nodes: 2, Duration: 26), (Source: resources.Second, Size: 780, Nodes: 9, Duration: 66) })
        {
            Check(item.Source, item.Size, item.Nodes, item.Duration);
        }
    }

    private static void Check(NativeBattleGfAnimationSource source, int size, int nodes, int duration)
    {
        int table = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(4));
        Assert.Equal(nodes, BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(table)));
        Assert.Equal(duration, BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(table + 4)));
        NativeBattleGfAnimationWorkspace live = new NativeBattleGfAnimationWorkspace(new byte[size].ToImmutableArray()).Initialize7016B0(source, size);
        for (int callback = 0; callback < 20; callback++)
        {
            live = live.Advance701970(source, 0, (owner, _) => owner).Workspace;
        }
        NativeBattleGfAnimationWorkspace restored = JsonSerializer.Deserialize<NativeBattleGfAnimationWorkspace>(JsonSerializer.Serialize(live))!;
        for (int callback = 20; callback <= duration; callback++)
        {
            List<string> first = [];
            List<string> second = [];
            NativeBattleGfAnimationAdvance a = live.Advance701970(source, 0, (owner, node) =>
            {
                first.Add(JsonSerializer.Serialize(node));
                return owner;
            });
            NativeBattleGfAnimationAdvance b = restored.Advance701970(source, 0, (owner, node) =>
            {
                second.Add(JsonSerializer.Serialize(node));
                return owner;
            });
            Assert.Equal(first, second);
            Assert.Equal(a.Remaining, b.Remaining);
            Assert.Equal(JsonSerializer.Serialize(a.Workspace), JsonSerializer.Serialize(b.Workspace));
            live = a.Workspace;
            restored = b.Workspace;
        }
    }
}
