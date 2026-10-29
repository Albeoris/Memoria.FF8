using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGastricJuice169Tests
{
    [Fact]
    public void Landing_morph_uses_integer_difference_shift_and_preserves_actual_fourth_words()
    {
        byte[] first = new byte[8 + 73 * 8];
        byte[] second = new byte[73 * 8];
        byte[] output = new byte[73 * 8];
        BinaryPrimitives.WriteInt16LittleEndian(first.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(6), 0xCAFE);
        NativeBattleGastricJuice169.Morph(first.ToImmutableArray(), second.ToImmutableArray(), output, 8);
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(output));
        Assert.Equal((ushort)0xCAFE, BinaryPrimitives.ReadUInt16LittleEndian(output.AsSpan(6)));
        Assert.Equal(new[] { 0, 0, 3584, 4096, 512, 0, 3584 }, new[] { 7, 8, 15, 16, 23, 24, 31 }.Select(NativeBattleGastricJuice169.MeshFade));
    }

    [Fact]
    public void Custom_landing_packets_append_the_original_draw_mode_word_and_expand_the_tag_length()
    {
        byte[] model = new byte[84];
        BinaryPrimitives.WriteInt32LittleEndian(model, 32);
        BinaryPrimitives.WriteInt32LittleEndian(model.AsSpan(4), 3);
        BinaryPrimitives.WriteInt32LittleEndian(model.AsSpan(40), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(model.AsSpan(44), 0x24808080);
        BinaryPrimitives.WriteUInt16LittleEndian(model.AsSpan(50), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(model.AsSpan(52), 4);
        ImmutableArray<byte> vertices = World(new(-100, -100, 1000), new(100, -100, 1000), new(-100, 100, 1000));
        NativeBattleGfGeometryBatch batch = NativeBattleGastricJuice169.Emit5E0450(model.ToImmutableArray(), vertices, Identity, Projection, 0, NativeBattleGteColorInterpolation.CodeConstants45F270);
        NativeBattlePilotVisualIdentity identity = new(169, 0x23B1D08, 4, 8, 2);
        NativeBattleGfGeometryBatch captured = NativeBattleGastricJuice169.Emit5E0450(model.ToImmutableArray(), vertices, Identity, Projection, 0, NativeBattleGteColorInterpolation.CodeConstants45F270, identity);
        Assert.Equal(NativeBattleTestSnapshots.Json(batch), NativeBattleTestSnapshots.Json(captured));
        Assert.Null(Assert.Single(batch.Attempts).Primitive!.PilotGeometry);
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(captured.Attempts).Primitive!.PilotGeometry);
        Assert.Equal(new System.Numerics.Vector3(-100, -100, 1000), geometry.A);
        Assert.Equal(new System.Numerics.Vector3(100, -100, 1000), geometry.B);
        Assert.Equal(new System.Numerics.Vector3(-100, 100, 1000), geometry.C);
        Assert.Equal(identity with { Primitive = identity.Primitive * 397 + 44 }, geometry.Identity);
        Assert.Equal(36, batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.NotNull(attempt.Primitive);
        Assert.Equal(0x08000000u, attempt.Primitive!.Words[0]);
        Assert.Equal(0xE1000220u, attempt.Primitive.Words[^1]);
        Assert.Equal((uint)0x20, attempt.Primitive.Words[5] >> 16);
        Assert.All(attempt.WriteMask.AsSpan().Slice(32).ToArray(), value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
    }

    [Fact]
    public void Tint_outside_corners_keep_actual_shared_scratch_fourth_words_while_near_corner_gets_radial_fade()
    {
        byte[] face = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(4), 2);
        byte[] world = World(new(0, 0, 1000), new(2000, 0, 1000), new(0, 2000, 1000)).ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(world.AsSpan(14), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(world.AsSpan(22), -4096);
        NativeBattleGfGeometryBatch batch = NativeBattleGastricJuice169.Tint5DF540([new(false, face.ToImmutableArray())], world.ToImmutableArray(), Identity, Projection, new(0, 0, 1000), 0x808080, 8, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(2, batch.Attempts.Length);
        NativeBattleGeometryPrimitive body = batch.Attempts[0].Primitive!;
        NativeBattleGeometryPrimitive overlay = batch.Attempts[1].Primitive!;
        Assert.Equal(body.Bucket - 1, overlay.Bucket);
        Assert.Equal(0x28C098u, overlay.Words[1] & 0xFFFFFF);
        Assert.Equal(0x14604Cu, overlay.Words[3] & 0xFFFFFF);
        Assert.Equal(0u, overlay.Words[5] & 0xFFFFFF);
        Assert.Equal((short)2048, BinaryPrimitives.ReadInt16LittleEndian(world.AsSpan(14)));
    }

    [Fact]
    public void Group14_full_MAC_splash_uses_original_current_matrix_and_actual_view_only_for_capture()
    {
        byte[] quad = new byte[20];
        quad[0] = 16;
        quad[1] = 128;
        quad[2] = 8;
        quad[3] = 0x2C;
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
        {
            Resource = new(0, 0xD6A620, 0, 0, new byte[8].ToImmutableArray()),
            SpriteFrame = new(-1, false, quad.ToImmutableArray()),
            Anchor = new(0, 0, 0, 0),
            Scale = 4096,
            Direction = 4096,
            SpriteCameraXOffset = 1234,
            SpriteCameraYOffset = -120,
            SpriteCameraZOffset = 4000
        };
        NativeBattlePilotVisualIdentity identity = new(169, 0x23B1D08, 4, 8, 107) { ResourceGeneration = 3 };
        NativeBattleCameraProjection actualView = Projection with { View = Identity with { X = 100, Y = 50, Z = 200 } };
        NativeBattleActionMatrix current = NativeBattlePoseMatrices.ModuleSpriteCamera(draw, Identity, 0);
        NativeBattleModulePacketBatch native = NativeBattleModulePackets.EmitSprite571DC0(draw, Projection, 0, 17, 2);
        NativeBattleModulePacketBatch captured = NativeBattleModulePackets.EmitSprite571DC0(draw with { PilotIdentity = identity }, actualView, 0, 17, 2, current);
        Assert.Equal(NativeBattleTestSnapshots.Json(native), NativeBattleTestSnapshots.Json(captured));
        NativeBattleGeometryPrimitive baseline = Assert.Single(native.Primitives).Primitive;
        Assert.Null(baseline.PilotGeometry);
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(captured.Primitives).Primitive.PilotGeometry);
        Assert.Equal(identity with { Primitive = identity.Primitive * 397 }, geometry.Identity);
        NativeBattleActionMatrix quadMatrix = NativeBattlePoseMatrices.ModuleSpriteQuadCamera(current, quad);
        NativeBattleVector[] corners = [new(-128, -64, 0), new(128, -64, 0), new(-128, 64, 0), new(128, 64, 0)];
        for (int index = 0; index < corners.Length; index++)
        {
            System.Numerics.Vector3 camera = NativeBattlePilotVisualGeometry.Transform(quadMatrix, corners[index]);
            Assert.Equal(camera - new System.Numerics.Vector3(100, 50, 200), geometry.Corner(index));
        }
        Assert.NotNull(geometry.Sprite);
    }

    [Fact]
    public void Group14_drop_row_generation_is_shared_by_real_bank_owner_and_changes_on_reuse()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x23B1CF8, 0x23B1D08, 36, 100);
        (pool, int firstSlot) = pool.Append(0x5E18A0);
        (pool, int secondSlot) = pool.Append(0x5E18A0);
        NativeBattleModulePoolTask first = pool.Slots[firstSlot];
        NativeBattleModulePoolTask second = pool.Slots[secondSlot];
        NativeBattlePilotVisualIdentity before = NativeBattleGastricJuice169VisualLifetimes.Identity(first, 7, false);
        NativeBattleGastricJuice169VisualLifetimes.Allocated(first, 7);
        NativeBattlePilotVisualIdentity allocated = NativeBattleGastricJuice169VisualLifetimes.Identity(first, 7, false);
        Assert.Equal(before.ResourceGeneration + 1, allocated.ResourceGeneration);
        Assert.Equal(allocated.ResourceGeneration, NativeBattleGastricJuice169VisualLifetimes.Identity(second, 7, false).ResourceGeneration);
        Assert.NotEqual(allocated, NativeBattleGastricJuice169VisualLifetimes.Identity(second, 7, false));
        Assert.Equal(allocated, NativeBattleGastricJuice169VisualLifetimes.Identity(first.WriteWord(12, 9), 7, false));
        Assert.NotEqual(allocated, NativeBattleGastricJuice169VisualLifetimes.Identity(first, 7, true));
        Assert.Equal(before.ResourceGeneration, NativeBattleGastricJuice169VisualLifetimes.Identity(first, 8, false).ResourceGeneration);
        NativeBattleGastricJuice169VisualLifetimes.Allocated(second, 7);
        Assert.Equal(allocated.ResourceGeneration + 1, NativeBattleGastricJuice169VisualLifetimes.Identity(first, 7, false).ResourceGeneration);
    }

    private static NativeBattleActionMatrix Identity => NativeBattleCuragaMatrices.Identity8DD770();
    private static NativeBattleCameraProjection Projection => new(Identity, 256, 160, 108);
    private static ImmutableArray<byte> World(params NativeBattleVector[] points)
    {
        byte[] bytes = new byte[points.Length * 8];
        for (int index = 0; index < points.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 8), points[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 8 + 2), points[index].Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 8 + 4), points[index].Z);
        }
        return bytes.ToImmutableArray();
    }
}
