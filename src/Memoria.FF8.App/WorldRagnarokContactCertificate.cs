using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>548CD0 ordered dense-object scan for the actual controlled50 allocation.</summary>
public static class WorldRagnarokContactCertificate
{
    public static int Evaluate(WorldAssets assets, NativeRagnarokWorldRuntimeState runtime, IReadOnlyList<WorldNativeGardenProbe> probes, NativeWorldPathProjectionCertificate projection, int padding = 0, bool trainContactsExcluded = false, int selectedEntity = 50, int currentEntity = 50, bool forceCircle = false, int ignoredObjectIndex = -1)
    {
        runtime.Validate();
        if (probes.Count != 5 || padding is not (0 or 200))
        {
            throw new InvalidDataException("Ragnarok contacts require five original radius256 probes and native padding.");
        }
        if (selectedEntity is not (0 or 50) && !NativeCarWorldRuntimeState.IsSupportedEntity(selectedEntity) || currentEntity is not (0 or 50) && !NativeCarWorldRuntimeState.IsSupportedEntity(currentEntity))
        {
            throw new NotSupportedException("Ragnarok contact requires its original controlled/selected foot, car or airship entity.");
        }
        NativeWorldContactShape selected = assets.ReadNativeContactShape(WorldAssets.NativeGardenEntityShape(selectedEntity));
        NativeWorldPathWords[] points = probes.Select(probe => projection.RelativeWords(new NativeWorldPathPosition(probe.X, probe.Y, probe.GraphicsZ))).ToArray();
        for (int index = 0; index < runtime.Objects.Length; index++)
        {
            NativeRagnarokWorldObject item = runtime.Objects[index];
            if (index == ignoredObjectIndex || item.ShapeId is 0 or 2 or 3 or 94 || item.ShapeId == 1 && (selectedEntity == 50 || currentEntity == 50) || !projection.IsNear(item.Position))
            {
                continue;
            }
            NativeWorldPathNodeProjection anchor = projection.ProjectNode(item.Position) ?? throw new NotSupportedException(projection.UnsupportedReason);
            NativeWorldContactShape target = assets.ReadNativeContactShape(item.ShapeId);
            //54BA30/549320 are shared shape primitives, independent of the
            //Garden allocation/controller. Controlled50 has no ignore-height override.
            if (WorldGardenContactCertificate.EvaluateObject(target, item.Yaw, item.ShapeId, selected, selectedEntity, points, anchor.Words, padding, forceCircle))
            {
                return index;
            }
        }
        if (runtime.BackgroundRails is { } rails)
        {
            return WorldStationContactCertificate.EvaluateRailTail(assets, rails, runtime.Objects.Length, points[0], probes[0].Y, projection.IsNear, padding, NativeWorldTerrain.ProbeRadius(selectedEntity), forceCircle, selected);
        }
        if (!trainContactsExcluded)
        {
            throw new NotSupportedException("Ragnarok contact requires actual rail banks or original camera-block exclusion before skipping548CD0's train tail.");
        }
        return -1;
    }
}
