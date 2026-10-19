using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiablos325WellTests
{
    [Theory]
    [InlineData(31, 34, -16384)]
    [InlineData(60, 2013, -16384)]
    [InlineData(61, 2082, -8192)]
    [InlineData(90, 4061, -8192)]
    public void Native_age_division_uniform_scale_and_Y_switch_precede_current_view(int counter, short scale, int y)
    {
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 11, 22, 33, 4096);
        NativeBattleActionMatrix actual = NativeBattleDiablos325Well.Matrix65473A((ushort)counter, -2100, view);
        Assert.Equal(new short[] { scale, 0, 0, 0, scale, 0, 0, 0, scale }, actual.Rotation);
        Assert.Equal((11, y + 22, -2067), (actual.X, actual.Y, actual.Z));
    }

    [Fact]
    public void Uninitialized_scratch_RGB_publishes_unknown_controls_and_noncue_leaf_keeps_raw_color()
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models);
        NativeBattleDiablos325Resources resources = new(new(0xFD8584, new byte[0x3890].ToImmutableArray()), new(new string('0', 64), [0], [0, 0, 0, 0])) { WellGeometry = Geometry() };
        frame = frame with { Effect = frame.Effect with { Binding = frame.Effect.Binding with { Diablos325Resources = resources } } };
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 16384, 1000 - frame.Memory.Diablos325!.CenterZ51D4, 4096);
        NativeBattleDiablos325WellDraw result = NativeBattleDiablos325Well.Emit65473A(frame, 31, new(view, 288, 160, 108));
        NativeBattleGeometryPrimitive primitive = Assert.Single(result.Packets.Attempts).Primitive!;
        Assert.NotNull(primitive);
        Assert.Equal(0x20112233u, primitive.Words[1]);
        Assert.Equal(79, primitive.Bucket);
        NativeBattleModelRendererRegisters registers = result.Memory.PrimitiveProcess!.ModelRendererRegisters!;
        Assert.Null(registers.FarRed);
        Assert.Null(registers.FarGreen);
        Assert.Null(registers.FarBlue);
    }

    private static ImmutableArray<byte> Geometry()
    {
        byte[] bytes = new byte[76];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 32);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 3);
        short[] values = [-1024, -1024, 0, 0, 1024, -1024, 0, 0, 0, 1024, 0, 0];
        for (int index = 0; index < values.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 2), values[index]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(32), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(36), 0x20112233);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(42), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(44), 4);
        return bytes.ToImmutableArray();
    }
}
