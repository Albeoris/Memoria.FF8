using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicCapturePresentationTests
{
    [Theory]
    [InlineData((ushort)209)]
    [InlineData((ushort)218)]
    public void Discrete_screen_preparation_preserves_existing_moving_world_quad_capture(ushort effect)
    {
        NativeBattlePilotVisualGeometry moving = new(new(effect, 1, 2, 3, 4) { ResourceGeneration = 5 }, new(-400, -400, 2000), new(400, -400, 2000), new(-400, 400, 2000), new(400, 400, 2000), 4) { SourceDepths = new Vector4(2000), UseSourceDepths = true };
        ImmutableArray<uint> words = [0x08000000u, 0x2C808080u, 0x00100020u, 0, 0x00100040u, 0, 0x00300020u, 0, 0x00300040u];
        NativeBattleGeometryPrimitive primitive = new(142, words, [2000, 2000, 2000, 2000]) { PilotGeometry = moving };
        byte[] bytes = new byte[words.Length * 4];
        for (int word = 0; word < words.Length; word++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(word * 4), words[word]);
        }
        NativeBattleGfGeometryBatch native = new([new(0, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, bytes.Length).ToImmutableArray(), primitive)], bytes.Length);
        string frozen = NativeBattleTestSnapshots.Json(native);
        NativeBattleGfGeometryBatch prepared = NativeBattleCinematicCapturePresentation.Discrete(native, new(effect, 6, 7, 8, 9));
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(prepared));
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(native));
        Assert.Same(primitive, Assert.Single(prepared.Attempts).Primitive);
        Assert.Same(moving, prepared.Attempts[0].Primitive!.PilotGeometry);
        Assert.False(moving.ScreenSpace);
        Assert.True(moving.Continuous);
        Assert.Equal(new Vector3(-400, -400, 2000), moving.A);
    }
}
