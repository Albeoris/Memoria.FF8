using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGroup16Windowed108CaptureTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(0, 2)]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 0)]
    [InlineData(3, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 0)]
    [InlineData(4, 1)]
    [InlineData(4, 2)]
    [InlineData(5, 0)]
    [InlineData(5, 1)]
    [InlineData(5, 2)]
    [InlineData(6, 0)]
    [InlineData(6, 1)]
    [InlineData(6, 2)]
    [InlineData(7, 0)]
    [InlineData(7, 1)]
    [InlineData(7, 2)]
    public void All_native_groups_capture_actual_inline_selected_and_packed_operands_without_changing_ledger(int group, int mode)
    {
        List<string> plainReads = [];
        List<string> capturedReads = [];
        List<NativeBattleGfGeometryAttempt> plainObserved = [];
        List<NativeBattleGfGeometryAttempt> capturedObserved = [];
        NativeBattleWindowedMeshContext108Step plain = Draw(group, mode, null, plainReads, plainObserved);
        NativeBattleWindowedMeshContext108Step captured = Draw(group, mode, new(210, 0x100, 0, 123, 7), capturedReads, capturedObserved);
        Assert.Equal(plainReads, capturedReads);
        Assert.Equal(NativeBattleTestSnapshots.Json(plain), NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(NativeBattleTestSnapshots.Json(plainObserved), NativeBattleTestSnapshots.Json(capturedObserved));
        NativeBattleGeometryPrimitive polygon = Assert.Single(captured.Batch.Attempts.Where(attempt => attempt.Primitive?.PilotGeometry is not null)).Primitive!;
        NativeBattlePilotVisualGeometry geometry = polygon.PilotGeometry!;
        Assert.Equal(mode == 2 ? new Vector3(-180, -80, 2030) : new Vector3(-80, -80, 2000), geometry.A);
        Assert.Equal((byte)((group & 1) != 0 ? 4 : 3), geometry.CornerCount);
        Assert.True(geometry.UseSourceDepths);
        Vector4 payload = polygon.VertexPayload4.Length == 4 ? new(polygon.VertexPayload4[0], polygon.VertexPayload4[1], polygon.VertexPayload4[2], polygon.VertexPayload4[3]) : Vector4.Zero;
        Assert.Equal(payload, geometry.SourceDepths);
        Assert.All(captured.Batch.Attempts.Where(attempt => attempt.Primitive is not null && (attempt.Primitive.Words[1] & 0xE0000000) == 0xE0000000), attempt => Assert.Null(attempt.Primitive!.PilotGeometry));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    public void Rejected_quad_keeps_partial_writes_and_observer_without_reading_fourth_selected_row(int group)
    {
        List<string> plainReads = [];
        List<string> capturedReads = [];
        List<NativeBattleGfGeometryAttempt> plainObserved = [];
        List<NativeBattleGfGeometryAttempt> capturedObserved = [];
        NativeBattleWindowedMeshContext108Step plain = Draw(group, 2, null, plainReads, plainObserved, reject: true);
        NativeBattleWindowedMeshContext108Step captured = Draw(group, 2, new(207, 0x100, 0, 123, 7), capturedReads, capturedObserved, reject: true);
        Assert.Equal(plainReads, capturedReads);
        Assert.DoesNotContain("vertex:24", capturedReads);
        Assert.DoesNotContain("packed:24", capturedReads);
        Assert.Equal(NativeBattleTestSnapshots.Json(plain), NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(NativeBattleTestSnapshots.Json(plainObserved), NativeBattleTestSnapshots.Json(capturedObserved));
        Assert.All(captured.Batch.Attempts, attempt => Assert.Null(attempt.Primitive));
    }

    [Fact]
    public void Empty_source_keeps_default_and_opted_in_native_register_and_context_behavior()
    {
        NativeBattleWindowedMeshContext108Step plain = Draw(3, 2, null, [], [], empty: true);
        NativeBattleWindowedMeshContext108Step captured = Draw(3, 2, new(140, 0x100, 0, 123, 7), [], [], empty: true, nullView: true);
        Assert.Equal(NativeBattleTestSnapshots.Json(plain), NativeBattleTestSnapshots.Json(captured));
        Assert.Empty(captured.Batch.Attempts);
    }

    [Fact]
    public void Default_null_identity_adds_no_camera_view_requirement()
    {
        NativeBattleWindowedMeshContext108Step withView = Draw(3, 1, null, [], []);
        NativeBattleWindowedMeshContext108Step withoutView = Draw(3, 1, null, [], [], nullView: true);
        Assert.Equal(NativeBattleTestSnapshots.Json(withView), NativeBattleTestSnapshots.Json(withoutView));
        Assert.Contains(withoutView.Batch.Attempts, attempt => attempt.Primitive is not null);
    }

    private static NativeBattleWindowedMeshContext108Step Draw(int group, int mode, NativeBattlePilotVisualIdentity? identity, List<string> reads, List<NativeBattleGfGeometryAttempt> observed, bool reject = false, bool empty = false, bool nullView = false)
    {
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { X = 70, Y = 30, Z = 2100 };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), matrix, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleCameraProjection projection = new(nullView ? null : NativeBattleCuragaMatrices.Identity8DD770() with { X = 50, Y = 10, Z = 100 }, 100, 160, 108);
        NativeBattleStaticMeshResource mesh = Mesh(group, empty);
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 108).Write(0, BitConverter.GetBytes(mesh.Address)).Write(4, BitConverter.GetBytes(0x20000000u)).Write(8, new byte[4]).Write(12, new byte[4]).Write(24, new byte[4]).Write(28, BitConverter.GetBytes(mode == 0 ? 0x30u : 0x2030u)).Write(32, new byte[20]);
        return NativeBattleWindowedMeshContext108.DrawLoaded(memory, mesh, projection, context, NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2, 4, offset =>
        {
            Assert.False(reject && offset == 24);
            reads.Add($"vertex:{offset}");
            return (Point(offset, false), null);
        }, observeAttempt: (attempt, _) => observed.Add(attempt), readSelectedPackedVertex: mode != 2 ? null : offset =>
        {
            Assert.False(reject && offset == 24);
            reads.Add($"packed:{offset}");
            NativeBattleVector point = Point(offset, true);
            return NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
        }, pilotIdentity: identity);

        NativeBattleVector Point(int offset, bool packed)
        {
            NativeBattleVector[] vertices = [new(-100, -100, 0), new(100, -100, 0), new(-100, 100, 0), new(100, 100, 0)];
            NativeBattleVector point = vertices[offset / 8];
            return point with { X = packed ? unchecked((short)(point.X * 2)) : point.X, Z = reject ? (short)-2100 : packed ? (short)30 : (short)0 };
        }
    }

    private static NativeBattleStaticMeshResource Mesh(int group, bool empty)
    {
        int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
        uint[] commands = [0x20, 0x28, 0x24, 0x2C, 0x30, 0x38, 0x34, 0x3C];
        byte[] source = new byte[72 + (empty ? 0 : lengths[group])];
        BinaryPrimitives.WriteInt32LittleEndian(source, 40);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(4), 4);
        (short X, short Y)[] points = [(-100, -100), (100, -100), (-100, 100), (100, 100)];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(8 + index * 8), points[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(10 + index * 8), points[index].Y);
        }
        if (!empty)
        {
            int face = 44 + group * 4;
            BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(face - 4), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(face), commands[group] << 24 | 0x808080);
            for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(face + 4 + corner * 2), (ushort)(corner * 2));
            }
        }
        return new(0, 0x10000000, source.ToImmutableArray());
    }
}
