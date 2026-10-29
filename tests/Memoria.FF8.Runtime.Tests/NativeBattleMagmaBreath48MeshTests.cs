using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMagmaBreath48MeshTests
{
    private const uint Address = 0x15AE4FC;

    [Theory]
    [InlineData(6, 0x34u)]
    [InlineData(7, 0x3Cu)]
    public void Captured_mutable_source_corners_preserve_full_native_state_and_packet_ledger(int group, uint opcode)
    {
        NativeBattlePilotVisualIdentity identity = new(48, 1, 2, 3, 4) { ResourceGeneration = Address };
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, 100, 160, 108);
        NativeBattleStaticMeshContext88Step native = Draw(group, opcode, projection: projection);
        NativeBattleStaticMeshContext88Step captured = Draw(group, opcode, pilotIdentity: identity, projection: projection);
        Assert.Equal(NativeBattleTestSnapshots.Json(native), NativeBattleTestSnapshots.Json(captured));
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(captured.Batch.Attempts).Primitive!.PilotGeometry);
        Assert.Equal(group == 6 ? 3 : 4, geometry.CornerCount);
        Assert.Equal(identity.Generation, geometry.Identity.Generation);
        Assert.Equal(identity.ResourceGeneration, geometry.Identity.ResourceGeneration);
        Assert.NotEqual(geometry.A, geometry.B);
    }

    [Theory]
    [InlineData(6, 0x34u)]
    [InlineData(6, 0x36u)]
    [InlineData(7, 0x3Cu)]
    [InlineData(7, 0x3Eu)]
    public void Actual_F0_material_commands_keep_current_three_or_four_depth_sidecars(int group, uint opcode)
    {
        var actual = Draw(group, opcode);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(actual.Batch.Attempts);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(group == 6 ? 40 : 52, actual.Batch.AllocationBytes);
        Assert.Equal(opcode, primitive.Words[1] >> 24);
        Assert.Equal(group == 6 ? new[] { 1900, 2000, 2100, 0 } : new[] { 1900, 2000, 2100, 2200 }, primitive.VertexPayload4);
        Assert.Equal(group == 6 ? new short[] { -400, -400, -100, 11 } : new short[] { 400, 400, 200, 44 }, Words(actual.Memory.ScriptedColorRegisters!.NormalInput!.Value));
        uint expectedDepth = (uint)(group == 6 ? (6000 * 0x155) >> 12 : (8200 * 0x100) >> 12);
        Assert.Equal(expectedDepth, actual.Memory.GeometryRegisters!.Otz);
        Assert.Equal(expectedDepth, Dword(actual.Context, 0x2C));
        Assert.Equal(unchecked((uint)2048), actual.Memory.ScriptedColorRegisters.Ir0);
        Assert.Equal(0u, Dword(actual.Context, 0x18));
    }

    [Fact]
    public void F0_GT3_near_rejection_does_not_read_unknown_cue_or_consume_color_interpolation()
    {
        NativeBattleEffectMemory memory = Memory(0);
        NativeBattleScriptedColorRegisters before = memory.ScriptedColorRegisters!;
        var actual = Draw(6, 0x34, memory, Context().MarkUnknown(8, 8));
        Assert.Null(Assert.Single(actual.Batch.Attempts).Primitive);
        Assert.Equal(0, actual.Batch.AllocationBytes);
        Assert.Equal(before.Rgb, actual.Memory.ScriptedColorRegisters!.Rgb);
        Assert.Equal(0xDEADBEEFu, Dword(actual.Context, 0x2C));
        Assert.NotEqual(0u, Dword(actual.Context, 0x30) & 0x60000);
    }

    [Fact]
    public void F0_GT3_bypasses_negative_NCLIP_but_retains_it_and_shades_only_after_acceptance()
    {
        var accepted = Draw(6, 0x34, reversed: true);
        Assert.NotNull(Assert.Single(accepted.Batch.Attempts).Primitive);
        Assert.True(unchecked((int)Dword(accepted.Context, 0x24)) < 0);
        var culled = Draw(6, 0x34, context: Context().Write(0x1C, BitConverter.GetBytes(0xC0u)), reversed: true);
        Assert.Null(Assert.Single(culled.Batch.Attempts).Primitive);
        Assert.Equal(0xDEADBEEFu, Dword(culled.Context, 0x2C));
    }

    [Fact]
    public void F0_GT3_outside_rejection_keeps_old_context_depth_and_acceptance_requires_actual_far_and_cue()
    {
        var outside = Draw(6, 0x34, Memory(2000, -30000), Context().MarkUnknown(8, 8));
        Assert.Null(Assert.Single(outside.Batch.Attempts).Primitive);
        Assert.Equal(0xDEADBEEFu, Dword(outside.Context, 0x2C));
        Assert.Throws<NotSupportedException>(() => Draw(6, 0x34, context: Context().MarkUnknown(12, 4)));
        Assert.Throws<NotSupportedException>(() => Draw(6, 0x34, context: Context().MarkUnknown(8, 3)));
    }

    private static NativeBattleStaticMeshContext88Step Draw(int group, uint opcode, NativeBattleEffectMemory? memory = null, NativeBattleEffectArena? context = null, bool reversed = false, NativeBattlePilotVisualIdentity? pilotIdentity = null, NativeBattleCameraProjection? projection = null) => NativeBattleStaticMeshContext88.DrawLoaded(memory ?? Memory(), Mesh(group, opcode, reversed), projection ?? new(null, 100, 160, 108), context ?? Context(), NativeBattleGteColorInterpolation.CodeConstants45F270, pilotIdentity: pilotIdentity);

    private static NativeBattleEffectMemory Memory(int z = 2000, int x = 0) => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { X = x, Z = z }, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleEffectArena Context() => NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(Address)).Write(8, new byte[4]).Write(12, BitConverter.GetBytes(2048)).Write(0x1C, BitConverter.GetBytes(0xF0u)).Write(0x2C, BitConverter.GetBytes(0xDEADBEEFu));
    private static uint Dword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
    private static short[] Words(ImmutableArray<byte> bytes) => Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(index * 2))).ToArray();

    internal static NativeBattleStaticMeshResource Mesh(int selected, uint opcode, bool reversed = false)
    {
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        byte[] bytes = new byte[8 + 32 + 32 + strides[selected]];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        short[] points = [-400, -400, -100, 11, 400, -400, 0, 22, -400, 400, 100, 33, 400, 400, 200, 44];
        for (int index = 0; index < points.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 2), points[index]);
        }
        int cursor = 40;
        for (int group = 0; group < 8; group++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(cursor), group == selected ? 1 : 0);
            cursor += 4;
            if (group != selected)
            {
                continue;
            }
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor), opcode << 24 | 0x808080);
            ushort[] indices = reversed ? [0, 4, 2, 6] : [0, 2, 4, 6];
            for (int corner = 0; corner < ((group & 1) == 0 ? 3 : 4); corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 4 + corner * 2), indices[corner]);
            }
            int color = (group & 1) == 0 ? 20 : 24;
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + color), 0x00112233);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + color + 4), 0x00445566);
            if ((group & 1) != 0)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + color + 8), 0x00778899);
            }
            cursor += strides[group];
        }
        return new(0, Address, bytes.ToImmutableArray());
    }
}
