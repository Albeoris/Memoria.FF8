using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleOilShot136ActMeshTests
{
    [Theory]
    [InlineData(3, 48, 3)]
    [InlineData(5, 52, 4)]
    public void Native_custom_packets_keep_physical_and_logical_environment_order_after_restore(int group, int length, int attempts)
    {
        NativeBattleEffectMemory memory = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(Memory()))!;
        NativeBattleDefaultActMeshStep result = Draw(group, memory, Context());
        Assert.Equal(length, result.Batch.AllocationBytes);
        Assert.Equal(attempts, result.Batch.Attempts.Length);
        Assert.Equal(group == 3 ? new[] { 0, 40, 0 } : new[] { 0, 36, 0, 44 }, result.Batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        NativeBattleGeometryPrimitive[] primitives = result.Batch.Attempts.Where(attempt => attempt.Primitive is not null).Select(attempt => attempt.Primitive!).ToArray();
        Assert.Equal(0xE1000220u, primitives[0].Words[1]);
        Assert.Equal(group == 3 ? 10 : 9, primitives[1].Words.Length);
        if (group == 5)
        {
            Assert.Equal(0xE1000240u, primitives[2].Words[1]);
        }
        Assert.All(result.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value.Skip(6), known => Assert.False(known));
        Assert.Throws<NotSupportedException>(() => result.Context88.Read(0x24, 4));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void Near_rejection_retains_exact_partial_prefix_without_reading_fourth_vertex_or_color_policy(int group)
    {
        List<int> reads = [];
        NativeBattleDefaultActMeshStep result = Draw(group, Memory(0), Context().MarkUnknown(8, 8), reads);
        Assert.Equal(new[] { 0, 8, 16 }, reads);
        Assert.Equal(0, result.Batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
        Assert.Equal(group == 3 ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[12]);
        Assert.Throws<NotSupportedException>(() => result.Context88.Read(0x2C, 4));
    }

    [Fact]
    public void G4_negative_bias_rejects_after_remaining_RGB_and_biased_OTZ_stores()
    {
        NativeBattleDefaultActMeshStep result = Draw(5, Memory(), Context(-10000));
        Assert.Equal(0, result.Batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.All(attempt.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        Assert.Equal(0xA1224466u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(12)));
        Assert.True(BinaryPrimitives.ReadInt32LittleEndian(result.Context88.Read(0x2C, 4)) < 0);
    }

    [Fact]
    public void FT4_nonzero_context_C_is_color_factor_and_does_not_bias_ordering_depth()
    {
        NativeBattleDefaultActMeshStep raw = Draw(3, Memory(), Context());
        NativeBattleDefaultActMeshStep shaded = Draw(3, Memory(), Context(4096).Write(8, new byte[] { 10, 20, 30 }));
        Assert.Equal(raw.Memory.GeometryRegisters!.Otz, shaded.Memory.GeometryRegisters!.Otz);
        Assert.Equal(raw.Context88.Read(0x2C, 4), shaded.Context88.Read(0x2C, 4));
        NativeBattleGeometryPrimitive polygon = Assert.Single(shaded.Batch.Attempts, attempt => attempt.Primitive?.Words.Length == 10).Primitive!;
        Assert.Equal(0x2E1E140Au, polygon.Words[1]);
        Assert.Equal(160u, shaded.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Equal(4096u, shaded.Memory.ScriptedColorRegisters!.Ir0);
    }

    private const uint Address = 0x1234000;
    private static NativeBattleEffectMemory Memory(int z = 2000) => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = z }, true, true, null), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleEffectArena Context(int factor = 0) => NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(Address + 16)).Write(4, BitConverter.GetBytes(0x12345678u)).Write(12, BitConverter.GetBytes(factor));
    private static NativeBattleDefaultActMeshStep Draw(int group, NativeBattleEffectMemory memory, NativeBattleEffectArena context, List<int>? reads = null)
    {
        NativeBattleVector[] vertices = [new(-400, -400, 0), new(400, -400, 0), new(-400, 400, 0), new(400, 400, 0)];
        return NativeBattleOilShot136ActMesh.Draw(memory, Source(group), 16, context, new(null, 100, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, Read, _ => null);
        NativeBattleVector Read(int offset)
        {
            reads?.Add(offset);
            return vertices[offset / 8];
        }
    }
    private static NativeBattleGfAnimationSource Source(int group)
    {
        byte[] bytes = new byte[128];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 44);
        int count = 60 + group * 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(count), 1);
        int face = count + 4;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), group == 3 ? 0x2E112233u : 0x3A112233u);
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + index * 2), (ushort)(index * 2));
        }
        uint[] tail = group == 3 ? [0x12341010, 0x56782020, 0x40403030] : [0xA1224466, 0xB16688AA, 0xC1AACCEE];
        for (int index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12 + index * 4), tail[index]);
        }
        return new(Address, bytes.ToImmutableArray());
    }
}
