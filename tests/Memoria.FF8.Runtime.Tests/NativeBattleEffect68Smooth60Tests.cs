using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect68Smooth60Tests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Stateful_spinner_capture_preserves_native_context_registers_and_partial_allocation(bool rejectFirst)
    {
        NativeBattleStaticMeshResource mesh = Spinner(rejectFirst);
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
        NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770(), true, true, 0), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleEffectArena Context() => NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(mesh.Address)).Write(8, BitConverter.GetBytes(0u)).Write(12, BitConverter.GetBytes(0u)).Write(0x1C, BitConverter.GetBytes(0xF0u));
        NativeBattleStaticMeshContext88Step native = NativeBattleEffect68Lower.DrawSpinner(Memory(), mesh, projection, Context(), NativeBattleGteColorInterpolation.CodeConstants45F270);
        NativeBattlePilotVisualIdentity identity = new(68, 2, 1, 3, 0) { ResourceGeneration = mesh.Address };
        NativeBattleStaticMeshContext88Step captured = NativeBattleEffect68Lower.DrawSpinner(Memory(), mesh, projection, Context(), NativeBattleGteColorInterpolation.CodeConstants45F270, identity);
        Assert.Equal(NativeBattleTestSnapshots.Json(native.Memory), NativeBattleTestSnapshots.Json(captured.Memory));
        Assert.Equal(NativeBattleTestSnapshots.Json(native.Context), NativeBattleTestSnapshots.Json(captured.Context));
        Assert.Equal(native.Batch.AllocationBytes, captured.Batch.AllocationBytes);
        Assert.Equal(2, captured.Batch.Attempts.Length);
        for (int index = 0; index < 2; index++)
        {
            NativeBattleGfGeometryAttempt expected = native.Batch.Attempts[index];
            NativeBattleGfGeometryAttempt actual = captured.Batch.Attempts[index];
            Assert.Equal(expected.ByteOffsetRelative, actual.ByteOffsetRelative);
            Assert.Equal(expected.Data, actual.Data);
            Assert.Equal(expected.WriteMask, actual.WriteMask);
            if (expected.Primitive is { } primitive)
            {
                Assert.Equal(primitive.Words, actual.Primitive!.Words);
                Assert.Equal(primitive.Bucket, actual.Primitive.Bucket);
                NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(actual.Primitive.PilotGeometry);
                Assert.Equal(identity with { Primitive = index }, geometry.Identity);
                Assert.Equal(new Vector3(-100, -100, 1000), geometry.A);
                Assert.Equal(new Vector3(100, 100, 1000), geometry.D);
            }
            else
            {
                Assert.Null(actual.Primitive);
            }
        }
    }

    [Fact]
    public void Screen_record_receipts_preserve_native_zero_flags_and_exact_signed_source_endpoints()
    {
        NativeBattleModulePoolTask task = new(0, 0, null, 0, new byte[108].ToImmutableArray());
        task = task.WriteWord(0x64, 2048);
        byte[] records = new byte[18];
        BinaryPrimitives.WriteInt16LittleEndian(records.AsSpan(4), -3);
        BinaryPrimitives.WriteInt16LittleEndian(records.AsSpan(6), 3);
        BinaryPrimitives.WriteInt16LittleEndian(records.AsSpan(8), 7);
        BinaryPrimitives.WriteInt16LittleEndian(records.AsSpan(10), -7);
        BinaryPrimitives.WriteUInt16LittleEndian(records.AsSpan(16), 0xFF);
        NativeBattleModulePacketBatch native = NativeBattleEffect68Lower.RecordLines(task, records);
        NativeBattlePilotVisualIdentity identity = new(68, 3, 1, 4, 2);
        NativeBattleModulePacketBatch captured = NativeBattleEffect68Lower.RecordLines(task, records, identity);
        NativeBattleAllocatedPrimitive line = Assert.Single(captured.Primitives);
        Assert.Equal(Assert.Single(native.Primitives).Primitive.Words, line.Primitive.Words);
        Assert.Equal(0, line.Flags20);
        Assert.Equal(16, captured.AllocationBytes);
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(line.Primitive.PilotGeometry);
        Assert.True(geometry.ScreenSpace);
        Assert.Equal(1, geometry.ScreenScale);
        Assert.Equal(new Vector3(159, 109, 0), geometry.A);
        Assert.Equal(new Vector3(163, 105, 0), geometry.B);
    }

    private static NativeBattleStaticMeshResource Spinner(bool rejectFirst)
    {
        byte[] bytes = new byte[136];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 56);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 6);
        short[] xy = [-100, -100, 100, -100, -100, 100, 100, 100, 0, 0, 0, 0];
        for (int vertex = 0; vertex < 6; vertex++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + vertex * 8), xy[vertex * 2]);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + vertex * 8), xy[vertex * 2 + 1]);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12 + vertex * 8), 1000);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(68), 2);
        for (int face = 0; face < 2; face++)
        {
            int offset = 72 + face * 24;
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), 0x2C808080);
            for (int corner = 0; corner < 4; corner++)
            {
                ushort index = (ushort)(corner * 2);
                if (face == 0 && rejectFirst)
                {
                    index = corner == 3 ? ushort.MaxValue : (ushort)0;
                }
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 4 + corner * 2), index);
            }
        }
        return new(0, 0x15934CC, bytes.ToImmutableArray());
    }
}
