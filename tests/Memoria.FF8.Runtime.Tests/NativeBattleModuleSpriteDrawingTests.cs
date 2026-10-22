using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModuleSpriteDrawingTests
{
    [Fact]
    public void Caller_color_uses_three_actual_BYTES_and_leaves_unknown_high_BYTE_and_GTE_color_owners_untouched()
    {
        var fixture = Fixture(2000, false);
        NativeBattleModuleSpriteDraw ordinary = Draw(fixture.Memory, fixture.Temporary, fixture.Resource);
        NativeBattleEffectArena temporary = fixture.Temporary.Write(0x24, [4, 0]).Write(0x1C, [64, 64, 64]);
        NativeBattleModuleSpriteDraw result = Draw(fixture.Memory, temporary, fixture.Resource);
        NativeBattleGeometryPrimitive[] quads = result.Batch.Attempts.Where(attempt => attempt.Primitive is not null).Select(attempt => attempt.Primitive!).ToArray();
        Assert.All(quads, quad => Assert.Equal(0x2C323232u, quad.Words[1]));
        Assert.Throws<NotSupportedException>(() => result.Temporary180.Read(0x1F, 1));
        Assert.Equal(JsonSerializer.Serialize(ordinary.Memory), JsonSerializer.Serialize(result.Memory));
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
        NativeBattleModuleSpriteDraw first = Draw(fixture.Memory, fixture.Temporary, fixture.Resource);
        NativeBattleModuleSpriteDraw restored = JsonSerializer.Deserialize<NativeBattleModuleSpriteDraw>(JsonSerializer.Serialize(first))!;
        byte[] positive = fixture.Resource.Bytes.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(positive.AsSpan(14), 2);
        NativeBattleModuleResource resource = fixture.Resource with { Bytes = positive.ToImmutableArray() };
        NativeBattleModuleSpriteDraw Next(NativeBattleModuleSpriteDraw current) => Draw(current.Memory, current.Temporary180, resource);
        NativeBattleModuleSpriteDraw next = Next(first);
        Assert.Equal(92, next.Batch.AllocationBytes);
        Assert.Equal((ushort)0x100, BinaryPrimitives.ReadUInt16LittleEndian(next.Temporary180.Read(0x24, 2)));
        Assert.Single(next.Batch.Attempts.Where(attempt => attempt.Primitive?.Words.Length == 2));
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(Next(restored)));
        NativeBattleEffectArena d8 = NativeBattleEffectNodeContextMemory.Copy(first.Temporary180, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 216), 0, 180).Write(0xB4, [0, 0]);
        NativeBattleProjectedSpriteDraw biased = NativeBattleProjectedSpriteD8.Draw(new(first.Memory, d8), _ => resource, Projection, 0, 17);
        Assert.Equal(JsonSerializer.Serialize(next.Batch), JsonSerializer.Serialize(biased.Batch));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_caller_shift14_changes_all_links_but_preserves_cached_depth_GTE_and_packet_words(bool extended)
    {
        var fixture = Fixture(2000, extended);
        NativeBattleModuleSpriteDraw ordinary = Draw(fixture.Memory, fixture.Temporary, fixture.Resource);
        NativeBattleModuleSpriteDraw shifted = NativeBattleModuleSpriteDrawing.Draw571C80(fixture.Memory, fixture.Temporary, _ => fixture.Resource, Projection, 0, 17, 14);
        Assert.Equal(JsonSerializer.Serialize(ordinary.Memory), JsonSerializer.Serialize(shifted.Memory));
        Assert.Equal(JsonSerializer.Serialize(ordinary.Temporary180), JsonSerializer.Serialize(shifted.Temporary180));
        Assert.Equal(ordinary.Batch.AllocationBytes, shifted.Batch.AllocationBytes);
        Assert.Equal(500, I32(shifted.Temporary180, 0x3C));
        Assert.All(shifted.Batch.Attempts.Where(attempt => attempt.Primitive is not null), attempt => Assert.Equal(17, attempt.Primitive!.Bucket));
        Assert.Equal(JsonSerializer.Serialize(ordinary.Batch.Attempts.Select(attempt => attempt.Data)), JsonSerializer.Serialize(shifted.Batch.Attempts.Select(attempt => attempt.Data)));
        Assert.Throws<InvalidDataException>(() => NativeBattleModuleSpriteDrawing.Draw571C80(fixture.Memory, fixture.Temporary, _ => fixture.Resource, Projection, 0, 17, 32));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Flags_zero_180_uses_loaded_GTE_and_cached_depth_without_any_D8_bias_owner(bool extended)
    {
        var fixture = Fixture(2000, extended);
        NativeBattleModuleSpriteDraw result = Draw(fixture.Memory, fixture.Temporary, fixture.Resource);
        NativeBattleGeometryPrimitive[] quads = result.Batch.Attempts.Where(attempt => attempt.Primitive?.Words.Length == 10).Select(attempt => attempt.Primitive!).ToArray();
        Assert.Equal(extended ? 92 : 80, result.Batch.AllocationBytes);
        Assert.Equal(new[] { 142, 142 }, quads.Select(quad => quad.Bucket));
        Assert.Equal(500, I32(result.Temporary180, 0x3C));
        Assert.Equal((uint?)500, result.Memory.GeometryRegisters!.Otz);
        Assert.Equal(1024, I32(result.Temporary180, 0xAC));
        Assert.Equal(new byte[] { 0x34, 0x12 }, result.Temporary180.Read(0x96, 2));
        Assert.False(result.Memory.GeometryRegisters.Rotation20[18].HasValue);
        Assert.Throws<NotSupportedException>(() => result.Temporary180.Read(6, 2));
        Assert.All(result.Temporary180.Regions, region => Assert.True(region.Offset + region.Data.Length <= 180));
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
        NativeBattleModuleSpriteDraw actual = Draw(fixture.Memory, fixture.Temporary, fixture.Resource);
        NativeBattleEffectArena d8 = NativeBattleEffectNodeContextMemory.Copy(fixture.Temporary, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 216), 0, 180).Write(0xB4, [0, 0]);
        NativeBattleProjectedSpriteDraw expected = NativeBattleProjectedSpriteD8.Draw(new(fixture.Memory, d8), _ => fixture.Resource, Projection, 0, 17);
        Assert.Equal(JsonSerializer.Serialize(expected.Batch), JsonSerializer.Serialize(actual.Batch));
        Assert.Equal(JsonSerializer.Serialize(expected.Memory), JsonSerializer.Serialize(actual.Memory));
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
        Assert.Throws<InvalidDataException>(() => Draw(fixture.Memory, fixture.Temporary.Write(0x24, [8, 0]), fixture.Resource));
        Assert.Throws<NotSupportedException>(() => Draw(fixture.Memory with { GeometryRegisters = null }, fixture.Temporary, fixture.Resource));
        Assert.Throws<NotSupportedException>(() => Draw(fixture.Memory with { ScriptedColorRegisters = null }, fixture.Temporary, fixture.Resource));
    }

    [Fact]
    public void JSON_next_preserves_GTE_temporary_knownness_and_packet_byte_order()
    {
        var fixture = Fixture(2000, true);
        NativeBattleModuleSpriteDraw first = Draw(fixture.Memory, fixture.Temporary, fixture.Resource);
        NativeBattleModuleSpriteDraw restored = JsonSerializer.Deserialize<NativeBattleModuleSpriteDraw>(JsonSerializer.Serialize(first))!;
        NativeBattleModuleSpriteDraw Next(NativeBattleModuleSpriteDraw input)
        {
            return NativeBattleModuleSpriteDrawing.Draw571C80(input.Memory, input.Temporary180.Write(0x24, [0, 0]), _ => fixture.Resource, Projection, 1024, 17);
        }
        Assert.Equal(JsonSerializer.Serialize(Next(first)), JsonSerializer.Serialize(Next(restored)));
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
