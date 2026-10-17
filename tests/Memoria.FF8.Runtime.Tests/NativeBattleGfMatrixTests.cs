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

    [Fact]
    public void SecondWindowContextUsesFullDwordTranslationAndFullPriorViewComposition()
    {
        NativeBattleActionMatrix context = NativeBattleGfMatrices.SecondWindowContext6C3C47(Identity, 40000);
        Assert.Equal(new short[] { -4096, 0, 0, 0, 4096, 0, 0, 0, -4096 }, context.Rotation.ToArray());
        Assert.Equal((600, 200, 40300), (context.X, context.Y, context.Z));
        NativeBattleActionMatrix view = NativeBattleGfMatrices.Euler701310(new(0, 1024, 0)) with { X = 100, Y = 200, Z = 300 };
        context = NativeBattleGfMatrices.SecondWindowContext6C3C47(view, 40000);
        Assert.Equal((40100, 200, -200), (context.X, context.Y, context.Z));
    }

    [Fact]
    public void SecondCallbackScalesAfterCompositionAndAddsOnlyPassedContextTranslation()
    {
        NativeBattleActionMatrix context = NativeBattleGfMatrices.Euler701310(new(0, 1024, 0)) with { X = 1000, Y = 2000, Z = 3000 };
        NativeBattleActionMatrix rows = NativeBattleGfMatrices.Compose6C53A0(Node(0, new(0, 0, 0), new(5120, 4096, 8192)), context, null);
        Assert.Equal(new short[] { 0, 0, 5120, 0, 4096, 0, -8192, 0, 0 }, rows.Rotation.ToArray());
        Assert.Equal((1030, 2020, 2990), (rows.X, rows.Y, rows.Z));
        NativeBattleActionMatrix columns = NativeBattleGfMatrices.Compose6C53A0(Node(0x100, new(0, 0, 0), new(5120, 4096, 8192)), context, null);
        Assert.Equal(new short[] { 0, 0, 8192, 0, 4096, 0, -5120, 0, 0 }, columns.Rotation.ToArray());
        NativeBattleActionMatrix billboard = NativeBattleGfMatrices.Compose6C53A0(Node(0x1000, new(0, 0, 0), new(4096, 4096, 4096)), context, Identity);
        Assert.Equal((1010, 2020, 3030), (billboard.X, billboard.Y, billboard.Z));
        NativeBattleActionMatrix billboardPriority = NativeBattleGfMatrices.Compose6C53A0(Node(0x1200, new(0, 0, 0), new(4096, 4096, 4096)), context, context);
        Assert.Equal((1030, 2020, 2990), (billboardPriority.X, billboardPriority.Y, billboardPriority.Z));
        Assert.Equal(context.Rotation.ToArray(), billboardPriority.Rotation.ToArray());
    }

    [Fact]
    public void ThirdWindowPassesItsBaseCoordinatesToTheSameCameraComposition()
    {
        NativeBattleGfAnimationNode node = Node(0, new(0, 0, 0), new(4096, 4096, 4096));
        NativeBattleActionMatrix first = NativeBattleGfMatrices.Compose6C6060(node, Identity, null, new(0, -5000, 0));
        NativeBattleActionMatrix third = NativeBattleGfMatrices.Compose6C6060(node, Identity, null, new(0, 0, 3690));
        Assert.Equal((110, -4780, 330), (first.X, first.Y, first.Z));
        Assert.Equal((110, 220, 4020), (third.X, third.Y, third.Z));
        Assert.Equal(first.Rotation.ToArray(), third.Rotation.ToArray());
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
