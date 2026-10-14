using System.Numerics;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

public sealed record NativeMotionAuthority(int Version, byte MovementDirection, byte FacingHold);

/// <summary>Script-driven field actor state. Direction uses FF8's 256-unit full turn.</summary>
public sealed class ActorState
{
    [JsonConstructor]
    public ActorState(int objectId, int label)
    {
        ObjectId = objectId;
        Label = label;
    }

    public int ObjectId { get; }
    public int Label { get; }
    [JsonInclude]
    public int ModelId { get; internal set; } = -1;
    [JsonInclude]
    public int CharacterId { get; internal set; } = -2;
    [JsonInclude]
    public bool Active { get; internal set; } = true;
    [JsonInclude]
    public bool Visible { get; internal set; } = true;
    [JsonInclude]
    public bool AdditionalControlDisabled { get; internal set; }
    [JsonInclude]
    public float X { get; internal set; }
    [JsonInclude]
    public float Y { get; internal set; }
    [JsonInclude]
    public float Z { get; internal set; }
    [JsonInclude]
    public int WalkmeshTriangle { get; internal set; }
    [JsonInclude]
    public float Direction { get; internal set; }
    [JsonInclude]
    public Vector3 DisplayOffset { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeDisplayOffsetState? NativeDisplayOffset { get; internal set; }
    [JsonInclude]
    public int AnimationId { get; internal set; }
    [JsonInclude]
    public float AnimationFrame { get; internal set; }
    [JsonInclude]
    public int AnimationSpeed { get; internal set; } = 16;
    [JsonInclude]
    public bool TalkEnabled { get; internal set; } = true;
    [JsonInclude]
    public bool PushEnabled { get; internal set; } = true;
    [JsonInclude]
    public bool NativePushContactPending { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? NativeLineFacingDirection { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? NativeLineFacingContact { get; internal set; }
    [JsonInclude]
    public bool CollisionEnabled { get; internal set; } = true;
    [JsonInclude]
    public bool FootstepsEnabled { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeFootstepPairState? NativeFootstepPair { get; internal set; }
    [JsonInclude]
    public int TalkRadius { get; internal set; } = 128;
    [JsonInclude]
    public int PushRadius { get; internal set; } = 48;
    [JsonInclude]
    public int MovementSpeed { get; internal set; } = 1000;
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public short? NativeMovementBaseSpeed { get; internal set; }
    /// <summary>Shared native actor WORD516; unknown until an executed writer supplies it.</summary>
    [JsonInclude]
    [JsonRequired]
    public short? NativeMovementAccelerationTarget { get; internal set; }
    [JsonInclude]
    public byte MovementDirectionLimit { get; internal set; }
    [JsonInclude]
    public NativeScriptMovementState? NativeScriptMovement { get; internal set; }
    /// <summary>Shared physical WORD542, allocated by the fresh native actor loader and written by motion, DOFFSET and RET.</summary>
    [JsonInclude]
    [JsonRequired]
    public ushort? NativeMovementCompletion542 { get; internal set; }
    /// <summary>Shared physical WORD508; JUMP selects its landing triangle and DOFFSET writes its inline low word.</summary>
    [JsonInclude]
    [JsonRequired]
    public ushort? NativeLandingTriangle508 { get; internal set; }
    /// <summary>Shared signed WORD474: mode1 turn accumulation and mode2 jump elapsed counter, preserved by JOIN/SPLIT and request backups.</summary>
    [JsonInclude]
    [JsonRequired]
    public short? NativeTurnAccumulator474 { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeMovementRequestState? NativeMovementRequest { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeJumpState? NativeJump { get; internal set; }
    [JsonInclude]
    public NativeMotionAuthority? NativeMotionAuthority { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeJoinedPosition? NativeJoinedPosition { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeJoinedPosition? NativeSplitPosition { get; internal set; }
    [JsonInclude]
    public int MovementFlags { get; internal set; }
    [JsonInclude]
    public bool MovementIgnoresWalkmesh { get; internal set; }
    [JsonInclude]
    public int GetaParameter { get; internal set; }
    /// <summary>Native actor WORD+544, copied to the model's separate root Z translation word.</summary>
    [JsonInclude]
    public short RootTranslationZ { get; internal set; }
    [JsonInclude]
    public int ShadeLevel { get; internal set; } = 96;
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeActorShadowSet? NativeShadowSet { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeActorShadowForm? NativeShadowForm { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeActorShadowLevel? NativeShadowLevel { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeActorShadowAllocation? NativeShadowAllocation { get; internal set; }
    [JsonInclude]
    public Vector3 PolygonColor { get; internal set; } = new(128, 128, 128);
    [JsonInclude]
    public int FaceFirstAngle { get; internal set; }
    [JsonInclude]
    public int FaceSecondAngle { get; internal set; }
    [JsonInclude]
    public int FaceElapsed { get; internal set; }
    [JsonInclude]
    public Vector3 FaceLimits { get; internal set; }
    [JsonInclude]
    public int FaceStateVersion { get; internal set; } = 1;
    [JsonInclude]
    public bool FaceReturning { get; internal set; }
    [JsonInclude]
    public int FacePendingBaseline { get; internal set; }
    [JsonInclude]
    public Vector3? FaceTargetPoint { get; internal set; }
    [JsonInclude]
    public int FacePitchTarget { get; internal set; }
    [JsonInclude]
    public int FaceYawTarget { get; internal set; }
    [JsonInclude]
    public int FaceDuration { get; internal set; }
    [JsonInclude]
    public int FaceProgress { get; internal set; }
    [JsonInclude]
    public Vector3? FirstControlRotation { get; internal set; }
    [JsonInclude]
    public Vector3? LineStart { get; internal set; }
    [JsonInclude]
    public Vector3? LineEnd { get; internal set; }
    [JsonInclude]
    public bool LineEnabled { get; internal set; } = true;
    [JsonInclude]
    public bool DoorLineEnabled { get; internal set; } = true;
    [JsonInclude]
    public bool DoorLineContact { get; internal set; }
    [JsonInclude]
    public bool DoorLineEntryPending { get; internal set; }
    [JsonInclude]
    public bool LineContact { get; internal set; }
    [JsonInclude]
    public int NativeLineContactVersion { get; internal set; }
    [JsonInclude]
    public bool NativeLineCrossingPending { get; internal set; }
    [JsonInclude]
    public int LineOperation { get; internal set; } = -1;

    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSetLineReceipt? NativeSetLineReceipt { get; internal set; }

    [JsonInclude]
    public int BaseAnimationId { get; internal set; } = 42;
    [JsonInclude]
    public int WalkAnimationId { get; internal set; } = 42;
    [JsonInclude]
    public int RunAnimationId { get; internal set; } = 42;
    [JsonInclude]
    public int NativeBaseAnimationVersion { get; internal set; } = 1;
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeFollowOffReceipt? NativeFollowOff { get; internal set; }
    [JsonInclude]
    public int BaseFirstFrame { get; internal set; }
    [JsonInclude]
    public int BaseLastFrame { get; internal set; }
    [JsonInclude]
    public int AnimationFirst { get; internal set; }
    [JsonInclude]
    public int AnimationLast { get; internal set; }
    [JsonInclude]
    public bool AnimationKeep { get; internal set; }
    [JsonInclude]
    public bool AnimationLoop { get; internal set; } = true;
    [JsonInclude]
    public bool AnimationPlaying { get; internal set; }
    [JsonInclude]
    public Vector3? MovementTarget { get; internal set; }
    [JsonInclude]
    public int? MovementActorId { get; internal set; }
    [JsonInclude]
    public float MovementStopDistance { get; internal set; }
    [JsonInclude]
    public float? RotationTarget { get; internal set; }
    [JsonInclude]
    public int RotationFrames { get; internal set; }
    [JsonInclude]
    public int RotationTotalFrames { get; internal set; }
    [JsonInclude]
    public float RotationStart { get; internal set; }
    [JsonInclude]
    public bool RotationUsesNativeCosine { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool RotationUsesNativeLinear { get; internal set; }

    internal void StepAnimation()
    {
        if (AnimationPlaying)
        {
            float next = AnimationFrame + Math.Max(1, AnimationSpeed) / 16f;
            if (next > AnimationLast)
            {
                if (AnimationLoop)
                {
                    AnimationFrame = AnimationFirst + (next - AnimationFirst) % Math.Max(1, AnimationLast - AnimationFirst + 1);
                }
                else
                {
                    AnimationPlaying = false;
                    AnimationFrame = AnimationLast;
                    if (!AnimationKeep)
                    {
                        SetBaseAnimation();
                    }
                }
            }
            else
            {
                AnimationFrame = next;
            }
        }
    }

    internal void StepRotation()
    {
        if (!RotationUsesNativeCosine && !RotationUsesNativeLinear && RotationTarget is float direction)
        {
            RotationFrames--;
            float delta = (direction - RotationStart + 384) % 256 - 128;
            Direction = (RotationStart + delta * (1 - RotationFrames / (float)Math.Max(1, RotationTotalFrames)) + 256) % 256;
            if (RotationFrames <= 0)
            {
                Direction = direction;
                RotationTarget = null;
            }
        }
    }

    internal void InvalidateNativeMotionAuthority()
    {
        NativeMotionAuthority = null;
        NativeScriptMovement = null;
        NativeJump = null;
        NativeJoinedPosition = null;
        NativeSplitPosition = null;
    }

    internal void SetBaseAnimation()
    {
        AnimationId = BaseAnimationId;
        AnimationFirst = BaseFirstFrame;
        AnimationLast = BaseLastFrame;
        AnimationFrame = BaseFirstFrame;
        AnimationLoop = true;
        AnimationKeep = false;
        AnimationPlaying = true;
    }
}
