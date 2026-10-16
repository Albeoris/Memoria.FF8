using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGfMatrixTests
{
    private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 100, 200, 300, 4096);

    [Fact]
    public void OriginalEulerUsesItsOwnSignedAxesAndZXYComposition()
    {
        Assert.Equal((0, 0, -4096), NativeBattleGfMatrices.Rotate56C3D0(NativeBattleGfMatrices.Euler701310(new(1024, 0, 0)), new(0, 4096, 0)));
        Assert.Equal((4096, 0, 0), NativeBattleGfMatrices.Rotate56C3D0(NativeBattleGfMatrices.Euler701310(new(0, 1024, 0)), new(0, 0, 4096)));
        Assert.Equal((0, -4096, 0), NativeBattleGfMatrices.Rotate56C3D0(NativeBattleGfMatrices.Euler701310(new(0, 0, 1024)), new(4096, 0, 0)));
        Assert.Equal(new short[] { -4096, 0, 0, 0, 0, -4096, 0, -4096, 0 }, NativeBattleGfMatrices.Euler701310(new(1024, 1024, 1024)).Rotation.ToArray());
    }

    [Fact]
    public void CallbackKeepsRowSarScalingDistinctFromRoundedColumnScaling()
    {
        NativeBattleGfAnimationNode node = Node(0x1000, new(0, 1024, 0), new(5120, 4096, 8192));
        NativeBattleActionMatrix rows = NativeBattleGfMatrices.Compose6C6060(node, Identity, Identity, new(0, -5000, 0));
        Assert.Equal(new short[] { 0, 0, 5120, 0, 4096, 0, -8192, 0, 0 }, rows.Rotation.ToArray());
        NativeBattleActionMatrix columns = NativeBattleGfMatrices.Compose6C6060(Node(0x1100, new(0, 1024, 0), new(5120, 4096, 8192)), Identity, Identity, new(0, -5000, 0));
        Assert.Equal(new short[] { 0, 0, 8192, 0, 4096, 0, -5120, 0, 0 }, columns.Rotation.ToArray());
        Assert.Equal((110, -4780, 330), (rows.X, rows.Y, rows.Z));
    }

    [Fact]
    public void CallbackDistinguishesBillboardCameraAndDirectPositionFlags()
    {
        NativeBattleActionMatrix view = NativeBattleGfMatrices.Euler701310(new(0, 1024, 0)) with { X = 100, Y = 200, Z = 300 };
        NativeBattleActionMatrix billboard = NativeBattleGfMatrices.Compose6C6060(Node(0x1000, new(0, 0, 0), new(4096, 4096, 4096)), view, Identity, new(1, 2, 3));
        Assert.Equal((113, 222, 329), (billboard.X, billboard.Y, billboard.Z));
        NativeBattleActionMatrix camera = NativeBattleGfMatrices.Compose6C6060(Node(0, new(0, 0, 0), new(4096, 4096, 4096)), view, null, new(1, 2, 3));
        Assert.Equal((133, 222, 289), (camera.X, camera.Y, camera.Z));
        NativeBattleActionMatrix direct = NativeBattleGfMatrices.Compose6C6060(Node(0x200, new(0, 0, 0), new(4096, 4096, 4096)), view, null, new(1, 2, 3));
        Assert.Equal((113, 222, 329), (direct.X, direct.Y, direct.Z));
        Assert.Equal(Identity.Rotation.ToArray(), direct.Rotation.ToArray());
        Assert.Throws<NotSupportedException>(() => NativeBattleGfMatrices.Compose6C6060(Node(0x40000, new(0, 0, 0), new(4096, 4096, 4096)), Identity, Identity, new(0, 0, 0)));
    }

    private static NativeBattleGfAnimationNode Node(uint header, NativeBattleVector angles, NativeBattleVector scale)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        Vector(8, new(10, 20, 30));
        Vector(16, angles);
        Vector(24, scale);
        return new(0, 0, bytes.ToImmutableArray());

        void Vector(int offset, NativeBattleVector value)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value.X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), value.Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 4), value.Z);
        }
    }
}
