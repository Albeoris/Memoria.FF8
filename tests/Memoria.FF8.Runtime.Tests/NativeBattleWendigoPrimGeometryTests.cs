using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWendigoPrimGeometryTests
{
    [Fact]
    public void Screen_axes_flag_bypasses_camera_rotation_for_both_offset_and_object_axes()
    {
        NativeBattleActionMatrix camera = new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 10, 20, 30, 4096);
        NativeBattleActionMatrix local = NativeBattleWendigoPrimGeometry.Compose(Node(0, default), camera);
        NativeBattleActionMatrix screen = NativeBattleWendigoPrimGeometry.Compose(Node(0x200, default), camera);
        Assert.Equal((7, 22, 34), (local.X, local.Y, local.Z));
        Assert.Equal((12, 23, 34), (screen.X, screen.Y, screen.Z));
        Assert.Equal(camera.Rotation.ToArray(), local.Rotation.ToArray());
        Assert.Equal(new short[] { 4096, 0, 0, 0, 4096, 0, 0, 0, 4096 }, screen.Rotation);
    }

    [Fact]
    public void Alternate_rotation_is_selected_by400_and40000_retains_the_default_order()
    {
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleVector angle = new(1024, 1024, 0);
        NativeBattleActionMatrix alternate = NativeBattleWendigoPrimGeometry.Compose(Node(0x600, angle), identity);
        NativeBattleActionMatrix ordinary = NativeBattleWendigoPrimGeometry.Compose(Node(0x40200, angle), identity);
        Assert.Equal(new short[] { 0, -4096, 0, 0, 0, 4096, -4096, 0, 0 }, alternate.Rotation);
        Assert.Equal(new short[] { 0, 0, 4096, -4096, 0, 0, 0, -4096, 0 }, ordinary.Rotation);
    }

    private static NativeBattleGfAnimationNode Node(uint flags, NativeBattleVector rotation)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), flags);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 2);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), 3);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), 4);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16), rotation.X);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(18), rotation.Y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(20), rotation.Z);
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24 + axis * 2), 4096);
        }
        return new(0, 0, bytes.ToImmutableArray());
    }
}
