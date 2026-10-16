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
            case Jsm.Opcode.Unknown1:
                args = ExecuteNativeSavedBattleFlagBit4(fiber);
                break;
            case Jsm.Opcode.LBL:
                if (NativeRuntimeProfile is { IsVerified: true })
                {
                    if (IsNativeDoorRequest(fiber.ObjectId))
                    {
                        EnterNativeDoorResultFrame(fiber);
                    }
                    else
                    {
                        EnterNativeActorResultFrame(fiber);
                    }
                    fiber.NativeResultFrameEntered = true;
                }
                break;
            case Jsm.Opcode.NOP:
                break;
            case Jsm.Opcode.OPENEYES:
            case Jsm.Opcode.CLOSEEYES:
                actor.NativeEyes = PrepareNativeEyes(actor, opcode == Jsm.Opcode.CLOSEEYES);
                break;
            case Jsm.Opcode.DISPBAR:
            case Jsm.Opcode.BROKEN:
            case Jsm.Opcode.SETBAR:
            case Jsm.Opcode.KILLBAR:
                NativeFieldBarsState bars = PrepareNativeBars(fiber, opcode);
                args = Pop(fiber, opcode == Jsm.Opcode.BROKEN ? 8 : opcode == Jsm.Opcode.DISPBAR ? 7 : opcode == Jsm.Opcode.SETBAR ? 2 : 1);
                services.SetFieldBars(bars);
                break;
            case Jsm.Opcode.RET:
                if (fiber.Stack.Count != 0 && (!HasNativeOperandRecord(fiber.ObjectId) || fiber.Initialization))
                {
                    throw Fault(fiber, "Script returned with an unbalanced operand stack.");
                }
                CompleteNativeDoorRequest(fiber, parameter);
                CompleteNativeActorResultFrame(fiber);
                CompleteNativeMovementRequest(fiber, parameter);
                fiber.Complete = true;
                break;
            case Jsm.Opcode.HALT:
                if (NativeRuntimeProfile is { IsVerified: true })
                {
                    fiber.Position--;
                    fiber.WaitReason = WaitReason.NativeHalt;
                }
                else
                {
                    fiber.Complete = true;
                }
                break;
            case Jsm.Opcode.MOVEFLUSH:
                if (actor.NativeMovementRequest is { } flushedBackup)
                {
                    actor.NativeMovementRequest = flushedBackup with { RestoreSuppressedByFlush = true };
                }
                // Native5247E0 clears only352.bit10000 on first issue, then
                // returns1 forever without advancing PC or stopping the body.
                fiber.Position--;
                fiber.WaitReason = WaitReason.NativeMovementFlush;
                break;
            case Jsm.Opcode.PSHN_L:
            case Jsm.Opcode.PSHAC:
                fiber.Stack.Push(parameter);
                fiber.StackExpressions.Push(opcode == Jsm.Opcode.PSHAC ? $"actor[{parameter}]" : parameter.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return;
            case Jsm.Opcode.PSHM_B:
            case Jsm.Opcode.PSHSM_B:
            case Jsm.Opcode.PSHM_W:
            case Jsm.Opcode.PSHSM_W:
            case Jsm.Opcode.PSHM_L:
            case Jsm.Opcode.PSHSM_L:
                NativeScalarValue memoryRead = ReadEventMemory(operation);
                fiber.Stack.Push(memoryRead);
                fiber.StackExpressions.Push($"native[{parameter}/{NativeMemoryWidth(opcode)}]");
                return;
            case Jsm.Opcode.POPM_B:
            case Jsm.Opcode.POPM_W:
            case Jsm.Opcode.POPM_L:
                if (TryWriteWorldEntryMemory(fiber, operation, out args) || TryWriteWorldMusicSelection(fiber, operation, out args) || TryWriteNativeFadeMemory(fiber, operation, out args) || TryWriteNativeDrawPointMemory(fiber, operation, out args) || TryWriteJunctionBackupMemory(fiber, operation, out args) || TryWriteNativeEconomyMemory(fiber, operation, out args) || TryWriteNativeOwnedEventMemory(fiber, operation, out args))
                {
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
                fiber.Position = JsmProgram.JumpTarget(operation.Index, parameter);
                return;
            case Jsm.Opcode.JPF:
                int condition = Pop(fiber, 1)[0];
                RecordBranch(fiber, operation.Index, fiber.LastExpressions[0], condition, JsmProgram.JumpTarget(operation.Index, parameter));
                if (condition == 0)
                {
                    fiber.Position = JsmProgram.JumpTarget(operation.Index, parameter);
                }
                return;
            case Jsm.Opcode.RND:
                ExecuteNativeRandom(fiber);
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
            case Jsm.Opcode.Unknown13:
                args = Pop(fiber, 1);
                //520200 forwards the full DWORD to46BFC0 (SdEffectAttrCheck).
                //The verified original backend logs its empty-function warning
                //and returns0 unconditionally; only this fiber's R0 is written.
                fiber.Results[0] = 0;
                break;
            case Jsm.Opcode.SETWITCH:
                args = ExecuteNativeSetWitch(fiber);
                break;
            case Jsm.Opcode.Unknown4:
                args = ExecuteNativeFacingHold(fiber);
                break;
            case Jsm.Opcode.Unknown5:
                args = ExecuteNativeItemPresence(fiber);
                break;
            case Jsm.Opcode.WHOAMI:
                args = ExecuteNativeWhoAmI(fiber);
                break;
            case Jsm.Opcode.GETINFO:
                args = ExecuteNativeActorInfo(fiber, false);
                break;
            case Jsm.Opcode.COPYINFO:
                args = ExecuteNativeCopyInfo(fiber);
                break;
            case Jsm.Opcode.INITTRACE:
                InitializeNativePartyTrace(fiber);
                break;
            case Jsm.Opcode.SETCAMERA:
                args = Pop(fiber, 1);
                Scene.CameraIndex = args[0] & 3;
                break;
            case Jsm.Opcode.SETDCAMERA:
                NativeScalarValue movieMaskOperand = PopNativeScalar(fiber);
                byte movieMaskByte = checked((byte)movieMaskOperand.ToWidth(1).RequireExact());
                scalarArgs = [movieMaskOperand];
                args = [movieMaskOperand.IsExact ? movieMaskOperand.RequireExact() : movieMaskByte];
                Scene.NativeMovieMask = Scene.NativeMovieMask! with { Value = movieMaskByte };
                break;
            case Jsm.Opcode.SCROLLMODE2:
                args = ExecuteBackgroundScroll(fiber);
                break;
            case Jsm.Opcode.SCROLLRATIO2:
                args = ExecuteBackgroundScrollRatio(fiber);
                break;
            case Jsm.Opcode.DSCROLL2:
                args = ExecuteDirectBackgroundScroll(fiber);
                break;
            case Jsm.Opcode.DSCROLL:
                args = ExecuteDirectBackgroundScroll(fiber, layerZero: true);
                break;
            case Jsm.Opcode.DSCROLLA:
                args = ExecuteActorBackgroundScroll(fiber);
                break;
            case Jsm.Opcode.DSCROLLA2:
                args = ExecuteLayerActorBackgroundScroll(fiber, cosine: false);
                break;
            case Jsm.Opcode.CSCROLLA2:
                args = ExecuteLayerActorBackgroundScroll(fiber, cosine: true);
                break;
            case Jsm.Opcode.CSCROLLA:
                args = ExecuteNativeActorCosineBackgroundScroll(fiber);
                break;
            case Jsm.Opcode.DSCROLLP:
                args = ExecuteNativePartyBackgroundScroll(fiber);
                break;
            case Jsm.Opcode.SHAKE:
            case Jsm.Opcode.SHAKEOFF:
                args = ExecuteShake(fiber, opcode == Jsm.Opcode.SHAKE);
                break;
            case Jsm.Opcode.SEVOLTRANS:
                args = ExecuteSoundVolumeTransition(fiber);
                break;
            case Jsm.Opcode.SEPOS:
            case Jsm.Opcode.SEPOSTRANS:
                args = ExecuteSoundPosition(fiber, opcode == Jsm.Opcode.SEPOSTRANS);
                break;
            case Jsm.Opcode.CSCROLLP:
                args = ExecutePartyBackgroundScroll(fiber);
                break;
            case Jsm.Opcode.CSCROLL2:
                args = ExecuteCosineBackgroundScroll(fiber);
                break;
            case Jsm.Opcode.CSCROLL:
                args = ExecuteLayerZeroCosineBackgroundScroll(fiber);
                break;
            case Jsm.Opcode.JUMP:
                args = ExecuteNativeJump(fiber, parameter);
                break;
            case Jsm.Opcode.JUMP3:
                args = ExecuteNativeJump(fiber, parameter, true);
                break;
            case Jsm.Opcode.PJUMPA:
                args = ExecuteNativeJump(fiber, parameter, partyTarget: true);
                break;
            case Jsm.Opcode.LADDERUP:
            case Jsm.Opcode.LADDERDOWN:
                args = ExecuteNativeLadderMode3(fiber, parameter, opcode == Jsm.Opcode.LADDERUP, out scalarArgs);
                break;
            case Jsm.Opcode.LADDERUP2:
                args = ExecuteNativeLadder(fiber, parameter, true);
                break;
            case Jsm.Opcode.LADDERDOWN2:
                args = ExecuteNativeLadder(fiber, parameter, false);
                break;
            case Jsm.Opcode.Unknown15:
                args = Pop(fiber, 1);
                Scene.NativeLadderSpeed490E = unchecked((byte)args[0]);
                break;
            case Jsm.Opcode.PLTURN:
            case Jsm.Opcode.PCTURN:
                args = ExecutePartyTurn(fiber, opcode == Jsm.Opcode.PCTURN);
                break;
            case Jsm.Opcode.Unknown11:
                args = ExecutePartyTurn(fiber, true, asynchronous: true);
                break;
            case Jsm.Opcode.Unknown12:
                if (actor.NativeTurnMode580 != 3)
                {
                    fiber.Position--;
                    fiber.WaitReason = WaitReason.NativeTurnCompletion;
                }
                break;
            case Jsm.Opcode.CMOVE:
                args = ExecuteNativeScriptMove(fiber);
                break;
            case Jsm.Opcode.LSCROLL3:
                args = ExecuteLinearBackgroundScroll(fiber);
                break;
            case Jsm.Opcode.LSCROLL2:
                args = ExecuteLinearBackgroundScroll2(fiber);
                break;
            case Jsm.Opcode.LSCROLL:
                args = ExecuteNativeLayerZeroLinearBackgroundScroll(fiber);
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
            case Jsm.Opcode.PDIRA:
                args = ExecutePartyActorDirection(fiber);
                break;
            case Jsm.Opcode.FACEDIRI:
            case Jsm.Opcode.RFACEDIRI:
                args = ExecuteNativeFaceImmediate(fiber, opcode == Jsm.Opcode.RFACEDIRI, out scalarArgs);
                break;
            case Jsm.Opcode.FACEDIR:
            case Jsm.Opcode.RFACEDIR:
                args = ExecuteNativeFacePoint(fiber, opcode == Jsm.Opcode.RFACEDIR, out scalarArgs);
                break;
            case Jsm.Opcode.FACEDIRLIMIT:
                args = Pop(fiber, 3);
                actor.FaceLimits = new Vector3((byte)args[0], (byte)args[1], (byte)args[2]);
                break;
            case Jsm.Opcode.FACEDIRINIT:
                InitializeFaceDirection(actor, fiber);
                break;
            case Jsm.Opcode.FACEDIRSYNC:
                ExecuteNativeFaceSync(fiber);
                break;
            case Jsm.Opcode.FACEDIRP:
            case Jsm.Opcode.RFACEDIRP:
                args = ExecuteNativePartyFace(fiber, opcode == Jsm.Opcode.RFACEDIRP, out scalarArgs);
                break;
            case Jsm.Opcode.FACEDIRA:
            case Jsm.Opcode.RFACEDIRA:
                args = ExecuteNativeActorFace(fiber, opcode == Jsm.Opcode.RFACEDIRA, out scalarArgs);
                break;
            case Jsm.Opcode.FACEDIROFF:
            case Jsm.Opcode.RFACEDIROFF:
                args = ExecuteNativeFaceReturn(fiber, opcode == Jsm.Opcode.RFACEDIROFF, out scalarArgs);
                break;
            case Jsm.Opcode.ISPARTY:
                args = Pop(fiber, 1);
                fiber.Results[0] = Scene.Party.IndexOf((byte)args[0]);
                break;
            case Jsm.Opcode.GETPARTY:
                args = Pop(fiber, 1);
                fiber.Results[0] = Scene.Party[args[0]];
                break;
            case Jsm.Opcode.PGETINFO:
                args = ExecuteNativeActorInfo(fiber, true);
                break;
            case Jsm.Opcode.ISTOUCH:
                args = Pop(fiber, 1);
                int touchedId = program is not null && (uint)args[0] < program.NativeObjects.Count ? program.NativeObjects[args[0]] : -1;
                ActorState? touched = touchedId >= 0 && (Scene.NativeActorLayoutVersion != 1 || Scene.NativeActorObjectIds.Contains(touchedId)) ? actors[touchedId] : null;
                fiber.Results[0] = touched is not null && ActorsTouch(actor, touched) ? 1 : 0;
                break;
            case Jsm.Opcode.REQ:
            case Jsm.Opcode.REQSW:
            case Jsm.Opcode.REQEW:
                if (opcode != Jsm.Opcode.REQ && NativeRuntimeProfile is { IsVerified: true } && (fiber.NativeDoorRequestWait is not null || IsNativeDoorRequest(program!.NativeObjects[parameter])))
                {
                    args = ExecuteNativeDoorRequestWait(fiber, parameter);
                    break;
                }
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
            case Jsm.Opcode.FOLLOWON:
                ExecuteFollowOn(fiber);
                break;
            case Jsm.Opcode.JOIN:
                ExecuteJoin(fiber);
                break;
            case Jsm.Opcode.SPLIT:
                ExecuteSplit(fiber);
                break;
            case Jsm.Opcode.CLEAR:
                SetEventMemory(EventMemory.Clear(256, 1280));
                ClearWorldEntryMemory();
                // 51D8B0 clears the entire256..1279 bank, including268.
                Scene.WorldMusicSelection = new NativeWorldMusicSelection(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, null, 0, NativeWorldMusicSelectionOrigin.ExecutedClear);
                break;
            case Jsm.Opcode.SETMODEL:
                actor.ModelId = parameter;
                break;
            case Jsm.Opcode.MODELDOOROPEN:
            case Jsm.Opcode.MODELDOORCLOSE:
                args = ExecuteNativeModelDoor(fiber, opcode == Jsm.Opcode.MODELDOORCLOSE);
                break;
            case Jsm.Opcode.SEALEDOFF:
                args = ExecuteNativeSealedOff(fiber, out scalarArgs);
                break;
            case Jsm.Opcode.LASTIN:
                args = ExecuteNativeLastIn(fiber);
                break;
            case Jsm.Opcode.JUNCTION:
                args = ExecuteJunction(fiber);
                break;
            case Jsm.Opcode.SETDRESS:
                args = ExecuteNativeDress(fiber);
                break;
            case Jsm.Opcode.SETPC:
                args = Pop(fiber, 1);
                actor.CharacterId = (byte)args[0];
                actor.FootstepsEnabled = true;
                if (actor.CharacterId is 3 or 4 or 5 or 7)
                {
                    actor.NativeFootstepSpecial = true;
                }
                if (!Scene.Party.Contains(actor.CharacterId))
                {
                    actor.Active = false;
                }
                break;
            case Jsm.Opcode.ACTORMODE:
                args = Pop(fiber, 1);
                if (args[0] is 0 or 1)
                {
                    actor.PrimitiveTransparencyEnabled = args[0] == 0;
                }
                else
                {
                    actor.AdditionalControlDisabled = args[0] == 3;
                }
                break;
            case Jsm.Opcode.UNUSE:
                actor.Active = false;
                break;
            case Jsm.Opcode.USE:
                actor.Active = true;
                break;
            case Jsm.Opcode.HIDE:
                SetNativeActorVisibility(actor, false);
                break;
            case Jsm.Opcode.SHOW:
                SetNativeActorVisibility(actor, true);
                break;
            case Jsm.Opcode.SET3:
                args = Pop(fiber, 3);
                actor.NativeJoinedPosition = null;
                actor.NativeSplitPosition = null;
                actor.NativeCopiedPosition = null;
                actor.X = args[0];
                actor.Y = args[1];
                actor.Z = args[2];
                actor.WalkmeshTriangle = parameter;
                if (actor.NativeScriptMovement is { } positionedMovement)
                {
                    // Native51D780 changes400/404/408/506 only. Preserve
                    // mode572, target436..444, completion542 and physical flags.
                    actor.NativeScriptMovement = positionedMovement with { FixedX = args[0] << 12, FixedY = args[1] << 12, FixedZ = args[2] << 12, Triangle = actor.WalkmeshTriangle };
                }
                else
                {
                    actor.MovementTarget = null;
                }
                actor.NativeJump = null;
                actor.NativeLadder = null;
                actor.NativeLadderMode3 = null;
                RecordNativeScriptPlacement(actor);
                break;
            case Jsm.Opcode.SET:
                args = ExecuteNativeSet(fiber, parameter);
                break;
            case Jsm.Opcode.DIRP:
                args = ExecuteNativeDirectionPoint(fiber);
                break;
            case Jsm.Opcode.DIR:
                args = Pop(fiber, 1);
                actor.Direction = args[0] & 255;
                if (actor.NativeScriptMovement is { } previousMovement && (!previousMovement.Active || previousMovement.Suspended || NativeMoveFacingHold(previousMovement) != 0))
                {
                    actor.NativeScriptMovement = previousMovement with { FacingDirection = (byte)actor.Direction };
                }
                break;
            case Jsm.Opcode.Unknown8:
                args = ExecuteNegativeCosineTurn(fiber);
                break;
            case Jsm.Opcode.Unknown9:
                args = ExecutePositiveCosineTurn(fiber);
                break;
            case Jsm.Opcode.CTURNL:
            case Jsm.Opcode.CTURNR:
                args = Pop(fiber, 2);
                actor.RotationStart = (byte)actor.Direction;
                actor.RotationTarget = NearestFaceAngle((int)actor.RotationStart, unchecked((short)args[0]));
                actor.RotationTotalFrames = (byte)args[1];
                actor.RotationFrames = actor.RotationTotalFrames + 1;
                actor.RotationUsesNativeCosine = true;
                actor.NativeTurnMode580 = 2;
                fiber.WaitReason = WaitReason.Rotation;
                break;
            case Jsm.Opcode.LTURNL:
            case Jsm.Opcode.LTURNR:
            case Jsm.Opcode.Unknown6:
            case Jsm.Opcode.Unknown7:
                args = ExecuteDirectedLinearTurn(fiber, opcode);
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
                if (actor.RotationTarget is not null)
                {
                    actor.NativeTurnMode580 = opcode == Jsm.Opcode.CTURN ? (byte)2 : (byte)1;
                }
                actor.RotationFrames = actor.RotationTarget is null ? 0 : actor.RotationTotalFrames + (actor.RotationUsesNativeCosine ? 1 : 0);
                fiber.WaitReason = actor.RotationTarget is null ? WaitReason.None : WaitReason.Rotation;
                break;
            case Jsm.Opcode.MSPEED:
                args = Pop(fiber, 1);
                short nativeSpeed = unchecked((short)args[0]);
                actor.MovementSpeed = nativeSpeed;
                actor.NativeMovementBaseSpeed = nativeSpeed;
                if (actor.NativeScriptMovement is NativeScriptMovementState speedMovement)
                {
                    actor.NativeScriptMovement = speedMovement with { Speed = nativeSpeed };
                }
                break;
            case Jsm.Opcode.MLIMIT:
                args = Pop(fiber, 1);
                actor.MovementDirectionLimit = (byte)args[0];
                break;
            case Jsm.Opcode.MACCEL:
            case Jsm.Opcode.MOVE:
            case Jsm.Opcode.RMOVE:
            case Jsm.Opcode.FMOVE:
            case Jsm.Opcode.RFMOVE:
            case Jsm.Opcode.RCMOVE:
                ExecuteNativeScriptMove(fiber);
                break;
            case Jsm.Opcode.MOVESYNC:
                if (actor.NativeMovementCompletion542 != 2)
                {
                    fiber.Position--;
                    fiber.WaitReason = WaitReason.Movement;
                }
                break;
            case Jsm.Opcode.MOVECANCEL:
                args = Pop(fiber, 1);
                if (program is null || (uint)args[0] >= program.NativeObjects.Count)
                {
                    throw Fault(fiber, "MOVECANCEL targets an unknown native object ID.");
                }
                ActorState cancelledActor = actors[program.NativeObjects[args[0]]];
                CancelNativeScriptMovement(cancelledActor);
                break;
            case Jsm.Opcode.MOVEA:
            case Jsm.Opcode.FMOVEA:
            case Jsm.Opcode.RMOVEA:
            case Jsm.Opcode.PMOVEA:
            case Jsm.Opcode.RPMOVEA:
                args = ExecuteNativeScriptMove(fiber);
                break;
            case Jsm.Opcode.SETGETA:
                args = Pop(fiber, 1);
                actor.GetaParameter = args[0];
                break;
            case Jsm.Opcode.LADDERANIME:
                args = ExecuteLadderAnimation(fiber, actor, parameter);
                break;
            case Jsm.Opcode.SETROOTTRANS:
                args = Pop(fiber, 1);
                actor.RootTranslationZ = unchecked((short)args[0]);
                break;
            case Jsm.Opcode.SHADELEVEL:
                args = ExecuteActorShadowLevel(fiber, actor);
                break;
            case Jsm.Opcode.SHADEFORM:
                args = ExecuteActorShadowForm(fiber, actor);
                break;
            case Jsm.Opcode.SHADESET:
                args = ExecuteActorShadowSet(fiber, actor);
                break;
            case Jsm.Opcode.DOFFSET:
            case Jsm.Opcode.LOFFSET:
            case Jsm.Opcode.LOFFSETS:
            case Jsm.Opcode.COFFSET:
            case Jsm.Opcode.COFFSETS:
                args = ExecuteDisplayOffset(fiber, actor, operation);
                break;
            case Jsm.Opcode.OFFSETSYNC:
                ExecuteDisplayOffsetSync(fiber);
                break;
            case Jsm.Opcode.PARTICLEON:
            case Jsm.Opcode.PARTICLEOFF:
            case Jsm.Opcode.PARTICLESET:
                args = ExecuteParticle(fiber, operation.Opcode);
                break;
            case Jsm.Opcode.BASEANIME:
                args = Pop(fiber, 2);
                actor.BaseAnimationId = (byte)parameter;
                actor.WalkAnimationId = (byte)args[1];
                actor.RunAnimationId = (byte)args[0];
                actor.NativeBaseAnimationVersion = 1;
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
                if (NativeRuntimeProfile is { IsVerified: true })
                {
                    SetNativeAnimationIncrement(actor, unchecked((ushort)args[0]));
                    break;
                }
                if (args[0] <= 0)
                {
                    throw Fault(fiber, "ANIMESPEED must be positive.");
                }
                actor.AnimationSpeed = args[0];
                break;
            case Jsm.Opcode.ANIMESYNC:
                if (NativeRuntimeProfile is { IsVerified: true })
                {
                    if ((RequireNativeAnimationHeader(actor).Flags!.Value & AnimationComplete) == 0)
                    {
                        fiber.WaitReason = WaitReason.NativeAnimationSync;
                    }
                    break;
                }
                fiber.WaitReason = WaitReason.Animation;
                break;
            case Jsm.Opcode.ANIMESTOP:
                if (NativeRuntimeProfile is { IsVerified: true })
                {
                    SelectNativeBodyAnimation(actor, unchecked((byte)actor.BaseAnimationId), AnimationRepeating);
                    WaitFrames(fiber, 1);
                    break;
                }
                actor.AnimationPlaying = false;
                break;
            case Jsm.Opcode.PUSHANIME:
            case Jsm.Opcode.POPANIME:
                ExecuteNativeAnimationBackup(actor, opcode == Jsm.Opcode.POPANIME);
                break;
            case Jsm.Opcode.WAIT:
                if (synchronousFieldInitialization && (fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact || fiber.Stack.Peek() <= 0))
                {
                    throw Fault(fiber, "Native synchronous WAIT requires its exact positive DWORD countdown; wraparound polling is unsupported.");
                }
                args = Pop(fiber, 1);
                if (!synchronousFieldInitialization)
                {
                    WaitFrames(fiber, args[0]);
                }
                break;
            case Jsm.Opcode.LOADSYNC:
                // LoadField receives a fully loaded field and its models. There is no pending loader task.
                break;
            case Jsm.Opcode.UCOFF:
                ExecuteNativeUserControlOff();
                break;
            case Jsm.Opcode.UCON:
                ExecuteNativeUserControlOn();
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
                RequestNativeTutorial(fiber, opcode, args[0]);
                break;
            case Jsm.Opcode.MENUPHS:
            case Jsm.Opcode.MENUSAVE:
            case Jsm.Opcode.MENUNORMAL:
                RequestPartyMenu(fiber, opcode);
                break;
            case Jsm.Opcode.MENUSHOP:
                args = RequestNativeShop(fiber);
                break;
            case Jsm.Opcode.MENUTUTO:
                RequestNativeTutorial(fiber, opcode, 0);
                break;
            case Jsm.Opcode.RUNENABLE:
            case Jsm.Opcode.RUNDISABLE:
                Scene.RunEnabled = opcode == Jsm.Opcode.RUNENABLE;
                break;
            case Jsm.Opcode.MOVIEREADY:
                PrepareNativeMovieMask(fiber, fiber.Stack.Skip(1).First());
                args = Pop(fiber, 2);
                preparedMovie = args[0];
                WriteGenericEventMemory(80, 2, NativeScalarValue.Exact(0));
                movieProgressExpected = false;
                Scene.MovieActors = new NativeMovieActorLifecycle(1, preparedMovie, args[1], Frame, null, null);
                fiber.WaitReason = WaitReason.Frames;
                fiber.WaitUntil = Frame + 1;
                break;
            case Jsm.Opcode.MOVIE:
                if (preparedMovie < 0)
                {
                    throw Fault(fiber, "MOVIE has no preceding MOVIEREADY.");
                }
                movieProgressExpected = true;
                StartNativeMovieActors(fiber);
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
            case Jsm.Opcode.POLYCOLOR:
            case Jsm.Opcode.POLYCOLORALL:
                args = ExecuteNativePolygonColor(fiber, opcode == Jsm.Opcode.POLYCOLORALL);
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
            case Jsm.Opcode.DISC:
                args = AssignNativeDisc(fiber);
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
            case Jsm.Opcode.DISCJUMP:
                args = ExecuteNativeDiscJump(fiber, parameter);
                break;
            case Jsm.Opcode.AXIS:
                args = Pop(fiber, 2);
                axisTarget = (short)args[0];
                axisDuration = (ushort)args[1];
                axisElapsed = 0;
                break;
            case Jsm.Opcode.AMESW:
            case Jsm.Opcode.AMES:
                if (WaitForNativeDialogueChannel(fiber, 4, false))
                {
                    break;
                }
                args = Pop(fiber, 4);
                ShowDialogue(fiber, Message(args[0], args[1], args[2], args[3]) with { CanConfirm = opcode == Jsm.Opcode.AMESW }, opcode == Jsm.Opcode.AMESW);
                if (opcode == Jsm.Opcode.AMES)
                {
                    // 5291E0 return3 advances the PC and yields this actor's VM turn.
                    WaitFrames(fiber, 1);
                }
                break;
            case Jsm.Opcode.RAMESW:
                // 529380 consumes all four operands before its occupied-channel
                // return5. The missing physical stack-residue retry is refused by preflight.
                args = Pop(fiber, 4);
                ShowDialogue(fiber, Message(args[0], args[1], args[2], args[3]), false);
                break;
            case Jsm.Opcode.MES:
                if (WaitForNativeDialogueChannel(fiber, 2, false))
                {
                    break;
                }
                args = Pop(fiber, 2);
                ShowDialogue(fiber, Message(args[0], args[1], 0, 0) with { CanConfirm = false }, false);
                // 528F20 return3 also yields after successful persistent opening.
                WaitFrames(fiber, 1);
                break;
            case Jsm.Opcode.AASK:
                if (WaitForNativeDialogueChannel(fiber, 8, true))
                {
                    break;
                }
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
                int nativeNameArgument = fiber.Stack.Peek();
                if (TryNativeGuardianForceName(nativeNameArgument, out _, out int namingGuardianForce))
                {
                    // Native521DA0 acquires through47E480 at issue, before the naming menu returns.
                    // Only flag1 changes; existing saved HP and every other GF input remain owned.
                    CommitSavedCharacters(fiber, services.CharacterVitals!.AcquireGuardianForce(namingGuardianForce), opcode);
                }
                string defaultName = GameNames.DefaultName(NativeNamingSubject(nativeNameArgument));
                args = Pop(fiber, 1);
                nameEntity = NativeNamingSubject(args[0]);
                // Native4B3140 bare naming selectors have80 clear;4E696A initializes the saved buffer from its table default.
                // Item-driven renames explicitly select80 and retain their independently owned current-name draft.
                Scene.Names[nameEntity] = defaultName;
                ShowDialogue(fiber, new DialogueState(0, -1, $"Name: {defaultName}", 0, 0, IsNamePrompt: true), true);
                break;
            case Jsm.Opcode.MESVAR:
                args = SetMessageVariable(fiber);
                break;
            case Jsm.Opcode.MESMODE:
                args = Pop(fiber, 3);
                Scene.MessageMode = args[2];
                break;
            case Jsm.Opcode.SETMESSPEED:
                args = PrepareNativeMessageSpeed(fiber);
                Scene.MessageChannelTiming[args[0]] = new(unchecked((ushort)args[1]), 0);
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
            case Jsm.Opcode.DRAWPOINT:
                args = ExecuteDrawPointInteraction(fiber);
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
                Scene.BackgroundShadeLoops[module] = new BackgroundShadeLoop(fiber.ObjectId, module, (ushort)args[0], (ushort)args[1], firstShade, secondShade, (byte)args[8], (byte)args[9], true, 0, (ushort)args[0] == 0 ? 0 : (ushort)args[0] + 1, (byte)args[8], firstShade);
                break;
            case Jsm.Opcode.BGSHADE:
                args = ExecuteBackgroundShade(fiber);
                break;
            case Jsm.Opcode.BGSHADESTOP:
                StopBackgroundShade(fiber.BackgroundId);
                break;
            case Jsm.Opcode.BGSHADEOFF:
                // Native 0x520B00 clears shade flags, timing, holds and all RGB endpoints.
                // Renderer 0x475480 reads the zero endpoint when duration and elapsed are zero.
                Scene.BackgroundShadeLoops.Remove(fiber.BackgroundId);
                Scene.BackgroundShades[fiber.BackgroundId] = new BackgroundShade(0, Vector3.Zero, Vector3.Zero, Frame, fiber.BackgroundId);
                break;
            case Jsm.Opcode.KEY:
                args = Pop(fiber, 1);
                //51E0B0 writes only global input-disable BYTE1CE4775, independently of UCOFF's sequence guard.
                IsUserControlEnabled = args[0] != 0;
                break;
            case Jsm.Opcode.KEYSCAN:
            case Jsm.Opcode.KEYSCAN2:
            case Jsm.Opcode.KEYON:
                ushort buttonWord = opcode == Jsm.Opcode.KEYSCAN2 ? ButtonInput!.RawHeld : (opcode == Jsm.Opcode.KEYON ? ButtonInput!.Pressed : ButtonInput!.Held) ?? throw new NotSupportedException("Mapped input configuration is unknown.");
                args = Pop(fiber, 1);
                fiber.Results[0] = (unchecked((uint)args[0]) & buttonWord) != 0 ? 1 : 0;
                break;
            case Jsm.Opcode.SETVIBRATE:
                args = ExecuteVibrationRequest(fiber);
                break;
            case Jsm.Opcode.DCOLADD:
            case Jsm.Opcode.DCOLSUB:
            case Jsm.Opcode.TCOLADD:
            case Jsm.Opcode.TCOLSUB:
            case Jsm.Opcode.FCOLADD:
            case Jsm.Opcode.FCOLSUB:
                args = ExecuteNativeFieldColor(fiber, opcode);
                break;
            case Jsm.Opcode.COLSYNC:
                ExecuteNativeFadeSync(fiber, true);
                break;
            case Jsm.Opcode.FADEIN:
                services.SetFieldFade(FieldFade!.IssueFadeIn());
                break;
            case Jsm.Opcode.FADEOUT:
                services.SetFieldFade(FieldFade!.IssueFadeOut());
                break;
            case Jsm.Opcode.FADEBLACK:
                services.SetFieldFade(FieldFade!.IssueBlack());
                break;
            case Jsm.Opcode.FADENONE:
                services.SetFieldFade(FieldFade!.IssueNone());
                break;
            case Jsm.Opcode.FADESYNC:
                ExecuteNativeFadeSync(fiber, false);
                break;
            case Jsm.Opcode.MAPFADEOFF:
                services.SetFieldFade(FieldFade!.SetMapFadeDisabled(true));
                break;
            case Jsm.Opcode.MAPFADEON:
                services.SetFieldFade(FieldFade!.SetMapFadeDisabled(false));
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
                    actor.PushRadius = unchecked((ushort)args[0]);
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
            case Jsm.Opcode.FOOTSTEP:
                args = ExecuteFootstepPair(fiber, operation);
                break;
            case Jsm.Opcode.FOOTSTEPCOPY:
                ExecuteFootstepCopy(fiber);
                break;
            case Jsm.Opcode.FOOTSTEPCUT:
                ExecuteFootstepCut(fiber);
                break;
            case Jsm.Opcode.SETLINE:
                args = ExecuteNativeSetLine(fiber, operation);
                break;
            case Jsm.Opcode.LINEON:
            case Jsm.Opcode.LINEOFF:
                actor.LineEnabled = opcode == Jsm.Opcode.LINEON;
                break;
            case Jsm.Opcode.DOORLINEOFF:
            case Jsm.Opcode.DOORLINEON:
                // 51DD20/51DD40 write area+392 only and return2; contact/pending flags persist.
                actor.DoorLineEnabled = opcode == Jsm.Opcode.DOORLINEON;
                break;
            case Jsm.Opcode.PREMAPJUMP:
                args = Pop(fiber, 4);
                Scene.PreparedField = args[0];
                break;
            case Jsm.Opcode.BATTLE:
                args = ExecuteMinimalBattle(fiber);
                break;
            case Jsm.Opcode.CARDGAME:
                args = ExecuteTripleTriad(fiber);
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
            case Jsm.Opcode.SARALYDISPOFF:
            case Jsm.Opcode.SARALYDISPON:
                ExecuteNativeSalary(fiber, opcode);
                break;
            case Jsm.Opcode.ADDSEEDLEVEL:
                args = ExecuteAddSeeDLevel(fiber);
                break;
            case Jsm.Opcode.REST:
                ExecuteRest(fiber);
                break;
            case Jsm.Opcode.SETPLACE:
                args = Pop(fiber, 1);
                Scene.PlaceId = unchecked((ushort)args[0]);
                break;
            case Jsm.Opcode.ADDMEMBER:
            case Jsm.Opcode.SUBMEMBER:
            case Jsm.Opcode.ISMEMBER:
                args = ExecuteMembership(fiber, opcode);
                break;
            case Jsm.Opcode.ADDPARTY:
                args = ExecuteAddPartyCharacter(fiber);
                break;
            case Jsm.Opcode.SUBPARTY:
                args = ExecutePartyRemoval(fiber);
                break;
            case Jsm.Opcode.SETPARTY:
                args = ExecuteSetParty(fiber);
                break;
            case Jsm.Opcode.CHANGEPARTY:
                args = ExecuteNativeChangeParty(fiber);
                break;
            case Jsm.Opcode.SWAP:
                ExecuteNativeSwap(fiber);
                break;
            case Jsm.Opcode.DYING:
                CommitSavedCharacters(fiber, services.CharacterVitals!.RepairDyingParty(), opcode);
                break;
            case Jsm.Opcode.SETHP:
                args = ExecuteSetHitPoints(fiber);
                break;
            case Jsm.Opcode.HOLD:
                args = ExecuteNativeHold(fiber, out scalarArgs);
                break;
            case Jsm.Opcode.PHSPOWER:
                args = [ExecutePartyPower(fiber)];
                break;
            case Jsm.Opcode.SAVEENABLE:
            case Jsm.Opcode.PHSENABLE:
                args = [ExecuteMenuEnable(fiber, operation.Opcode)];
                break;
            case Jsm.Opcode.GETCARD:
                args = [ExecuteGetCard(fiber)];
                break;
            case Jsm.Opcode.HOWMANYCARD:
                args = [ExecuteHowManyCard(fiber)];
                break;
            case Jsm.Opcode.WHERECARD:
                args = [ExecuteWhereCard(fiber)];
                break;
            case Jsm.Opcode.SETCARD:
                args = ExecuteSetCard(fiber);
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
                // Native51FDE0 writes physical music/effect BYTE202 before backend polling.
                Scene.MusicProcess = (Scene.MusicProcess ?? new NativeMusicProcessState(1, null, null, null, null, null)) with { Bank202 = (byte)soundIndex };
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
            case Jsm.Opcode.SPUREADY:
                args = ExecuteNativeSpuReady(fiber);
                break;
            case Jsm.Opcode.SPUSYNC:
                args = ExecuteNativeSpuSync(fiber);
                break;
            case Jsm.Opcode.MUSICREPLAY:
                ExecuteNativeMusicReplay(fiber);
                break;
            case Jsm.Opcode.MUSICSTATUS:
                ExecuteNativeMusicStatus(fiber);
                break;
            case Jsm.Opcode.MUSICLOAD:
                args = ExecuteNativeMusicLoad(fiber);
                break;
            case Jsm.Opcode.INITSOUND:
                ExecuteSoundInitialization(fiber);
                break;
            case Jsm.Opcode.MUSICCHANGE:
            case Jsm.Opcode.MUSICSKIP:
            case Jsm.Opcode.CHOICEMUSIC:
                args = ExecuteNativeMusicPlay(fiber, opcode);
                break;
            case Jsm.Opcode.CROSSMUSIC:
            case Jsm.Opcode.DUALMUSIC:
                args = ExecuteNativeMusicPlay(fiber, opcode);
                break;
            case Jsm.Opcode.MUSICSTOP:
                args = ExecuteMusicStop(fiber);
                break;
            case Jsm.Opcode.MUSICVOL:
            case Jsm.Opcode.MUSICVOLTRANS:
            case Jsm.Opcode.MUSICVOLFADE:
                args = ExecuteMusicVolume(fiber, opcode);
                break;
            case Jsm.Opcode.MUSICVOLSYNC:
                // Native51FDC0 calls46C040(1); the installed helper is xor eax,eax/ret.
                // Return2 continues immediately without touching operands, registers or audio ramps.
                
                break;
            case Jsm.Opcode.ALLSEVOLTRANS:
                args = ExecuteAllSoundVolumeTransition(fiber);
                break;
            case Jsm.Opcode.ALLSEVOL:
                args = ExecuteAllSoundVolume(fiber);
                break;
            case Jsm.Opcode.SEVOL:
                args = ExecuteSoundVolume(fiber);
                break;
            case Jsm.Opcode.RESETGF:
                args = ExecuteGuardianForceReset(fiber);
                break;
            case Jsm.Opcode.ADDITEM:
            case Jsm.Opcode.ADDGIL:
            case Jsm.Opcode.ADDPASTGIL:
                args = ExecuteNativeEconomy(fiber, opcode);
                break;
            case Jsm.Opcode.FOOTSTEPOFFALL:
                args = ExecuteFootstepOffAll(fiber);
                break;
            case Jsm.Opcode.BATTLEMODE:
                args = ExecuteNativeBattleMode(fiber);
                break;
            case Jsm.Opcode.LSCROLLA:
                args = ExecuteNativeActorLinearBackgroundScroll(fiber);
                break;
            case Jsm.Opcode.LSCROLLP:
                args = ExecuteNativePartyLinearBackgroundScroll(fiber);
                break;
            default:
                throw Fault(fiber, $"Unsupported required opcode {opcode} (parameter {parameter}); no state was fabricated.");
        }
        CommandExecuted?.Invoke(new ScriptCommand(FieldId, fiber.ObjectId, fiber.Script.Id, operation.Index, opcode.ToString(), args, scalarArgs));
    }

    private void Calculate(Fiber fiber, int kind)
    {
        NativeScalarValue result = PrepareNativeCalculation(fiber, kind);
        PopNativeScalar(fiber);
        string rightExpression = fiber.LastExpressions[0];
        if (NativeScalarCalculation.OperandCount(kind) == 1)
        {
            fiber.Stack.Push(result);
            fiber.StackExpressions.Push($"{(kind == 5 ? "-" : "~")}({rightExpression})");
            return;
        }
        PopNativeScalar(fiber);
        string leftExpression = fiber.LastExpressions[0];
        fiber.Stack.Push(result);
        string symbol = kind switch { 0 => "+", 1 => "-", 2 => "*", 3 => "/", 4 => "%", 6 => "==", 7 => ">", 8 => ">=", 9 => "<", 10 => "<=", 11 => "!=", 12 => "&", 13 => "|", 14 => "^", 16 => ">>", 17 => "<<", _ => "?" };
        fiber.StackExpressions.Push($"({leftExpression} {symbol} {rightExpression})");
    }

    private Fiber? Request(Fiber caller, int objectId, int scriptId, int priority, Jsm.Opcode opcode)
    {
        if (program is not null && (uint)objectId < program.NativeObjects.Count)
        {
            int rawObjectId = objectId;
            objectId = program.NativeObjects[rawObjectId];
            if (Scene.NativeActorLayoutVersion == 1 && rawObjectId < program.NativeActorCount && !Scene.NativeActorObjectIds.Contains(objectId))
            {
                return null;
            }
        }
        if (objectId < 0 || objectId >= actors.Count)
        {
            throw Fault(caller, $"Request targets invalid object index {objectId}.");
        }
        if (!actors[objectId].Active && !IsRetainedNativeRequestBody(objectId))
        {
            throw Fault(caller, $"Request targets inactive object {objectId}; behavior requires further evidence.");
        }
        string? unsupportedSlot = UnsupportedRequestSlot(objectId, priority, opcode, out int nativePriority, out bool busy);
        if (unsupportedSlot is not null)
        {
            throw Fault(caller, unsupportedSlot);
        }
        priority = nativePriority;
        if (busy && (opcode == Jsm.Opcode.REQ || NativeRuntimeProfile is { IsVerified: true }))
        {
            return null;
        }
        if (NativeRuntimeProfile is { IsVerified: true })
        {
            scriptId = unchecked((ushort)scriptId);
        }
        bool doorRequest = IsNativeDoorRequest(objectId);
        Fiber requested = Queue(objectId, scriptId, doorRequest ? 7 - priority : priority, false);
        if (doorRequest)
        {
            requested.NativeDoorSlot = (byte)priority;
            SelectNativeDoorRequest(requested);
        }
        else
        {
            if (IsNativeActorRequest(objectId))
            {
                requested.NativeActorSlot = (byte)priority;
                SelectNativeActorRequest(requested);
            }
            // Original door records lack the physical actor movement-backup flag.
            SuspendNativeMovementForRequest(actors[objectId], requested);
        }
        if (opcode == Jsm.Opcode.REQEW)
        {
            caller.Request = requested;
            caller.WaitReason = WaitReason.RequestEnd;
        }
        else if (opcode == Jsm.Opcode.REQSW && (busy || IsNativeActorRequest(objectId) && CurrentNativeRequestSlot(actors[objectId]) > priority))
        {
            caller.Request = requested;
            caller.WaitReason = WaitReason.RequestStart;
        }
        return requested;
    }

    private void RequestParty(Fiber caller, int partySlot, int scriptIndex, int priority, Jsm.Opcode opcode)
    {
        if (program is null || (uint)partySlot >= 3 || (uint)priority >= 8)
        {
            throw Fault(caller, $"{opcode} party slot or priority is outside the native request bounds.");
        }
        ActorState? target = ResolveJoinPartyActor(partySlot);
        if (target is null)
        {
            return;
        }
        if (RequestPriorityOccupied(target.ObjectId, priority))
        {
            // Native PREQ/PREQSW/PREQEW consume and drop a request whose priority slot is occupied.
            return;
        }
        if ((uint)scriptIndex >= program.Objects[target.ObjectId].Scripts.Count)
        {
            throw Fault(caller, $"{opcode} targets an unknown party actor script index.");
        }
        Fiber requested = Queue(target.ObjectId, program.Objects[target.ObjectId].Scripts[scriptIndex].Id, priority, false);
        if (IsNativeActorRequest(target.ObjectId))
        {
            requested.NativeActorSlot = (byte)priority;
            SelectNativeActorRequest(requested);
        }
        // Native51D1F0/51D360/51D530 use the same physical mode1 backup
        // as51CD60/51CED0/51D060. Saved party selection does not change ownership.
        SuspendNativeMovementForRequest(target, requested);
        if (opcode == Jsm.Opcode.PREQEW)
        {
            caller.Request = requested;
            caller.WaitReason = WaitReason.RequestEnd;
        }
        else if (opcode == Jsm.Opcode.PREQSW && fibers.Any(fiber => fiber.ObjectId == target.ObjectId && !fiber.Complete && NativeActorRequestSlot(fiber) > priority))
        {
            caller.Request = requested;
            caller.WaitReason = WaitReason.RequestStart;
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
        if (NativeRuntimeProfile is { IsVerified: true })
        {
            PlayNativeAnimation(fiber, actor, opcode, animationId, out args);
            return;
        }
        if (opcode == Jsm.Opcode.RANIMELOOP)
        {
            // Native526A3A selects the signed low WORD of the inline operand.
            animationId = unchecked((short)animationId);
        }
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
        else if (opcode == Jsm.Opcode.RANIMELOOP)
        {
            // Native526A30 returns3: advance the PC and yield this actor's VM turn.
            WaitFrames(fiber, 1);
        }
    }
}
