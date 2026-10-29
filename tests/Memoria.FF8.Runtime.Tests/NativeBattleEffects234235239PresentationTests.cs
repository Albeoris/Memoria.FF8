using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffects234235239PresentationTests
{
    [Theory]
    [InlineData(234)]
    [InlineData(235)]
    [InlineData(239)]
    public void Body_zero_linked_zero_and_authored_copy_sources_have_distinct_capture_identity_with_native_masks_unchanged(int effect)
    {
        NativeBattlePilotVisualIdentity owner = new((ushort)effect, 0x100, 2, 123, 0);
        NativeBattlePilotVisualIdentity body = NativeBattleCinematicActorPresentation.Object(owner, 0x2000, 0, false, 0x100000);
        NativeBattlePilotVisualIdentity linked = NativeBattleCinematicActorPresentation.Object(owner, 0x2000, 0, true, 0x100000);
        NativeBattlePilotVisualIdentity copy = NativeBattleCinematicActorPresentation.Object(owner, 0x2100, 0, false, 0x100000);
        NativeBattlePilotVisualIdentity otherSource = NativeBattleCinematicActorPresentation.Object(owner, 0x2000, 0, false, 0x100100);
        Assert.Equal(4, new[] { body, linked, copy, otherSource }.Distinct().Count());
        Assert.Equal(owner.Bank, linked.Bank);
        Assert.Equal(owner.Slot, linked.Slot);
        Assert.Equal(owner.Generation, linked.Generation);
        Assert.Equal(body.Primitive + 1, linked.Primitive);
        Assert.Equal(0x100000L, body.ResourceGeneration);
    }

    [Theory]
    [InlineData(234, false)]
    [InlineData(235, true)]
    [InlineData(239, true)]
    public void Actual_glow_packet_capture_preserves_native_minimum_depth_payload_palette_and_unknown_color_lanes(int effect, bool quad)
    {
        byte[] face = new byte[quad ? 20 : 16];
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(4), 2);
        if (quad)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(6), 3);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(8), 0x12345678);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x9ABCDEF0);
        NativeBattleRawMesh mesh = new([], [new(quad, face.ToImmutableArray())]);
        NativeBattleProjectedVertex[] vertices = [new(0, 10), new(8, 32), new(8u << 16, 48), new(8u | 8u << 16, 64)];
        uint[] colors = [0x010203, 0x040506, 0x070809, 0x0A0B0C];
        bool[] highKnown = [false, false, true, false];
        Vector3[] world = [new(0, 0, 5), new(1, 0, 32), new(0, 1, 48), new(1, 1, 64)];
        NativeBattleGfGeometryBatch plain = NativeBattleCinematicMonsterPackets.Glow(mesh, vertices, colors, 17, highKnown);
        NativeBattleGfGeometryBatch captured = NativeBattleCinematicActorPresentation.NativeDepths(NativeBattleCinematicMonsterPackets.Glow(mesh, vertices, colors, 17, highKnown, world, new((ushort)effect, 0x100, 0, 123, 9)));
        Assert.Equal(NativeBattleTestSnapshots.Json(plain), NativeBattleTestSnapshots.Json(captured));
        NativeBattleGeometryPrimitive primitive = Assert.Single(captured.Attempts).Primitive!;
        NativeBattlePilotVisualGeometry geometry = primitive.PilotGeometry!;
        Assert.Equal(world[0], geometry.A);
        Assert.Equal(5, geometry.A.Z);
        Assert.True(geometry.UseSourceDepths);
        Assert.Equal(new Vector4(10, 32, 48, quad ? 64 : 0), geometry.SourceDepths);
        Assert.Equal(NativeBattleGfPacketByteState.Unknown, captured.Attempts[0].WriteMask[19]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, captured.Attempts[0].WriteMask[38]);
        Assert.Null(Assert.Single(plain.Attempts).Primitive!.PilotGeometry);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ochu_shadow_receipts_keep_native_fan_commands_masks_and_exact_biased_depths(bool gouraud)
    {
        NativeBattleModuleProjectedVertex[] vertices = new NativeBattleModuleProjectedVertex[10];
        Vector3[] world = new Vector3[10];
        vertices[0] = new(100u | 100u << 16, 1600, 0);
        world[0] = new(0, 0, 1600);
        for (int index = 1; index < 10; index++)
        {
            int angle = (index - 1) * 512;
            short x = (short)(100 + (NativeBattleCameraMath.Cosine(angle) >> 7));
            short y = (short)(100 + (NativeBattleCameraMath.Sine(angle) >> 7));
            vertices[index] = new(unchecked((ushort)x) | unchecked((uint)(ushort)y << 16), (ushort)(1600 + index), 0);
            world[index] = new(x, y, 1600 + index);
        }
        NativeBattleGfGeometryBatch plain = NativeBattleShadowPackets.EmitBatch5088A0(vertices, 0x40000000, gouraud, 4121, 31);
        NativeBattleGfGeometryBatch captured = NativeBattleCinematicActorPresentation.NativeDepths(NativeBattleShadowPackets.EmitBatch5088A0(vertices, 0x40000000, gouraud, 4121, 31, world, new(239, 0x100, 0, 123, int.MinValue)));
        Assert.Equal(NativeBattleTestSnapshots.Json(plain), NativeBattleTestSnapshots.Json(captured));
        Assert.Contains(captured.Attempts, attempt => attempt.Primitive?.PilotGeometry is not null);
        foreach (NativeBattleGfGeometryAttempt attempt in captured.Attempts.Where(attempt => attempt.Primitive?.PilotGeometry is not null))
        {
            NativeBattleGeometryPrimitive primitive = attempt.Primitive!;
            Assert.True(primitive.PilotGeometry!.UseSourceDepths);
            Assert.Equal(new Vector4(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), primitive.PilotGeometry.SourceDepths);
        }
    }
}
