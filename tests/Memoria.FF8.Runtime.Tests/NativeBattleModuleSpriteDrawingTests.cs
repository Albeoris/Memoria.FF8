using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModuleSpriteDrawingTests
{
    [Theory]
    [InlineData((ushort)327)]
    [InlineData((ushort)328)]
    [InlineData((ushort)329)]
    [InlineData((ushort)330)]
    public void Gilgamesh_composed_sprite_capture_uses_the_actual_camera_without_changing_native_packets_or_authored_rectangle(ushort effect)
    {
        var fixture = Fixture(2000, false);
        NativeBattleCameraProjection camera = new(NativeBattleCuragaMatrices.Identity8DD770() with { X = 10 }, 100, 160, 108);
        NativeBattleActionMatrix parent = NativeBattleGfMatrices.Compose56C2F0(camera.View!, NativeBattleCuragaMatrices.Identity8DD770() with { X = 500 });
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = fixture.Resource, SpriteFrame = fixture.Resource.ProjectedFrame571C80(0), Anchor = new(100, 0, 2000, 0), Scale = 4096, Direction = 4096, SpriteFlags = 3 };
        NativeBattleModulePacketBatch native = NativeBattleModulePackets.EmitSprite571DC0(draw, camera with { View = parent }, 0, 17, 2);
        NativeBattlePilotVisualIdentity identity = new(effect, 0x21FFD58, 1, 23, 0) { ResourceGeneration = fixture.Resource.Address };
        NativeBattleModulePacketBatch captured = NativeBattleModulePackets.EmitSprite571DC0(draw with { PilotIdentity = identity, PilotProjection = camera }, camera with { View = parent }, 0, 17, 2);
        Assert.Equal(JsonSerializer.Serialize(native), JsonSerializer.Serialize(captured));
        NativeBattlePilotVisualGeometry a = Assert.IsType<NativeBattlePilotVisualGeometry>(captured.Primitives[0].Primitive.PilotGeometry);
        Assert.Equal(new System.Numerics.Vector3(600, 0, 2000), a.A);
        Assert.Equal(new System.Numerics.Vector3(632, 32, 2000), a.D);
        Assert.NotNull(a.Sprite);
        byte[] quads = draw.SpriteFrame!.Quads.ToArray();
        quads[0] = 4;
        NativeBattleModuleDraw later = draw with { PilotIdentity = identity, PilotProjection = camera, SpriteFrame = draw.SpriteFrame with { Quads = quads.ToImmutableArray() } };
        NativeBattleActionMatrix moved = parent with { X = parent.X + 100 };
        NativeBattleModulePacketBatch next = NativeBattleModulePackets.EmitSprite571DC0(later, camera with { View = moved }, 0, 17, 2);
        NativeBattlePilotVisualGeometry b = Assert.IsType<NativeBattlePilotVisualGeometry>(next.Primitives[0].Primitive.PilotGeometry);
        Assert.True(System.Numerics.Vector3.Distance(a.B + new System.Numerics.Vector3(50, 0, 0), NativeBattlePilotVisualGeometry.SampleCorner(a, b, 1, 0.5f)) < 0.001f);
        Assert.Equal(b.B, NativeBattlePilotVisualGeometry.SampleCorner(a, b, 1, 1));
    }

    [Theory]
    [InlineData(0xE19C14u, 2000)]
    [InlineData(0xE19CBCu, 2000)]
    [InlineData(0xE19E68u, 2000)]
    [InlineData(0xE19C14u, -2000)]
    [InlineData(0xE19CBCu, -2000)]
    [InlineData(0xE19E68u, -2000)]
    public void Effect218_sprite_binding_uses_the_single_actual_resource_resolution_and_preserves_native_draw_state(uint actualAddress, int depth)
    {
        var fixture = Fixture(depth, false);
        NativeBattleModuleResource resource = fixture.Resource with { Address = actualAddress };
        uint requested = actualAddress + 4;
        NativeBattleEffectArena temporary = fixture.Temporary.Write(0, BitConverter.GetBytes(requested));
        NativeBattleCameraProjection projection = Projection with { View = NativeBattleCuragaMatrices.Identity8DD770() };
        int nativeReads = 0;
        int capturedReads = 0;
        NativeBattleModuleSpriteDraw native = NativeBattleModuleSpriteDrawing.Draw571C80(NativeBattleTestSnapshots.Independent(fixture.Memory), NativeBattleTestSnapshots.Independent(temporary), address =>
        {
            Assert.Equal(requested, address);
            nativeReads++;
            return resource;
        }, projection, 0);
        NativeBattlePilotVisualIdentity owner = new(218, 0x1000, 3, 123, 7);
        NativeBattleModuleSpriteDraw captured = NativeBattleEffect218Presentation.DrawSprite(NativeBattleTestSnapshots.Independent(fixture.Memory), NativeBattleTestSnapshots.Independent(temporary), address =>
        {
            Assert.Equal(requested, address);
            capturedReads++;
            return resource;
        }, projection, 0, owner);
        Assert.Equal(1, nativeReads);
        Assert.Equal(1, capturedReads);
        Assert.Equal(NativeBattleTestSnapshots.Json(native), NativeBattleTestSnapshots.Json(captured));
        NativeBattlePilotVisualGeometry[] visuals = captured.Batch.Attempts.Where(attempt => attempt.Primitive?.PilotGeometry is not null).Select(attempt => attempt.Primitive!.PilotGeometry!).ToArray();
        if (depth > 0)
        {
            Assert.NotEmpty(visuals);
        }
        else
        {
            Assert.Empty(visuals);
        }
        foreach (NativeBattlePilotVisualGeometry visual in visuals)
        {
            Assert.Equal((long)actualAddress, visual.Identity.ResourceGeneration);
            Assert.Equal(owner.Effect, visual.Identity.Effect);
            Assert.Equal(owner.Bank, visual.Identity.Bank);
            Assert.Equal(owner.Slot, visual.Identity.Slot);
            Assert.Equal(owner.Generation, visual.Identity.Generation);
            Assert.False(visual.ScreenSpace);
        }
    }

    [Fact]
    public void Caller_color_uses_three_actual_BYTES_and_leaves_unknown_high_BYTE_and_GTE_color_owners_untouched()
    {
        var fixture = Fixture(2000, false);
        NativeBattleModuleSpriteDraw ordinary = Draw(NativeBattleTestSnapshots.Independent(fixture.Memory), NativeBattleTestSnapshots.Independent(fixture.Temporary), fixture.Resource);
        NativeBattleEffectArena temporary = fixture.Temporary.Write(0x24, [4, 0]).Write(0x1C, [64, 64, 64]);
        NativeBattleModuleSpriteDraw result = Draw(fixture.Memory, temporary, fixture.Resource);
        NativeBattleGeometryPrimitive[] quads = result.Batch.Attempts.Where(attempt => attempt.Primitive is not null).Select(attempt => attempt.Primitive!).ToArray();
        Assert.All(quads, quad => Assert.Equal(0x2C323232u, quad.Words[1]));
        Assert.Throws<NotSupportedException>(() => result.Temporary180.Read(0x1F, 1));
        Assert.Equal(NativeBattleTestSnapshots.Json(ordinary.Memory), NativeBattleTestSnapshots.Json(result.Memory));
        Assert.Null(result.Memory.ScriptedColorRegisters!.Material);
        byte[] bright = fixture.Resource.Bytes.ToArray();
        bright[19] = 255;
        NativeBattleModuleSpriteDraw wrapped = Draw(fixture.Memory, temporary.Write(0x1C, [255, 0, 255]), fixture.Resource with { Bytes = bright.ToImmutableArray() });
        Assert.Equal(0x2CFC00FCu, wrapped.Batch.Attempts.First(attempt => attempt.Primitive is not null).Primitive!.Words[1]);
    }

    [Fact]
    public void Caller_color_is_required_before_near_rejection_but_not_for_an_empty_frame()
    {
        var fixture = Fixture(0, false);
        NativeBattleEffectArena flags = fixture.Temporary.Write(0x24, [4, 0]);
        Assert.Throws<NotSupportedException>(() => Draw(fixture.Memory, flags, fixture.Resource));
        NativeBattleModuleSpriteDraw rejected = Draw(fixture.Memory, flags.Write(0x1C, [64, 64, 64]), fixture.Resource);
        Assert.All(rejected.Batch.Attempts, attempt => Assert.Equal(new byte[] { 50, 50, 50 }, attempt.Data.Skip(4).Take(3)));
        byte[] empty = fixture.Resource.Bytes.AsSpan(0, 18).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(empty.AsSpan(14), 0);
        NativeBattleModuleSpriteDraw result = Draw(fixture.Memory, flags, fixture.Resource with { Bytes = empty.ToImmutableArray() });
        Assert.Empty(result.Batch.Attempts);
        Assert.Throws<NotSupportedException>(() => result.Temporary180.Read(0x1C, 4));
    }

    [Fact]
    public void Caller_angle_requires_full_DWORD_and_native_wrapped_subtraction_even_for_empty_frames()
    {
        var fixture = Fixture(2000, false);
        int angle = int.MinValue;
        short roll = 1234;
        NativeBattleEffectArena temporary = fixture.Temporary.Write(0x24, [1, 0]).Write(8, BitConverter.GetBytes(angle));
        NativeBattleModuleSpriteDraw result = NativeBattleModuleSpriteDrawing.Draw571C80(fixture.Memory, temporary, _ => fixture.Resource, Projection, roll);
        NativeBattleActionMatrix expected = NativeBattleGfMatrices.RotateZ56D090(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, unchecked(angle - roll));
        for (int index = 0; index < 9; index++)
        {
            Assert.Equal(expected.Rotation[index], BinaryPrimitives.ReadInt16LittleEndian(result.Temporary180.Read(0x84 + index * 2, 2)));
        }
        byte[] empty = fixture.Resource.Bytes.AsSpan(0, 18).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(empty.AsSpan(14), 0);
        Assert.Throws<NotSupportedException>(() => Draw(fixture.Memory, temporary.MarkUnknown(10, 2), fixture.Resource with { Bytes = empty.ToImmutableArray() }));
        NativeBattleEffectArena d8 = NativeBattleEffectNodeContextMemory.Copy(temporary, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 216), 0, 180);
        Assert.Throws<InvalidDataException>(() => NativeBattleProjectedSpriteD8.Draw(new(fixture.Memory, d8), _ => fixture.Resource, Projection, roll, 17));
    }

    [Fact]
    public void Extended_bit_persists_on_the_same_physical_temporary_across_positive_frame_headers_and_JSON()
    {
        var fixture = Fixture(2000, true);
        NativeBattleModuleSpriteDraw first = Draw(NativeBattleTestSnapshots.Independent(fixture.Memory), NativeBattleTestSnapshots.Independent(fixture.Temporary), fixture.Resource);
        NativeBattleModuleSpriteDraw restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModuleSpriteDraw>(NativeBattleTestSnapshots.Json(first))!;
        byte[] positive = fixture.Resource.Bytes.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(positive.AsSpan(14), 2);
        NativeBattleModuleResource resource = fixture.Resource with { Bytes = positive.ToImmutableArray() };
        NativeBattleModuleSpriteDraw Next(NativeBattleModuleSpriteDraw current) => Draw(current.Memory, current.Temporary180, resource);
        NativeBattleModuleSpriteDraw next = Next(first);
        Assert.Equal(92, next.Batch.AllocationBytes);
        Assert.Equal((ushort)0x100, BinaryPrimitives.ReadUInt16LittleEndian(next.Temporary180.Read(0x24, 2)));
        Assert.Single(next.Batch.Attempts.Where(attempt => attempt.Primitive?.Words.Length == 2));
        Assert.Equal(NativeBattleTestSnapshots.Json(next), NativeBattleTestSnapshots.Json(Next(restored)));
        NativeBattleEffectArena d8 = NativeBattleEffectNodeContextMemory.Copy(first.Temporary180, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 216), 0, 180).Write(0xB4, [0, 0]);
        NativeBattleProjectedSpriteDraw biased = NativeBattleProjectedSpriteD8.Draw(new(first.Memory, d8), _ => resource, Projection, 0, 17);
        Assert.Equal(NativeBattleTestSnapshots.Json(next.Batch), NativeBattleTestSnapshots.Json(biased.Batch));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_caller_shift14_changes_all_links_but_preserves_cached_depth_GTE_and_packet_words(bool extended)
    {
        var fixture = Fixture(2000, extended);
        NativeBattleModuleSpriteDraw ordinary = Draw(NativeBattleTestSnapshots.Independent(fixture.Memory), NativeBattleTestSnapshots.Independent(fixture.Temporary), fixture.Resource);
        NativeBattleModuleSpriteDraw shifted = NativeBattleModuleSpriteDrawing.Draw571C80(fixture.Memory, fixture.Temporary, _ => fixture.Resource, Projection, 0, 17, 14);
        Assert.Equal(NativeBattleTestSnapshots.Json(ordinary.Memory), NativeBattleTestSnapshots.Json(shifted.Memory));
        Assert.Equal(NativeBattleTestSnapshots.Json(ordinary.Temporary180), NativeBattleTestSnapshots.Json(shifted.Temporary180));
        Assert.Equal(ordinary.Batch.AllocationBytes, shifted.Batch.AllocationBytes);
        Assert.Equal(500, I32(shifted.Temporary180, 0x3C));
        Assert.All(shifted.Batch.Attempts.Where(attempt => attempt.Primitive is not null), attempt => Assert.Equal(17, attempt.Primitive!.Bucket));
        Assert.Equal(NativeBattleTestSnapshots.Json(ordinary.Batch.Attempts.Select(attempt => attempt.Data)), NativeBattleTestSnapshots.Json(shifted.Batch.Attempts.Select(attempt => attempt.Data)));
        Assert.Throws<InvalidDataException>(() => NativeBattleModuleSpriteDrawing.Draw571C80(fixture.Memory, fixture.Temporary, _ => fixture.Resource, Projection, 0, 17, 32));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Flags_zero_180_uses_loaded_GTE_and_cached_depth_without_any_D8_bias_owner(bool extended)
    {
        var fixture = Fixture(2000, extended);
        NativeBattleModuleSpriteDraw result = Draw(NativeBattleTestSnapshots.Independent(fixture.Memory), NativeBattleTestSnapshots.Independent(fixture.Temporary), fixture.Resource);
        NativeBattleGeometryPrimitive[] quads = result.Batch.Attempts.Where(attempt => attempt.Primitive?.Words.Length == 10).Select(attempt => attempt.Primitive!).ToArray();
        Assert.Equal(extended ? 92 : 80, result.Batch.AllocationBytes);
        Assert.Equal(new[] { 142, 142 }, quads.Select(quad => quad.Bucket));
        Assert.Equal(500, I32(result.Temporary180, 0x3C));
        Assert.Equal((uint?)500, result.Memory.GeometryRegisters!.Otz);
        Assert.Equal(1024, I32(result.Temporary180, 0xAC));
        Assert.Equal(new byte[] { 0x34, 0x12 }, result.Temporary180.Read(0x96, 2));
        Assert.False(result.Memory.GeometryRegisters.Rotation20[18].HasValue);
        Assert.Throws<NotSupportedException>(() => result.Temporary180.Read(6, 2));
        Assert.All(NativeBattleStorageSnapshots.Capture(result.Temporary180).Regions, region => Assert.True(region.Offset + region.Data.Length <= 180));
        foreach (NativeBattleGfGeometryAttempt attempt in result.Batch.Attempts.Where(attempt => attempt.Data.Length == 40 && attempt.WriteMask[4] == NativeBattleGfPacketByteState.Known))
        {
            foreach (int offset in new[] { 30, 31, 38, 39 })
            {
                Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[offset]);
            }
        }
    }

    [Fact]
    public void Shared_core_matches_D8_with_actual_zero_bias_without_requiring_D8_tail_for_180()
    {
        var fixture = Fixture(2000, true);
        NativeBattleModuleSpriteDraw actual = Draw(NativeBattleTestSnapshots.Independent(fixture.Memory), NativeBattleTestSnapshots.Independent(fixture.Temporary), fixture.Resource);
        NativeBattleEffectArena d8 = NativeBattleEffectNodeContextMemory.Copy(fixture.Temporary, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 216), 0, 180).Write(0xB4, [0, 0]);
        NativeBattleProjectedSpriteDraw expected = NativeBattleProjectedSpriteD8.Draw(new(fixture.Memory, d8), _ => fixture.Resource, Projection, 0, 17);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected.Batch), NativeBattleTestSnapshots.Json(actual.Batch));
        Assert.Equal(NativeBattleTestSnapshots.Json(expected.Memory), NativeBattleTestSnapshots.Json(actual.Memory));
        var first = expected.TemporaryD8.ReadSnapshot(0, 180);
        var second = actual.Temporary180.ReadSnapshot(0, 180);
        Assert.Equal(first.Known, second.Known);
        Assert.Equal(first.Data, second.Data);
        Assert.Throws<InvalidDataException>(() => Draw(fixture.Memory, d8, fixture.Resource));
    }

    [Fact]
    public void Rejected_180_sprite_publishes_candidate_FLAG_without_fourth_projection_or_AVSZ_read()
    {
        var fixture = Fixture(0, false);
        NativeBattleEffectMemory memory = fixture.Memory with { ScriptedGraphics = fixture.Memory.ScriptedGraphics! with { Zsf4 = null } };
        NativeBattleModuleSpriteDraw result = Draw(memory, fixture.Temporary, fixture.Resource);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(2, result.Batch.Attempts.Length);
        Assert.All(result.Batch.Attempts, attempt =>
        {
            Assert.Null(attempt.Primitive);
            Assert.Equal(0, attempt.ByteOffsetRelative);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[36]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[29]);
        });
        Assert.NotEqual(0u, U32(result.Temporary180, 0x40) & 0x60000);
        Assert.Equal(-1, I32(result.Temporary180, 0x3C));
        Assert.Null(result.Memory.GeometryRegisters!.Otz);
    }

    [Fact]
    public void Empty_frame_does_not_read_unreached_projection_controls_or_average_depth()
    {
        var fixture = Fixture(2000, false);
        byte[] bytes = fixture.Resource.Bytes.AsSpan(0, 18).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(14), 0);
        NativeBattleModuleResource resource = fixture.Resource with { Bytes = bytes.ToImmutableArray() };
        NativeBattleModuleSpriteDraw result = Draw(fixture.Memory with { ScriptedGraphics = null }, fixture.Temporary, resource);
        Assert.Empty(result.Batch.Attempts);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(-1, I32(result.Temporary180, 0x3C));
        Assert.Null(result.Memory.GeometryRegisters!.Otz);
    }

    [Fact]
    public void Caller_flags_and_actual_loaded_register_owners_are_required()
    {
        var fixture = Fixture(2000, false);
        Assert.Throws<InvalidDataException>(() => Draw(fixture.Memory, NativeBattleTestSnapshots.Independent(fixture.Temporary).Write(0x24, [2, 0]), fixture.Resource));
        Assert.Throws<NotSupportedException>(() => Draw(fixture.Memory with { GeometryRegisters = null }, fixture.Temporary, fixture.Resource));
        Assert.Throws<NotSupportedException>(() => Draw(fixture.Memory with { ScriptedColorRegisters = null }, fixture.Temporary, fixture.Resource));
    }

    [Fact]
    public void JSON_next_preserves_GTE_temporary_knownness_and_packet_byte_order()
    {
        var fixture = Fixture(2000, true);
        NativeBattleModuleSpriteDraw first = Draw(NativeBattleTestSnapshots.Independent(fixture.Memory), NativeBattleTestSnapshots.Independent(fixture.Temporary), fixture.Resource);
        NativeBattleModuleSpriteDraw restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModuleSpriteDraw>(NativeBattleTestSnapshots.Json(first))!;
        NativeBattleModuleSpriteDraw Next(NativeBattleModuleSpriteDraw input)
        {
            return NativeBattleModuleSpriteDrawing.Draw571C80(input.Memory, input.Temporary180.Write(0x24, [0, 0]), _ => fixture.Resource, Projection, 1024, 17);
        }
        Assert.Equal(NativeBattleTestSnapshots.Json(Next(first)), NativeBattleTestSnapshots.Json(Next(restored)));
    }

    private static NativeBattleModuleSpriteDraw Draw(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary, NativeBattleModuleResource resource)
    {
        return NativeBattleModuleSpriteDrawing.Draw571C80(memory, temporary, address => address == resource.Address ? resource : throw new InvalidDataException(), Projection, 0, 17);
    }

    private static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);

    private static (NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary, NativeBattleModuleResource Resource) Fixture(int depth, bool extended)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = depth };
        memory = memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), matrix, true, true, 0x1234), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        byte[] bytes = new byte[58];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 0xFFFF);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(14), extended ? 0x80000002u : 2u);
        for (int quad = 0; quad < 2; quad++)
        {
            Span<byte> row = bytes.AsSpan(18 + 20 * quad, 20);
            row[0] = row[2] = 2;
            row[1] = 100;
            row[3] = 0x2C;
            row[4] = 10;
            row[5] = 20;
            BinaryPrimitives.WriteUInt16LittleEndian(row.Slice(14), quad == 0 ? (ushort)0x123 : (ushort)0xD23);
            if (quad == 1)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(row.Slice(12), 1024);
                BinaryPrimitives.WriteUInt16LittleEndian(row.Slice(16), 4096);
                BinaryPrimitives.WriteUInt16LittleEndian(row.Slice(18), 2048);
            }
        }
        NativeBattleModuleResource resource = new(0, 0x1048C94, 0, 0, bytes.ToImmutableArray());
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().MarkUnknown(0, 180).Write(0, BitConverter.GetBytes(resource.Address)).Write(4, [0, 0]).Write(0x24, [0, 0]);
        return (memory, temporary, resource);
    }

    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static int I32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt32LittleEndian(arena.Read(offset, 4));
}
