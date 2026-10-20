using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleFullCure280ResourcesTests
{
    [Fact]
    public void Original_layout_and_sparks_complete_and_the_retained_matrix_comes_from_cold_loader_storage()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleFullCure280Resources resources = game.ReadNativeFullCure280Resources();
        NativeExecutableMemory image = new(game.ReadNativeExecutable());
        Assert.All(image.ReadLoader(0x2508050, 32), value => Assert.Equal((byte)0, value));
        Assert.Equal((ushort)0, NativeBattleFullCure280State.FromColdProcess().EntryRandom);
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(image.ReadLoader(0x2508040, 4)));
        Assert.Equal(0x138, resources.Camera.Data.Length);
        int table = BinaryPrimitives.ReadInt32LittleEndian(resources.Layout.Data.AsSpan().Slice(4));
        Assert.Equal(24, BinaryPrimitives.ReadInt32LittleEndian(resources.Layout.Data.AsSpan().Slice(table)));
        Assert.Equal(30, BinaryPrimitives.ReadInt32LittleEndian(resources.Layout.Data.AsSpan().Slice(table + 4)));
        NativeBattleGfAnimationWorkspace workspace = new(new byte[2244].ToImmutableArray());
        workspace = workspace.Initialize7016B0(resources.Layout, 2244);
        for (int counter = 0; counter < 30; counter++)
        {
            NativeBattleGfAnimationWorkspace restored = JsonSerializer.Deserialize<NativeBattleGfAnimationWorkspace>(JsonSerializer.Serialize(workspace))!;
            NativeBattleGfAnimationAdvance next = workspace.Advance701970(resources.Layout, 0, (current, _) => current);
            Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(restored.Advance701970(resources.Layout, 0, (current, _) => current)));
            Assert.Equal(29 - counter, next.Remaining);
            workspace = next.Workspace;
        }
        for (int frame = 0; frame < 8; frame++)
        {
            Assert.NotEmpty(resources.Spark.ProjectedFrame571C80(frame).Quads);
        }
    }
}
