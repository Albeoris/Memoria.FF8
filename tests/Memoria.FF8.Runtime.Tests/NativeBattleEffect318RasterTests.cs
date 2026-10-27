using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect318RasterTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Fixed_bucket_19_uses_current_SZ_and_never_consumes_extended_flag(bool unknownFlag)
    {
        NativeBattleEffectArena temporary = Temporary();
        temporary = unknownFlag ? temporary.MarkUnknown(0x25, 1) : temporary.Write(0x25, [1]);
        NativeBattleEffectMemory memory = Memory();
        NativeBattleEffect318RasterStep result = NativeBattleEffect318Raster.DrawLoaded(memory, temporary, Source(), Projection);
        NativeBattleGfGeometryAttempt packet = Assert.Single(result.Batch.Attempts);
        Assert.Equal(40, result.Batch.AllocationBytes);
        Assert.Equal(19, packet.Primitive!.Bucket);
        Assert.Equal(result.Memory.GeometryRegisters!.Sz.Select(value => unchecked((int)value!.Value)), packet.Primitive.VertexPayload4);
        Assert.Equal(999u, result.Memory.GeometryRegisters.Otz);
        Assert.Equal(0u, result.Memory.GeometryRegisters.Mac0);
        Assert.Equal(0u, result.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(memory.ScriptedColorRegisters!.Material, result.Memory.ScriptedColorRegisters.Material);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[38]);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[18]);
        Assert.DoesNotContain(true, result.Temporary180.ReadSnapshot(0x76, 2).Known);
        Assert.False(result.Memory.ScriptedColorRegisters.NormalInputKnown!.Value[6]);
    }

    [Fact]
    public void Triple_rejection_preserves_prefix_and_does_not_read_fourth_Z_or_W()
    {
        NativeBattleEffectArena temporary = Temporary(0).MarkUnknown(0x60, 4);
        NativeBattleEffect318RasterStep result = NativeBattleEffect318Raster.DrawLoaded(Memory(), temporary, Source(), Projection);
        NativeBattleGfGeometryAttempt packet = Assert.Single(result.Batch.Attempts);
        Assert.Null(packet.Primitive);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(NativeBattleGfPacketByteState.Known, packet.WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, packet.WriteMask[12]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, packet.WriteMask[20]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[8]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[22]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[29]);
        Assert.Equal(999u, result.Memory.GeometryRegisters!.Otz);
    }

    [Fact]
    public void Fourth_RTSP_flag_is_not_a_second_admission_gate()
    {
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Rotation = [4096, 0, 0, 0, 4096, 0, -4096, -4096, 4096], Z = 100 };
        NativeBattleEffectArena temporary = Temporary().Write(0x84, NativeBattlePackedMatrix32.FromMatrix(matrix, 0).Bytes.AsSpan());
        NativeBattleModuleResource source = Source();
        byte[] bytes = source.Bytes.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(48), -4);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(50), -4);
        NativeBattleEffect318RasterStep result = NativeBattleEffect318Raster.DrawLoaded(Memory(), temporary, source with { Bytes = bytes.ToImmutableArray() }, Projection);
        Assert.Equal(40, result.Batch.AllocationBytes);
        Assert.NotNull(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.NotEqual(0u, result.Memory.ScriptedColorRegisters!.Flags!.Value & 0x60000);
        Assert.Equal(999u, result.Memory.GeometryRegisters!.Otz);
    }

    [Fact]
    public void UV_endpoints_clamp_and_TPAGE_mask_preserves_sparse_upper_words()
    {
        NativeBattleModuleResource source = Source();
        byte[] bytes = source.Bytes.ToArray();
        bytes[44] = 250;
        bytes[45] = 252;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(54), 0xA345);
        NativeBattleEffect318RasterStep result = NativeBattleEffect318Raster.DrawLoaded(Memory(), Temporary(), source with { Bytes = bytes.ToImmutableArray() }, Projection);
        NativeBattleGfGeometryAttempt packet = Assert.Single(result.Batch.Attempts);
        Assert.Equal(255, packet.Data[20]);
        Assert.Equal(252, packet.Data[21]);
        Assert.Equal(250, packet.Data[28]);
        Assert.Equal(255, packet.Data[29]);
        Assert.Equal(255, packet.Data[36]);
        Assert.Equal(255, packet.Data[37]);
        Assert.Equal(0x145, BinaryPrimitives.ReadUInt16LittleEndian(packet.Data.AsSpan().Slice(22)));
        Assert.All(new[] { 30, 31, 38, 39 }, offset => Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[offset]));
    }

    [Fact]
    public void Empty_count_does_not_consume_unobserved_source_pointer_or_GTE()
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().MarkUnknown(0, 180).Write(0x30, BitConverter.GetBytes(0));
        NativeBattleEffect318RasterStep result = NativeBattleEffect318Raster.DrawLoaded(memory, temporary, null!, Projection);
        Assert.Same(memory, result.Memory);
        Assert.Empty(result.Batch.Attempts);
    }

    [Fact]
    public void Unknown_padding_and_corner_W_survive_JSON_next_draw()
    {
        NativeBattleEffect318RasterStep result = NativeBattleEffect318Raster.DrawLoaded(Memory(), Temporary(), Source(), Projection);
        NativeBattleEffect318RasterStep restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect318RasterStep>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)))!);
        NativeBattleEffect318RasterStep Next(NativeBattleEffect318RasterStep value) => NativeBattleEffect318Raster.DrawLoaded(value.Memory, value.Temporary180, Source(), Projection);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Next(result))), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Next(restored))));
    }

    internal static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown() with { Otz = 999, Mac0 = 777 }, ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    internal static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);
    internal static NativeBattleModuleResource Source() => NativeBattleEffect318LowerTests.Fixture.Projected();
    internal static NativeBattleEffectArena Temporary(int z = 2000)
    {
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().MarkUnknown(0, 180).Write(0x2C, BitConverter.GetBytes(0xFF7B6Cu + 40)).Write(0x30, BitConverter.GetBytes(1));
        temporary = temporary.Write(0x84, NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = z }, 0).Bytes.AsSpan()).MarkUnknown(0x96, 2);
        foreach (int offset in new[] { 0x48, 0x50, 0x58, 0x60 })
        {
            temporary = temporary.Write(offset, [0, 0]);
        }
        return temporary;
    }
}
