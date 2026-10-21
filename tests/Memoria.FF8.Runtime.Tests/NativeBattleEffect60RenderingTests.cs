using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect60RenderingTests
{
    private static NativeBattleSummonColors Constants => new(0, 0, 1f / 4096f, BitConverter.Int64BitsToDouble(0x4330000080000000L));
    private static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);

    [Fact]
    public void ACT_morph_writes_all_121_vertices_within_968_bytes_and_preserves_last_unknown_W_across_cold_next()
    {
        NativeBattleGfAnimationSource source = Source(121);
        NativeBattleGfAnimationNode node = Node(0x200, 2048);
        NativeBattleEffectArena scratch = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 968).ToArray()).MarkUnknown(966, 2);
        NativeBattleActMeshDraw result = Draw(scratch);
        Assert.Equal(0x2606C78u, Dword(result.Temporary104, 4));
        Assert.Equal(290, BinaryPrimitives.ReadInt16LittleEndian(result.MorphScratch.Read(960, 2)));
        Assert.Equal(-260, BinaryPrimitives.ReadInt16LittleEndian(result.MorphScratch.Read(962, 2)));
        Assert.Equal(0, BinaryPrimitives.ReadInt16LittleEndian(result.MorphScratch.Read(964, 2)));
        Assert.All(result.MorphScratch.ReadSnapshot(966, 2).Known, Assert.False);
        Assert.All(result.MorphScratch.Regions, region => Assert.True(region.Offset + region.Data.Length <= 968));
        Assert.NotNull(Assert.Single(result.Batch.Attempts).Primitive);
        NativeBattleActMeshDraw restored = JsonSerializer.Deserialize<NativeBattleActMeshDraw>(JsonSerializer.Serialize(result))!;
        Assert.Equal(JsonSerializer.Serialize(Draw(result.MorphScratch)), JsonSerializer.Serialize(Draw(restored.MorphScratch)));

        NativeBattleActMeshDraw Draw(NativeBattleEffectArena retained) => NativeBattleEffect60Rendering.DrawNode(source, node, new(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, -3), NativeBattleEffectArena.Clear().MarkUnknown(0, 104), retained, Registers(), new(1, 2, 3, 4, 5), Projection, 0, 0, 0x155, 0x100, Constants);
    }

    [Fact]
    public void ACT_uses_fixed_draw_flags_and_arithmetic_column_scale_even_when_header_requests_the_other_family_policy()
    {
        byte[] bytes = Node(0x6300, 0).Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(20), 512);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24), 2049);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(36), 1024);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), 0xC08040);
        NativeBattleGfAnimationNode node = new(0, 0, bytes.ToImmutableArray());
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 };
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 104);
        NativeBattleEffectArena scratch = NativeBattleEffectArena.Clear().MarkUnknown(0, 968);
        NativeBattleActMeshDraw result = NativeBattleEffect60Rendering.DrawNode(Source(3), node, new(identity, 0), context, scratch, Registers(), new(1, 2, 3, 4, 5), Projection, 0, 0, 0x155, 0x100, Constants);
        NativeBattlePsychoBlast23NodeDraw other = NativeBattlePsychoBlast23Rendering.DrawNode(Source(3), node, new(identity, 0), context, NativeBattleEffectArena.Clear().MarkUnknown(0, 672), Registers(), new(1, 2, 3, 4, 5), Projection, 0, 0, 0x155, 0x100, Constants);
        Assert.Equal(0x20F0u, Dword(result.Temporary104, 0x14));
        Assert.Equal(0x20CCu, Dword(other.Temporary104, 0x14));
        Assert.Equal(1448, Rotation(result.Registers.Geometry, 0));
        Assert.Equal(2896, Rotation(result.Registers.Geometry, 1));
        Assert.Equal(-1449, Rotation(result.Registers.Geometry, 3));
        Assert.Equal(1449, Rotation(other.Registers.Geometry, 0));
        Assert.Equal(2896, Rotation(other.Registers.Geometry, 1));
        Assert.Equal(-1449, Rotation(other.Registers.Geometry, 3));
        Assert.Null(result.Registers.Geometry.Rotation20[18]);
    }

    [Fact]
    public void Primary_workspace_write_stops_at_motion_payload_and_preserves_saved_position_and_unknown_suffix()
    {
        byte[] data = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 8);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(12), 2);
        NativeBattleGfAnimationSource source = new(0x17A0D04, data.ToImmutableArray());
        NativeBattleModulePoolTask task = new(0, 0, null, 0, Enumerable.Repeat((byte)0xA5, 396 - 12).ToImmutableArray());
        task = task.WriteDword(0x178, 0x12345678).WriteDword(0x17C, 0x9ABCDEF0).WriteDword(0x180, 0x11223344).WriteDword(0x184, 0x55667788).MarkPayloadUnknown(0x18A, 2);
        NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(task.ReadBytes(0x94, 228).ToImmutableArray()).Initialize7016B0(source, 228);
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (current, _) => current);
        task = task.WriteBytes(0x94, advanced.Workspace.Data.AsSpan());
        Assert.Equal(1, advanced.Remaining);
        Assert.Equal(1u, task.ReadDword(0x98));
        Assert.Equal(0x12345678u, task.ReadDword(0x178));
        Assert.Equal(0x9ABCDEF0u, task.ReadDword(0x17C));
        Assert.Equal(0x11223344u, task.ReadDword(0x180));
        Assert.Equal(0x55667788u, task.ReadDword(0x184));
        Assert.Throws<NotSupportedException>(() => task.ReadWord(0x18A));
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
        return new(0x17A0D04, bytes.ToImmutableArray());
    }
}
