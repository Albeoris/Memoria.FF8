using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScrollingMeshContext124Tests
{
    private const uint Address = 0x10000000;

    [Theory]
    [InlineData(2, 0x33u)]
    [InlineData(3, 0x33u)]
    [InlineData(7, 0x33u)]
    [InlineData(2, 0xF3u)]
    [InlineData(3, 0xF3u)]
    [InlineData(7, 0xF3u)]
    public void Group13_capture_retains_compound_packet_windows_color_registers_and_zero_depths(int group, uint flags)
    {
        NativeBattleStaticMeshResource mesh = Mesh(group);
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
        NativeBattlePilotVisualIdentity identity = new(151, 0x2000, 2, 7, 1) { ResourceGeneration = Address };
        NativeBattleScrollingMeshContext124Step native = DrawCaptured(null, 2000);
        NativeBattleScrollingMeshContext124Step captured = DrawCaptured(identity, 2000);
        Assert.Equal(NativeBattleTestSnapshots.Json(native.Memory), NativeBattleTestSnapshots.Json(captured.Memory));
        Assert.Equal(NativeBattleTestSnapshots.Json(native.Context124), NativeBattleTestSnapshots.Json(captured.Context124));
        Assert.Equal(NativeBattleTestSnapshots.Json(native.Batch), NativeBattleTestSnapshots.Json(captured.Batch));
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(captured.Batch.Attempts).Primitive!.PilotGeometry);
        Assert.Equal(identity with { Primitive = unchecked(397 + Face(group)) }, geometry.Identity);
        Assert.Equal(new System.Numerics.Vector3(-400, -400, 2000), geometry.A);
        Assert.Equal(new System.Numerics.Vector3(400, -400, 2000), geometry.B);
        Assert.Equal(new System.Numerics.Vector3(-400, 400, 2000), geometry.C);
        if (group != 2)
        {
            Assert.Equal(new System.Numerics.Vector3(400, 400, 2000), geometry.D);
        }
        Assert.True(geometry.UseSourceDepths);
        Assert.Equal(System.Numerics.Vector4.Zero, geometry.SourceDepths);
        NativeBattleScrollingMeshContext124Step DrawCaptured(NativeBattlePilotVisualIdentity? capture, int depth) => NativeBattleScrollingMeshContext124.DrawLoaded(Memory(depth), mesh, projection, Context(flags, 14), NativeBattleGteColorInterpolation.CodeConstants45F270, pilotIdentity: capture);
    }

    [Fact]
    public void Group13_capture_does_not_read_bad_fourth_after_native_near_rejection()
    {
        NativeBattleStaticMeshResource source = Mesh(7);
        byte[] bytes = source.Bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(Face(7) + 10), ushort.MaxValue);
        source = source with { Bytes = bytes.ToImmutableArray() };
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
        NativeBattleScrollingMeshContext124Step native = DrawCaptured(null);
        NativeBattleScrollingMeshContext124Step captured = DrawCaptured(new(151, 0x2000, 2, 7, 0));
        Assert.Null(Assert.Single(captured.Batch.Attempts).Primitive);
        Assert.Equal(NativeBattleTestSnapshots.Json(native.Memory), NativeBattleTestSnapshots.Json(captured.Memory));
        Assert.Equal(NativeBattleTestSnapshots.Json(native.Context124), NativeBattleTestSnapshots.Json(captured.Context124));
        Assert.Equal(NativeBattleTestSnapshots.Json(native.Batch), NativeBattleTestSnapshots.Json(captured.Batch));
        NativeBattleScrollingMeshContext124Step DrawCaptured(NativeBattlePilotVisualIdentity? capture) => NativeBattleScrollingMeshContext124.DrawLoaded(Memory(0), source, projection, Context(0xF3, 14), NativeBattleGteColorInterpolation.CodeConstants45F270, pilotIdentity: capture);
    }

    [Theory]
    [InlineData(2, 40)]
    [InlineData(3, 48)]
    [InlineData(7, 60)]
    public void Selected_groups_preserve_compound_E2_zero_sidecars_and_full_UV_workspace(int group, int length)
    {
        NativeBattleStaticMeshResource mesh = Mesh(group);
        NativeBattleScrollingMeshContext124Step actual = Draw(mesh, Context(0x33, 14));
        NativeBattleGfGeometryAttempt attempt = Assert.Single(actual.Batch.Attempts);
        Assert.NotNull(attempt.Primitive);
        Assert.Equal(length, actual.Batch.AllocationBytes);
        Assert.Equal(0xE2000000u, attempt.Primitive.Words[^1]);
        Assert.Equal(0xE2000000u, attempt.Primitive.Words[1] & 0xFF000000);
        Assert.Equal(new int[] { 0, 0, 0, 0 }, attempt.Primitive.VertexPayload4);
        Assert.Equal(Address + (uint)mesh.Bytes.Length, Dword(actual.Context124, 0x24));
        Assert.Equal(14u, Dword(actual.Context124, 0x70));
        Assert.Equal(136u, Dword(actual.Context124, 0x5C));
        Assert.Equal((byte)136, attempt.Data[17]);
        Assert.Equal(0u, actual.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Throws<NotSupportedException>(() => actual.Context124.Read(11, 1));
        Assert.Throws<NotSupportedException>(() => actual.Context124.Read(0x2C, 4));
        if (group == 2)
        {
            Assert.Throws<NotSupportedException>(() => actual.Context124.Read(0x68, 4));
        }
    }

    [Fact]
    public void Near_rejection_keeps_prefix_and_skips_cue_clip_UV_and_depth_stores()
    {
        NativeBattleScrollingMeshContext124Step actual = Draw(Mesh(7), Context(0xF3, 14).MarkUnknown(12, 4), z: 0);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(actual.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, actual.Batch.AllocationBytes);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[8]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[16]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[12]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[4]);
        Assert.Throws<NotSupportedException>(() => actual.Context124.Read(0x28, 4));
        Assert.Throws<NotSupportedException>(() => actual.Context124.Read(0x30, 4));
        Assert.Throws<NotSupportedException>(() => actual.Context124.Read(0x5C, 4));
        Assert.NotEqual(0u, Dword(actual.Context124, 0x34) & 0x60000);
        Assert.Null(actual.Memory.ScriptedColorRegisters!.Material);
    }

    [Fact]
    public void Zero_area_rejection_keeps_scroll_scratch_and_clip_but_does_not_publish_OTZ()
    {
        NativeBattleStaticMeshResource mesh = Mesh(3);
        byte[] bytes = mesh.Bytes.ToArray();
        Array.Clear(bytes, Face(3) + 4, 8);
        NativeBattleScrollingMeshContext124Step actual = Draw(mesh with { Bytes = bytes.ToImmutableArray() }, Context(0x33, -14));
        Assert.Null(Assert.Single(actual.Batch.Attempts).Primitive);
        Assert.Equal(0u, Dword(actual.Context124, 0x28));
        Assert.Equal(unchecked((uint)-14), Dword(actual.Context124, 0x70));
        Assert.Equal(236u, Dword(actual.Context124, 0x5C));
        Assert.Throws<NotSupportedException>(() => actual.Context124.Read(0x30, 4));
    }

    [Fact]
    public void Screen_rejection_keeps_AVSZ_effects_without_accepted_depth_context()
    {
        NativeBattleScrollingMeshContext124Step actual = NativeBattleScrollingMeshContext124.DrawLoaded(Memory(2000), Mesh(3), new(null, 100, 500, 108), Context(0x33, 0), NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Null(Assert.Single(actual.Batch.Attempts).Primitive);
        Assert.Equal(500u, actual.Memory.GeometryRegisters!.Otz);
        Assert.Throws<NotSupportedException>(() => actual.Context124.Read(0x30, 4));
        Assert.Throws<NotSupportedException>(() => actual.Context124.Read(0x5C, 4));
    }

    [Fact]
    public void Fourth_near_FLAG_does_not_reject_and_shading_publishes_material_IR0_and_FIFO()
    {
        NativeBattleStaticMeshResource mesh = Mesh(7);
        byte[] bytes = mesh.Bytes.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + 3 * 8 + 4), -2000);
        NativeBattleScrollingMeshContext124Step actual = Draw(mesh with { Bytes = bytes.ToImmutableArray() }, Context(0xF3, 0));
        Assert.NotNull(Assert.Single(actual.Batch.Attempts).Primitive);
        Assert.Equal(0u, Dword(actual.Context124, 0x34) & 0x60000);
        Assert.Equal(0u, actual.Memory.GeometryRegisters!.Sz[3]);
        Assert.Equal(375u, actual.Memory.GeometryRegisters.Otz);
        Assert.Equal(2048u, actual.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(0x3E112233u, actual.Memory.ScriptedColorRegisters.Material);
        Assert.NotNull(actual.Memory.ScriptedColorRegisters.Rgb[2]);
    }

    [Fact]
    public void Restored_registers_and_context_continue_identically_and_unknown_unused_fields_stay_unknown()
    {
        NativeBattleScrollingMeshContext124Step first = Draw(Mesh(2), Context(0x33, 0));
        Assert.Throws<NotSupportedException>(() => first.Context124.Read(0x5C, 16));
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(first.Memory))!;
        NativeBattleEffectArena context = first.Context124.Write(0x14, BitConverter.GetBytes(28));
        NativeBattleScrollingMeshContext124Step expected = Draw(Mesh(2), context, first.Memory);
        NativeBattleScrollingMeshContext124Step resumed = Draw(Mesh(2), context, restored);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(resumed));
        Assert.Throws<NotSupportedException>(() => Draw(Mesh(2), context.Write(8, [1, 0, 0])));
    }

    private static NativeBattleScrollingMeshContext124Step Draw(NativeBattleStaticMeshResource mesh, NativeBattleEffectArena context, NativeBattleEffectMemory? memory = null, int z = 2000) => NativeBattleScrollingMeshContext124.DrawLoaded(memory ?? Memory(z), mesh, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270);
    private static NativeBattleEffectMemory Memory(int z) => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = z }, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleEffectArena Context(uint flags, int scroll) => NativeBattleEffectArena.Clear().MarkUnknown(0, 124).Write(0, BitConverter.GetBytes(Address)).Write(8, [0, 0, 0]).Write(12, BitConverter.GetBytes(2048)).Write(0x10, new byte[4]).Write(0x14, BitConverter.GetBytes(scroll)).Write(0x18, new byte[4]).Write(0x1C, BitConverter.GetBytes((short)64)).Write(0x1E, BitConverter.GetBytes((short)128)).Write(0x20, BitConverter.GetBytes(flags));
    private static uint Dword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
    private static int Face(int group) => 44 + group * 4;

    private static NativeBattleStaticMeshResource Mesh(int group)
    {
        int stride = group == 2 ? 20 : group == 3 ? 24 : 36;
        byte[] bytes = new byte[72 + stride];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        short[] points = [-400, -400, 0, 11, 400, -400, 0, 22, -400, 400, 0, 33, 400, 400, 0, 44];
        for (int index = 0; index < points.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 2), points[index]);
        }
        int face = Face(group);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face - 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), (group == 7 ? 0x3C000000u : group == 3 ? 0x2C000000u : 0x24000000u) | 0x112233);
        for (int corner = 0; corner < (group == 2 ? 3 : 4); corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + corner * 2), (ushort)(corner * 2));
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x1111FA01);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x2222FA02);
        if (group == 2)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 10), 0xFA03);
        }
        else
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 20), 0xFA04FA03);
        }
        if (group == 7)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 24), 0x3C224466);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 28), 0x3C6688AA);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 32), 0x3CAACCEE);
        }
        return new(0, Address, bytes.ToImmutableArray());
    }
}
