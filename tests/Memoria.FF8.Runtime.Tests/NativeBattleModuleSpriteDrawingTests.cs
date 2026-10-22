using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModuleSpriteDrawingTests
{
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
