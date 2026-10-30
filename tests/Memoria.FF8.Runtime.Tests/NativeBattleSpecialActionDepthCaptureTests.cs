using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSpecialActionDepthCaptureTests
{
    [Theory]
    [InlineData(304, false)]
    [InlineData(304, true)]
    [InlineData(305, false)]
    [InlineData(305, true)]
    [InlineData(306, false)]
    [InlineData(306, true)]
    [InlineData(307, false)]
    [InlineData(307, true)]
    public void Group21_actual_payload_preserves_native_batch_and_other_authored_geometry(ushort effect, bool empty)
    {
        NativeBattlePilotVisualGeometry geometry = new(new(effect, 1, 2, 3, 4), new(1, 2, 2000), new(3, 4, 3000), new(5, 6, 4000), default, 3, false)
        {
            Sprite = new(default, default, default, default, Vector3.One, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ),
            ScreenSpace = true,
            ScreenScale = 8,
            ScreenOffsets = [new(1, 2), new(3, 4), new(5, 6)]
        };
        NativeBattleGeometryPrimitive primitive = new(17, [0x06000000, 0x30000000, 0, 0, 0, 0, 0], empty ? [] : [777, 300, 400, 0]) { PilotGeometry = geometry };
        NativeBattleGfGeometryBatch batch = new([new(0, [], [], primitive)], 28);
        string native = NativeBattleTestSnapshots.Json(batch);
        var captured = Capture(effect, batch);
        var actual = Assert.Single(captured.Attempts).Primitive!.PilotGeometry!;
        Assert.Equal(native, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(empty ? Vector4.Zero : new Vector4(777, 300, 400, 0), actual.SourceDepths);
        Assert.True(actual.UseSourceDepths);
        Assert.Equal(geometry, actual with { SourceDepths = geometry.SourceDepths, UseSourceDepths = geometry.UseSourceDepths });
        Assert.Same(captured, Capture(effect, captured));
    }

    [Theory]
    [InlineData(304)]
    [InlineData(305)]
    [InlineData(306)]
    [InlineData(307)]
    public void Group21_default_state_commands_and_existing_explicit_policy_remain_native(ushort effect)
    {
        NativeBattleGeometryPrimitive environment = new(17, [0x01000000, 0xE1000200], []);
        NativeBattleGfGeometryBatch native = new([new(0, [], [], environment), new(8, [], [], null)], 8);
        Assert.Same(native, Capture(effect, native));
        var geometry = new NativeBattlePilotVisualGeometry(new(effect, 1, 2, 3, 4), default, default, default, default, 3);
        var state = native with { Attempts = [native.Attempts[0] with { Primitive = environment with { PilotGeometry = geometry } }] };
        Assert.Same(geometry, Assert.Single(Capture(effect, state).Attempts).Primitive!.PilotGeometry);
        var explicitDepth = geometry with { SourceDepths = Vector4.One, UseSourceDepths = true };
        var retained = native with { Attempts = [native.Attempts[0] with { Primitive = environment with { PilotGeometry = explicitDepth } }] };
        Assert.Same(retained, Capture(effect, retained));
    }

    [Theory]
    [InlineData(304)]
    [InlineData(305)]
    [InlineData(306)]
    [InlineData(307)]
    public void Group21_unproved_depth_arity_fails_at_optin_boundary(ushort effect)
    {
        NativeBattleGeometryPrimitive primitive = new(17, [0x06000000, 0x30000000, 0, 0, 0, 0, 0], [1, 2, 3]) { PilotGeometry = new(new(effect, 1, 2, 3, 4), default, default, default, default, 3) };
        NativeBattleGfGeometryBatch batch = new([new(0, [], [], primitive)], 28);
        Assert.Throws<NotSupportedException>(() => Capture(effect, batch));
    }

    private static NativeBattleGfGeometryBatch Capture(ushort effect, NativeBattleGfGeometryBatch batch)
    {
        return effect switch
        {
            304 => NativeBattleBoomerangSword304Presentation.PreserveNativeDepths(batch),
            305 => NativeBattleGatlingGun305Presentation.PreserveNativeDepths(batch),
            306 => NativeBattleDegenerator306Presentation.PreserveNativeDepths(batch),
            307 => NativeBattleRayBomb307Presentation.PreserveNativeDepths(batch),
            _ => throw new ArgumentOutOfRangeException(nameof(effect))
        };
    }
}
