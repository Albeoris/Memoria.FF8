using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleThundaga105CaptureTransform(NativeBattleActionMatrix Matrix, NativeBattleCameraProjection Projection, short PriorOffsetX, short PriorOffsetY);
public sealed record NativeBattleThundaga105CaptureStage(NativeBattleStageState Stage, ImmutableArray<NativeBattleGeometryPrimitive> Packets);

/// <summary>6D5620's signed capture transform and group1 pose draw; environment packets and physical allocation remain caller-owned.</summary>
public static class NativeBattleThundaga105CaptureGeometry
{
    public static NativeBattleThundaga105CaptureTransform Transform6D5620(ImmutableArray<byte> anchor, NativeBattleThundaga105Bounds bounds, NativeBattleCameraProjection projection)
    {
        if (anchor.IsDefault || anchor.Length != 8)
        {
            throw new InvalidDataException("Native Thundaga capture requires its actual eight-byte actor anchor.");
        }
        ushort focal = projection.FocalLength ?? throw new NotSupportedException("Native Thundaga capture requires its retained projection H WORD.");
        short offsetX = projection.OffsetX ?? throw new NotSupportedException("Native Thundaga capture requires its prior screen offset X.");
        short offsetY = projection.OffsetY ?? throw new NotSupportedException("Native Thundaga capture requires its prior screen offset Y.");
        int anchorX = BinaryPrimitives.ReadInt16LittleEndian(anchor.AsSpan());
        int anchorZ = BinaryPrimitives.ReadInt16LittleEndian(anchor.AsSpan().Slice(4));
        int width = unchecked(bounds.Maximum.X - bounds.Minimum.X);
        int depth = unchecked(bounds.Maximum.Z - bounds.Minimum.Z);
        int quotientX = width / 128;
        int quotientZ = depth / 128;
        if (quotientX % 128 != 0)
        {
            quotientX = unchecked(quotientX + 1);
        }
        if (quotientZ % 128 != 0)
        {
            quotientZ = unchecked(quotientZ + 1);
        }
        int x = unchecked(unchecked(-bounds.Minimum.X - bounds.Maximum.X) / 2 / quotientX - anchorX / quotientX);
        int y = unchecked(anchorZ / quotientZ - unchecked(-bounds.Minimum.Z - bounds.Maximum.Z) / 2 / quotientZ);
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(1024));
        short sine = unchecked((short)NativeBattleCameraMath.Sine(1024));
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, cosine, unchecked((short)-sine), 0, sine, cosine], x, y, unchecked((short)focal), 4096);
        int scaleX = 0x1000000 / (unchecked(width << 12) / 128);
        int scaleZ = 0x1000000 / (unchecked(depth << 12) / 128);
        matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scaleX, 0, scaleZ);
        NativeBattleCameraProjection capture = projection with { View = matrix, OffsetX = 64, OffsetY = 64 };
        return new(matrix, capture, offsetX, offsetY);
    }

    public static NativeBattleThundaga105CaptureStage DrawGroup6D5620(NativeBattleStageState stage, ImmutableArray<NativeBattleRawMesh> geometry, NativeBattleThundaga105CaptureTransform capture, NativeBattleSummonColors colors)
    {
        stage.Validate();
        NativeBattleStageGroup group = stage.Groups[1];
        if ((group.Flags & 1) == 0)
        {
            return new(stage, []);
        }
        NativeBattlePoseSource source = group.Source ?? throw new NotSupportedException("Native Thundaga capture requires the actual current stage group1 pose source.");
        NativeBattleDecodedPose pose = source.Sample(group.Animation, group.Counter, group.PoseFlags);
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattlePoseMatrices.StageBoneMatrices(source, pose);
        group = group with { Matrices = NativeBattlePoseMatrices.ComposeStageCamera(bones, capture.Matrix, 0) };
        ImmutableArray<NativeBattleRawMesh> selected = geometry.Where(mesh => mesh.StageGroup == 1).ToImmutableArray();
        ImmutableArray<NativeBattleGeometryPrimitive> packets = NativeBattleRawGeometryPackets.Emit5099D0(selected, group.Matrices, capture.Projection, group.ColorFlags, uint.MaxValue, group.ColorDepth, 17, 16, null, colors);
        return new(stage with { Groups = stage.Groups.SetItem(1, group) }, packets);
    }
}
