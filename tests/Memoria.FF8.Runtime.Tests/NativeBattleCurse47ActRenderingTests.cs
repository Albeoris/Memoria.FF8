using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCurse47ActRenderingTests
{
    private static NativeBattleSummonColors Constants => new(0, 0, 1f / 4096f, BitConverter.Int64BitsToDouble(0x4330000080000000L));
    private static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);

    [Fact]
    public void Presentation_capture_preserves_actual_ACT_native_ledger_and_selected_corners()
    {
        NativeBattlePilotVisualIdentity identity = new(47, 1, 2, 3, 4) { ResourceGeneration = 5 };
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, 100, 160, 108);
        NativeBattleActMeshDraw native = Draw(Source(3), ModifiedNode(0x200), NativeBattleEffectArena.Clear().MarkUnknown(0, 840), projection: projection);
        NativeBattleActMeshDraw captured = Draw(Source(3), ModifiedNode(0x200), NativeBattleEffectArena.Clear().MarkUnknown(0, 840), pilotIdentity: identity, projection: projection);
        Assert.Equal(SnapshotJson(native), SnapshotJson(captured));
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(captured.Batch.Attempts).Primitive!.PilotGeometry);
        Assert.Equal(identity.Generation, geometry.Identity.Generation);
        Assert.Equal(identity.ResourceGeneration, geometry.Identity.ResourceGeneration);
        Assert.Equal(3, geometry.CornerCount);
        Assert.NotEqual(geometry.A, geometry.B);
    }

    [Theory]
    [InlineData(0x300u)]
    [InlineData(0x600u)]
    [InlineData(0x2200u)]
    [InlineData(0x4200u)]
    [InlineData(0x6700u)]
    public void Caller_ignores_unread_scale_Euler_UV_and_draw_flag_bits_and_keeps_fixed_native_policy(uint header)
    {
        NativeBattleGfAnimationNode node = ModifiedNode(header);
        NativeBattleActMeshDraw expected = Draw(Source(3), ModifiedNode(0x200), NativeBattleEffectArena.Clear().MarkUnknown(0, 840));
        NativeBattleActMeshDraw actual = Draw(Source(3), node, NativeBattleEffectArena.Clear().MarkUnknown(0, 840));
        Assert.Equal(SnapshotJson(expected), SnapshotJson(actual));
        Assert.Equal(0x20F0u, Dword(actual.Temporary104, 0x14));
        Assert.Equal(1448, Rotation(actual.Registers.Geometry, 0));
        Assert.Equal(2896, Rotation(actual.Registers.Geometry, 1));
        Assert.Equal(-1449, Rotation(actual.Registers.Geometry, 3));
        Assert.Null(actual.Registers.Geometry.Rotation20[18]);
        Assert.NotNull(Assert.Single(actual.Batch.Attempts).Primitive);
    }

    [Fact]
    public void Three_nonzero_angles_keep_701310_order_even_when_header400_requests_the_other_caller_order()
    {
        byte[] bytes = Node(0x600, 0).Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16), 123);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(18), -456);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(20), 789);
        NativeBattleGfAnimationNode node = new(0, 0, bytes.ToImmutableArray());
        NativeBattleActMeshDraw result = Draw(Source(3), node, NativeBattleEffectArena.Clear().MarkUnknown(0, 840));
        NativeBattleActionMatrix expected = NativeBattleGfMatrices.Euler701310(node.Vector16);
        Assert.Equal(expected.Rotation.ToArray(), Enumerable.Range(0, 9).Select(index => Rotation(result.Registers.Geometry, index)));
        Assert.False(expected.Rotation.SequenceEqual(NativeBattleGfMatrices.Euler701430(node.Vector16).Rotation));
    }

    [Fact]
    public void Morph_reaches_the_actual_last_vertex_and_preserves_W_before_the_next_physical_global()
    {
        NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 840).ToArray()).MarkUnknown(838, 2);
        NativeBattleActMeshDraw result = Draw(Source(105), Node(0x200, 2048), retained);
        Assert.Equal(0x264EAC0u, NativeBattleCurse47ActRendering.MorphAddress + NativeBattleCurse47ActRendering.MorphLength);
        Assert.Equal(NativeBattleCurse47ActRendering.MorphAddress, Dword(result.Temporary104, 4));
        Assert.Equal((short)258, BinaryPrimitives.ReadInt16LittleEndian(result.MorphScratch.Read(832, 2)));
        Assert.Equal((short)-212, BinaryPrimitives.ReadInt16LittleEndian(result.MorphScratch.Read(834, 2)));
        Assert.All(result.MorphScratch.ReadSnapshot(838, 2).Known, Assert.False);
        Assert.All(NativeBattleStorageSnapshots.Capture(result.MorphScratch).Regions, region => Assert.True(region.Offset + region.Data.Length <= 840));
        NativeBattleActMeshDraw restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleActMeshDraw>(SnapshotJson(result))!);
        Assert.Equal(SnapshotJson(Draw(Source(105), Node(0x200, 2048), result.MorphScratch)), SnapshotJson(Draw(Source(105), Node(0x200, 2048), restored.MorphScratch)));
        Assert.Throws<InvalidDataException>(() => Draw(Source(106), Node(0x200, 2048), retained));
    }

    [Fact]
    public void Unfaded_selected_source_and_untouched_temporary_color_remain_actual_owners()
    {
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().MarkUnknown(0, 104).Write(8, [1, 2, 3, 4]);
        NativeBattleActMeshDraw result = Draw(Source(3), Node(0x200, 0), NativeBattleEffectArena.Clear().MarkUnknown(0, 840), temporary);
        Assert.Equal(0x2030u, Dword(result.Temporary104, 0x14));
        Assert.Equal(0x04030201u, Dword(result.Temporary104, 8));
        Assert.Equal(0x17BB150u, Dword(result.Temporary104, 4));
        Assert.All(result.MorphScratch.ReadSnapshot(0, 840).Known, Assert.False);
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(result.Temporary104.Read(0x18, 2)));
        Assert.Equal((ushort)256, BinaryPrimitives.ReadUInt16LittleEndian(result.Temporary104.Read(0x20, 2)));
    }

    private static NativeBattleGfAnimationNode ModifiedNode(uint header)
    {
        byte[] bytes = Node(header, 0).Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(20), 512);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24), 2049);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(36), 1024);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), 0xC08040);
        return new(0, 0, bytes.ToImmutableArray());
    }

    private static NativeBattleActMeshDraw Draw(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena scratch, NativeBattleEffectArena? temporary = null, NativeBattlePilotVisualIdentity? pilotIdentity = null, NativeBattleCameraProjection? projection = null)
    {
        return NativeBattleCurse47ActRendering.DrawNode(source, node, new(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, -3), temporary ?? NativeBattleEffectArena.Clear().MarkUnknown(0, 104), scratch, Registers(), new(1, 2, 3, 4, 5), projection ?? Projection, 0, 0, 0x155, 0x100, Constants, pilotIdentity: pilotIdentity);
    }

    private static NativeBattleGeometryRegisterStep Registers() => new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown() with { Material = 0xAA123456, Rgb = [0x11, 0x22, 0x33] });
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static short Rotation(NativeBattleGeometryRegisters registers, int index) => unchecked((short)(registers.Rotation20[index * 2]!.Value | registers.Rotation20[index * 2 + 1]!.Value << 8));

    private static NativeBattleGfAnimationNode Node(uint flags, int ratio)
    {
        byte[] data = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), flags);
        foreach (int offset in new[] { 24, 26, 28 })
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset), 4096);
        }
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(38), (short)ratio);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(42), 1);
        return new(0, 0, data.ToImmutableArray());
    }

    private static NativeBattleGfAnimationSource Source(int vertices)
    {
        int directory = 12 + vertices * 16;
        byte[] bytes = new byte[16 + directory + 52];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), directory);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), vertices);
        short[] initial = [-400, -400, 400, -400, -400, 400];
        for (int frame = 0; frame < 2; frame++)
        {
            for (int vertex = 0; vertex < vertices; vertex++)
            {
                int offset = 28 + (frame * vertices + vertex) * 8;
                int x = vertex < 3 ? initial[vertex * 2] : vertex * 2;
                int y = vertex < 3 ? initial[vertex * 2 + 1] : -vertex * 3;
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), (short)(x + frame * 100));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), (short)(y + frame * 200));
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 6), 0x1234);
            }
        }
        int count = 16 + directory + 8;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(count), 1);
        int face = count + 4;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), 0x24332211);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 6), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 8), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 10), 0x0807);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x12340302);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x56780605);
        return new(0x17BB134, bytes.ToImmutableArray());
    }

    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
}
