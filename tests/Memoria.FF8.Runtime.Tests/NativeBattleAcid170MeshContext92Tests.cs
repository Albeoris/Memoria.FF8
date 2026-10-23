using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAcid170MeshContext92Tests
{
    [Theory]
    [InlineData(2, 36, 8)]
    [InlineData(3, 44, 10)]
    [InlineData(7, 56, 13)]
    public void Actual_groups_keep_current_TPAGE_color_depth_sidecars_and_append_compound_E1(int group, int length, int words)
    {
        var actual = Draw(group);
        var packet = Assert.Single(actual.Batch.Attempts);
        Assert.Equal(length, actual.Batch.AllocationBytes);
        Assert.Equal(length, packet.PacketLength);
        Assert.Equal((uint)words << 24, packet.Primitive!.Words[0]);
        Assert.Equal(0xE1000220u, packet.Primitive.Words[^1]);
        int tpage = group == 7 ? 6 : 5;
        Assert.Equal((ushort)0x9ADC, (ushort)(packet.Primitive.Words[tpage] >> 16));
        Assert.Equal(group == 2 ? 141 : 142, packet.Primitive.Bucket);
        Assert.Equal(group == 2 ? new int[] { 2000, 2000, 2000, 0 } : new int[] { 2000, 2000, 2000, 2000 }, packet.Primitive.VertexPayload4);
        Assert.Equal(0xE1000220u, Dword(actual.Context92, 0x58));
        Assert.Equal(2048u, actual.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(group == 2 ? 499u : 500u, Dword(actual.Context92, 0x2C));
        Assert.Throws<NotSupportedException>(() => actual.Context92.Read(11, 1));
        Assert.All(packet.WriteMask.Skip(length - 4), value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    public void Near_rejected_prefix_has_native_incremented_tag_and_untouched_E1_tail(int group)
    {
        var actual = Draw(group, depth: 0);
        var packet = Assert.Single(actual.Batch.Attempts);
        Assert.Null(packet.Primitive);
        Assert.Equal(0, actual.Batch.AllocationBytes);
        Assert.Equal((uint)(packet.PacketLength / 4 - 1) << 24, BinaryPrimitives.ReadUInt32LittleEndian(packet.Data.AsSpan()));
        Assert.All(packet.WriteMask.TakeLast(4), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        Assert.NotEqual(0u, Dword(actual.Context92, 0x30) & 0x60000);
        Assert.Null(actual.Memory.GeometryRegisters!.Otz);
    }

    [Fact]
    public void Zero_NCLIP_rejects_after_TPAGE_prefix_without_AVSZ_while_negative_area_is_admitted()
    {
        var zero = Draw(3, flat: true);
        var packet = Assert.Single(zero.Batch.Attempts);
        Assert.Null(packet.Primitive);
        Assert.Equal(0u, Dword(zero.Context92, 0x24));
        Assert.Equal((ushort)0x9ADC, BinaryPrimitives.ReadUInt16LittleEndian(packet.Data.AsSpan().Slice(22)));
        Assert.Null(zero.Memory.GeometryRegisters!.Otz);
        Assert.NotNull(Assert.Single(Draw(3, reverse: true).Batch.Attempts).Primitive);
    }

    [Fact]
    public void Deferred_fourth_near_uses_actual_current_ZSF_and_retains_SXY_FIFO_order()
    {
        var memory = Memory(2000);
        memory = memory with { ScriptedGraphics = memory.ScriptedGraphics! with { Zsf4 = 0x80 } };
        var actual = Draw(7, fourthZ: -1999, memory: memory);
        var primitive = Assert.Single(actual.Batch.Attempts).Primitive!;
        Assert.Equal(new int[] { 2000, 2000, 2000, 1 }, primitive.VertexPayload4);
        Assert.Equal(187u, actual.Memory.GeometryRegisters!.Otz);
        Assert.Equal(63, primitive.Bucket);
        Assert.Equal(0u, actual.Memory.ScriptedColorRegisters!.Flags);
    }

    [Fact]
    public void Negative_screen_X_outside_rectangle_rejects_but_still_executes_AVSZ_without_E1_write()
    {
        var actual = Draw(3, outside: true);
        var packet = Assert.Single(actual.Batch.Attempts);
        Assert.Null(packet.Primitive);
        Assert.Equal(500u, actual.Memory.GeometryRegisters!.Otz);
        Assert.All(packet.WriteMask.TakeLast(4), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
    }

    [Fact]
    public void Guard_rejects_unreached_group_and_missing_selected_owner_and_JSON_preserves_next_state()
    {
        Assert.Throws<NotSupportedException>(() => Draw(6));
        var first = Draw(7);
        var restored = JsonSerializer.Deserialize<NativeBattleAcid170MeshContext92Step>(JsonSerializer.Serialize(first))!;
        var next = Draw(7, memory: first.Memory, context: first.Context92);
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(Draw(7, memory: restored.Memory, context: restored.Context92)));
        Assert.Throws<NotSupportedException>(() => NativeBattleAcid170MeshContext92.DrawLoaded(Memory(2000), Mesh(3), new(null, 100, 160, 108), Context(), NativeBattleGteColorInterpolation.CodeConstants45F270));
    }

    private static NativeBattleAcid170MeshContext92Step Draw(int group, int depth = 2000, bool flat = false, bool reverse = false, short fourthZ = 0, bool outside = false, NativeBattleEffectMemory? memory = null, NativeBattleEffectArena? context = null)
    {
        NativeBattleVector[] positions = [new(-400, -400, 0), new(400, -400, 0), new(-400, flat ? (short)-400 : (short)400, 0), new(400, 400, fourthZ)];
        if (outside)
        {
            positions = [new(-5000, -400, 0), new(-4000, -400, 0), new(-5000, 400, 0), new(-4000, 400, 0)];
        }
        return NativeBattleAcid170MeshContext92.DrawLoaded(memory ?? Memory(depth), Mesh(group, reverse), new(null, 100, 160, 108), context ?? Context(), NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2, 0x23B10F0, offset => (positions[offset / 8], null));
    }
    private static NativeBattleEffectMemory Memory(int z) => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = z }, true, true, 123), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleEffectArena Context() => NativeBattleEffectArena.Clear().MarkUnknown(0, 92).Write(0, BitConverter.GetBytes(0xD6904Cu)).Write(4, BitConverter.GetBytes(0x23B10F0u)).Write(8, new byte[3]).Write(12, BitConverter.GetBytes(2048)).Write(16, BitConverter.GetBytes((ushort)32)).Write(28, BitConverter.GetBytes(0x24F3u));
    private static uint Dword(NativeBattleEffectArena arena, int at) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(at, 4));
    private static NativeBattleStaticMeshResource Mesh(int group, bool reverse = false)
    {
        int length = group == 2 ? 20 : group == 3 ? 24 : group == 6 ? 28 : 36;
        byte[] bytes = new byte[72 + length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        int face = 44 + group * 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face - 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), (group == 2 ? 0x24000000u : group == 3 ? 0x2C000000u : 0x3C000000u) | 0x112233);
        int count = (group & 1) != 0 ? 4 : 3;
        for (int corner = 0; corner < count; corner++)
        {
            int index = reverse && corner is 1 or 2 ? 3 - corner : corner;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + corner * 2), (ushort)(index * 2));
        }
        if (group == 2)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 10), 0x7654);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x12345678);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x9ABCDEF0);
        if (group != 2)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 20), 0xF3456789);
        }
        if (group == 7)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 24), 0x3C224466);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 28), 0x3C6688AA);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 32), 0x3CAACCEE);
        }
        return new(0, 0xD6904C, bytes.ToImmutableArray());
    }
}
