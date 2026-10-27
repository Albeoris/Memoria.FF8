using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStaticMeshContext88F4Tests
{
    private const uint Address = 0x10000000;

    [Theory]
    [InlineData(0u, 0x28112233u)]
    [InlineData(0xC3u, 0x2A081119u)]
    [InlineData(0xF0u, 0x28081119u)]
    public void F4_accepted_quad_uses_canonical_24_byte_packet_and_retains_unused_fields(uint flags, uint expectedColor)
    {
        var actual = Draw(flags);
        NativeBattleGfGeometryAttempt packet = Assert.Single(actual.Batch.Attempts);
        Assert.NotNull(packet.Primitive);
        Assert.Equal(24, actual.Batch.AllocationBytes);
        Assert.Equal(0x05000000u, Dword(packet.Data, 0));
        Assert.Equal(expectedColor, Dword(packet.Data, 4));
        Assert.Equal(Address + 8, Dword(actual.Context, 4));
        Assert.Equal(0u, Dword(actual.Context, 0x18));
        Assert.Equal(500u, Dword(actual.Context, 0x2C));
        Assert.Equal((uint)500, actual.Memory.GeometryRegisters!.Otz);
        Assert.Throws<NotSupportedException>(() => actual.Context.Read(0x10, 4));
        if (flags != 0)
        {
            Assert.Equal((uint)2048, actual.Memory.ScriptedColorRegisters!.Ir0);
            Assert.Equal(expectedColor, actual.Memory.ScriptedColorRegisters.Rgb[2]);
        }
    }

    [Fact]
    public void F4_first_three_near_rejection_writes_prefix_without_fourth_or_cue()
    {
        var actual = Draw(0xC3, z: 0, context: Context(0xC3).MarkUnknown(12, 4));
        var packet = Assert.Single(actual.Batch.Attempts);
        Assert.Null(packet.Primitive);
        Assert.Equal(0, actual.Batch.AllocationBytes);
        Assert.Equal(0x05000000u, Dword(packet.Data, 0));
        Assert.Equal(0x2A112233u, Dword(packet.Data, 4));
        Assert.Equal(0x12345678u, Dword(actual.Context, 0x24));
        Assert.Equal(0xABCDEF01u, Dword(actual.Context, 0x2C));
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(actual.Memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan().Slice(4)));
    }

    [Fact]
    public void F4_backface_rejects_before_fourth_and_preserves_old_OTZ()
    {
        var actual = Draw(0, reversed: true);
        Assert.Null(Assert.Single(actual.Batch.Attempts).Primitive);
        Assert.True(unchecked((int)Dword(actual.Context, 0x24)) < 0);
        Assert.Equal(0xABCDEF01u, Dword(actual.Context, 0x2C));
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(actual.Memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan().Slice(4)));
    }

    [Fact]
    public void F4_fourth_near_flag_is_ignored_and_final_AVSZ_uses_four_depths()
    {
        NativeBattleGeometryRegisterStep fourth = new(NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, true, true, null), NativeBattleScriptedColorRegisters.Unknown());
        fourth = NativeBattleGeometry.LoadVector(fourth, 0, new(400, 400, -1999), 44);
        NativeBattleScriptedGraphicsMemory controls = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0();
        fourth = NativeBattleGeometry.ProjectSingle(fourth, new(null, 100, 160, 108), controls.Dqa, controls.Dqb);
        Assert.NotEqual(0u, fourth.Colors.Flags!.Value & 0x60000);
        var actual = Draw(0, fourthZ: -1999);
        Assert.NotNull(Assert.Single(actual.Batch.Attempts).Primitive);
        Assert.Equal((uint)375, actual.Memory.GeometryRegisters!.Otz);
        Assert.Equal(375u, Dword(actual.Context, 0x2C));
        Assert.Equal(0u, actual.Memory.ScriptedColorRegisters!.Flags!.Value);
        Assert.Equal((short)-1999, BinaryPrimitives.ReadInt16LittleEndian(actual.Memory.ScriptedColorRegisters.NormalInput!.Value.AsSpan().Slice(4)));
    }

    [Fact]
    public void F4_outside_rejection_runs_fourth_AVSZ_without_publishing_context_OTZ()
    {
        var actual = Draw(0, x: -30000);
        Assert.Null(Assert.Single(actual.Batch.Attempts).Primitive);
        Assert.Equal((uint)500, actual.Memory.GeometryRegisters!.Otz);
        Assert.Equal(0xABCDEF01u, Dword(actual.Context, 0x2C));
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(actual.Memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan().Slice(4)));
    }

    [Fact]
    public void F4_other_flag_policies_remain_unsupported_and_JSON_restores_same_next_draw()
    {
        Assert.Throws<NotSupportedException>(() => Draw(0x33));
        var first = Draw(0xC3);
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(first.Memory))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(Draw(0xC3, memory: first.Memory)), NativeBattleTestSnapshots.Json(Draw(0xC3, memory: restored)));
    }

    private static NativeBattleStaticMeshContext88Step Draw(uint flags, int z = 2000, bool reversed = false, short fourthZ = 0, int x = 0, NativeBattleEffectArena? context = null, NativeBattleEffectMemory? memory = null)
    {
        memory ??= NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { X = x, Z = z }, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        return NativeBattleStaticMeshContext88.DrawLoaded(memory, Mesh(reversed, fourthZ), new(null, 100, 160, 108), context ?? Context(flags), NativeBattleGteColorInterpolation.CodeConstants45F270);
    }

    private static NativeBattleEffectArena Context(uint flags) => NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(Address)).Write(8, new byte[4]).Write(12, BitConverter.GetBytes(2048)).Write(0x1C, BitConverter.GetBytes(flags)).Write(0x24, BitConverter.GetBytes(0x12345678u)).Write(0x2C, BitConverter.GetBytes(0xABCDEF01u));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => Dword(arena.Read(offset, 4), 0);
    private static uint Dword(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(offset));
    private static uint Dword(byte[] data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset));

    private static NativeBattleStaticMeshResource Mesh(bool reversed, short fourthZ)
    {
        byte[] bytes = new byte[84];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        short[] points = [-400, -400, 0, 11, 400, -400, 0, 22, -400, 400, 0, 33, 400, 400, fourthZ, 44];
        for (int index = 0; index < points.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 2), points[index]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(44), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(48), 0x28112233);
        ushort[] indices = reversed ? [0, 4, 2, 6] : [0, 2, 4, 6];
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(52 + corner * 2), indices[corner]);
        }
        return new(0, Address, bytes.ToImmutableArray());
    }
}
