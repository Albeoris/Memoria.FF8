using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed record GuardianFormCameraEnvelopeKey(long InvocationId, NativeBattleCameraPoint Eye, NativeBattleCameraPoint Target, short Roll, NativeBattleModelTransform Transform, NativeBattleVector Rotation, NativeBattleModelScaleVector? PointedScale, byte Animation, byte PoseFlags, ushort Maximum);
    private (GuardianFormCameraEnvelopeKey Key, ushort Focal)? guardianFormCameraEnvelope;

    private NativeBattleGraphicsState FrameGuardianFormIdleCamera(NativeBattleGraphicsState graphics)
    {
        if (!GuardianFormsEnabled)
        {
            return graphics;
        }
        if (graphics.GuardianFormHumanFocalToRestore is short humanFocal && ActiveAction is null && NativeCamera is { CurrentPose: { } humanPose } humanCamera && !humanCamera.Tasks.Any(task => task is not null) && !NativeOriginalEffectBusy(graphics))
        {
            NativeCamera = humanCamera.WriteEffectGlobals(humanPose with { FocalLength = humanFocal }).PublishProjection(graphics.Flags);
            graphics = graphics with { GuardianFormHumanFocalToRestore = null };
        }
        if (graphics.GuardianFormPresentation is not { Phase: 3, Mode: GuardianFormInvocationMode.EntryOnly } presentation || GetGuardianForm(presentation.ActorId) is not { Phase: BattleGuardianFormPhase.Active })
        {
            return graphics;
        }
        if (ActiveAction is not null)
        {
            return graphics with { GuardianFormPresentation = presentation with { IdleCameraRestorePending = true } };
        }
        if (NativeCamera is not { CurrentPose: { FocalLength: > 0 }, Projection: { } } camera || camera.Tasks.Any(task => task is not null) || NativeOriginalEffectBusy(graphics))
        {
            return graphics;
        }
        if (presentation.IdleCameraRestorePending)
        {
            NativeBattleCameraPose idle = presentation.Camera.CurrentPose ?? throw new InvalidDataException("GF idle camera restoration requires its saved entry pose.");
            camera = camera.WriteEffectGlobals(idle with { FocalLength = presentation.HumanFocalLength }).PublishProjection(graphics.Flags);
            NativeCamera = camera;
            presentation = presentation with { IdleCameraRestorePending = false };
            graphics = graphics with { GuardianFormPresentation = presentation };
        }
        NativeBattleCameraPose pose = camera.CurrentPose!;
        NativeBattleCameraProjection projection = camera.Projection!;
        NativeBattleGraphicsModel model = graphics.Models[presentation.ActorIndex];
        if ((model.Flags0 & 6) != 2 || (model.Flags2!.Value & 0x84) != 0)
        {
            return graphics;
        }
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, presentation.ActorIndex);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("GF camera framing requires its active body rig.");
        ImmutableArray<NativeBattleRawMesh> geometry = resource.Geometry ?? throw new NotSupportedException("GF camera framing requires its original body geometry.");
        NativeBattleSequenceState state = model.SequenceState ?? throw new NotSupportedException("GF camera framing requires its active pose owner.");
        byte animation = state.Pose ?? throw new NotSupportedException("GF camera framing requires its decoded standing animation.");
        NativeBattleModelScaleVector? pointedScale = ReadNativeModelScaleVector502AB0(graphics, model);
        ushort maximum = checked((ushort)presentation.HumanFocalLength);
        GuardianFormCameraEnvelopeKey key = new(presentation.InvocationId, pose.Eye, pose.Target, pose.Roll, model.Transform!, new(model.Rotation12!.Value, model.Rotation14!.Value, model.Rotation16!.Value), pointedScale, animation, state.PoseFlags109, maximum);
        if (guardianFormCameraEnvelope is not { } cached || cached.Key != key)
        {
            if (resource.AnimationFrames.IsDefault || animation >= resource.AnimationFrames.Length || resource.AnimationFrames[animation] == 0)
            {
                throw new NotSupportedException("GF camera framing requires the original standing animation extent.");
            }
            int counters = (state.PoseFlags109 & 1) != 0 ? resource.AnimationFrames[animation] * 2 - 1 : resource.AnimationFrames[animation];
            if (counters > byte.MaxValue)
            {
                throw new NotSupportedException("GF camera standing animation exceeds its native pose counter.");
            }
            NativeBattleActionMatrix view = NativeBattlePoseMatrices.CameraView(pose);
            ushort envelope = maximum;
            // Fit every source standing pose, including the next callback's pose, without advancing its live owner.
            for (int counter = 1; counter <= counters; counter++)
            {
                NativeBattleGraphicsModel sampled = NativeBattleBodyBoneMatrices.Publish508C90(model with { SequenceState = state with { DecodedPose = source.Sample(animation, checked((byte)counter), state.PoseFlags109) } }, source);
                sampled = sampled with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(sampled, source, pointedScale) };
                ImmutableArray<NativeBattleActionMatrix> bones = NativeBattlePoseMatrices.GeometryMatrices(sampled, source, view);
                envelope = NativeBattleGuardianFormCamera.FitFocalLength(envelope, projection, bones, geometry);
            }
            guardianFormCameraEnvelope = (key, envelope);
        }
        ushort focal = guardianFormCameraEnvelope.Value.Focal;
        if (focal != pose.FocalLength)
        {
            NativeCamera = camera.WriteEffectGlobals(pose with { FocalLength = checked((short)focal) }).PublishProjection(graphics.Flags);
        }
        return graphics;
    }
}

/// <summary>Fits the active GF's actual posed body using the same vertex projector and viewport as its native draw.</summary>
public static class NativeBattleGuardianFormCamera
{
    public static ushort FitFocalLength(ushort maximum, NativeBattleCameraProjection projection, IReadOnlyList<NativeBattleActionMatrix> bones, ImmutableArray<NativeBattleRawMesh> geometry)
    {
        if (maximum == 0 || maximum > short.MaxValue || geometry.IsDefaultOrEmpty || !geometry.Any(mesh => !mesh.Vertices.IsEmpty))
        {
            throw new InvalidDataException("GF camera framing requires a positive source focal length and body vertices.");
        }
        // Native5099D0 projects the body into this same original screen rectangle.
        NativeBattleGeometryViewport viewport = new(0, 0, 320, 216);
        bool Fits(ushort focal)
        {
            NativeBattleCameraProjection candidate = projection with { FocalLength = focal };
            foreach (NativeBattleRawMesh mesh in geometry)
            {
                foreach (NativeBattleRawVertex vertex in mesh.Vertices)
                {
                    if (vertex.Bone >= bones.Count)
                    {
                        throw new InvalidDataException("GF camera body vertex has no posed bone matrix.");
                    }
                    NativeBattleProjectedVertex projected = NativeBattleGeometryProjection.Project50F900(bones[vertex.Bone], vertex.Position, candidate, viewport);
                    if ((projected.DepthAndClip & 0x3020000) != 0)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        if (Fits(maximum))
        {
            return maximum;
        }
        if (!Fits(1))
        {
            throw new NotSupportedException("GF body cannot fit the current camera's forward view without moving its eye or target.");
        }
        int lower = 1;
        int upper = maximum - 1;
        while (lower < upper)
        {
            int middle = lower + (upper - lower + 1) / 2;
            if (Fits(checked((ushort)middle)))
            {
                lower = middle;
            }
            else
            {
                upper = middle - 1;
            }
        }
        return checked((ushort)lower);
    }
}
