using System.Numerics;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private void Execute(Fiber fiber, JsmOperation operation)
    {
        ActorState actor = actors[fiber.ObjectId];
        Jsm.Opcode opcode = operation.Opcode;
        int parameter = operation.Parameter;
        int[] args = [];
        NativeScalarValue[]? scalarArgs = null;
        switch (opcode)
        {
            case Jsm.Opcode.LBL:
            case Jsm.Opcode.NOP:
                break;
            case Jsm.Opcode.RET:
            case Jsm.Opcode.HALT:
                if (fiber.Stack.Count != 0)
                {
                    throw Fault(fiber, "Script returned with an unbalanced operand stack.");
                }
                fiber.Complete = true;
                break;
            case Jsm.Opcode.PSHN_L:
            case Jsm.Opcode.PSHAC:
                fiber.Stack.Push(parameter);
                fiber.StackExpressions.Push(opcode == Jsm.Opcode.PSHAC ? $"actor[{parameter}]" : parameter.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return;
            case Jsm.Opcode.PSHM_B:
            case Jsm.Opcode.PSHM_W:
            case Jsm.Opcode.PSHM_L:
            case Jsm.Opcode.PSHSM_W:
                if (TryReadNativeScalar(operation, out NativeScalarValue nativeRead))
                {
                    fiber.Stack.Push(nativeRead);
                    fiber.StackExpressions.Push($"native[{parameter}]");
                    return;
                }
                int value = opcode == Jsm.Opcode.PSHM_B && parameter == 209 && services.WorldControls is not null ? services.WorldControls.Menu209 : variables.TryGetValue(parameter, out int stored) ? stored : 0;
                if (opcode == Jsm.Opcode.PSHM_B && TryReadDrawPointByte(parameter, out int drawPointByte))
                {
                    value = drawPointByte;
                }
                if (opcode == Jsm.Opcode.PSHM_B && TryReadSoundVolumeByte(parameter, out int soundVolumeByte))
                {
                    value = soundVolumeByte;
                }
                if (TryReadMusicStopMemory(operation, out int stoppedMusicValue))
                {
                    value = stoppedMusicValue;
                }
                if (TryReadPreviousFieldWord(operation, out int previousFieldWord))
                {
                    value = previousFieldWord;
                }
                if (parameter == 216 && opcode is Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W && Scene.FadeMode216 is ushort fadeMode)
                {
                    value = fadeMode;
                }
                fiber.Stack.Push(opcode == Jsm.Opcode.PSHSM_W ? (short)value : value);
                fiber.StackExpressions.Push($"{(opcode == Jsm.Opcode.PSHM_B ? "B" : opcode == Jsm.Opcode.PSHM_W ? "W" : opcode == Jsm.Opcode.PSHSM_W ? "SW" : "L")}[{parameter}]");
                return;
            case Jsm.Opcode.POPM_B:
            case Jsm.Opcode.POPM_W:
            case Jsm.Opcode.POPM_L:
                if (fiber.Stack.Count > 0 && (!fiber.Stack.PeekValue().IsExact || services.NativeVariableValues?.ContainsKey(parameter) == true))
                {
                    NativeScalarValue nativeStored = StoreNativeScalar(fiber, operation);
                    if (nativeStored.IsExact)
                    {
                        args = [nativeStored.RequireExact()];
                    }
                    else
                    {
                        scalarArgs = [nativeStored];
                    }
                    break;
                }
                if (opcode == Jsm.Opcode.POPM_W && parameter == 84)
                {
                    args = Pop(fiber, 1);
                    SetPreviousFieldWord(unchecked((ushort)args[0]));
                    break;
                }
                if (opcode == Jsm.Opcode.POPM_B && parameter is 197 or 198)
                {
                    args = Pop(fiber, 1);
                    SetSoundVolumeByte(parameter, (byte)args[0]);
                    break;
                }
                if (opcode == Jsm.Opcode.POPM_B && parameter == 209 && services.WorldControls is Memoria.FF8.Runtime.NativeWorldControlState controls)
                {
                    if (fiber.Stack.Count == 0)
                    {
                        throw Fault(fiber, "POPM_B requires its original operand.");
                    }
                    SetNativeWorldControls(fiber, controls with { Menu209 = (byte)fiber.Stack.Peek() });
                    args = Pop(fiber, 1);
                    break;
                }
                args = Pop(fiber, 1);
                variables[parameter] = opcode == Jsm.Opcode.POPM_B ? args[0] & 255 : opcode == Jsm.Opcode.POPM_W ? args[0] & 65535 : args[0];
                break;
            case Jsm.Opcode.PSHI_L:
                fiber.Stack.Push(fiber.Results.GetValueOrDefault(parameter) ?? NativeScalarValue.Exact(0));
                fiber.StackExpressions.Push($"R[{parameter}]");
                return;
            case Jsm.Opcode.POPI_L:
                NativeScalarValue resultStored = PopNativeScalar(fiber);
                fiber.Results[parameter] = resultStored;
                if (resultStored.IsExact)
                {
                    args = [resultStored.RequireExact()];
                }
                else
                {
                    scalarArgs = [resultStored];
                }
                break;
            case Jsm.Opcode.CAL:
                Calculate(fiber, parameter);
                return;
            case Jsm.Opcode.JMP:
                fiber.Position = operation.Index + parameter;
                // TODO(TD-004): replace the artificial yield and scoped exception with the shared native scheduler budget
                if (parameter < 0 && !IsNativeLinearScrollLoopJump(fiber, operation))
                {
                    // Field polling loops cooperate with the frame scheduler instead of spinning on one frame.
                    WaitFrames(fiber, 1);
                }
                return;
            case Jsm.Opcode.JPF:
                int condition = Pop(fiber, 1)[0];
                RecordBranch(fiber, operation.Index, fiber.LastExpressions[0], condition, operation.Index + parameter);
                if (condition == 0)
                {
                    fiber.Position = operation.Index + parameter;
                }
                return;
            case Jsm.Opcode.RND:
                fiber.Results[0] = NextRandom();
                break;
            case Jsm.Opcode.SETTIMER:
            case Jsm.Opcode.DISPTIMER:
            case Jsm.Opcode.GETTIMER:
            case Jsm.Opcode.KILLTIMER:
                args = ExecuteTimer(fiber, opcode);
                break;
            case Jsm.Opcode.Unknown10:
                // Installed FF8_EN.exe music-position backend 46C040 returns zero unconditionally.
                fiber.Results[0] = 0;
                fiber.Results[1] = 0;
                break;
            case Jsm.Opcode.GETINFO:
                fiber.Results[0] = (int)actor.X;
                fiber.Results[1] = (int)actor.Y;
                fiber.Results[2] = (int)actor.Z;
                break;
            case Jsm.Opcode.INITTRACE:
                ActorState tracePlayer = actors.FirstOrDefault(item => item.Active && item.CharacterId == 0) ?? throw Fault(fiber, "INITTRACE has no registered player actor.");
                PartyTraceSample trace = new((short)(int)tracePlayer.X, (short)(int)tracePlayer.Y, (short)(int)tracePlayer.Z, tracePlayer.WalkmeshTriangle, (byte)tracePlayer.Direction);
                Scene.PartyTrace = new PartyTraceState(0, 15, 30, Enumerable.Repeat(trace, 64).ToArray(), Enumerable.Repeat(trace, 64).ToArray());
                break;
            case Jsm.Opcode.SETCAMERA:
                args = Pop(fiber, 1);
                Scene.CameraIndex = args[0] & 3;
                break;
            case Jsm.Opcode.SCROLLMODE2:
                args = ExecuteBackgroundScroll(fiber);
                break;
            case Jsm.Opcode.DSCROLL2:
                args = ExecuteDirectBackgroundScroll(fiber);
                break;
            case Jsm.Opcode.DSCROLL:
                args = ExecuteDirectBackgroundScroll(fiber, layerZero: true);
                break;
            case Jsm.Opcode.SHAKE:
            case Jsm.Opcode.SHAKEOFF:
                args = ExecuteShake(fiber, opcode == Jsm.Opcode.SHAKE);
                break;
            case Jsm.Opcode.SEVOLTRANS:
                args = ExecuteSoundVolumeTransition(fiber);
                break;
            case Jsm.Opcode.CSCROLLP:
                args = ExecutePartyBackgroundScroll(fiber);
                break;
            case Jsm.Opcode.CSCROLL2:
                args = ExecuteCosineBackgroundScroll(fiber);
                break;
            case Jsm.Opcode.LSCROLL3:
                args = ExecuteLinearBackgroundScroll(fiber);
                break;
            case Jsm.Opcode.SCROLLSYNC2:
                args = ExecuteBackgroundScrollSync2(fiber);
                break;
            case Jsm.Opcode.SCROLLSYNC:
                ExecuteBackgroundScrollSync(fiber);
                break;
            case Jsm.Opcode.DIRA:
                args = Pop(fiber, 1);
                if (program is null || (uint)args[0] >= program.NativeObjects.Count)
                {
                    throw Fault(fiber, "DIRA targets an unknown native object ID.");
                }
                ActorState directionTarget = actors[program.NativeObjects[args[0]]];
                actor.Direction = NativeDirection(actor.X, actor.Y, directionTarget.X, directionTarget.Y);
                break;
            case Jsm.Opcode.FACEDIRLIMIT:
                args = Pop(fiber, 3);
                actor.FaceLimits = new Vector3((byte)args[0], (byte)args[1], (byte)args[2]);
                break;
            case Jsm.Opcode.FACEDIRINIT:
                InitializeFaceDirection(actor, fiber);
                break;
            case Jsm.Opcode.FACEDIRA:
                args = Pop(fiber, 2);
                if (program is null || (uint)args[0] >= program.NativeObjects.Count)
                {
                    throw Fault(fiber, "FACEDIRA targets an unknown native object ID.");
                }
                ActorState faceTarget = actors[program.NativeObjects[args[0]]];
                float headHeight = ReadFaceAnchorHeight?.Invoke(faceTarget) ?? throw Fault(fiber, "FACEDIRA requires the target model's declared control-node height.");
                if (!float.IsFinite(headHeight))
                {
                    throw Fault(fiber, "Model face anchor height is not finite.");
                }
                // Legacy snapshots recorded FACEDIRINIT from root0. Derive the corrected
                // declared-node baseline once before its first supported transition.
                // TODO(TD-007): use canonical face
                if (actor.FaceStateVersion == 0)
                {
                    if (FieldId != 232 || actor.ObjectId != 15 || actor.ModelId != 14 || actor.AnimationId != 0)
                    {
                        throw Fault(fiber, "Legacy face baseline cannot be reconstructed exactly for this actor; resume a checkpoint from before FACEDIRINIT.");
                    }
                    InitializeFaceDirection(actor, fiber);
                }
                actor.FaceReturning = false;
                actor.FaceDuration = (ushort)args[1];
                actor.FaceProgress = 0;
                actor.FaceTargetPoint = new Vector3((short)(int)faceTarget.X, (short)(int)faceTarget.Y, (short)((int)faceTarget.Z + (int)headHeight));
                fiber.WaitReason = actor.FaceDuration == 0 ? WaitReason.None : WaitReason.FaceDirection;
                break;
            case Jsm.Opcode.FACEDIROFF:
                args = Pop(fiber, 1);
                FaceDirectionBaseline returnBaseline = ReadFaceBaseline?.Invoke(actor) ?? throw Fault(fiber, "FACEDIROFF requires the declared control-node animation baseline.");
                if (returnBaseline.FirstAngle is < 0 or > 255 || returnBaseline.SecondAngle is < 0 or > 255 || returnBaseline.XAngle is < 0 or > 255)
                {
                    throw Fault(fiber, "Model face return baseline is outside native byte-angle bounds.");
                }
                actor.FaceDuration = (ushort)args[0];
                actor.FaceProgress = 0;
                actor.FacePitchTarget = returnBaseline.XAngle;
                actor.FacePendingBaseline = returnBaseline.FirstAngle;
                actor.FaceYawTarget = returnBaseline.SecondAngle;
                actor.FaceTargetPoint = null;
                actor.FaceReturning = true;
                fiber.WaitReason = actor.FaceDuration == 0 ? WaitReason.None : WaitReason.FaceDirection;
                break;
            case Jsm.Opcode.ISPARTY:
                args = Pop(fiber, 1);
                fiber.Results[0] = Scene.Party.IndexOf((byte)args[0]);
                break;
            case Jsm.Opcode.GETPARTY:
                args = Pop(fiber, 1);
                fiber.Results[0] = args[0] < Scene.Party.Count ? Scene.Party[args[0]] : 255;
                break;
            case Jsm.Opcode.PGETINFO:
                args = Pop(fiber, 1);
                if ((uint)args[0] >= (Scene.FieldPartyNativeActorIds.Count == 3 ? 3 : Scene.Party.Count))
                {
                    throw Fault(fiber, $"PGETINFO refers to missing party slot {args[0]}.");
                }
                ActorState partyActor = ResolvePartyActor(args[0]) ?? throw Fault(fiber, $"PGETINFO party slot {args[0]} has no field actor.");
                fiber.Results[0] = (int)partyActor.X;
                fiber.Results[1] = (int)partyActor.Y;
                fiber.Results[2] = (int)partyActor.Z;
                fiber.Results[4] = (int)partyActor.Direction & 255;
                break;
            case Jsm.Opcode.ISTOUCH:
                args = Pop(fiber, 1);
                ActorState? touched = actors.FirstOrDefault(item => item.ObjectId == args[0]);
                fiber.Results[0] = touched is not null && ActorsTouch(actor, touched) ? 1 : 0;
                break;
            case Jsm.Opcode.REQ:
            case Jsm.Opcode.REQSW:
            case Jsm.Opcode.REQEW:
                args = Pop(fiber, 2);
                Request(fiber, parameter, args[1], args[0], opcode);
                break;
            case Jsm.Opcode.PREQ:
            case Jsm.Opcode.PREQSW:
            case Jsm.Opcode.PREQEW:
                args = Pop(fiber, 2);
                RequestParty(fiber, parameter, args[1], args[0], opcode);
                break;
            case Jsm.Opcode.REFRESHPARTY:
                RefreshParty();
                break;
            case Jsm.Opcode.FOLLOWOFF:
                ExecuteFollowOff(fiber);
                break;
            case Jsm.Opcode.JOIN:
                ExecuteJoin(fiber);
                break;
            case Jsm.Opcode.SPLIT:
                ExecuteSplit(fiber);
                break;
            case Jsm.Opcode.CLEAR:
                if (services.NativeVariableValues is { Count: > 0 } retainedMemory)
                {
                    services.SetNativeVariableValues(retainedMemory.Where(pair => pair.Key < 256 || pair.Key >= 1280).ToDictionary());
                }
                // Original51D8B0 clears exactly event bytes256..1279, outside party and character records.
                foreach (int address in variables.Keys.Where(address => address is >= 256 and < 1280).ToArray())
                {
                    variables.Remove(address);
                }
                break;
            case Jsm.Opcode.SETMODEL:
                actor.ModelId = parameter;
                break;
            case Jsm.Opcode.SETDRESS:
                args = ExecuteNativeDress(fiber);
                break;
            case Jsm.Opcode.SETPC:
                args = Pop(fiber, 1);
                actor.CharacterId = (byte)args[0];
                if (!Scene.Party.Contains(actor.CharacterId))
                {
                    actor.Active = false;
                }
                break;
            case Jsm.Opcode.ACTORMODE:
                args = Pop(fiber, 1);
                actor.AdditionalControlDisabled = args[0] == 3;
                break;
            case Jsm.Opcode.UNUSE:
                actor.Active = false;
                break;
            case Jsm.Opcode.USE:
                actor.Active = true;
                break;
            case Jsm.Opcode.HIDE:
                actor.Visible = false;
                break;
            case Jsm.Opcode.SHOW:
                actor.Visible = true;
                break;
            case Jsm.Opcode.SET3:
                args = Pop(fiber, 3);
                actor.X = args[0];
                actor.Y = args[1];
                actor.Z = args[2];
                actor.WalkmeshTriangle = parameter;
                actor.MovementTarget = null;
                actor.NativeScriptMovement = null;
                RecordNativeScriptPlacement(actor);
                break;
            case Jsm.Opcode.DIR:
                args = Pop(fiber, 1);
                actor.Direction = args[0] & 255;
                if (actor.NativeScriptMovement is { Active: false } previousMovement)
                {
                    actor.NativeScriptMovement = previousMovement with { FacingDirection = (byte)actor.Direction };
                }
                break;
            case Jsm.Opcode.CTURNL:
            case Jsm.Opcode.CTURNR:
                args = Pop(fiber, 2);
                actor.RotationStart = (byte)actor.Direction;
                actor.RotationTarget = NearestFaceAngle((int)actor.RotationStart, unchecked((short)args[0]));
                actor.RotationTotalFrames = (byte)args[1];
                actor.RotationFrames = actor.RotationTotalFrames + 1;
                actor.RotationUsesNativeCosine = true;
                fiber.WaitReason = WaitReason.Rotation;
                break;
            case Jsm.Opcode.LTURNL:
            case Jsm.Opcode.LTURNR:
                args = Pop(fiber, 2);
                actor.RotationUsesNativeCosine = false;
                actor.RotationStart = actor.Direction;
                actor.RotationTarget = args[0] & 255;
                actor.RotationFrames = Math.Max(1, args[1]);
                actor.RotationTotalFrames = actor.RotationFrames;
                fiber.WaitReason = WaitReason.Rotation;
                break;
            case Jsm.Opcode.CTURN:
            case Jsm.Opcode.LTURN:
                args = Pop(fiber, 2);
                if (program is null || (uint)args[0] >= program.NativeObjects.Count)
                {
                    throw Fault(fiber, $"Turn targets unknown actor {args[0]}.");
                }
                actor.RotationUsesNativeCosine = false;
                ActorState targetActor = actors[program.NativeObjects[args[0]]];
                int nativeRotationStart = (byte)actor.Direction;
                int nativeRotationTarget = NativeDirection(actor.X, actor.Y, targetActor.X, targetActor.Y);
                actor.RotationStart = nativeRotationStart;
                actor.RotationTarget = nativeRotationStart == nativeRotationTarget ? null : NearestFaceAngle(nativeRotationStart, nativeRotationTarget);
                actor.RotationTotalFrames = (byte)args[1];
                actor.RotationUsesNativeCosine = opcode == Jsm.Opcode.CTURN && actor.RotationTarget is not null;
                actor.RotationFrames = actor.RotationTarget is null ? 0 : actor.RotationTotalFrames + (actor.RotationUsesNativeCosine ? 1 : 0);
                fiber.WaitReason = actor.RotationTarget is null ? WaitReason.None : WaitReason.Rotation;
                break;
            case Jsm.Opcode.PCTURN:
                args = Pop(fiber, 2);
                if ((uint)args[0] >= (Scene.FieldPartyNativeActorIds.Count == 3 ? 3 : Scene.Party.Count))
                {
                    throw Fault(fiber, "PCTURN refers to a missing party actor slot.");
                }
                ActorState rotationPlayer = ResolvePartyActor(args[0]) ?? throw Fault(fiber, "PCTURN party slot has no registered field actor.");
                int rotationStart = (byte)actor.Direction;
                int rotationTarget = NativeDirection(actor.X, actor.Y, rotationPlayer.X, rotationPlayer.Y);
                actor.RotationStart = rotationStart;
                actor.RotationTotalFrames = (byte)args[1];
                actor.RotationTarget = rotationStart == rotationTarget ? null : NearestFaceAngle(rotationStart, rotationTarget);
                actor.RotationUsesNativeCosine = actor.RotationTarget is not null;
                actor.RotationFrames = actor.RotationUsesNativeCosine ? actor.RotationTotalFrames + 1 : 0;
                fiber.WaitReason = actor.RotationTarget is null ? WaitReason.None : WaitReason.Rotation;
                break;
            case Jsm.Opcode.MSPEED:
                args = Pop(fiber, 1);
                actor.MovementSpeed = args[0];
                break;
            case Jsm.Opcode.MLIMIT:
                args = Pop(fiber, 1);
                actor.MovementDirectionLimit = (byte)args[0];
                break;
            case Jsm.Opcode.MOVE:
            case Jsm.Opcode.RMOVE:
            case Jsm.Opcode.FMOVE:
            case Jsm.Opcode.RFMOVE:
                if (actor.NativeScriptMovement is { Active: true } || CanUseNativeScriptMove?.Invoke(actor) == true)
                {
                    ExecuteNativeScriptMove(fiber);
                    break;
                }
                args = Pop(fiber, 4);
                actor.InvalidateNativeMotionAuthority();
                actor.MovementTarget = new Vector3(args[0], args[1], args[2]);
                actor.MovementFlags = args[3];
                actor.MovementIgnoresWalkmesh = opcode is Jsm.Opcode.FMOVE or Jsm.Opcode.RFMOVE;
                if (opcode is Jsm.Opcode.MOVE or Jsm.Opcode.FMOVE)
                {
                    fiber.WaitReason = WaitReason.Movement;
                }
                break;
            case Jsm.Opcode.MOVESYNC:
                fiber.WaitReason = WaitReason.Movement;
                break;
            case Jsm.Opcode.MOVECANCEL:
                args = Pop(fiber, 1);
                if (program is null || (uint)args[0] >= program.NativeObjects.Count)
                {
                    throw Fault(fiber, "MOVECANCEL targets an unknown native object ID.");
                }
                ActorState cancelledActor = actors[program.NativeObjects[args[0]]];
                // Native 0x524620 acts only on a model entity in ordinary scripted movement.
                // A stationary target retains its current animation and all other state.
                if (cancelledActor.ModelId >= 0 && (cancelledActor.MovementTarget is not null || cancelledActor.MovementActorId is not null))
                {
                    int cancelledAnimationFrames = AnimationFrameCount?.Invoke(cancelledActor.ModelId, cancelledActor.BaseAnimationId) ?? throw Fault(fiber, "MOVECANCEL requires the target base animation's frame count.");
                    if (cancelledAnimationFrames <= 0)
                    {
                        throw Fault(fiber, "MOVECANCEL target base animation has no frames.");
                    }
                    cancelledActor.MovementTarget = null;
                    cancelledActor.MovementActorId = null;
                    if (cancelledActor.NativeScriptMovement is { Active: true, Asynchronous: true } cancelledMovement)
                    {
                        // Native524620 stops mode572 without writing completion542.
                        cancelledActor.NativeScriptMovement = cancelledMovement with { Active = false, Cancelled = true };
                    }
                    cancelledActor.AnimationId = cancelledActor.BaseAnimationId;
                    cancelledActor.AnimationFrame = 0;
                    cancelledActor.AnimationFirst = 0;
                    cancelledActor.AnimationLast = cancelledAnimationFrames - 1;
                    cancelledActor.AnimationKeep = false;
                    cancelledActor.AnimationLoop = true;
                    cancelledActor.AnimationPlaying = true;
                }
                break;
            case Jsm.Opcode.MOVEA:
                if (actor.NativeScriptMovement is { Active: true } || CanUseNativeScriptMove?.Invoke(actor) == true)
                {
                    ExecuteNativeScriptMove(fiber);
                    break;
                }
                args = Pop(fiber, 2);
                if ((uint)args[0] >= actors.Count || args[1] < 0)
                {
                    throw Fault(fiber, "MOVEA target or stopping distance is invalid.");
                }
                actor.InvalidateNativeMotionAuthority();
                actor.MovementActorId = args[0];
                actor.MovementStopDistance = args[1];
                fiber.WaitReason = WaitReason.MoveActor;
                break;
            case Jsm.Opcode.SETGETA:
                args = Pop(fiber, 1);
                actor.GetaParameter = args[0];
                break;
            case Jsm.Opcode.SETROOTTRANS:
                args = Pop(fiber, 1);
                actor.RootTranslationZ = unchecked((short)args[0]);
                break;
            case Jsm.Opcode.SHADELEVEL:
                args = Pop(fiber, 1);
                actor.ShadeLevel = args[0];
                break;
            case Jsm.Opcode.DOFFSET:
            case Jsm.Opcode.LOFFSET:
            case Jsm.Opcode.LOFFSETS:
            case Jsm.Opcode.COFFSET:
                args = ExecuteDisplayOffset(fiber, actor, operation);
                break;
            case Jsm.Opcode.OFFSETSYNC:
                ExecuteDisplayOffsetSync(fiber);
                break;
            case Jsm.Opcode.PARTICLEON:
            case Jsm.Opcode.PARTICLEOFF:
                args = ExecuteParticle(fiber, operation.Opcode == Jsm.Opcode.PARTICLEON);
                break;
            case Jsm.Opcode.BASEANIME:
                args = Pop(fiber, 2);
                actor.BaseAnimationId = (byte)parameter;
                actor.WalkAnimationId = (byte)args[1];
                actor.RunAnimationId = (byte)args[0];
                actor.NativeBaseAnimationVersion = 1;
                actor.BaseFirstFrame = 0;
                actor.BaseLastFrame = Math.Max(0, (AnimationFrameCount?.Invoke(actor.ModelId, actor.BaseAnimationId) ?? 1) - 1);
                break;
            case Jsm.Opcode.ANIME:
            case Jsm.Opcode.ANIMEKEEP:
            case Jsm.Opcode.RANIME:
            case Jsm.Opcode.RANIMEKEEP:
            case Jsm.Opcode.RANIMELOOP:
            case Jsm.Opcode.CANIME:
            case Jsm.Opcode.CANIMEKEEP:
            case Jsm.Opcode.RCANIME:
            case Jsm.Opcode.RCANIMEKEEP:
            case Jsm.Opcode.RCANIMELOOP:
                PlayAnimation(fiber, actor, opcode, parameter, out args);
                break;
            case Jsm.Opcode.ANIMESPEED:
                args = Pop(fiber, 1);
                if (args[0] <= 0)
                {
                    throw Fault(fiber, "ANIMESPEED must be positive.");
                }
                actor.AnimationSpeed = args[0];
                break;
            case Jsm.Opcode.ANIMESYNC:
                fiber.WaitReason = WaitReason.Animation;
                break;
            case Jsm.Opcode.ANIMESTOP:
                actor.AnimationPlaying = false;
                break;
            case Jsm.Opcode.WAIT:
                args = Pop(fiber, 1);
                WaitFrames(fiber, args[0]);
                break;
            case Jsm.Opcode.LOADSYNC:
                // LoadField receives a fully loaded field and its models. There is no pending loader task.
                break;
            case Jsm.Opcode.UCOFF:
                IsUserControlEnabled = false;
                break;
            case Jsm.Opcode.UCON:
                IsUserControlEnabled = true;
                break;
            case Jsm.Opcode.MENUENABLE:
            case Jsm.Opcode.MENUDISABLE:
                Scene.MenuEnabled = opcode == Jsm.Opcode.MENUENABLE;
                break;
            case Jsm.Opcode.MENUTIPS:
                args = Pop(fiber, 1);
                // Native 0x522070 masks the ID before setting its persistent tutorial-info bit.
                Scene.UnlockedTutorials.Add(args[0] & 127);
                break;
            case Jsm.Opcode.TUTO:
                args = Pop(fiber, 1);
                CompleteMinimalMenu(fiber, opcode, args[0]);
                break;
            case Jsm.Opcode.RUNENABLE:
            case Jsm.Opcode.RUNDISABLE:
                Scene.RunEnabled = opcode == Jsm.Opcode.RUNENABLE;
                break;
            case Jsm.Opcode.MOVIEREADY:
                args = Pop(fiber, 2);
                preparedMovie = args[0];
                variables[80] = 0;
                movieProgressExpected = false;
                break;
            case Jsm.Opcode.MOVIE:
                if (preparedMovie < 0)
                {
                    throw Fault(fiber, "MOVIE has no preceding MOVIEREADY.");
                }
                movieProgressExpected = true;
                services.RequestMovie(preparedMovie);
                break;
            case Jsm.Opcode.MOVIESYNC:
                fiber.WaitReason = WaitReason.Movie;
                break;
            case Jsm.Opcode.IDLOCK:
                if (parameter < 0)
                {
                    throw Fault(fiber, "Negative walkmesh triangle ID.");
                }
                Scene.LockedTriangles.Add(parameter);
                break;
            case Jsm.Opcode.IDUNLOCK:
                Scene.LockedTriangles.Remove(parameter);
                break;
            case Jsm.Opcode.POLYCOLORALL:
                args = Pop(fiber, 3);
                Vector3 polygonColor = new((byte)args[0], (byte)args[1], (byte)args[2]);
                foreach (ActorState fieldActor in actors)
                {
                    fieldActor.PolygonColor = polygonColor;
                }
                break;
            case Jsm.Opcode.PREMAPJUMP2:
                args = Pop(fiber, 1);
                Scene.PreparedField = args[0];
                break;
            case Jsm.Opcode.MAPJUMPO:
                args = Pop(fiber, 2);
                transitionRequested = true;
                services.RequestField(FieldTransition.FromMapJumpOrigin(args[0], args[1]));
                break;
            case Jsm.Opcode.MAPJUMPON:
                Scene.MapJumpDisabled = false;
                break;
            case Jsm.Opcode.MAPJUMPOFF:
                Scene.MapJumpDisabled = true;
                break;
            case Jsm.Opcode.WORLDMAPJUMP:
                args = ExecuteWorldJump(fiber);
                break;
            case Jsm.Opcode.MAPJUMP:
                args = Pop(fiber, 4);
                transitionRequested = true;
                services.RequestField(new Memoria.FF8.Runtime.FieldTransition((ushort)args[0], (short)args[1], (short)args[2], (ushort)parameter, (byte)args[3]));
                break;
            case Jsm.Opcode.MAPJUMP3:
                args = Pop(fiber, 5);
                transitionRequested = true;
                // Native 0x521B1D writes this extra word; field arrival derives height from its triangle instead.
                services.RequestField(new Memoria.FF8.Runtime.FieldTransition((ushort)args[0], (short)args[1], (short)args[2], (ushort)parameter, (byte)args[4], (short)args[3]));
                break;
            case Jsm.Opcode.AXIS:
                args = Pop(fiber, 2);
                axisTarget = (short)args[0];
                axisDuration = (ushort)args[1];
                axisElapsed = 0;
                break;
            case Jsm.Opcode.AMESW:
            case Jsm.Opcode.AMES:
            case Jsm.Opcode.RAMESW:
                args = Pop(fiber, 4);
                ShowDialogue(fiber, Message(args[0], args[1], args[2], args[3]) with { CanConfirm = opcode != Jsm.Opcode.AMES }, opcode == Jsm.Opcode.AMESW);
                break;
            case Jsm.Opcode.MES:
                args = Pop(fiber, 2);
                ShowDialogue(fiber, Message(args[0], args[1], 0, 0) with { CanConfirm = false }, false);
                break;
            case Jsm.Opcode.AASK:
                args = Pop(fiber, 8);
                ShowDialogue(fiber, Message(args[0], args[1], args[6], args[7]) with { FirstChoice = args[2], LastChoice = args[3], InitialChoice = args[4], CancelChoice = args[5] }, true);
                break;
            case Jsm.Opcode.WINCLOSE:
                args = Pop(fiber, 1);
                CloseDialogue(args[0]);
                break;
            case Jsm.Opcode.WINSIZE:
                args = Pop(fiber, 5);
                Scene.DialogueWindows[args[0]] = new DialogueWindow(args[0], args[1], args[2], args[3], args[4]);
                break;
            case Jsm.Opcode.MESSYNC:
                args = Pop(fiber, 1);
                int channel = args[0];
                if (dialogues.TryGetValue(channel, out DialogueState? synchronized))
                {
                    dialogues[channel] = synchronized with { CanConfirm = true };
                    RefreshCurrentDialogue();
                }
                fiber.DialogueChannel = channel;
                fiber.WaitReason = WaitReason.Dialogue;
                break;
            case Jsm.Opcode.MENUNAME:
                args = Pop(fiber, 1);
                if (TryNativeGuardianForceName(args[0], out _, out _))
                {
                    CompleteMinimalMenu(fiber, opcode, args[0]);
                    break;
                }
                nameEntity = args[0];
                string defaultName = Scene.Names.GetValueOrDefault(nameEntity, nameEntity == 0 ? "Squall" : $"Character {nameEntity}");
                ShowDialogue(fiber, new DialogueState(0, -1, $"Name: {defaultName}", 0, 0, IsNamePrompt: true), true);
                break;
            case Jsm.Opcode.MESMODE:
                args = Pop(fiber, 3);
                Scene.MessageMode = args[2];
                break;
            case Jsm.Opcode.SETMESSPEED:
                args = Pop(fiber, 1);
                Scene.MessageSpeed = args[0];
                break;
            case Jsm.Opcode.BGDRAW:
                args = Pop(fiber, 1);
                HoldBackground(fiber.BackgroundId, (short)(args[0] << 6));
                break;
            case Jsm.Opcode.BGOFF:
                HoldBackground(fiber.BackgroundId, -1);
                break;
            case Jsm.Opcode.BGCLEAR:
                args = Pop(fiber, 1);
                Scene.BackgroundClearValue = unchecked((byte)args[0]);
                break;
            case Jsm.Opcode.SETDRAWPOINT:
                args = SetNativeDrawPointDisplay(fiber);
                break;
            case Jsm.Opcode.Unknown16:
                args = SetNativeDrawPointIndex(fiber);
                break;
            case Jsm.Opcode.BGANIMESPEED:
                args = Pop(fiber, 1);
                BackgroundAnimation backgroundRate = GetBackgroundAnimation(fiber.BackgroundId);
                backgroundRate.Speed = (short)args[0];
                backgroundRate.Counter = backgroundRate.Speed;
                break;
            case Jsm.Opcode.BGANIME:
            case Jsm.Opcode.RBGANIME:
            case Jsm.Opcode.RBGANIMELOOP:
                args = Pop(fiber, 2);
                BackgroundAnimation background = GetBackgroundAnimation(fiber.BackgroundId);
                background.First = args[0];
                background.Last = args[1];
                background.Position = (short)((args[0] << 6) + (args[1] < args[0] ? 63 : 0));
                background.Active = true;
                background.Complete = false;
                background.Loop = opcode == Jsm.Opcode.RBGANIMELOOP;
                backgroundStates[fiber.BackgroundId] = BackgroundFrame(background.Position);
                if (opcode == Jsm.Opcode.BGANIME)
                {
                    fiber.WaitReason = WaitReason.BackgroundAnimation;
                }
                break;
            case Jsm.Opcode.BGANIMESYNC:
                fiber.WaitReason = WaitReason.BackgroundAnimation;
                break;
            case Jsm.Opcode.RBGSHADELOOP:
                args = Pop(fiber, 10);
                int module = program!.Objects[fiber.ObjectId].BackgroundModuleIndex ?? throw Fault(fiber, "RBGSHADELOOP requires a source background module.");
                Vector3 firstShade = new((byte)args[2], (byte)args[3], (byte)args[4]);
                Vector3 secondShade = new((byte)args[5], (byte)args[6], (byte)args[7]);
                Scene.BackgroundShades.Remove(module);
                Scene.BackgroundShadeLoops[module] = new BackgroundShadeLoop(fiber.ObjectId, module, (ushort)args[0], (ushort)args[1], firstShade, secondShade, (byte)args[8], (byte)args[9], true, 0, (ushort)args[0], (byte)args[8], firstShade);
                break;
            case Jsm.Opcode.BGSHADE:
                args = Pop(fiber, 7);
                Scene.BackgroundShade = new BackgroundShade((ushort)args[0], new Vector3((byte)args[1], (byte)args[2], (byte)args[3]), new Vector3((byte)args[4], (byte)args[5], (byte)args[6]), Frame, fiber.BackgroundId);
                Scene.BackgroundShadeLoops.Remove(fiber.BackgroundId);
                Scene.BackgroundShades[fiber.BackgroundId] = Scene.BackgroundShade;
                break;
            case Jsm.Opcode.BGSHADEOFF:
                // Native 0x520B00 clears shade flags, timing, holds and all RGB endpoints.
                // Renderer 0x475480 reads the zero endpoint when duration and elapsed are zero.
                Scene.BackgroundShadeLoops.Remove(fiber.BackgroundId);
                Scene.BackgroundShade = new BackgroundShade(0, Vector3.Zero, Vector3.Zero, Frame, fiber.BackgroundId);
                Scene.BackgroundShades[fiber.BackgroundId] = Scene.BackgroundShade;
                break;
            case Jsm.Opcode.KEYON:
                args = Pop(fiber, 1);
                fiber.Results[0] = (unchecked((uint)args[0]) & Scene.ButtonInput!.Pressed) != 0 ? 1 : 0;
                break;
            case Jsm.Opcode.DCOLADD:
            case Jsm.Opcode.DCOLSUB:
                args = Pop(fiber, 3);
                if (opcode == Jsm.Opcode.DCOLADD)
                {
                    Scene.AdditiveColor = new Vector3(args[0], args[1], args[2]);
                }
                else
                {
                    Scene.SubtractiveColor = new Vector3(args[0], args[1], args[2]);
                }
                break;
            case Jsm.Opcode.FCOLADD:
            case Jsm.Opcode.FCOLSUB:
                args = Pop(fiber, 7);
                colorStart = new Vector3(args[0], args[1], args[2]);
                colorEnd = new Vector3(args[3], args[4], args[5]);
                colorStartFrame = Frame;
                colorEndFrame = Frame + Math.Max(1, args[6]);
                subtractColor = opcode == Jsm.Opcode.FCOLSUB;
                StepVisualTransitions();
                break;
            case Jsm.Opcode.COLSYNC:
                fiber.WaitReason = WaitReason.Color;
                break;
            case Jsm.Opcode.FADEIN:
            case Jsm.Opcode.FADEOUT:
                InvalidateFadeModeAuthority();
                fadeStart = opcode == Jsm.Opcode.FADEIN ? 1 : 0;
                fadeEnd = 1 - fadeStart;
                fadeStartFrame = Frame;
                fadeEndFrame = Frame + SimulationFps;
                Scene.FadeOpacity = fadeStart;
                break;
            case Jsm.Opcode.FADEBLACK:
                InvalidateFadeModeAuthority();
                Scene.FadeOpacity = 1;
                break;
            case Jsm.Opcode.FADENONE:
                ExecuteFadeNone(fiber);
                break;
            case Jsm.Opcode.FADESYNC:
                fiber.WaitReason = WaitReason.Fade;
                break;
            case Jsm.Opcode.MAPFADEOFF:
                Scene.MapFadeEnabled = false;
                break;
            case Jsm.Opcode.MAPFADEON:
                Scene.MapFadeEnabled = true;
                break;
            case Jsm.Opcode.TALKON:
            case Jsm.Opcode.TALKOFF:
                actor.TalkEnabled = opcode == Jsm.Opcode.TALKON;
                break;
            case Jsm.Opcode.PUSHON:
            case Jsm.Opcode.PUSHOFF:
                actor.PushEnabled = opcode == Jsm.Opcode.PUSHON;
                break;
            case Jsm.Opcode.TALKRADIUS:
            case Jsm.Opcode.PUSHRADIUS:
                args = Pop(fiber, 1);
                if (opcode == Jsm.Opcode.TALKRADIUS)
                {
                    actor.TalkRadius = args[0];
                }
                else
                {
                    actor.PushRadius = args[0];
                }
                break;
            case Jsm.Opcode.THROUGHON:
            case Jsm.Opcode.THROUGHOFF:
                actor.CollisionEnabled = opcode == Jsm.Opcode.THROUGHOFF;
                break;
            case Jsm.Opcode.FOOTSTEPON:
            case Jsm.Opcode.FOOTSTEPOFF:
                actor.FootstepsEnabled = opcode == Jsm.Opcode.FOOTSTEPON;
                break;
            case Jsm.Opcode.SETLINE:
                args = ExecuteNativeSetLine(fiber, operation);
                break;
            case Jsm.Opcode.LINEON:
            case Jsm.Opcode.LINEOFF:
                actor.LineEnabled = opcode == Jsm.Opcode.LINEON;
                break;
            case Jsm.Opcode.DOORLINEOFF:
                actor.DoorLineEnabled = false;
                break;
            case Jsm.Opcode.PREMAPJUMP:
                args = Pop(fiber, 4);
                Scene.PreparedField = args[0];
                break;
            case Jsm.Opcode.BATTLE:
                args = ExecuteMinimalBattle(fiber);
                break;
            case Jsm.Opcode.BATTLERESULT:
                fiber.Results[0] = Scene.NativeBattleResult!.Value;
                break;
            case Jsm.Opcode.BATTLEOFF:
            case Jsm.Opcode.BATTLEON:
                Scene.RandomBattlesEnabled = opcode == Jsm.Opcode.BATTLEON;
                break;
            case Jsm.Opcode.SARALYOFF:
            case Jsm.Opcode.SARALYON:
                Scene.SalaryEnabled = opcode == Jsm.Opcode.SARALYON;
                break;
            case Jsm.Opcode.SETPLACE:
                args = Pop(fiber, 1);
                Scene.PlaceId = args[0];
                break;
            case Jsm.Opcode.ADDMEMBER:
            case Jsm.Opcode.SUBMEMBER:
                args = Pop(fiber, 1);
                if (opcode == Jsm.Opcode.ADDMEMBER)
                {
                    Scene.Members.Add(args[0]);
                }
                else
                {
                    Scene.Members.Remove(args[0]);
                }
                break;
            case Jsm.Opcode.ADDPARTY:
                args = Pop(fiber, 1);
                if (!Scene.Party.Contains(args[0]))
                {
                    AddCombatPartyCharacter(args[0]);
                    Scene.Party.Add(args[0]);
                }
                break;
            case Jsm.Opcode.SUBPARTY:
                args = ExecutePartyRemoval(fiber);
                break;
            case Jsm.Opcode.SETPARTY:
                args = Pop(fiber, 3);
                SetCombatParty(args);
                Scene.Party.Clear();
                Scene.Party.AddRange(args.Where(id => id != 255));
                break;
            case Jsm.Opcode.DYING:
                services.SetCharacterVitals(services.CharacterVitals!.RepairDyingParty());
                break;
            case Jsm.Opcode.HOLD:
                args = Pop(fiber, 3);
                Scene.Holds[args[0]] = [args[1], args[2]];
                break;
            case Jsm.Opcode.PHSPOWER:
                args = [ExecutePartyPower(fiber)];
                break;
            case Jsm.Opcode.SETBATTLEMUSIC:
                args = Pop(fiber, 1);
                Scene.BattleMusic = args[0];
                break;
            case Jsm.Opcode.EFFECTLOAD:
                if (LoadSoundResource is null)
                {
                    throw Fault(fiber, "EFFECTLOAD requires an explicit field-audio backend (decoded or muted).");
                }
                args = Pop(fiber, 1);
                int soundIndex = (byte)args[0];
                SoundResourceStatus soundStatus = LoadSoundResource(soundIndex);
                if (!Enum.IsDefined(soundStatus))
                {
                    throw Fault(fiber, "Field-audio backend returned an invalid resource status.");
                }
                Scene.LoadedSoundEffects[soundIndex] = new SoundResourceRequest(soundIndex, soundIndex == 11 ? "wave0811.dat" : null, soundStatus);
                if (soundStatus == SoundResourceStatus.Pending)
                {
                    fiber.SoundResourceId = soundIndex;
                    fiber.WaitReason = WaitReason.SoundResource;
                }
                break;
            case Jsm.Opcode.EFFECTPLAY2:
                args = ExecuteFieldSound(fiber, parameter);
                break;
            case Jsm.Opcode.EFFECTPLAY:
                ExecuteGlobalSound(fiber, out args);
                break;
            case Jsm.Opcode.SESTOP:
                args = ExecuteSoundStop(fiber);
                break;
            case Jsm.Opcode.MUSICLOAD:
                args = Pop(fiber, 1);
                InvalidateMusicChannelAuthority();
                Scene.LoadedMusic = args[0];
                break;
            case Jsm.Opcode.INITSOUND:
                ExecuteSoundInitialization(fiber);
                break;
            case Jsm.Opcode.MUSICCHANGE:
                InvalidateMusicChannelAuthority();
                Scene.MusicPlaying = true;
                break;
            case Jsm.Opcode.CROSSMUSIC:
                args = Pop(fiber, 2);
                InvalidateMusicChannelAuthority();
                Scene.MusicPlaying = true;
                Scene.MusicCrossfadeFrames = args[1];
                break;
            case Jsm.Opcode.MUSICSTOP:
                args = ExecuteMusicStop(fiber);
                break;
            case Jsm.Opcode.MUSICVOL:
            case Jsm.Opcode.MUSICVOLTRANS:
                args = ExecuteMusicVolume(fiber, opcode);
                break;
            default:
                throw Fault(fiber, $"Unsupported required opcode {opcode} (parameter {parameter}); no state was fabricated.");
        }
        CommandExecuted?.Invoke(new ScriptCommand(FieldId, fiber.ObjectId, fiber.Script.Id, operation.Index, opcode.ToString(), args, scalarArgs));
    }

    private void Calculate(Fiber fiber, int kind)
    {
        int right = Pop(fiber, 1)[0];
        string rightExpression = fiber.LastExpressions[0];
        if (kind == 5 || kind == 15)
        {
            fiber.Stack.Push(kind == 5 ? -right : ~right);
            fiber.StackExpressions.Push($"{(kind == 5 ? "-" : "~")}({rightExpression})");
            return;
        }
        int left = Pop(fiber, 1)[0];
        string leftExpression = fiber.LastExpressions[0];
        int result = kind switch
        {
            0 => unchecked(left + right),
            1 => unchecked(left - right),
            2 => unchecked(left * right),
            3 => left / right,
            4 => left % right,
            6 => left == right ? 1 : 0,
            7 => left > right ? 1 : 0,
            8 => left >= right ? 1 : 0,
            9 => left < right ? 1 : 0,
            10 => left <= right ? 1 : 0,
            11 => left != right ? 1 : 0,
            12 => left & right,
            13 => left | right,
            14 => left ^ right,
            _ => throw Fault(fiber, $"Unsupported calculation operator {kind}.")
        };
        fiber.Stack.Push(result);
        string symbol = kind switch { 0 => "+", 1 => "-", 2 => "*", 3 => "/", 4 => "%", 6 => "==", 7 => ">", 8 => ">=", 9 => "<", 10 => "<=", 11 => "!=", 12 => "&", 13 => "|", 14 => "^", _ => "?" };
        fiber.StackExpressions.Push($"({leftExpression} {symbol} {rightExpression})");
    }

    private void Request(Fiber caller, int objectId, int scriptId, int priority, Jsm.Opcode opcode)
    {
        if (program is not null && (uint)objectId < program.NativeObjects.Count)
        {
            int rawObjectId = objectId;
            objectId = program.NativeObjects[rawObjectId];
            if (Scene.NativeActorLayoutVersion == 1 && rawObjectId < program.NativeActorCount && !Scene.NativeActorObjectIds.Contains(objectId))
            {
                return;
            }
        }
        if (objectId < 0 || objectId >= actors.Count)
        {
            throw Fault(caller, $"Request targets invalid object index {objectId}.");
        }
        if (!actors[objectId].Active)
        {
            throw Fault(caller, $"Request targets inactive object {objectId}; behavior requires further evidence.");
        }
        bool busy = fibers.Any(item => item.ObjectId == objectId && item.Priority == priority && !item.Complete);
        if (busy && opcode == Jsm.Opcode.REQ)
        {
            return;
        }
        Fiber requested = Queue(objectId, scriptId, priority, false);
        if (opcode == Jsm.Opcode.REQEW)
        {
            caller.Request = requested;
            caller.WaitReason = WaitReason.RequestEnd;
        }
        else if (opcode == Jsm.Opcode.REQSW && busy)
        {
            caller.Request = requested;
            caller.WaitReason = WaitReason.RequestStart;
        }
    }

    private void RequestParty(Fiber caller, int partySlot, int scriptIndex, int priority, Jsm.Opcode opcode)
    {
        if (program is null || (uint)partySlot >= 3 || (uint)priority >= 8)
        {
            throw Fault(caller, $"{opcode} party slot or priority is outside the native request bounds.");
        }
        if (Scene.FieldPartyNativeActorIds.Count != 3 && partySlot >= Scene.Party.Count)
        {
            return;
        }
        ActorState? target = ResolvePartyActor(partySlot);
        if (target is null)
        {
            return;
        }
        Fiber[] active = fibers.Where(fiber => fiber.ObjectId == target.ObjectId && !fiber.Complete).ToArray();
        if (active.Any(fiber => fiber.Priority == priority))
        {
            // Native PREQ/PREQSW/PREQEW consume and drop a request whose priority slot is occupied.
            return;
        }
        if (active.Length != 0)
        {
            throw Fault(caller, $"{opcode} competing-priority preemption requires native movement/operand suspension support.");
        }
        if ((uint)scriptIndex >= program.Objects[target.ObjectId].Scripts.Count)
        {
            throw Fault(caller, $"{opcode} targets an unknown party actor script index.");
        }
        Fiber requested = Queue(target.ObjectId, program.Objects[target.ObjectId].Scripts[scriptIndex].Id, priority, false);
        if (opcode == Jsm.Opcode.PREQEW)
        {
            caller.Request = requested;
            caller.WaitReason = WaitReason.RequestEnd;
        }
    }

    private DialogueState Message(int channel, int messageId, int x, int y)
    {
        if (messageId < 0 || messageId >= messages.Count)
        {
            throw new InvalidDataException($"Message {messageId} is outside the field MSD table ({messages.Count}).");
        }
        return new DialogueState(channel, messageId, messages[messageId], x, y);
    }

    private void PlayAnimation(Fiber fiber, ActorState actor, Jsm.Opcode opcode, int animationId, out int[] args)
    {
        bool ranged = opcode is Jsm.Opcode.CANIME or Jsm.Opcode.CANIMEKEEP or Jsm.Opcode.RCANIME or Jsm.Opcode.RCANIMEKEEP or Jsm.Opcode.RCANIMELOOP;
        args = ranged ? Pop(fiber, 2) : [];
        int count = ranged ? 0 : AnimationFrameCount?.Invoke(actor.ModelId, animationId) ?? throw Fault(fiber, $"Model {actor.ModelId} animation {animationId} needs actual animation frame metadata.");
        if (!ranged && count <= 0)
        {
            throw Fault(fiber, $"Model {actor.ModelId} animation {animationId} has no frames.");
        }
        actor.AnimationId = animationId;
        actor.AnimationFirst = ranged ? args[1] : 0;
        actor.AnimationLast = ranged ? args[0] : count - 1;
        actor.AnimationFrame = actor.AnimationFirst;
        actor.AnimationKeep = opcode is Jsm.Opcode.ANIMEKEEP or Jsm.Opcode.CANIMEKEEP or Jsm.Opcode.RANIMEKEEP or Jsm.Opcode.RCANIMEKEEP;
        actor.AnimationLoop = opcode is Jsm.Opcode.RANIMELOOP or Jsm.Opcode.RCANIMELOOP;
        actor.AnimationPlaying = true;
        if (actor.AnimationLast < actor.AnimationFirst)
        {
            throw Fault(fiber, $"Animation range {actor.AnimationFirst}..{actor.AnimationLast} is reversed.");
        }
        if (opcode is Jsm.Opcode.ANIME or Jsm.Opcode.ANIMEKEEP or Jsm.Opcode.CANIME or Jsm.Opcode.CANIMEKEEP)
        {
            fiber.WaitReason = WaitReason.Animation;
        }
    }
}
