using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleHoly175CaptureDirectoryEntry(int TableBaseBucket, byte DepthShift);
public sealed record NativeBattleHoly175CaptureStage(NativeBattleStageState Stage, ImmutableArray<NativeBattleGeometryPrimitive> Packets);

public static class NativeBattleHoly175CaptureGeometry
{
    public static NativeBattleHoly175CaptureStage Draw5D29E0(NativeBattleStageState stage, ImmutableArray<NativeBattleRawMesh> geometry, ImmutableArray<NativeBattleHoly175CaptureDirectoryEntry> directory, NativeBattleActionMatrix currentView, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        stage.Validate();
        currentView.Validate();
        if (directory.IsDefault || directory.Length != 4 || directory.Any(entry => entry is null || entry.TableBaseBucket < 0 || entry.DepthShift > 31))
        {
            throw new InvalidDataException("Native Holy capture requires its actual four-group ordering directory.");
        }
        if (geometry.IsDefault || geometry.Any(mesh => mesh is null || mesh.StageGroup is null or < 0 or >= 4))
        {
            throw new NotSupportedException("Native Holy capture requires its actual stage geometry group owners.");
        }
        NativeBattleCameraProjection capture = projection with { View = currentView, OffsetX = 160, OffsetY = 116 };
        ImmutableArray<NativeBattleStageGroup>.Builder groups = stage.Groups.ToBuilder();
        ImmutableArray<NativeBattleGeometryPrimitive>.Builder packets = ImmutableArray.CreateBuilder<NativeBattleGeometryPrimitive>();
        for (int index = 0; index < 4; index++)
        {
            NativeBattleStageGroup group = groups[index];
            NativeBattlePoseSource source = group.Source ?? throw new NotSupportedException("Native Holy capture requires every actual stage pose source, including invisible groups.");
            NativeBattleDecodedPose pose = source.Sample(group.Animation, group.Counter, group.PoseFlags);
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattlePoseMatrices.StageBoneMatrices(source, pose);
            group = group with { Matrices = NativeBattlePoseMatrices.ComposeStageCamera(bones, currentView, 0) };
            groups[index] = group;
            if ((group.Flags & 1) == 0)
            {
                continue;
            }
            NativeBattleHoly175CaptureDirectoryEntry draw = directory[index];
            ImmutableArray<NativeBattleRawMesh> selected = geometry.Where(mesh => mesh.StageGroup == index).ToImmutableArray();
            packets.AddRange(NativeBattleRawGeometryPackets.Emit5099D0(selected, group.Matrices, capture, group.ColorFlags, uint.MaxValue, group.ColorDepth, draw.TableBaseBucket, draw.DepthShift, null, colors));
        }
        return new(stage with { Groups = groups.ToImmutable() }, packets.ToImmutable());
    }
}
