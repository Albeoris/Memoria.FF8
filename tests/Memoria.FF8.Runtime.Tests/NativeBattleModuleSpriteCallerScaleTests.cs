using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModuleSpriteCallerScaleTests
{
    [Theory]
    [InlineData(-4096, 4096)]
    [InlineData(4096, -4096)]
    [InlineData(-2049, 1023)]
    public void Opt_in_scales_signed_rows_after_roll_preserving_T_padding_and_physical_scale_Z(int x, int y)
    {
        var fixture = Fixture(2000, false);
        var matrix = NativeBattleGfMatrices.Euler56CE30(new(123, 456, 789)) with { X = 20, Y = 30, Z = 2000 };
        var memory = fixture.Memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(fixture.Memory.GeometryRegisters!, matrix, true, true, 0x1234) };
        var context = fixture.Temporary.Write(0x24, [2, 0]).Write(12, BitConverter.GetBytes(x)).Write(16, BitConverter.GetBytes(y)).MarkUnknown(20, 4);
        var actual = NativeBattleModuleSpriteDrawing.Draw571C80(memory, context, _ => fixture.Resource, Projection, 321, allowCallerScale: true);
        var rolled = NativeBattleGfMatrices.RotateZ56D090(matrix, -321);
        int[] scale = [x, y, 4096];
        for (int index = 0; index < 9; index++)
        {
            Assert.Equal(unchecked((short)(unchecked(rolled.Rotation[index] * scale[index / 3]) >> 12)), BinaryPrimitives.ReadInt16LittleEndian(actual.Temporary180.Read(0x84 + index * 2, 2)));
        }
        Assert.Equal(new byte[] { 0x34, 0x12 }, actual.Temporary180.Read(0x96, 2));
        Assert.Equal(4096, I32(actual.Temporary180, 20));
        Assert.Equal(new int[] { 20, 30, 2000 }, Enumerable.Range(0, 3).Select(axis => I32(actual.Temporary180, 0x98 + axis * 4)));
    }

    [Fact]
    public void Default_180_and_D8_reject_bit2_and_clear_bit_does_not_read_scale_DWORDs()
    {
        var fixture = Fixture(2000, false);
        var context = fixture.Temporary.Write(0x24, [2, 0]);
        Assert.Throws<InvalidDataException>(() => NativeBattleModuleSpriteDrawing.Draw571C80(fixture.Memory, context, _ => fixture.Resource, Projection, 0));
        var d8 = NativeBattleEffectNodeContextMemory.Copy(context, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 216), 0, 180);
        Assert.Throws<InvalidDataException>(() => NativeBattleProjectedSpriteD8.Draw(new(fixture.Memory, d8), _ => fixture.Resource, Projection, 0, 17));
        var actual = NativeBattleModuleSpriteDrawing.Draw571C80(fixture.Memory, fixture.Temporary, _ => fixture.Resource, Projection, 0, allowCallerScale: true);
        Assert.Throws<NotSupportedException>(() => actual.Temporary180.Read(12, 12));
        Assert.Throws<NotSupportedException>(() => NativeBattleModuleSpriteDrawing.Draw571C80(fixture.Memory, context, _ => fixture.Resource, Projection, 0, allowCallerScale: true));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2000)]
    public void Unit_bit2_matches_canonical_loaded_matrix_packets_depths_and_rejected_prefixes(int depth)
    {
        var fixture = Fixture(depth, false);
        var scaled = fixture.Temporary.Write(0x24, [2, 0]).Write(12, BitConverter.GetBytes(4096)).Write(16, BitConverter.GetBytes(4096));
        var actual = NativeBattleModuleSpriteDrawing.Draw571C80(fixture.Memory, scaled, _ => fixture.Resource, Projection, 0, allowCallerScale: true);
        var expected = NativeBattleModuleSpriteDrawing.Draw571C80(fixture.Memory, fixture.Temporary, _ => fixture.Resource, Projection, 0);
        Assert.Equal(JsonSerializer.Serialize(expected.Batch), JsonSerializer.Serialize(actual.Batch));
        Assert.Equal(JsonSerializer.Serialize(expected.Memory), JsonSerializer.Serialize(actual.Memory));
    }

    private static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);

    private static (NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary, NativeBattleModuleResource Resource) Fixture(int depth, bool extended)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = depth };
        memory = memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), matrix, true, true, 0x1234), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        byte[] bytes = new byte[58];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 0xFFFF);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(14), extended ? 0x80000002u : 2u);
        for (int quad = 0; quad < 2; quad++)
        {
            Span<byte> row = bytes.AsSpan(18 + 20 * quad, 20);
            row[0] = row[2] = 2;
            row[1] = 100;
            row[3] = 0x2C;
            row[4] = 10;
            row[5] = 20;
            BinaryPrimitives.WriteUInt16LittleEndian(row.Slice(14), quad == 0 ? (ushort)0x123 : (ushort)0xD23);
            if (quad == 1)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(row.Slice(12), 1024);
                BinaryPrimitives.WriteUInt16LittleEndian(row.Slice(16), 4096);
                BinaryPrimitives.WriteUInt16LittleEndian(row.Slice(18), 2048);
            }
        }
        NativeBattleModuleResource resource = new(0, 0x1048C94, 0, 0, bytes.ToImmutableArray());
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().MarkUnknown(0, 180).Write(0, BitConverter.GetBytes(resource.Address)).Write(4, [0, 0]).Write(0x24, [0, 0]);
        return (memory, temporary, resource);
    }

    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static int I32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt32LittleEndian(arena.Read(offset, 4));
}
