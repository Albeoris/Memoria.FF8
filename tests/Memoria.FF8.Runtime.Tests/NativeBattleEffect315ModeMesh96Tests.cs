using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect315ModeMesh96Tests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Presentation_mode_mesh_captures_accepted_corners_only_and_preserves_native_modes(int rejection)
    {
        var plain = Draw(Mesh(zeroArea: rejection == 1, outside: rejection == 2), Context(0xF3));
        string frozen = JsonSerializer.Serialize(new { Memory = NativeBattleStorageSnapshots.Capture(plain.Memory), Context96 = NativeBattleStorageSnapshots.Capture(plain.Context96) });
        var captured = Draw(Mesh(zeroArea: rejection == 1, outside: rejection == 2), Context(0xF3), new(315, 0x123400, 2, 19, 0) { ResourceGeneration = 43 });
        Assert.Equal(frozen, JsonSerializer.Serialize(new { Memory = NativeBattleStorageSnapshots.Capture(captured.Memory), Context96 = NativeBattleStorageSnapshots.Capture(captured.Context96) }));
        Assert.Equal(plain.Batch.AllocationBytes, captured.Batch.AllocationBytes);
        Assert.Equal(plain.Batch.Attempts.Length, captured.Batch.Attempts.Length);
        for (int index = 0; index < plain.Batch.Attempts.Length; index++)
        {
            var expected = plain.Batch.Attempts[index];
            var actual = captured.Batch.Attempts[index];
            Assert.Equal(expected.Data.ToArray(), actual.Data.ToArray());
            Assert.Equal(expected.WriteMask.ToArray(), actual.WriteMask.ToArray());
            if (rejection != 0)
            {
                Assert.Null(actual.Primitive);
                continue;
            }
            var geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(actual.Primitive!.PilotGeometry);
            Assert.Equal((byte)4, geometry.CornerCount);
            Assert.False(geometry.ScreenSpace);
            Assert.True(geometry.UseSourceDepths);
            Assert.Equal(2000, geometry.SourceDepths!.Value.X);
            Assert.Equal(expected.Primitive!.Words.ToArray(), actual.Primitive.Words.ToArray());
        }
    }

    [Theory]
    [InlineData(0x33u)]
    [InlineData(0xF3u)]
    public void Selected_G4_keeps_inline_modes_explicit_four_depths_and_current_color_FIFO(uint flags)
    {
        NativeBattleStaticMeshResource mesh = Mesh();
        NativeBattleEffect315ModeMesh96Step result = Draw(mesh, Context(flags));
        Assert.Equal(33 * 44, result.Batch.AllocationBytes);
        Assert.Equal(33, result.Batch.Attempts.Length);
        Assert.Equal(Enumerable.Range(0, 33).Select(index => index * 44), result.Batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        foreach (NativeBattleGfGeometryAttempt attempt in result.Batch.Attempts)
        {
            NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
            Assert.Equal(0x0A000000u, primitive.Words[0]);
            Assert.Equal(0xE1000220u, primitive.Words[1]);
            Assert.Equal(0xE1000220u, primitive.Words[10]);
            Assert.Equal(new[] { 2000, 2000, 2000, 2000 }, primitive.VertexPayload4);
            Assert.Equal(142, primitive.Bucket);
            Assert.Equal(flags == 0xF3 ? 0x3A081119u : 0x3A112233u, primitive.Words[2]);
            Assert.Equal(flags == 0xF3 ? 0x00112233u : 0x00224466u, primitive.Words[4]);
            Assert.Equal(flags == 0xF3 ? 0x00223344u : 0x00446688u, primitive.Words[6]);
            Assert.Equal(flags == 0xF3 ? 0x00334455u : 0x006688AAu, primitive.Words[8]);
            Assert.All(attempt.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        }
        Assert.Equal(mesh.Address + 8, U(result.Context96, 4));
        Assert.Equal(mesh.Address + (uint)mesh.Bytes.Length, U(result.Context96, 0x20));
        Assert.Equal(0u, U(result.Context96, 0x18));
        Assert.Equal(500u, U(result.Context96, 0x2C));
        Assert.Equal(0u, U(result.Context96, 0x30) & 0x60000);
        Assert.Equal(0u, result.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        if (flags == 0xF3)
        {
            Assert.Equal(2048u, result.Memory.ScriptedColorRegisters!.Ir0);
            Assert.Equal(0x3A112233u, result.Memory.ScriptedColorRegisters.Material);
            Assert.Equal(new uint?[] { 0x00223344, 0x00334455, 0x3A081119 }, result.Memory.ScriptedColorRegisters.Rgb.ToArray());
            Assert.Equal(new int?[] { 408, 272, 136 }, result.Memory.ScriptedColorRegisters.Mac.ToArray());
        }
        else
        {
            Assert.Null(result.Memory.ScriptedColorRegisters!.Material);
        }
    }

    [Fact]
    public void Zero_area_keeps_only_the_reached_header_and_first_color_attempt_stores()
    {
        NativeBattleEffect315ModeMesh96Step result = Draw(Mesh(zeroArea: true), Context(0xF3));
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(33, result.Batch.Attempts.Length);
        Assert.All(result.Batch.Attempts, attempt =>
        {
            Assert.Null(attempt.Primitive);
            Assert.Equal(0, attempt.ByteOffsetRelative);
            Assert.Equal(0x0A000000u, Word(attempt, 0));
            Assert.Equal(0x3A112233u, Word(attempt, 8));
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[0]);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[8]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[4]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[12]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[40]);
        });
        Assert.Equal(0u, U(result.Context96, 0x24));
        Assert.DoesNotContain(true, result.Context96.ReadSnapshot(0x2C, 4).Known);
        Assert.Null(result.Memory.ScriptedColorRegisters!.Material);
    }

    [Fact]
    public void Viewport_rejection_occurs_after_fourth_projection_and_average_but_before_mode_stores()
    {
        NativeBattleEffect315ModeMesh96Step result = Draw(Mesh(outside: true), Context(0xF3));
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(500u, result.Memory.GeometryRegisters!.Otz);
        Assert.DoesNotContain(true, result.Context96.ReadSnapshot(0x2C, 4).Known);
        Assert.All(result.Batch.Attempts, attempt =>
        {
            Assert.Null(attempt.Primitive);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[12]);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[36]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[4]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[16]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[40]);
        });
        Assert.Null(result.Memory.ScriptedColorRegisters!.Material);
    }

    [Fact]
    public void Actual_caller_flags_admit_negative_area_and_state_round_trips()
    {
        NativeBattleEffect315ModeMesh96Step result = Draw(Mesh(reverse: true), Context(0xF3));
        Assert.True(unchecked((int)U(result.Context96, 0x24)) < 0);
        Assert.Equal(33 * 44, result.Batch.AllocationBytes);
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result));
        Assert.Equal(json, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect315ModeMesh96Step>(json)!))));
    }

    [Fact]
    public void Unselected_group_or_geometry_count_fails_before_rendering()
    {
        NativeBattleStaticMeshResource mesh = Mesh();
        byte[] bytes = mesh.Bytes.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 49);
        Assert.Throws<InvalidDataException>(() => Draw(mesh with { Bytes = bytes.ToImmutableArray() }, Context(0x33)));
        bytes = mesh.Bytes.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(392), 1);
        Assert.Throws<NotSupportedException>(() => Draw(mesh with { Bytes = bytes.ToImmutableArray() }, Context(0x33)));
    }

    private static NativeBattleEffect315ModeMesh96Step Draw(NativeBattleStaticMeshResource mesh, NativeBattleEffectArena context, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        if (pilotIdentity is null)
        {
            return NativeBattleEffect315ModeMesh96.DrawLoaded(memory, mesh, new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270);
        }
        return NativeBattleEffect315ModeMesh96.DrawLoadedWithPresentation(memory, mesh, new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270, pilotIdentity: pilotIdentity);
    }

    private static NativeBattleEffectArena Context(uint flags) => NativeBattleEffectArena.Clear().MarkUnknown(0, 96).Write(0, BitConverter.GetBytes(0x10000000u)).Write(8, new byte[4]).Write(12, BitConverter.GetBytes(2048u)).Write(0x18, BitConverter.GetBytes(0x12345678u)).Write(0x1C, BitConverter.GetBytes(flags)).Write(0x58, BitConverter.GetBytes(0xE1000220u)).Write(0x5C, BitConverter.GetBytes(0xE1000220u));
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static uint Word(NativeBattleGfGeometryAttempt attempt, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(offset));

    private static NativeBattleStaticMeshResource Mesh(bool zeroArea = false, bool outside = false, bool reverse = false)
    {
        byte[] bytes = new byte[1216];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 392);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 48);
        NativeBattleVector[] vertices = zeroArea ? [new(0, 0, 0), new(0, 0, 0), new(0, 0, 0), new(0, 0, 0)] : outside ? [new(7000, -400, 0), new(7800, -400, 0), new(7400, 400, 0), new(7800, 400, 0)] : [new(-400, -400, 0), new(400, -400, 0), new(0, 400, 0), new(400, 400, 0)];
        for (int vertex = 0; vertex < vertices.Length; vertex++)
        {
            int offset = 8 + vertex * 8;
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), vertices[vertex].X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), vertices[vertex].Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 4), vertices[vertex].Z);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(412), 33);
        for (int face = 0; face < 33; face++)
        {
            int offset = 416 + face * 24;
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), 0x38112233);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 6), (ushort)(reverse ? 4 : 2));
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 8), (ushort)(reverse ? 2 : 4));
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 10), 6);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset + 12), 0x00224466);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset + 16), 0x00446688);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset + 20), 0x006688AA);
        }
        return new(0, 0x10000000, bytes.ToImmutableArray());
    }
}
