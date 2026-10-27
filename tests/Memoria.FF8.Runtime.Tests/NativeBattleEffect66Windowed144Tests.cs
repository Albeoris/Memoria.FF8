using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect66Windowed144Tests
{
    [Fact]
    public void Accepted_FT4_and_GT4_keep_native_palette_byte_wrap_and_current_OTZ()
    {
        NativeBattleEffect66Windowed144Step result = Draw(0x33);
        NativeBattleEffectArena context = result.Context144;
        Assert.Equal(2, result.Batch.Attempts.Count(attempt => attempt.Primitive is not null));
        Assert.Equal(108, result.Batch.AllocationBytes);
        Assert.Equal(0xDE97ACu, U(context, 4));
        Assert.Equal(0xDE97A4u + (uint)Mesh().Bytes.Length, U(context, 0x28));
        Assert.Equal(0x00FF8041u, U(context, 0x5C));
        Assert.Equal(0x3EFC8080u, U(context, 0x60));
        Assert.Equal(0xCAFECAFEu, U(context, 0x30));
        Assert.Equal(result.Memory.GeometryRegisters!.Otz, U(context, 0x34));
        Assert.All(result.Batch.Attempts.Where(attempt => attempt.Primitive is not null), attempt => Assert.Equal(17 + (unchecked((int)U(context, 0x34)) >> 3), attempt.Primitive!.Bucket));
        Assert.Equal(0xAABBCCDDu, result.Memory.ScriptedColorRegisters!.Material);
        Assert.Equal(0xE2000000u, U(context, 0x8C));
        Assert.Equal(new byte[] { 0xEF, 0xBE }, context.Read(0x3C, 2));
    }

    [Fact]
    public void Signed_UV_remainders_preserve_full_scratch_words_and_window_stores()
    {
        NativeBattleEffect66Windowed144Step result = Draw(0x33);
        Assert.Equal(10u, U(result.Context144, 0x80));
        Assert.Equal(unchecked((uint)-20), U(result.Context144, 0x84));
        Assert.Equal(new uint[] { 20, 30, 40, 50 }, Enumerable.Range(0, 4).Select(index => U(result.Context144, 0x70 + index * 4)).ToArray());
        NativeBattleGfGeometryAttempt quad = result.Batch.Attempts.Last();
        Assert.Equal((byte)196, quad.Data[16]);
        Assert.Equal((byte)20, quad.Data[17]);
        Assert.Equal(U(result.Context144, 0x88), BinaryPrimitives.ReadUInt32LittleEndian(quad.Data.AsSpan(4, 4)));
    }

    [Fact]
    public void F3_finishes_DPCT_colors_one_two_three_then_DPCS_color_zero()
    {
        NativeBattleEffect66Windowed144Step result = Draw(0xF3);
        uint firstTinted = U(result.Context144, 0x60);
        Assert.Equal(firstTinted, result.Memory.ScriptedColorRegisters!.Material);
        Assert.Equal(2048u, result.Memory.ScriptedColorRegisters.Ir0);
        NativeBattleGfGeometryAttempt quad = result.Batch.Attempts.Last();
        Assert.Equal(result.Memory.ScriptedColorRegisters.Rgb[2], BinaryPrimitives.ReadUInt32LittleEndian(quad.Data.AsSpan(8, 4)));
        Assert.NotNull(result.Memory.GeometryRegisters!.Sz[3]);
        Assert.NotNull(result.Memory.ScriptedColorRegisters.Flags);
    }

    [Fact]
    public void Rejected_projection_keeps_prefix_stores_but_retains_unwritten_area_depth_and_UV_scratch()
    {
        NativeBattleEffect66Windowed144Step result = Draw(0x33, -2000);
        Assert.All(result.Batch.Attempts, attempt => Assert.Null(attempt.Primitive));
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(0x11223344u, U(result.Context144, 0x2C));
        Assert.Equal(0x55667788u, U(result.Context144, 0x34));
        Assert.Equal(0xDEADBEEFu, U(result.Context144, 0x70));
        Assert.NotEqual(0u, U(result.Context144, 0x38) & 0x60000);
        Assert.Equal(0x3CFF80FEu | 0x02000000u, U(result.Context144, 0x60));
        Assert.Contains(result.Batch.Attempts, attempt => attempt.WriteMask.Contains(NativeBattleGfPacketByteState.Known));
    }

    [Fact]
    public void Current_context_registers_and_unknown_suffix_bytes_resume_after_JSON()
    {
        NativeBattleEffect66Windowed144Step first = Draw(0xF3);
        NativeBattleEffect66Windowed144Step restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect66Windowed144Step>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(first)))!);
        NativeBattleEffect66Windowed144Step Next(NativeBattleEffect66Windowed144Step prior) => NativeBattleEffect66Windowed144.DrawLoaded(prior.Memory, Mesh(), Projection, prior.Context144, NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 3);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Next(first))), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Next(restored))));
        Assert.Throws<NotSupportedException>(() => restored.Context144.Read(0x3E, 2));
    }

    [Fact]
    public void Empty_groups_do_not_consume_palette_fade_or_mutate_geometry_registers()
    {
        byte[] bytes = Mesh().Bytes.AsSpan(0, 152).ToArray();
        Array.Clear(bytes, 120, 32);
        NativeBattleEffectArena context = Context(0xF3).MarkUnknown(12, 4).MarkUnknown(0x24, 4);
        NativeBattleEffectMemory memory = Memory();
        NativeBattleEffect66Windowed144Step result = NativeBattleEffect66Windowed144.DrawLoaded(memory, Mesh() with { Bytes = bytes.ToImmutableArray() }, Projection, context, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Empty(result.Batch.Attempts);
        Assert.Equal(memory.GeometryRegisters, result.Memory.GeometryRegisters);
        Assert.Equal(memory.ScriptedColorRegisters, result.Memory.ScriptedColorRegisters);
        Assert.Throws<NotSupportedException>(() => result.Context144.Read(0x24, 4));
        Assert.Equal(0x55667788u, U(result.Context144, 0x34));
    }

    [Fact]
    public void Zero_remainders_retain_unwritten_UV_scratch_knownness()
    {
        NativeBattleEffectArena context = Context(0x33).Write(0x10, new byte[8]);
        NativeBattleEffect66Windowed144Step result = NativeBattleEffect66Windowed144.DrawLoaded(Memory(), Mesh(), Projection, context, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(0xDEADBEEFu, U(result.Context144, 0x70));
        Assert.Throws<NotSupportedException>(() => result.Context144.Read(0x74, 4));
        Assert.Equal(0u, U(result.Context144, 0x80));
        Assert.Equal(0u, U(result.Context144, 0x84));
    }

    [Fact]
    public void Invalid_windows_sources_and_nonselected_groups_fail_at_actual_boundary()
    {
        NativeBattleEffectArena context = Context(0x33).Write(0x1C, new byte[2]);
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect66Windowed144.DrawLoaded(Memory(), Mesh(), Projection, context, NativeBattleGteColorInterpolation.CodeConstants45F270));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect66Windowed144.DrawLoaded(Memory(), Mesh() with { Address = 1 }, Projection, Context(0x33), NativeBattleGteColorInterpolation.CodeConstants45F270));
        byte[] unsupported = Mesh().Bytes.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(unsupported.AsSpan(128), 1);
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect66Windowed144.DrawLoaded(Memory(), Mesh() with { Bytes = unsupported.ToImmutableArray() }, Projection, Context(0x33), NativeBattleGteColorInterpolation.CodeConstants45F270));
    }

    private static NativeBattleEffect66Windowed144Step Draw(uint flags, int z = 2000) => NativeBattleEffect66Windowed144.DrawLoaded(Memory(z), Mesh(), Projection, Context(flags), NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 3);
    private static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);
    private static uint U(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));

    private static NativeBattleEffectMemory Memory(int z = 2000)
    {
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, z, 4096);
        return NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), matrix, true, true, unchecked((short)0xBEEF)), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Material = 0xAABBCCDD }, ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() with { Zsf4 = 0x100 } };
    }

    private static NativeBattleEffectArena Context(uint flags) => NativeBattleEffectArena.Clear().MarkUnknown(0, 144).Write(0, BitConverter.GetBytes(0xDE97A4u)).Write(8, new byte[3]).Write(12, BitConverter.GetBytes(2048)).Write(0x10, BitConverter.GetBytes(10)).Write(0x14, BitConverter.GetBytes(-20)).Write(0x18, BitConverter.GetBytes((short)0)).Write(0x1A, BitConverter.GetBytes((short)128)).Write(0x1C, BitConverter.GetBytes((short)64)).Write(0x1E, BitConverter.GetBytes((short)128)).Write(0x20, BitConverter.GetBytes(flags)).Write(0x24, BitConverter.GetBytes(0x00FF8041u)).Write(0x2C, BitConverter.GetBytes(0x11223344u)).Write(0x30, BitConverter.GetBytes(0xCAFECAFEu)).Write(0x34, BitConverter.GetBytes(0x55667788u)).Write(0x3C, new byte[] { 0xEF, 0xBE }).Write(0x70, BitConverter.GetBytes(0xDEADBEEFu));

    private static NativeBattleStaticMeshResource Mesh()
    {
        byte[] bytes = new byte[212];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 120);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 14);
        short[] xyz = [-100, -100, 0, 0, 100, -100, 0, 0, -100, 100, 0, 0, 100, 100, 0, 0];
        for (int index = 0; index < xyz.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 2), xyz[index]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(132), 1);
        Fill(136, false);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(172), 1);
        Fill(176, true);
        return new(0, 0xDE97A4, bytes.ToImmutableArray());

        void Fill(int at, bool gouraud)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), gouraud ? 0x3CFF80FEu : 0x2C808080u);
            for (int corner = 0; corner < 4; corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at + 4 + corner * 2), (ushort)(corner * 2));
            }
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at + 12), 0x123428FA);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at + 16), 0x56783205);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at + 20), 0x46143C0A);
            if (gouraud)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at + 24), 0x00804020);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at + 28), 0x00FF8080);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at + 32), 0x00010203);
            }
        }
    }
}
