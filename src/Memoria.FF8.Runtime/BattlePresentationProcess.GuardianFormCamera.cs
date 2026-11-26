using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private sealed record GuardianFormCameraEnvelopeKey(long InvocationId, NativeBattleCameraPoint Eye, NativeBattleCameraPoint Target, short Roll, NativeBattleModelTransform Transform, NativeBattleVector Rotation, NativeBattleModelScaleVector? PointedScale, byte Animation, byte PoseFlags, ushort Maximum);
    private (GuardianFormCameraEnvelopeKey Key, ushort Focal)? guardianFormCameraEnvelope;

    private NativeBattleGraphicsState FrameGuardianFormIdleCamera(NativeBattleGraphicsState graphics, HostedGuardianFormFrameFacts? facts = null)
    {
        if (!resources.GuardianFormsEnabled)
        {
            return graphics;
        }
        bool hasAction = facts?.HasAction == true;
        if (graphics.GuardianFormHumanFocalToRestore is short humanFocal && !hasAction && NativeCamera is { CurrentPose: { } humanPose } humanCamera && !humanCamera.Tasks.Any(task => task is not null) && !NativeOriginalEffectBusy(graphics))
        {
            NativeCamera = humanCamera.WriteEffectGlobals(humanPose with { FocalLength = humanFocal }).PublishProjection(graphics.Flags, EncounterCameraElevation);
            graphics = graphics with { GuardianFormHumanFocalToRestore = null };
        }
        if (graphics.GuardianFormPresentation is not { Phase: 3, Mode: GuardianFormInvocationMode.EntryOnly } presentation)
        {
            return graphics;
        }
        bool active = facts is { Phase: BattleGuardianFormPhase.Active, Form: { } owner } && owner.Actor == presentation.Actor && owner.InvocationId == presentation.InvocationId && owner.VariantId == presentation.VariantId;
        if (!active)
        {
            return graphics;
        }
        if (hasAction)
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
            camera = camera.WriteEffectGlobals(idle with { FocalLength = presentation.HumanFocalLength }).PublishProjection(graphics.Flags, EncounterCameraElevation);
            NativeCamera = camera;
            presentation = presentation with { IdleCameraRestorePending = false };
            graphics = graphics with { GuardianFormPresentation = presentation };
        }
        NativeBattleCameraPose pose = camera.CurrentPose!;
        NativeBattleCameraProjection projection = camera.Projection!;
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(presentation.Actor);
        if ((model.Flags0 & 6) != 2 || (model.Flags2!.Value & 0x84) != 0)
        {
            return graphics;
        }
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, presentation.Actor);
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
            NativeCamera = camera.WriteEffectGlobals(pose with { FocalLength = checked((short)focal) }).PublishProjection(graphics.Flags, EncounterCameraElevation);
        }
        return graphics;
    }
}
