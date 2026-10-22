using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleUltraWavesRingDrawingTests
{
    [Fact]
    public void Actual_FT4_context_uses_uniform_palette_and_publishes_GTE_FAR_and_physical_stores()
    {
        NativeBattleUltraWavesRingDraw drawn = Draw(Mesh(2000), Context(false));
        NativeBattleGfGeometryAttempt packet = Assert.Single(drawn.Batch.Attempts);
        Assert.Equal(40, drawn.Batch.AllocationBytes);
        Assert.Equal(0x2E112233u, packet.Primitive!.Words[1]);
        Assert.Equal(0u, drawn.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Equal(0u, U32(drawn.Temporary108, 0x18));
        Assert.Equal(500u, U32(drawn.Temporary108, 0x30));
        Assert.Equal(0u, U32(drawn.Temporary108, 0x34));
        Assert.Equal(142, packet.Primitive.Bucket);
        Assert.Equal(MeshAddress + 96, U32(drawn.Temporary108, 0x24));
        Assert.Equal(2000u, drawn.Memory.GeometryRegisters!.Sz[3]);
        Assert.True(drawn.Temporary108.ReadSnapshot(0x38, 4).Known.All(value => !value));
    }

    [Fact]
    public void Near_rejection_keeps_prefix_and_flags_but_does_not_publish_NCLIP_depth_or_fourth_vertex()
    {
        NativeBattleUltraWavesRingDraw drawn = Draw(Mesh(0), Context(false));
        NativeBattleGfGeometryAttempt packet = Assert.Single(drawn.Batch.Attempts);
        Assert.Null(packet.Primitive);
        Assert.Equal(0, drawn.Batch.AllocationBytes);
        Assert.Equal(NativeBattleGfPacketByteState.Known, packet.WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[8]);
        Assert.Throws<NotSupportedException>(() => drawn.Temporary108.Read(0x28, 4));
        Assert.Throws<NotSupportedException>(() => drawn.Temporary108.Read(0x30, 4));
        Assert.NotEqual(0u, U32(drawn.Temporary108, 0x34) & 0x60000);
        Assert.Equal((short)-100, BinaryPrimitives.ReadInt16LittleEndian(drawn.Memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan()));
    }

    [Fact]
    public void Zero_area_rejects_despite_backface_bypass_and_negative_area_is_drawn()
    {
        NativeBattleStaticMeshResource flat = Mesh(2000);
        byte[] source = flat.Bytes.ToArray();
        Array.Clear(source, 60, 6);
        Assert.Null(Assert.Single(Draw(flat with { Bytes = source.ToImmutableArray() }, Context(false)).Batch.Attempts).Primitive);
        source = flat.Bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(62), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(64), 2);
        NativeBattleUltraWavesRingDraw reversed = Draw(flat with { Bytes = source.ToImmutableArray() }, Context(false));
        Assert.NotNull(Assert.Single(reversed.Batch.Attempts).Primitive);
        Assert.True(unchecked((int)U32(reversed.Temporary108, 0x28)) < 0);
    }

    [Fact]
    public void Fade_updates_material_IR0_color_FIFO_and_JSON_next_without_pure_projection_fallback()
    {
        NativeBattleUltraWavesRingDraw drawn = Draw(Mesh(2000), Context(true));
        Assert.Equal(2048u, drawn.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(0x2E112233u, drawn.Memory.ScriptedColorRegisters.Material);
        Assert.Equal(0x2E081119u, drawn.Memory.ScriptedColorRegisters.Rgb[2]);
        NativeBattleUltraWavesRingDraw restored = JsonSerializer.Deserialize<NativeBattleUltraWavesRingDraw>(JsonSerializer.Serialize(drawn))!;
        NativeBattleUltraWavesRingDraw Next(NativeBattleUltraWavesRingDraw current) => Draw(Mesh(2000), current.Temporary108, current.Memory);
        Assert.Equal(JsonSerializer.Serialize(Next(drawn)), JsonSerializer.Serialize(Next(restored)));
        Assert.Throws<NotSupportedException>(() => Draw(Mesh(2000), Context(false), Memory() with { GeometryRegisters = null }));
    }

    private const uint MeshAddress = 0xD34450;
    private static NativeBattleUltraWavesRingDraw Draw(NativeBattleStaticMeshResource mesh, NativeBattleEffectArena temporary, NativeBattleEffectMemory? memory = null) => NativeBattleUltraWavesRingDrawing.Draw(memory ?? Memory(), temporary, mesh, new(null, 100, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270);
    private static NativeBattleEffectMemory Memory()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        return memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770(), true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), PrimitiveProcess = memory.PrimitiveProcess! with { ModelRendererRegisters = new(3, 4, 5, 6, 7) } };
    }
    private static NativeBattleEffectArena Context(bool fade) => NativeBattleEffectArena.Clear().MarkUnknown(0, 108).Write(0, BitConverter.GetBytes(MeshAddress)).Write(8, BitConverter.GetBytes(0u)).Write(0x1C, BitConverter.GetBytes(fade ? 0xF3u : 0x33u)).Write(0x20, BitConverter.GetBytes(0x112233u)).Write(12, BitConverter.GetBytes(fade ? 2048 : 0));
    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleStaticMeshResource Mesh(short z)
    {
        byte[] source = new byte[96];
        BinaryPrimitives.WriteInt32LittleEndian(source, 40);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(4), 4);
        short[] xy = [-100, -100, 100, -100, -100, 100, 100, 100];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(8 + index * 8), xy[index * 2]);
            BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(10 + index * 8), xy[index * 2 + 1]);
            BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(12 + index * 8), z);
            BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(14 + index * 8), 0xABCD);
        }
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(52), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(56), 0x2C804020);
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(60 + corner * 2), (ushort)(corner * 2));
        }
        return new(0, MeshAddress, source.ToImmutableArray());
    }
}
