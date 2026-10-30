using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleControl110CaptureTests
{
    [Fact]
    public void Control110_stream_captures_both_actual_mutable_quad_lists_without_changing_native_writes()
    {
        byte[] source = new byte[2600];
        I32(source, 0, 2500);
        I32(source, 2512, 1);
        I32(source, 2552, 1);
        Face(2516, 0x2C808080);
        Face(2556, 0x3C808080);
        I32(source, 2580, 0x404040);
        I32(source, 2584, 0x606060);
        I32(source, 2588, 0x808080);
        byte[] vertices = new byte[308 * 8];
        Vector(0, -100, -100);
        Vector(1, 100, -100);
        Vector(2, -100, 100);
        Vector(3, 100, 100);
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleCameraProjection projection = new(view, 256, 160, 108);
        NativeBattleSummonColors colors = NativeBattleGteColorInterpolation.CodeConstants45F270;
        NativeBattlePilotVisualIdentity identity = new(110, 0x2521A30, 0, 71, 0x10000);
        NativeBattleGfGeometryBatch plain = NativeBattleStorm11Geometry.Emit6CF0C0(source.ToImmutableArray(), vertices.ToImmutableArray(), view, projection, 10, colors);
        string detachedNative = NativeBattleTestSnapshots.Json(plain);
        NativeBattleGfGeometryBatch captured = NativeBattleStorm11Geometry.Emit6CF0C0(source.ToImmutableArray(), vertices.ToImmutableArray(), view, projection, 10, colors, identity);
        Assert.Equal(detachedNative, NativeBattleTestSnapshots.Json(captured));
        NativeBattlePilotVisualGeometry[] geometry = captured.Attempts.Where(attempt => attempt.Primitive?.PilotGeometry is not null).Select(attempt => attempt.Primitive!.PilotGeometry!).ToArray();
        Assert.Equal(2, geometry.Length);
        Assert.NotEqual(geometry[0].Identity.Primitive, geometry[1].Identity.Primitive);
        Assert.All(geometry, quad =>
        {
            Assert.Equal(new Vector3(-100, -100, 4096), quad.A);
            Assert.Equal(new Vector3(100, 100, 4096), quad.D);
            Assert.Equal(4, quad.CornerCount);
            Assert.Equal(71, quad.Identity.Generation);
        });
        Assert.Equal(4, captured.Attempts.Count(attempt => attempt.Primitive is { } primitive && primitive.PilotGeometry is null));
        void Face(int offset, uint command)
        {
            I32(source, offset, unchecked((int)command));
            for (int corner = 0; corner < 4; corner++)
            {
                W(source, offset + 4 + corner * 2, (short)(corner * 2));
            }
        }
        void Vector(int index, short x, short y)
        {
            W(vertices, index * 8, x);
            W(vertices, index * 8 + 2, y);
            W(vertices, index * 8 + 4, 4096);
        }
    }

    [Fact]
    public void Control110_near_rejection_does_not_read_the_unreached_fourth_vertex_for_capture()
    {
        byte[] source = new byte[2600];
        I32(source, 0, 2500);
        I32(source, 2512, 1);
        I32(source, 2552, 1);
        Face(2516, 0x2C808080);
        Face(2556, 0x3C808080);
        I32(source, 2580, 0x404040);
        I32(source, 2584, 0x606060);
        I32(source, 2588, 0x808080);
        byte[] vertices = new byte[308 * 8];
        Vector(0, -100, -100);
        Vector(1, 100, -100);
        Vector(2, -100, 100);
        Vector(3, 100, 100);
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleCameraProjection projection = new(view, 256, 160, 108);
        NativeBattleSummonColors colors = NativeBattleGteColorInterpolation.CodeConstants45F270;
        NativeBattlePilotVisualIdentity identity = new(110, 0x2521A30, 0, 71, 0x10000);
        W(source, 2516 + 10, -1);
        W(source, 2556 + 10, -1);
        W(vertices, 4, -1);
        NativeBattleGfGeometryBatch native = NativeBattleStorm11Geometry.Emit6CF0C0(source.ToImmutableArray(), vertices.ToImmutableArray(), view, projection, 10, colors);
        string detached = NativeBattleTestSnapshots.Json(native);
        NativeBattleGfGeometryBatch captured = NativeBattleStorm11Geometry.Emit6CF0C0(source.ToImmutableArray(), vertices.ToImmutableArray(), view, projection, 10, colors, identity);
        Assert.Equal(detached, NativeBattleTestSnapshots.Json(captured));
        Assert.All(captured.Attempts, attempt => Assert.Null(attempt.Primitive?.PilotGeometry));

        void Face(int offset, uint command)
        {
            I32(source, offset, unchecked((int)command));
            for (int corner = 0; corner < 4; corner++)
            {
                W(source, offset + 4 + corner * 2, (short)(corner * 2));
            }
        }
        void Vector(int index, short x, short y)
        {
            W(vertices, index * 8, x);
            W(vertices, index * 8 + 2, y);
            W(vertices, index * 8 + 4, 4096);
        }
    }

    private static void I32(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), value);
    private static void W(byte[] bytes, int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value);
}
