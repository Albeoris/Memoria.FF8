using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect213StreamTests
{
    [Theory]
    [InlineData(false, 64, 40)]
    [InlineData(true, 76, 52)]
    public void Accepted_stream_brackets_the_body_and_publishes_all_current_depths(bool gouraud, int allocation, int rawLength)
    {
        NativeBattleEffect213StreamStep result = Draw(gouraud);
        Assert.Equal(allocation, result.Batch.AllocationBytes);
        Assert.Equal(new[] { 0, rawLength, 0, rawLength + 12 }, result.Batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        Assert.Null(result.Batch.Attempts[0].Primitive);
        Assert.Equal(0xE2000000u, result.Batch.Attempts[1].Primitive!.Words[1]);
        Assert.Equal(0xE2000010u, result.Batch.Attempts[3].Primitive!.Words[1]);
        Assert.Equal(new[] { 2000, 2000, 2000, 2000 }, result.Batch.Attempts[2].Primitive!.VertexPayload4);
        Assert.Equal(new[] { 0, 0, 0, 0 }, result.Batch.Attempts[1].Primitive!.VertexPayload4);
        Assert.Equal(new[] { 0, 0, 0, 0 }, result.Batch.Attempts[3].Primitive!.VertexPayload4);
        Assert.Equal(500u, result.Memory.GeometryRegisters!.Mac0);
        Assert.Equal(500u, result.Memory.GeometryRegisters.Otz);
        Assert.False(result.Context92.ReadSnapshot(0x38, 1).Known[0]);
        Assert.Equal(result.Batch.AllocationBytes, NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleGfGeometryBatch>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result.Batch)))!).AllocationBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void First_projection_rejection_preserves_only_actual_early_stores(bool gouraud)
    {
        NativeBattleEffect213StreamStep result = Draw(gouraud, z: 0);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.True(attempt.WriteMask.Take(8).All(value => value == NativeBattleGfPacketByteState.Known));
        Assert.True(attempt.WriteMask.Skip(8).Take(4).All(value => value == NativeBattleGfPacketByteState.Untouched));
        Assert.DoesNotContain(true, result.Context92.ReadSnapshot(0x30, 4).Known);
        Assert.True((U(result.Context92, 0x34) & 0x60000) != 0);
        Assert.Equal((short)80, BinaryPrimitives.ReadInt16LittleEndian(result.Memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan().Slice(6, 2)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reverse_winding_is_drawn_without_an_NCLIP_instruction(bool gouraud)
    {
        NativeBattleEffect213StreamStep result = Draw(gouraud, reverse: true);
        Assert.Equal(3, result.Batch.Attempts.Count(attempt => attempt.Primitive is not null));
        Assert.Equal(500u, result.Memory.GeometryRegisters!.Mac0);
        Assert.Equal(500u, result.Memory.GeometryRegisters.Otz);
    }

    [Fact]
    public void Aggregate_U_overflow_subtracts_the_current_half_window_width()
    {
        NativeBattleEffect213StreamStep result = Draw(false, scroll: 16, firstU: 250);
        NativeBattleGeometryPrimitive body = result.Batch.Attempts[2].Primitive!;
        Assert.Equal((byte)138, (byte)body.Words[3]);
        Assert.Equal(0x1234u, body.Words[3] >> 16);
    }

    [Fact]
    public void Zero_fade_does_not_resolve_retained_far_RGB_and_nonzero_fade_publishes_current_color_state()
    {
        NativeBattleEffect213StreamStep plain = Draw(true);
        Assert.Equal(0u, plain.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        NativeBattleEffect213StreamStep faded = Draw(true, fade: 2048);
        Assert.Equal(2048u, faded.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(16u, faded.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.NotEqual(0x3C808080u, faded.Batch.Attempts[2].Primitive!.Words[1]);
    }

    internal static NativeBattleEffectMemory Memory(int z = 2000) => NativeBattleEffectMemory.FromColdProcess() with
    {
        Arena = NativeBattleEffectArena.Clear(),
        GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), Identity(z), true, true, null) with { Sz = [11, 22, 33, 777] },
        ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(),
        ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0()
    };

    internal static NativeBattleActionMatrix Identity(int z = 0) => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, z, 4096);

    private static NativeBattleEffect213StreamStep Draw(bool gouraud, int z = 2000, bool reverse = false, int scroll = 0, int firstU = 0, int fade = 0)
    {
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 92).Write(0, BitConverter.GetBytes(0x117ADD8u)).Write(8, BitConverter.GetBytes(fade)).Write(12, BitConverter.GetBytes((ushort)scroll));
        context = context.Write(0x10, new byte[16]).Write(0x14, BitConverter.GetBytes((short)256)).Write(0x16, BitConverter.GetBytes((short)256)).Write(0x1C, BitConverter.GetBytes((short)128)).Write(0x1E, BitConverter.GetBytes((short)256));
        if (fade != 0)
        {
            context = context.Write(4, new byte[] { 1, 2, 3, 0 });
        }
        return NativeBattleEffect213Stream.DrawLoaded(Memory(z), Mesh(gouraud, reverse, firstU), new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270);
    }

    private static NativeBattleStaticMeshResource Mesh(bool gouraud, bool reverse, int firstU)
    {
        byte[] bytes = new byte[gouraud ? 96 : 84];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 28);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        (short X, short Y)[] vertices = [(-100, -100), (100, -100), (-100, 100), (100, 100)];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 8), vertices[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + index * 8), vertices[index].Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14 + index * 8), (short)(80 + index));
        }
        int face = gouraud ? 60 : 44;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(gouraud ? 56 : 40), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), gouraud ? 0x3C808080u : 0x2C808080u);
        int[] order = reverse ? [0, 2, 1, 3] : [0, 1, 2, 3];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + index * 2), (ushort)(order[index] * 2));
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x12340000u | (uint)firstU);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x01000040);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 20), 0x40404000);
        if (gouraud)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 24), 0x00808080);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 28), 0x00808080);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 32), 0x00808080);
        }
        return new(0, 0x117ADD8, bytes.ToImmutableArray());
    }

    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
}
