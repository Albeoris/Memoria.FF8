namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private double EncounterCameraElevation => EncounterElevationFor(NativeCamera);

    private double EncounterElevationFor(NativeBattleCameraState? camera)
    {
        bool nativeSceneView = camera is { Mask: 0, ForceCamera: false, CurrentPose: not null, CapturedPose: not null } && camera.CurrentPose == camera.CapturedPose;
        return NativeEffectMemory?.Active == 1 && !nativeSceneView ? 0 : definition.EncounterMod?.Camera?.ElevationDegrees ?? 0;
    }

    private NativeBattleCameraPose? EncounterPresentationCamera => NativeCamera?.PublishedPresentationPose ?? PrepareEncounterCamera(NativeCamera);

    private NativeBattleCameraPose? PrepareEncounterCamera(NativeBattleCameraState? camera)
    {
        if (camera?.CurrentPose is not { } pose)
        {
            return null;
        }
        pose = EncounterCameraPresentation.Elevate(pose with { Roll = camera.Roll }, EncounterElevationFor(camera));
        if (definition.EncounterMod?.Camera is null || NativeEffectMemory?.Active != 1 || NativeGraphics is not { OriginalEffect: { ReturnedDescriptor: not null, Parameters: { } parameters } } graphics)
        {
            return pose;
        }
        if (NativeEffectMemory.Scan?.Controller is not null && ((graphics.UiServices?.Flags942 ?? 0) & 0x100) != 0)
        {
            return pose;
        }
        List<NativeBattleVector> points = [];
        ushort mask = (ushort)(parameters.TargetMask7A | 1 << parameters.Actor78);
        for (int index = 0; index < graphics.Models.Length; index++)
        {
            NativeBattleGraphicsModel model = graphics.Models[index];
            if ((mask & 1 << index) == 0 || (model.Flags0 & 2) == 0)
            {
                continue;
            }
            NativeBattleEffectLocalBounds bounds = model.GeometryBounds52 ?? throw new NotSupportedException("Encounter target framing requires the loaded recipient's actual body bounds.");
            NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Encounter target framing requires the loaded recipient's actual world matrix.");
            foreach (short x in new[] { bounds.Minimum.X, bounds.Maximum.X })
            {
                foreach (short y in new[] { bounds.Minimum.Y, bounds.Maximum.Y })
                {
                    foreach (short z in new[] { bounds.Minimum.Z, bounds.Maximum.Z })
                    {
                        points.Add(NativeBattlePoseMatrices.TransformCameraPoint(world, new(x, y, z), false));
                    }
                }
            }
        }
        ushort focal = camera.FocalLength is short value ? checked((ushort)value) : camera.Projection?.FocalLength ?? throw new NotSupportedException("Encounter target framing requires its actual lens.");
        return EncounterCameraPresentation.FrameTargets(pose, points, focal, camera.Projection?.OffsetX ?? 160, camera.Projection?.OffsetY ?? 108);
    }
}
