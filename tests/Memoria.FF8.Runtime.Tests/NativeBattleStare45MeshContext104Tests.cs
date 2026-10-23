using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStare45MeshContext104Tests
{
    [Theory]
    [InlineData(3, 40, 110)]
    [InlineData(7, 52, 110)]
    public void Selected_groups_use_native104_fields_current_depth_and_physical_SZ(int group, int bytes, int bucket)
    {
        var actual = Draw(group);
        var attempt = actual.Batch.Attempts[2];
        Assert.Equal(3, actual.Batch.Attempts.Length);
        Assert.Null(actual.Batch.Attempts[0].Primitive);
        var mode = actual.Batch.Attempts[1];
        Assert.Equal(new uint[] { 0x02000000, 0xE1000020, 0 }, mode.Primitive!.Words);
        Assert.Equal(bytes, mode.ByteOffsetRelative);
        Assert.All(mode.WriteMask.Take(3), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        Assert.All(mode.WriteMask.Skip(3), value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
        Assert.All(attempt.WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        Assert.Equal(bytes + 12, actual.Batch.AllocationBytes);
        Assert.Equal(bucket, attempt.Primitive!.Bucket);
        Assert.Equal(group == 4 ? 371u : 372u, U(actual.Context104, 0x38));
        Assert.Equal(group == 4 ? new[] { 2000, 2000, 2000, 0 } : new[] { 2000, 2000, 2000, 2000 }, attempt.Primitive.VertexPayload4);
        Assert.Equal(0x2655428u, U(actual.Context104, 4));
        Assert.Throws<NotSupportedException>(() => actual.Context104.Read(0x34, 4));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    public void Rejected_first_three_do_not_consume_cue_or_execute_NCLIP_AVSZ_shading(int group)
    {
        var actual = Draw(group, z: 1, context: Context(0x20F0).MarkUnknown(8, 8));
        Assert.Null(actual.Batch.Attempts.Last().Primitive);
        Assert.Equal(0, actual.Batch.AllocationBytes);
        Assert.Throws<NotSupportedException>(() => actual.Context104.Read(0x30, 4));
        Assert.Null(actual.Memory.GeometryRegisters!.Otz);
        Assert.NotEqual(0u, U(actual.Context104, 0x3C) & 0x60000);
    }

    [Fact]
    public void Screen_rejection_retains_old_depth_and_never_executes_AVSZ()
    {
        var actual = Draw(7, outside: true);
        Assert.Null(actual.Batch.Attempts.Last().Primitive);
        Assert.Null(actual.Memory.GeometryRegisters!.Otz);
        Assert.Throws<NotSupportedException>(() => actual.Context104.Read(0x38, 4));
    }

    [Fact]
    public void Fourth_near_does_not_override_first_flags_or_reject_and_AVSZ_resets_FLAG()
    {
        var actual = Draw(7, fourthZ: -1999);
        Assert.NotNull(actual.Batch.Attempts.Last().Primitive);
        Assert.Equal(new[] { 2000, 2000, 2000, 1 }, actual.Batch.Attempts.Last().Primitive!.VertexPayload4);
        Assert.Equal(247u, U(actual.Context104, 0x38));
        Assert.Equal(0u, U(actual.Context104, 0x3C) & 0x60000);
        Assert.Equal(0u, actual.Memory.ScriptedColorRegisters!.Flags);
    }

    [Fact]
    public void Gouraud_shading_publishes_current_FIFO_and_FAR_and_JSON_preserves_next_draw()
    {
        var actual = Draw(7, context: Context(0x20F0));
        Assert.Equal(2048u, actual.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(160u, actual.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        var restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(actual.Memory))!;
        Assert.Equal(JsonSerializer.Serialize(Draw(7, memory: actual.Memory)), JsonSerializer.Serialize(Draw(7, memory: restored)));
    }

    [Fact]
    public void Only_actual_groups_and_zero_UV_are_admitted_and_depth_bias_clamps_zero()
    {
        var actual = Draw(3, context: Context(0x2030).Write(16, BitConverter.GetBytes(-1000)));
        Assert.Equal(17, actual.Batch.Attempts.Last().Primitive!.Bucket);
        Assert.Equal(0u, U(actual.Context104, 0x38));
        Assert.Throws<NotSupportedException>(() => Draw(3, context: Context(0x2030).Write(0x18, BitConverter.GetBytes((ushort)1))));
        Assert.Throws<NotSupportedException>(() => Draw(5));
    }

    private static NativeBattleStare45MeshContext104Step Draw(int group, int z = 2000, short fourthZ = 0, bool outside = false, NativeBattleEffectMemory? memory = null, NativeBattleEffectArena? context = null)
    {
        NativeBattleVector[] points = outside ? [new(-10000, -400, 0), new(-9000, -400, 0), new(-10000, 400, 0), new(-9000, 400, fourthZ)] : [new(-400, -400, 0), new(400, -400, 0), new(-400, 400, 0), new(400, 400, fourthZ)];
        return NativeBattleStare45MeshContext104.DrawLoaded(memory ?? Memory(z), Mesh(group), new(null, z < 100 ? (ushort)2 : (ushort)100, 160, 108), context ?? Context(0x2030), NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2, 0x2655428, offset => (points[offset / 8], null));
    }
    internal static NativeBattleEffectMemory Memory(int z) => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = z }, true, true, 123), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleEffectArena Context(uint flags) => NativeBattleEffectArena.Clear().MarkUnknown(0, 104).Write(0, BitConverter.GetBytes(0x17C4DB8u)).Write(4, BitConverter.GetBytes(0x2655428u)).Write(8, new byte[] { 10, 20, 30 }).Write(12, BitConverter.GetBytes(2048)).Write(16, BitConverter.GetBytes(-128)).Write(20, BitConverter.GetBytes(flags)).Write(24, new byte[4]);
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleStaticMeshResource Mesh(int group)
    {
        int length = group switch { 3 or 5 => 24, 4 => 20, _ => 36 };
        byte[] bytes = new byte[76 + length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 44);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 1);
        int face = 48 + group * 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face - 4), 1);
        uint command = group switch { 3 => 0x2E112233, 4 => 0x32112233, 5 => 0x3A112233, _ => 0x3E112233 };
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), command);
        for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + corner * 2), (ushort)(corner * 2));
        }
        if (group is 3 or 7)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x76541020);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x32103040);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 20), 0x70805060);
        }
        int color = group == 7 ? 24 : 12;
        if (group >= 4)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + color), 0x3E224466);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + color + 4), 0x3E336699);
            if ((group & 1) != 0)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + color + 8), 0x3E4488CC);
            }
        }
        return new(0, 0x17C4DB8, bytes.ToImmutableArray());
    }
}
