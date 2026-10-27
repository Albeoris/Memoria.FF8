using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBladeShot12MeshContext88Tests
{
    [Theory]
    [InlineData(4, 28)]
    [InlineData(5, 36)]
    public void Accepted_groups_publish_adjusted_depth_sidecars_and_current_RGB(int group, int bytes)
    {
        var actual = Draw(group);
        var primitive = Assert.Single(actual.Batch.Attempts).Primitive!;
        Assert.NotNull(primitive);
        Assert.Equal(bytes, actual.Batch.AllocationBytes);
        Assert.Equal(group == 4 ? 491u : 492u, U(actual.Context88, 0x2C));
        Assert.Equal(group == 4 ? 139 : 140, primitive.Bucket);
        Assert.Equal(group == 4 ? new[] { 2000, 2000, 2000, 0 } : new[] { 2000, 2000, 2000, 2000 }, primitive.VertexPayload4);
        Assert.Equal(2048u, actual.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Throws<NotSupportedException>(() => actual.Context88.Read(11, 1));
    }

    [Theory]
    [InlineData(4, 0)]
    [InlineData(5, 36)]
    public void Nonpositive_adjusted_depth_never_reads_RGB_FAR_or_cue_and_quad_reserves_unlinked_bytes(int group, int reserve)
    {
        var actual = Draw(group, 32, unreadColor: true);
        var attempt = Assert.Single(actual.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(reserve, actual.Batch.AllocationBytes);
        Assert.Equal(group == 5, actual.Batch.NativeUnlinkedAllocation);
        Assert.Equal(group == 4 ? uint.MaxValue : 0u, U(actual.Context88, 0x2C));
        Assert.All(attempt.WriteMask.Skip(12).Where((_, index) => index % 8 < 4), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void Near_rejection_keeps_only_tag_and_material_prefix_without_shading(int group)
    {
        var actual = Draw(group, 1, unreadColor: true);
        var attempt = Assert.Single(actual.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, actual.Batch.AllocationBytes);
        Assert.All(attempt.WriteMask.Take(8), value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
        Assert.All(attempt.WriteMask.Skip(8), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        Assert.NotEqual(0u, U(actual.Context88, 0x30) & 0x60000);
    }

    [Fact]
    public void Zero_area_is_admitted_and_negative_area_is_rejected_before_AVSZ()
    {
        Assert.NotNull(Assert.Single(Draw(4, flat: true).Batch.Attempts).Primitive);
        var reverse = Draw(5, reverse: true);
        Assert.Null(Assert.Single(reverse.Batch.Attempts).Primitive);
        Assert.Null(reverse.Memory.GeometryRegisters!.Otz);
    }

    [Fact]
    public void G3_publishes_RGB_FAR_and_G4_reuses_current_FAR_across_JSON()
    {
        var first = Draw(4);
        Assert.Equal(160u, first.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        var restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(first.Memory))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(Draw(5, memory: first.Memory)), NativeBattleTestSnapshots.Json(Draw(5, memory: restored)));
        Assert.Throws<InvalidDataException>(() => NativeBattleBladeShot12MeshContext88.DrawLoaded(Memory(2000), Mesh(4) with { Address = 123 }, new(null, 1, 160, 108), Context(), NativeBattleGteColorInterpolation.CodeConstants45F270));
    }

    private static NativeBattleBladeShot12MeshContext88Step Draw(int group, int z = 2000, bool unreadColor = false, bool flat = false, bool reverse = false, NativeBattleEffectMemory? memory = null)
    {
        memory ??= Memory(z);
        NativeBattleEffectArena context = Context();
        if (unreadColor)
        {
            context = context.MarkUnknown(8, 8);
            memory = memory with { PrimitiveProcess = null };
        }
        return NativeBattleBladeShot12MeshContext88.DrawLoaded(memory, Mesh(group, flat, reverse), new(null, (ushort)(z < 100 ? 2 : 100), 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270);
    }
    internal static NativeBattleEffectMemory Memory(int z) => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = z }, true, true, 123), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleEffectArena Context() => NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(0x1364F14u)).Write(8, new byte[] { 10, 20, 30 }).Write(12, BitConverter.GetBytes(2048)).Write(28, BitConverter.GetBytes(0xC0u));
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleStaticMeshResource Mesh(int group, bool flat = false, bool reverse = false)
    {
        byte[] bytes = new byte[112];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        short[] points = [-400, -400, 0, 111, 400, -400, 0, 222, -400, flat ? (short)-400 : (short)400, 0, 333, 400, 400, 0, 444];
        for (int index = 0; index < points.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 2), points[index]);
        }
        int face = 44 + group * 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face - 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), group == 4 ? 0x30112233u : 0x38112233u);
        for (int corner = 0; corner < (group == 4 ? 3 : 4); corner++)
        {
            int index = reverse && corner is 1 or 2 ? 3 - corner : corner;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + corner * 2), (ushort)(index * 2));
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x30224466);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x30336699);
        if (group == 5)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 20), 0x384488CC);
        }
        return new(0, 0x1364F14, bytes.ToImmutableArray());
    }
}
