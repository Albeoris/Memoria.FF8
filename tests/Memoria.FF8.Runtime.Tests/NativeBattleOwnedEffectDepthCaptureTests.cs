using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleOwnedEffectDepthCaptureTests
{
    [Theory]
    [InlineData(312, false)]
    [InlineData(312, true)]
    [InlineData(313, false)]
    [InlineData(313, true)]
    [InlineData(317, false)]
    [InlineData(317, true)]
    [InlineData(320, false)]
    [InlineData(320, true)]
    public void Family_capture_uses_actual_native_payload_and_retains_all_other_geometry(ushort effect, bool empty)
    {
        NativeBattlePilotVisualGeometry geometry = new(new(effect, 1, 2, 3, 4), new(1, 2, 1000.25f), new(3, 4, 1001.5f), new(5, 6, 1002.75f), default, 3, false)
        {
            Sprite = new(default, default, default, default, Vector3.One, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ),
            ScreenOffsets = [new(1, 2), new(3, 4), new(5, 6)],
            ScreenSpace = true,
            ScreenScale = 8,
            DepthOffset = -256,
            DepthQuantum = 4
        };
        NativeBattleGeometryPrimitive primitive = new(17, [0x06000000, 0x30000000, 1, 2, 3, 4, 5], empty ? [] : [777, 1000, 1001, -3]) { PilotGeometry = geometry };
        NativeBattleGfGeometryBatch original = new([new(0, new byte[28].ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, 28).ToImmutableArray(), primitive)], 28);
        string before = NativeBattleTestSnapshots.Json(original);
        NativeBattleGfGeometryBatch captured = Capture(effect, original);
        NativeBattlePilotVisualGeometry actual = Assert.Single(captured.Attempts).Primitive!.PilotGeometry!;
        Assert.Equal(before, NativeBattleTestSnapshots.Json(captured));
        Assert.True(actual.UseSourceDepths);
        Assert.Equal(empty ? Vector4.Zero : new Vector4(777, 1000, 1001, -3), actual.SourceDepths);
        Assert.Equal(geometry with { SourceDepths = actual.SourceDepths, UseSourceDepths = true }, actual);
        Assert.Same(captured, Capture(effect, captured));
        Assert.Equal(before, NativeBattleTestSnapshots.Json(original));
    }

    [Theory]
    [InlineData(312)]
    [InlineData(313)]
    [InlineData(317)]
    [InlineData(320)]
    public void Family_default_and_state_packets_keep_native_metadata_unchanged(ushort effect)
    {
        NativeBattleGeometryPrimitive native = new(17, [0x1000000, 0xE1000220], []);
        NativeBattleGfGeometryBatch omitted = new([new(0, new byte[8].ToImmutableArray(), new NativeBattleGfPacketByteState[8].ToImmutableArray(), native)], 8);
        Assert.Same(omitted, Capture(effect, omitted));
        NativeBattlePilotVisualGeometry geometry = new(new(effect, 1, 2, 3, 4), default, default, default, default, 3);
        NativeBattleGfGeometryBatch control = omitted with { Attempts = [omitted.Attempts[0] with { Primitive = native with { PilotGeometry = geometry } }] };
        Assert.Same(geometry, Assert.Single(Capture(effect, control).Attempts).Primitive!.PilotGeometry);
    }

    [Theory]
    [InlineData(312)]
    [InlineData(313)]
    [InlineData(317)]
    [InlineData(320)]
    public void Family_rejects_unproved_native_depth_arity(ushort effect)
    {
        NativeBattleGeometryPrimitive primitive = new(17, [0x6000000, 0x30000000, 0, 0, 0, 0, 0], [1, 2, 3]) { PilotGeometry = new(new(effect, 1, 2, 3, 4), default, default, default, default, 3) };
        NativeBattleGfGeometryBatch batch = new([new(0, new byte[28].ToImmutableArray(), new NativeBattleGfPacketByteState[28].ToImmutableArray(), primitive)], 28);
        Assert.Throws<NotSupportedException>(() => Capture(effect, batch));
    }

    private static NativeBattleGfGeometryBatch Capture(ushort effect, NativeBattleGfGeometryBatch batch)
    {
        return effect switch
        {
            312 => NativeBattleEffect312Presentation.PreserveNativeDepths(batch),
            313 => NativeBattleEffect313Lower.PreserveNativeDepths(batch),
            317 => NativeBattleAcid317ActMesh.PreserveNativeDepths(batch),
            320 => NativeBattleEffect320Lower.PreserveNativeDepths(batch),
            _ => throw new ArgumentOutOfRangeException(nameof(effect))
        };
    }
}
