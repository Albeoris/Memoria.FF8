using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleForeignModelOrderingTests
{
    private sealed record Material : INativeBattleForeignMaterial;

    [Fact]
    public void HostMaterialKeepsNativeDepthAndOrderWithoutPublishingANativeTexturePageOrCacheEntry()
    {
        NativeBattleForeignSurface surface = new(new Material(), [Vector2.Zero, Vector2.UnitX, Vector2.UnitY], [Vector4.One, Vector4.One, Vector4.One]);
        NativeBattlePilotVisualGeometry geometry = new(new(1, 1, 0, 1, 0), default, default, default, default, 3) { ForeignSurface = surface };
        NativeBattleLogicalOrdering native = Native();
        NativeBattleLogicalOrdering mixed = Native().Prepend(20, new("host-body", 0, 64), Triangle(0xFFFF), 0, [201, 202, 203, 0], pilotGeometry: geometry);
        NativeBattlePcTextureWindowCache cache = NativeBattlePrimitiveProcess.FromColdProcess().TextureWindowCache! with { ScaleU = 1, ScaleV = 1 };
        var expected = NativeBattleLogicalPasses.Execute(native, Tables(), new(0) { TextureWindowBits = 0 }, 1, cache);
        var actual = NativeBattleLogicalPasses.Execute(mixed, Tables(), new(0) { TextureWindowBits = 0 }, 1, cache);
        Assert.Equal(expected.Final, actual.Final);
        Assert.Equal(JsonSerializer.Serialize(expected.Cache), JsonSerializer.Serialize(actual.Cache));
        NativeBattleLogicalResolvedCommand foreign = Assert.Single(actual.Commands, command => command.Command.PilotGeometry?.ForeignSurface is not null);
        Assert.Equal((byte)1, foreign.Pass);
        Assert.Equal(20, foreign.Command.OrderingBucket);
        Assert.Equal(new[] { 201, 202, 203, 0 }, foreign.Command.VertexPayload4);
        Assert.Same(surface, foreign.Command.PilotGeometry!.ForeignSurface);
        NativeBattleLogicalResolvedCommand sprite = Assert.Single(actual.Commands, command => command.Pass == 1 && command.Command.Opcode == 0x64);
        Assert.Equal((ushort)0x60, sprite.DrawMode.TexturePage);
        var selected = NativeBattleLogicalPasses.SelectTextures([foreign with { DrawMode = new(null) }], cache);
        Assert.Null(Assert.Single(selected.Commands).DrawMode.TexturePage);
        Assert.Null(Assert.Single(selected.Commands).DrawMode.TextureSelection);
    }

    private static NativeBattleLogicalOrdering Native()
    {
        return NativeBattleLogicalOrdering.Clear().Prepend(30, new("native", 0, 0), Triangle(0x60), 0, [100, 100, 100, 0]).Prepend(10, new("native", 0, 32), [0x04000000, 0x64808080, 0, 0, 0x00100010], 1, []);
    }

    private static ImmutableArray<uint> Triangle(ushort page) => [0x07000000, 0x24808080, 0, 0, 80, (uint)page << 16, 80u << 16, 0];

    private static NativeBattlePrimitiveTables Tables()
    {
        byte[] lengths = new byte[256];
        byte[] flagged = new byte[256];
        uint[] handlers = new uint[256];
        lengths[0x24] = 7;
        lengths[0x64] = 4;
        flagged[0x24] = 1;
        flagged[0x64] = 1;
        handlers[0x24] = 1;
        handlers[0x64] = 1;
        return new(lengths.ToImmutableArray(), flagged.ToImmutableArray(), handlers.ToImmutableArray(), handlers.ToImmutableArray(), new uint[256].ToImmutableArray());
    }
}
