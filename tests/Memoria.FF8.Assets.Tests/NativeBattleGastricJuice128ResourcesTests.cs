using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleGastricJuice128Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGastricJuice128ResourcesTests
{
    [Fact]
    public void Original_ACTs_keep_its_exact_typed_front_and_workspaces()
    {
        using GameAssets game = Open();
        NativeBattleGastricJuice128Resources resources = game.ReadNativeGastricJuice128Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeGastricJuice128Resources());
        AssertShapes(resources.MainAct, [64], [2]);
        AssertWorkspace(resources.MainAct, 0x248, 20);
        Assert.Equal(64 * 8, MorphBytes);
        Assert.Equal(0x4000, resources.DistanceTable.Length);
        Assert.Equal(0x641DDu, resources.Sound);
    }

    [Fact]
    public void Original_sprites_and_addressed_ACT_selection_survive_resource_JSON()
    {
        using GameAssets game = Open();
        NativeBattleGastricJuice128Resources resources = game.ReadNativeGastricJuice128Resources();
        NativeBattleGastricJuice128Resources restored = JsonSerializer.Deserialize<NativeBattleGastricJuice128Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        Assert.Same(restored.MainAct, restored.Act(MainActAddress));
        Assert.Same(restored.SpriteA, restored.Sprite(SpriteAAddress));
        Assert.Same(restored.SpriteB, restored.Sprite(SpriteBAddress));
        Assert.Same(restored.SpriteC, restored.Sprite(SpriteCAddress));
        Assert.Throws<NotSupportedException>(() => restored.Act(SpriteAAddress));
        Assert.Throws<NotSupportedException>(() => restored.Sprite(MainActAddress));
    }

    [Fact]
    public void Strict_resource_extents_reject_truncated_ACT_sprite_and_distance_table()
    {
        using GameAssets game = Open();
        NativeBattleGastricJuice128Resources resources = game.ReadNativeGastricJuice128Resources();
        Assert.Throws<InvalidDataException>(() => (resources with { MainAct = resources.MainAct with { Data = resources.MainAct.Data.Take(0x1177).ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { SpriteA = resources.SpriteA with { Bytes = resources.SpriteA.Bytes.Take(0xF7).ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { DistanceTable = resources.DistanceTable.Take(0x3FFF).ToImmutableArray() }).Validate());
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
        for (int frame = 0; frame < duration; frame++)
        {
            NativeBattleGfAnimationAdvance next = workspace.Advance701970(source, 0, (current, node) => current);
            emitted += next.EmittedNodes;
            workspace = next.Workspace;
        }
        Assert.True(emitted > 0);
        Assert.Equal(duration, workspace.Frame);
        Assert.Equal(0, workspace.Advance701970(source, 0, (current, node) => current).EmittedNodes);
    }
    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Gastric Juice resources.");
        return new(directory!);
    }
}
