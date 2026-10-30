using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect66Mesh108Tests
{
    [Fact]
    public void Selected_G4_tints_only_after_geometry_admission_and_publishes_native_scratch_and_current_OTZ()
    {
        NativeBattleEffect66Mesh108Step result = Draw();
        NativeBattleGfGeometryAttempt first = result.Batch.Attempts[0];
        Assert.Equal(36, result.Batch.AllocationBytes);
        Assert.Equal(0x00636464u, first.Primitive!.Words[1] & 0xFFFFFF);
        Assert.Equal(0x3Au, first.Primitive.Words[1] >> 24);
        Assert.Equal(17 + 2000, first.Primitive.Bucket);
        Assert.Equal(0x00FF8040u, Dword(result.Context108, 0x58));
        Assert.Equal(0x3A3264C8u, Dword(result.Context108, 0x5C));
        Assert.Equal(0xDE90D4u, Dword(result.Context108, 4));
        Assert.Equal(0xDE90CCu + 1752, Dword(result.Context108, 0x24));
        Assert.Equal(0u, Dword(result.Context108, 0x18));
        Assert.Equal(NativeBattleGfPacketByteState.Known, first.WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, result.Batch.Attempts[1].WriteMask[4]);
    }

    [Fact]
    public void Zero_area_rejects_before_fourth_projection_and_keeps_initial_color_scratch_without_packet_RGB()
    {
        NativeBattleEffect66Mesh108Step result = Draw(degenerate: true);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.All(result.Batch.Attempts, attempt => Assert.Null(attempt.Primitive));
        Assert.Equal(0u, Dword(result.Context108, 0x28));
        Assert.Throws<NotSupportedException>(() => result.Context108.Read(0x58, 4));
        Assert.Equal(0x3A3264C8u, Dword(result.Context108, 0x5C));
        Assert.Equal(NativeBattleGfPacketByteState.Known, result.Batch.Attempts[0].WriteMask[0]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, result.Batch.Attempts[0].WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, result.Batch.Attempts[0].WriteMask[8]);
    }

    [Fact]
    public void Near_rejection_preserves_unknown_NCLIP_OTZ_tint_and_current_far_color_registers()
    {
        NativeBattleEffect66Mesh108Step result = Draw(z: 1);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.NotEqual(0u, Dword(result.Context108, 0x34) & 0x60000u);
        Assert.Throws<NotSupportedException>(() => result.Context108.Read(0x28, 4));
        Assert.Throws<NotSupportedException>(() => result.Context108.Read(0x30, 4));
        Assert.Throws<NotSupportedException>(() => result.Context108.Read(0x58, 4));
        Assert.Equal(16u, result.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Equal(32u, result.Memory.PrimitiveProcess.ModelRendererRegisters.FarGreen);
        Assert.Equal(48u, result.Memory.PrimitiveProcess.ModelRendererRegisters.FarBlue);
    }

    [Fact]
    public void Capture_preserves_actual_lazy_G4_native_stores_tint_registers_and_depths()
    {
        NativeBattleEffect66Mesh108Step plain = Draw();
        NativeBattleEffect66Mesh108Step captured = Draw(pilotIdentity: new(66, 1, 0, 1, 0));
        Assert.Equal(plain.Batch.AllocationBytes, captured.Batch.AllocationBytes);
        for (int index = 0; index < plain.Batch.Attempts.Length; index++)
        {
            Assert.Equal(plain.Batch.Attempts[index].Data.ToArray(), captured.Batch.Attempts[index].Data.ToArray());
            Assert.Equal(plain.Batch.Attempts[index].WriteMask.ToArray(), captured.Batch.Attempts[index].WriteMask.ToArray());
        }
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(plain.Memory)), System.Text.Json.JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(captured.Memory)));
        NativeBattlePilotVisualGeometry visual = Assert.IsType<NativeBattlePilotVisualGeometry>(captured.Batch.Attempts[0].Primitive!.PilotGeometry);
        Assert.Equal(new System.Numerics.Vector3(-100, -100, 2000), visual.A);
        Assert.Equal(new System.Numerics.Vector3(100, 100, 2000), visual.D);
        Assert.True(visual.UseSourceDepths);
    }

    [Fact]
    public void Capture_does_not_read_an_invalid_fourth_index_after_native_first_triple_rejection()
    {
        NativeBattleEffect66Mesh108Step result = Draw(degenerate: true, invalidFourth: true, pilotIdentity: new(66, 1, 0, 1, 0));
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.All(result.Batch.Attempts, attempt => Assert.Null(attempt.Primitive));
    }

    private static NativeBattleEffect66Mesh108Step Draw(bool degenerate = false, int z = 2000, bool invalidFourth = false, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        byte[] mesh = new byte[1752];
        BinaryPrimitives.WriteInt32LittleEndian(mesh, 568);
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(4), 70);
        short[] xy = [-100, -100, 100, -100, -100, 100, 100, 100];
        for (int vertex = 0; vertex < 4; vertex++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(8 + vertex * 8), xy[vertex * 2]);
            BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(10 + vertex * 8), xy[vertex * 2 + 1]);
            BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(14 + vertex * 8), 0x1234);
        }
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(588), 48);
        for (int face = 0; face < 48; face++)
        {
            int start = 592 + face * 24;
            for (int corner = 0; corner < 4; corner++)
            {
                int colorOffset = corner == 0 ? 0 : 8 + corner * 4;
                BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(start + colorOffset), 0x383264C8u);
                BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(start + 4 + corner * 2), (ushort)(invalidFourth && corner == 3 ? 0xFFFF : face == 0 && !degenerate ? corner * 2 : 0));
            }
        }
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with
        {
            GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, z, 4096), true, true, null),
            ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(),
            ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() with { Zsf4 = 4096 },
            PrimitiveProcess = new(null, null, null, 0, null) { ModelRendererRegisters = new(null, null, null, null, null) }
        };
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 108).Write(0, BitConverter.GetBytes(0xDE90CCu)).Write(8, [1, 2, 3]).Write(0x18, BitConverter.GetBytes(12345)).Write(0x1C, BitConverter.GetBytes(0x33u)).Write(0x20, BitConverter.GetBytes(0x00FF8040u));
        return NativeBattleEffect66Mesh108.DrawLoaded(memory, new(0, 0xDE90CC, mesh.ToImmutableArray()), new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270, pilotIdentity: pilotIdentity);
    }

    private static uint Dword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
}
