using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePsychoBlast23RenderingTests
{
    private static NativeBattleSummonColors Constants => new(0, 0, 1f / 4096f, BitConverter.Int64BitsToDouble(0x4330000080000000L));
    private static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);

    [Theory]
    [InlineData(0, 0x170001C)]
    [InlineData(4096, 0x1700034)]
    [InlineData(2048, 0x2704220)]
    public void ACT_selects_original_frames_or_canonical_morph_scratch_and_preserves_W(int ratio, uint selectedAddress)
    {
        NativeBattleGfAnimationSource source = Source();
        NativeBattleGfAnimationNode node = Node(0x200, ratio);
        NativeBattleEffectArena scratch = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 672).ToArray()).MarkUnknown(14, 2);
        NativeBattleGeometryRegisterStep registers = new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown() with { Material = 0xAA123456, Rgb = [0x11, 0x22, 0x33] });
        NativeBattlePsychoBlast23ActContext root = new(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, -7);
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().MarkUnknown(0, 104);
        NativeBattlePsychoBlast23NodeDraw result = Draw(scratch);
        Assert.Equal(selectedAddress, BinaryPrimitives.ReadUInt32LittleEndian(result.Temporary104.Read(4, 4)));
        Assert.Equal(0x2030u, BinaryPrimitives.ReadUInt32LittleEndian(result.Temporary104.Read(0x14, 4)));
        Assert.NotNull(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Equal(new int?[] { 0, 0, 2000 }, result.Registers.Geometry.Translation);
        if (ratio == 2048)
        {
            Assert.Equal(-350, BinaryPrimitives.ReadInt16LittleEndian(result.MorphScratch.Read(0, 2)));
            Assert.Equal(-350, BinaryPrimitives.ReadInt16LittleEndian(result.MorphScratch.Read(2, 2)));
            Assert.Equal(new byte[] { 0xA5, 0xA5 }, result.MorphScratch.Read(6, 2));
            Assert.All(result.MorphScratch.ReadSnapshot(14, 2).Known, Assert.False);
            Assert.Null(result.Registers.Geometry.V2[6]);
        }
        else
        {
            Assert.Equal(scratch.Read(0, 6), result.MorphScratch.Read(0, 6));
        }
        NativeBattleEffectArena restored = JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(result.MorphScratch))!;
        Assert.Equal(JsonSerializer.Serialize(Draw(result.MorphScratch)), JsonSerializer.Serialize(Draw(restored)));

        NativeBattlePsychoBlast23NodeDraw Draw(NativeBattleEffectArena retained) => NativeBattlePsychoBlast23Rendering.DrawNode(source, node, root, temporary, retained, registers, new(1, 2, 3, 4, 5), Projection, 0, 0, 0x155, 0x100, Constants);
    }

    [Fact]
    public void ACT_direct_offset_branch_does_not_rotate_node_offset_or_basis_with_root_rotation()
    {
        NativeBattleActionMatrix basis = NativeBattleGfMatrices.RotateY56D020(NativeBattleCuragaMatrices.Identity8DD770(), 1024) with { X = 100, Y = 200, Z = 2000 };
        byte[] data = Node(0x200, 0).Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(8), 10);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(10), 20);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(12), 30);
        NativeBattleGfAnimationNode node = new(0, 0, data.ToImmutableArray());
        NativeBattlePsychoBlast23NodeDraw result = NativeBattlePsychoBlast23Rendering.DrawNode(Source(), node, new(basis, 0), NativeBattleEffectArena.Clear().MarkUnknown(0, 104), NativeBattleEffectArena.Clear().MarkUnknown(0, 672), new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown()), new(1, 2, 3, 4, 5), Projection, 0, 0, 0x155, 0x100, Constants);
        Assert.Equal(new int?[] { 110, 220, 2030 }, result.Registers.Geometry.Translation);
        Assert.Equal(new byte?[] { 0, 16, 0, 0, 0, 0 }, result.Registers.Geometry.Rotation20.Take(6));
        Assert.Null(result.Registers.Geometry.Rotation20[18]);
    }

    [Fact]
    public void Invisible_ACT_nodes_skip_when_all_scale_words_zero_or_fully_faded_to_black()
    {
        byte[] data = Node(0, 0).Data.ToArray();
        data.AsSpan(24, 6).Clear();
        Assert.False(NativeBattlePsychoBlast23Rendering.Visible(new(0, 0, data.ToImmutableArray())));
        data = Node(0, 0).Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(36), 4096);
        Assert.False(NativeBattlePsychoBlast23Rendering.Visible(new(0, 0, data.ToImmutableArray())));
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(32), 0x123456);
        Assert.True(NativeBattlePsychoBlast23Rendering.Visible(new(0, 0, data.ToImmutableArray())));
    }

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

    private static NativeBattleGfAnimationSource Source()
    {
        byte[] bytes = new byte[120];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 60);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), 3);
        short[] xy = [-400, -400, 400, -400, -400, 400];
        for (int frame = 0; frame < 2; frame++)
        {
            for (int vertex = 0; vertex < 3; vertex++)
            {
                int offset = 28 + frame * 24 + vertex * 8;
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), (short)(xy[vertex * 2] + frame * 100));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), (short)(xy[vertex * 2 + 1] + frame * 100));
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 6), 0x1234);
            }
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(76), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(80), 0x20332211);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(86), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(88), 2);
        return new(0x1700000, bytes.ToImmutableArray());
    }
}
