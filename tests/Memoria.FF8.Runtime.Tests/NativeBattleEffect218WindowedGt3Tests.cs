using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect218WindowedGt3Tests
{
    [Theory]
    [InlineData(0x33u, 2000)]
    [InlineData(0xF3u, 2000)]
    [InlineData(0xF3u, -2000)]
    public void Effect326_optional_capture_preserves_default_window_UV_register_masks_and_zero_payload(uint flags, int z)
    {
        var native = Draw(flags, z);
        string baseline = NativeBattleTestSnapshots.Json(native);
        var captured = Draw(flags, z, new(326, 1, 2, 3, 4));
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        var primitive = Assert.Single(captured.Batch.Attempts).Primitive;
        if (z < 0)
        {
            Assert.Null(primitive);
            return;
        }
        var geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive!.PilotGeometry);
        Assert.Equal((ushort)326, geometry.Identity.Effect);
        Assert.Equal(new System.Numerics.Vector3(-100, -100, 2000), geometry.A);
        Assert.Equal(3, geometry.CornerCount);
        Assert.True(geometry.UseSourceDepths);
        Assert.Equal(System.Numerics.Vector4.Zero, geometry.SourceDepths);
        Assert.All(native.Batch.Attempts, attempt => Assert.Null(attempt.Primitive?.PilotGeometry));
    }


    [Theory]
    [InlineData(0x33u)]
    [InlineData(0xF3u)]
    public void Shaft_GT3_uses_its_actual_28_byte_source_and_48_byte_packet_with_three_color_stores(uint flags)
    {
        NativeBattleWindowedMesh144Step actual = Draw(flags);
        NativeBattleGfGeometryAttempt packet = Assert.Single(actual.Batch.Attempts);
        Assert.NotNull(packet.Primitive);
        Assert.Equal(48, actual.Batch.AllocationBytes);
        Assert.Equal(0x0B000000u, U(packet.Data, 0));
        Assert.Equal(0xE2000000u, U(packet.Data, 44));
        Assert.Equal(0u, U(packet.Data, 40) >> 16);
        Assert.Equal(0x36FF8041u, U(actual.Context144, 0x60));
        Assert.Equal(0x001F2018u, U(actual.Context144, 0x64));
        Assert.Equal(0x007F5030u, U(actual.Context144, 0x68));
        Assert.Equal(0xA1B2C3D4u, U(actual.Context144, 0x6C));
        Assert.Equal(0x11223344u, U(actual.Context144, 0x7C));
        Assert.Equal(0xE1A0F0u + (uint)Mesh().Bytes.Length, U(actual.Context144, 0x28));
        Assert.Equal(375u, U(actual.Context144, 0x34));
        Assert.Equal(17 + (375 >> 2), packet.Primitive!.Bucket);
        if (flags == 0x33)
        {
            Assert.Equal(U(actual.Context144, 0x60), U(packet.Data, 8));
            Assert.Equal(U(actual.Context144, 0x64), U(packet.Data, 20));
            Assert.Equal(U(actual.Context144, 0x68), U(packet.Data, 32));
        }
        else
        {
            Assert.Equal(actual.Memory.ScriptedColorRegisters!.Rgb[0], U(packet.Data, 20));
            Assert.Equal(actual.Memory.ScriptedColorRegisters.Rgb[1], U(packet.Data, 32));
            Assert.Equal(actual.Memory.ScriptedColorRegisters.Rgb[2], U(packet.Data, 8));
            Assert.Equal(U(actual.Context144, 0x60), actual.Memory.ScriptedColorRegisters.Material);
            Assert.Equal(2048u, actual.Memory.ScriptedColorRegisters.Ir0);
        }
    }

    [Fact]
    public void Rejected_GT3_keeps_three_prefix_colors_and_retains_unreached_depth_and_UV_scratch()
    {
        NativeBattleWindowedMesh144Step actual = Draw(0xF3, -2000);
        NativeBattleGfGeometryAttempt packet = Assert.Single(actual.Batch.Attempts);
        Assert.Null(packet.Primitive);
        Assert.Equal(0, actual.Batch.AllocationBytes);
        Assert.Equal(48, packet.Data.Length);
        Assert.Equal(0x36808080u, U(actual.Context144, 0x60));
        Assert.Equal(0x00102030u, U(actual.Context144, 0x64));
        Assert.Equal(0x00405060u, U(actual.Context144, 0x68));
        Assert.Equal(0xA1B2C3D4u, U(actual.Context144, 0x6C));
        Assert.Equal(0x55667788u, U(actual.Context144, 0x34));
        Assert.Equal(0xDEADBEEFu, U(actual.Context144, 0x70));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[8]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, packet.WriteMask[16]);
        Assert.NotEqual(0u, U(actual.Context144, 0x38) & 0x60000);
    }

    [Fact]
    public void Effect326_empty_source_does_not_require_unreached_unknown_matrix_with_capture_enabled()
    {
        NativeBattleWindowedMesh144Step Run(bool capture)
        {
            byte[] empty = new byte[64];
            Mesh().Bytes.AsSpan().Slice(0, 32).CopyTo(empty);
            var mesh = Mesh() with { Bytes = empty.ToImmutableArray() };
            var memory = Memory(2000) with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown() };
            return NativeBattleWindowedMesh144.DrawLoaded(memory, mesh, Projection, Context(0x33), NativeBattleGteColorInterpolation.CodeConstants45F270, groupMask: 1 << 6, pilotIdentity: capture ? new(326, 1, 2, 3, 4) : null);
        }
        var native = Run(false);
        string baseline = NativeBattleTestSnapshots.Json(native);
        var captured = Run(true);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        Assert.Empty(captured.Batch.Attempts);
        Assert.Equal(0, captured.Batch.AllocationBytes);
    }

    [Fact]
    public void Existing_default_group_admission_remains_strict_for_GT3()
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleWindowedMesh144.DrawLoaded(Memory(2000), Mesh(), Projection, Context(0x33), NativeBattleGteColorInterpolation.CodeConstants45F270));
    }

    private static NativeBattleWindowedMesh144Step Draw(uint flags, int z = 2000, NativeBattlePilotVisualIdentity? pilotIdentity = null) => NativeBattleWindowedMesh144.DrawLoaded(Memory(z), Mesh(), pilotIdentity.HasValue ? Projection with { View = NativeBattleCuragaMatrices.Identity8DD770() } : Projection, Context(flags), NativeBattleGteColorInterpolation.CodeConstants45F270, groupMask: 1 << 6, pilotIdentity: pilotIdentity);
    private static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static uint U(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));

    private static NativeBattleEffectMemory Memory(int z)
    {
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = z };
        return NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), matrix, true, true, 0), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Material = 0xCAFEBABE }, ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() with { Zsf3 = 0x100 } };
    }

    private static NativeBattleEffectArena Context(uint flags) => NativeBattleEffectArena.Clear().MarkUnknown(0, 144).Write(0, BitConverter.GetBytes(0xE1A0F0u)).Write(8, new byte[3]).Write(12, BitConverter.GetBytes(2048)).Write(0x10, BitConverter.GetBytes(10)).Write(0x14, BitConverter.GetBytes(-20)).Write(0x18, BitConverter.GetBytes((short)0)).Write(0x1A, BitConverter.GetBytes((short)128)).Write(0x1C, BitConverter.GetBytes((short)64)).Write(0x1E, BitConverter.GetBytes((short)128)).Write(0x20, BitConverter.GetBytes(flags)).Write(0x24, BitConverter.GetBytes(0x00FF8041u)).Write(0x34, BitConverter.GetBytes(0x55667788u)).Write(0x6C, BitConverter.GetBytes(0xA1B2C3D4u)).Write(0x70, BitConverter.GetBytes(0xDEADBEEFu)).Write(0x7C, BitConverter.GetBytes(0x11223344u));

    private static NativeBattleStaticMeshResource Mesh()
    {
        byte[] bytes = new byte[92];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 32);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 3);
        short[] vertices = [-100, -100, 0, 0, 100, -100, 0, 0, -100, 100, 0, 0];
        for (int index = 0; index < vertices.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 2), vertices[index]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(56), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(60), 0x34808080);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(64), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(66), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(68), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(70), 0x2003);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(72), 0x12340102);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(76), 0x56780304);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(80), 0x00102030);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(84), 0x00405060);
        return new(0, 0xE1A0F0, bytes.ToImmutableArray());
    }
}
