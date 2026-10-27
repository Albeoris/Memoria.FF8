using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleActorWaveMeshTests
{
    private const uint Address = 0x1744D34;
    private static NativeBattleActionMatrix Matrix => NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 };
    private static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);
    private static NativeBattleSummonColors Constants => new(0, 0, 1f / 4096f, BitConverter.Int64BitsToDouble(0x4330000080000000L));

    [Fact]
    public void Producer_preserves_unwritten_bytes_and_does_not_read_absent_far_color_without_fade()
    {
        NativeBattleEffectArena retained = Unknown().Write(0x40, [1, 2, 3, 4]);
        NativeBattleEffectArena context = NativeBattleActorWaveMesh.PrepareContext(retained, Address, 0, 0, null);
        Assert.Equal(Address, Dword(context, 0));
        Assert.Equal(0x30u, Dword(context, 0x14));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, context.Read(0x40, 4));
        Assert.Throws<NotSupportedException>(() => context.Read(8, 4));
        Assert.Throws<NotSupportedException>(() => context.Read(12, 4));
        Assert.Throws<NotSupportedException>(() => context.Read(4, 4));
        Assert.Equal((ushort)256, BinaryPrimitives.ReadUInt16LittleEndian(context.Read(0x20, 2)));
        NativeBattleEffectArena faded = NativeBattleActorWaveMesh.PrepareContext(context, Address, 1, -20, 0xABCDEF);
        Assert.Equal(0xC0u, Dword(faded, 0x14));
        Assert.Equal(unchecked((uint)-20), Dword(faded, 12));
        Assert.Equal(0xABCDEFu, Dword(faded, 8));
        Assert.Throws<NotSupportedException>(() => NativeBattleActorWaveMesh.PrepareContext(context, Address, 0, 1, null));
    }

    [Fact]
    public void Accepted_original_FT4_contract_reuses_Esuna_packets_and_publishes_canonical_registers()
    {
        ImmutableArray<byte> mesh = Mesh();
        NativeBattleActorWaveMeshStep result = Emit(mesh);
        NativeBattleEsunaMeshContext context = new(0x30, null, null, 0, 0, 0, new(0, 0, 256, 256), new(0, 0, 256, 256), 17, 2);
        NativeBattleGfGeometryBatch expected = NativeBattleRestorationMeshPackets.EmitBatch88E240(mesh, Matrix, Projection, context, Constants);
        Assert.Equal(SnapshotJson(expected), SnapshotJson(result.Batch));
        Assert.Equal(40, result.Batch.AllocationBytes);
        Assert.Equal(Address + 8, Dword(result.Context104, 4));
        Assert.Equal(Address + (uint)mesh.Length, Dword(result.Context104, 0x2C));
        Assert.Equal(500u, Dword(result.Context104, 0x38));
        Assert.Equal(500u, result.Registers.Geometry.Otz);
        Assert.Equal(500u, result.Registers.Geometry.Mac0);
        Assert.Equal(new uint?[] { 2000, 2000, 2000, 2000 }, result.Registers.Geometry.Sz);
        Assert.Equal((short)0x1234, BinaryPrimitives.ReadInt16LittleEndian(result.Registers.Colors.NormalInput!.Value.AsSpan().Slice(6)));
        Assert.Null(result.FarControls.FarRed);
        Assert.Null(result.FarControls.FarGreen);
        Assert.Null(result.FarControls.FarBlue);
        Assert.Null(result.Registers.Colors.Material);
    }

    [Fact]
    public void Backface_rejection_keeps_header_and_UVs_and_clips_without_loading_fourth_vertex()
    {
        ImmutableArray<byte> mesh = Mesh(reverse: true);
        NativeBattleEffectArena context = NativeBattleActorWaveMesh.PrepareContext(Unknown(), Address, 1, 0, null);
        NativeBattleActorWaveMeshStep result = Emit(mesh, context);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Null(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.True(unchecked((int)Dword(result.Context104, 0x30)) < 0);
        Assert.Throws<NotSupportedException>(() => result.Context104.Read(0x38, 4));
        Assert.Null(result.Registers.Geometry.Otz);
        Assert.Equal((short)-400, BinaryPrimitives.ReadInt16LittleEndian(result.Registers.Colors.NormalInput!.Value.AsSpan()));
        Assert.Equal((short)-400, BinaryPrimitives.ReadInt16LittleEndian(result.Registers.Colors.NormalInput.Value.AsSpan().Slice(2)));
    }

    [Fact]
    public void Triple_projection_rejection_preserves_clip_and_OTZ_scratch_and_never_loads_fourth()
    {
        NativeBattleEffectArena context = NativeBattleActorWaveMesh.PrepareContext(Unknown(), Address, 0, 0, null);
        NativeBattleGeometryRegisterStep registers = new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown());
        NativeBattleActorWaveMeshStep result = NativeBattleActorWaveMesh.EmitBatch(Mesh(), Address, Matrix with { Z = -1 }, Projection, context, registers, new(1, 2, 3, 4, 5), 0, 0, null, Constants);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.NotEqual(0u, Dword(result.Context104, 0x3C) & 0x60000);
        Assert.Throws<NotSupportedException>(() => result.Context104.Read(0x30, 4));
        Assert.Throws<NotSupportedException>(() => result.Context104.Read(0x38, 4));
        NativeBattleGfGeometryAttempt attempted = Assert.Single(result.Batch.Attempts);
        Assert.Null(attempted.Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempted.WriteMask[3]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempted.WriteMask[8]);
        Assert.Null(result.Registers.Geometry.Otz);
    }

    [Fact]
    public void Screen_rejection_still_publishes_average_registers_but_preserves_context_OTZ()
    {
        NativeBattleActorWaveMeshStep result = Emit(Mesh(), projection: new(null, 100, -100, -100));
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(500u, result.Registers.Geometry.Otz);
        Assert.Equal(0u, result.Registers.Colors.Flags);
        Assert.Throws<NotSupportedException>(() => result.Context104.Read(0x38, 4));
        Assert.NotEqual(0u, Dword(result.Context104, 0x30));
    }

    [Fact]
    public void DPCS_preserves_native_FIFO_order_and_uses_selected_far_control_lanes()
    {
        NativeBattleEffectArena context = NativeBattleActorWaveMesh.PrepareContext(Unknown(), Address, 0, 2048, 0xC08040);
        NativeBattleActorWaveMeshStep result = Emit(Mesh(), context);
        Assert.Equal(0x400u, result.FarControls.FarRed);
        Assert.Equal(0x800u, result.FarControls.FarGreen);
        Assert.Equal(0xC00u, result.FarControls.FarBlue);
        uint expected = Constants.Interpolate(0x2C332211, 2048, 0xC08040);
        Assert.Equal(expected, Assert.Single(result.Batch.Attempts).Primitive!.Words[1]);
        Assert.Equal(new uint?[] { 0x12345678, 0xABCDEF01, expected }, result.Registers.Colors.Rgb);
        Assert.Equal(0x2C332211u, result.Registers.Colors.Material);
        Assert.Equal(2048u, result.Registers.Colors.Ir0);
        Assert.Equal(500u, result.Registers.Geometry.Mac0);
        Assert.Equal(500u, result.Registers.Geometry.Otz);
    }

    [Fact]
    public void Retained_ZSF4_controls_actual_OTZ_and_bucket_without_changing_packet_math()
    {
        NativeBattleActorWaveMeshStep result = Emit(Mesh(), zsf4: 0x80);
        Assert.Equal(250u, result.Registers.Geometry.Otz);
        Assert.Equal(250u, Dword(result.Context104, 0x38));
        Assert.Equal(17 + (250 >> 2), Assert.Single(result.Batch.Attempts).Primitive!.Bucket);
    }

    [Fact]
    public void Empty_original_groups_still_load_matrix_and_unknown_far_but_do_not_read_ZSF4()
    {
        NativeBattleActorWaveMeshStep result = Emit(Mesh(empty: true), zsf4: null);
        Assert.Empty(result.Batch.Attempts);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(new int?[] { 0, 0, 2000 }, result.Registers.Geometry.Translation);
        Assert.Equal(new uint?[] { null, null, null, null }, result.Registers.Geometry.Sz);
        Assert.Equal(Address + 72, Dword(result.Context104, 0x2C));
        Assert.Throws<NotSupportedException>(() => result.Context104.Read(0x30, 4));
        NativeBattleActorWaveMeshStep independent = RoundTrip(result);
        Assert.Equal(SnapshotJson(result), SnapshotJson(independent));
    }

    private static NativeBattleActorWaveMeshStep Emit(ImmutableArray<byte> mesh, NativeBattleEffectArena? context = null, NativeBattleCameraProjection? projection = null, short? zsf4 = 0x100)
    {
        NativeBattleScriptedColorRegisters colors = NativeBattleScriptedColorRegisters.Unknown() with { Rgb = [null, 0x12345678, 0xABCDEF01] };
        return NativeBattleActorWaveMesh.EmitBatch(mesh, Address, Matrix, projection ?? Projection, context ?? NativeBattleActorWaveMesh.PrepareContext(Unknown(), Address, 0, 0, null), new(NativeBattleGeometryRegisters.Unknown(), colors), new(1, 2, 3, 4, 5), 0, 0, zsf4, Constants, 0x4321);
    }

    private static NativeBattleEffectArena Unknown() => NativeBattleEffectArena.Clear().MarkUnknown(0, 104);
    private static uint Dword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
    private static ImmutableArray<byte> Mesh(bool reverse = false, bool empty = false)
    {
        byte[] mesh = new byte[empty ? 72 : 96];
        BinaryPrimitives.WriteUInt32LittleEndian(mesh, 40);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(4), 4);
        short[] positions = [-400, -400, 0, 0x1234, 400, -400, 0, 0x1234, -400, 400, 0, 0x1234, 400, 400, 0, 0x1234];
        for (int index = 0; index < positions.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(8 + 2 * index), positions[index]);
        }
        if (!empty)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(52), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(56), 0x2C332211);
            ushort[] offsets = reverse ? [0, 4, 2, 6] : [0, 2, 4, 6];
            for (int index = 0; index < 4; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(60 + index * 2), offsets[index]);
            }
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(68), 0x12340302);
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(72), 0x56780605);
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(76), 0x0A090807);
        }
        return mesh.ToImmutableArray();
    }
    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
    private static T RoundTrip<T>(T value) => NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<T>(SnapshotJson(value))!);

}
