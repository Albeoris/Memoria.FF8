using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect301QuadTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Presentation_custom_quad_capture_keeps_native_rejection_and_unreached_fourth_unknown(bool rejected)
    {
        int z = rejected ? 0 : 2000;
        NativeBattleEffectArena Context() => rejected ? Temporary(z).MarkUnknown(0x60, 4) : Temporary(z);
        var plain = NativeBattleEffect301CustomQuads.DrawLoaded(Memory(), Context(), Source(), new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108));
        string frozen = JsonSerializer.Serialize(new { Memory = NativeBattleStorageSnapshots.Capture(plain.Memory), Temporary180 = NativeBattleStorageSnapshots.Capture(plain.Temporary180) });
        var captured = NativeBattleEffect301CustomQuads.DrawLoaded(Memory(), Context(), Source(), new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108), new(301, 0x123400, 1, 17, 0) { ResourceGeneration = 41 });
        Assert.Equal(frozen, JsonSerializer.Serialize(new { Memory = NativeBattleStorageSnapshots.Capture(captured.Memory), Temporary180 = NativeBattleStorageSnapshots.Capture(captured.Temporary180) }));
        var expected = Assert.Single(plain.Batch.Attempts);
        var actual = Assert.Single(captured.Batch.Attempts);
        Assert.Equal(expected.Data.ToArray(), actual.Data.ToArray());
        Assert.Equal(expected.WriteMask.ToArray(), actual.WriteMask.ToArray());
        if (rejected)
        {
            Assert.Null(actual.Primitive);
            return;
        }
        var geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(actual.Primitive!.PilotGeometry);
        Assert.Equal((byte)4, geometry.CornerCount);
        Assert.False(geometry.ScreenSpace);
        Assert.True(geometry.UseSourceDepths);
        Assert.Equal(2000, geometry.SourceDepths!.Value.X);
    }

    [Theory]
    [InlineData(1024, 0, 0)]
    [InlineData(1040, 40, 18)]
    [InlineData(2000, 40, 78)]
    public void AVSZ4_then_shift_two_minus64_requires_strictly_positive_depth(int z, int allocation, int bucket)
    {
        NativeBattleEffect301QuadStep result = NativeBattleEffect301CustomQuads.DrawLoaded(Memory(), Temporary(z), Source(), new(null, 100, 160, 108));
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Equal(allocation, result.Batch.AllocationBytes);
        Assert.Equal((uint)(z / 4), result.Memory.GeometryRegisters!.Otz);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[32]);
        if (allocation != 0)
        {
            Assert.Equal(bucket, attempt.Primitive!.Bucket);
            Assert.Equal(new[] { z, z, z, z }, attempt.Primitive.VertexPayload4);
        }
        else
        {
            Assert.Null(attempt.Primitive);
        }
    }

    [Fact]
    public void Raw_material_DWORD_and_uvholes_are_preserved_without_gray_expansion()
    {
        NativeBattleEffect301QuadStep result = NativeBattleEffect301CustomQuads.DrawLoaded(Memory(), Temporary(2000), Source(), new(null, 100, 160, 108));
        NativeBattleGfGeometryAttempt packet = Assert.Single(result.Batch.Attempts);
        Assert.Equal(0x2C088008u, BinaryPrimitives.ReadUInt32LittleEndian(packet.Data.AsSpan().Slice(4)));
        Assert.All(new[] { 30, 31, 38, 39 }, index => Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[index]));
        Assert.Equal(NativeBattleGfPacketByteState.Known, packet.WriteMask[29]);
        Assert.Null(result.Memory.GeometryRegisters!.Rotation20[18]);
    }

    [Fact]
    public void Triple_reject_preserves_prefix_and_does_not_read_fourth_corner_or_AVSZ()
    {
        NativeBattleEffect301QuadStep result = NativeBattleEffect301CustomQuads.DrawLoaded(Memory(), Temporary(0).MarkUnknown(0x60, 4), Source(), new(null, 100, 160, 108));
        NativeBattleGfGeometryAttempt packet = Assert.Single(result.Batch.Attempts);
        Assert.Null(packet.Primitive);
        Assert.Equal(999u, result.Memory.GeometryRegisters!.Otz);
        Assert.Equal(NativeBattleGfPacketByteState.Known, packet.WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[32]);
    }

    [Fact]
    public void Unknown_padding_survives_JSON_before_the_next_draw()
    {
        NativeBattleEffect301QuadStep first = NativeBattleEffect301CustomQuads.DrawLoaded(Memory(), Temporary(2000), Source(), new(null, 100, 160, 108));
        NativeBattleEffect301QuadStep copy = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect301QuadStep>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(first)))!);
        NativeBattleEffect301QuadStep Next(NativeBattleEffect301QuadStep value) => NativeBattleEffect301CustomQuads.DrawLoaded(value.Memory, value.Temporary180, Source(), new(null, 100, 160, 108));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Next(first))), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Next(copy))));
    }

    internal static NativeBattleModuleResource Source()
    {
        byte[] bytes = new byte[20];
        bytes[0] = 8;
        bytes[1] = 128;
        bytes[2] = 8;
        bytes[3] = 0x2C;
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(18), 4096);
        return new(5, 0x1234000, 20, 0, bytes.ToImmutableArray());
    }
    internal static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown() with { Otz = 999 }, ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    internal static NativeBattleEffectArena Temporary(int z)
    {
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 180).Write(0x2C, BitConverter.GetBytes(0x1234000u)).Write(0x30, BitConverter.GetBytes(1));
        context = context.Write(0x84, NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = z }, null).Bytes.AsSpan()).MarkUnknown(0x96, 2);
        foreach (int offset in new[] { 0x48, 0x50, 0x58, 0x60 })
        {
            context = context.Write(offset, [0, 0]);
        }
        return context;
    }
}
