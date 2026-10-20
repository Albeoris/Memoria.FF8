using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleBlindResourcesTests
{
    [Fact]
    public void Original_Blind_layout_retains_all_twenty_nine_records_and_its_midpoint_continuation()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleBlindResources resources = game.ReadNativeBlindResources();
        Assert.Equal(0x12726A0u, resources.Layout.Address);
        Assert.Equal(0x4940Cu, resources.Sound);
        int table = BinaryPrimitives.ReadInt32LittleEndian(resources.Layout.Data.AsSpan().Slice(4));
        Assert.Equal(29, BinaryPrimitives.ReadInt32LittleEndian(resources.Layout.Data.AsSpan().Slice(table)));
        Assert.Equal(38, BinaryPrimitives.ReadInt32LittleEndian(resources.Layout.Data.AsSpan().Slice(table + 4)));
        NativeBattleGfAnimationWorkspace live = new NativeBattleGfAnimationWorkspace(new byte[0x820].ToImmutableArray()).Initialize7016B0(resources.Layout, 0x820);
        for (int callback = 0; callback < 20; callback++)
        {
            live = live.Advance701970(resources.Layout, 0, (owner, _) => owner).Workspace;
        }
        NativeBattleGfAnimationWorkspace restored = JsonSerializer.Deserialize<NativeBattleGfAnimationWorkspace>(JsonSerializer.Serialize(live))!;
        for (int callback = 20; callback < 39; callback++)
        {
            List<string> first = [];
            List<string> second = [];
            NativeBattleGfAnimationAdvance a = live.Advance701970(resources.Layout, 0, (owner, node) =>
            {
                first.Add(JsonSerializer.Serialize(node));
                return owner;
            });
            NativeBattleGfAnimationAdvance b = restored.Advance701970(resources.Layout, 0, (owner, node) =>
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
