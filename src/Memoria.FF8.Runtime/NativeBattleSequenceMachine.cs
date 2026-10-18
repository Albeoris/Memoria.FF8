using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>504290 callback timing and the reached local/control operations of504BB0/50DB40.</summary>
public static class NativeBattleSequenceMachine
{
    public static NativeBattleSequenceStep Advance(NativeBattleGraphicsModel source, NativeBattleGraphicsResource resource, NativeBattleSequenceInputs inputs)
    {
        resource.Validate();
        NativeBattleSequenceState state = source.SequenceState ?? throw new InvalidDataException("Native graphics model lacks its resource-header sequence owner.");
        state.Validate();
        if ((source.Flags0 & 2) == 0 || source.Sequence is not byte sequence || source.ResourceFlags44 is not ushort flags)
        {
            throw new InvalidDataException("Native sequence callback requires a loaded graphics model and selected resource.");
        }
        NativeBattleGraphicsModel model = source;
        List<NativeBattleSequenceCue> cues = [];
        bool alternatePass = false;
        byte executingSequence = sequence;
        int position = resource.SequenceStart(sequence) + state.Offset46;
        if (state.Alternate43 != 0 && (RequireFlags2() & 8) == 0)
        {
            alternatePass = true;
            executingSequence = state.Alternate43;
            position = resource.SequenceStart(executingSequence);
            Execute();
            alternatePass = false;
            executingSequence = sequence;
            position = resource.SequenceStart(sequence) + state.Offset46;
        }
        if ((flags & 1) != 0)
        {
            flags &= 0xFFFE;
            Execute();
            if ((flags & 3) != 0)
            {
                return Finish();
            }
            if (AdvancePose())
            {
                SelectPose(state.Pose ?? throw new InvalidDataException("Native sequence lacks its current pose selector."));
                flags |= 2;
            }
            return Finish();
        }
        if (AdvancePose())
        {
            flags = (ushort)((flags & ~0xC) | 4);
            PublishBodyPose508C90();
        }
        else
        {
            flags &= 0xFFFB;
        }
        if ((flags & 8) == 0)
        {
            if (state.Wait1 != 0)
            {
                state = state with { Wait1 = (byte)(state.Wait1 - 1) };
            }
            else
            {
                Execute();
            }
        }
        return Finish();

        NativeBattleSequenceStep Finish()
        {
            int offset = position - resource.SequenceStart(sequence);
            state = state with { Offset46 = unchecked((ushort)offset), Callback42 = unchecked((byte)(state.Callback42 + 1)) };
            if (state.Pending3 != 0 && (flags & 0x40) != 0 && resource.Contains(state.Pending3))
            {
                sequence = state.Pending3;
                state = state with { Offset46 = 0, Pending3 = 0, Callback42 = 0 };
                flags = 1;
            }
            return new(model with { Sequence = sequence, SequenceFrame = checked(source.SequenceFrame + 1), ResourceFlags44 = flags, SequenceState = state }, cues.ToImmutableArray());
        }

        bool AdvancePose()
        {
            if (state.LinkedPose is { } linked)
            {
                NativeBattlePoseProgress nextLinked = linked.Advance();
                state = state with { LinkedPose = nextLinked };
                if (resource.LinkedPoseSource is { } linkedSource && nextLinked.Frame != 0 && nextLinked != linked)
                {
                    state = state with { LinkedDecodedPose = linkedSource.Sample(nextLinked.Pose, nextLinked.Frame, nextLinked.Flags) };
                }
            }
            byte frame = state.PoseFrame114 ?? throw new InvalidDataException("Native sequence reads a pose counter before its original selection.");
            byte limit = state.PoseLimit115 ?? throw new InvalidDataException("Native sequence reads a pose limit before its original selection.");
            if (frame >= limit)
            {
                return true;
            }
            NativeBattlePoseProgress progress = new NativeBattlePoseProgress(state.Pose ?? 0, frame, limit, state.PoseFlags109).Advance();
            state = state with { PoseFrame114 = progress.Frame, PoseFlags109 = progress.Flags };
            if (resource.PoseSource is { } poseSource && progress.Frame != 0)
            {
                state = state with { DecodedPose = poseSource.Sample(progress.Pose, progress.Frame, progress.Flags) };
            }
            PublishBodyPose508C90();
            return false;
        }

        void PublishBodyPose508C90()
        {
            model = resource.PoseSource is { } bodySource
                ? NativeBattleBodyBoneMatrices.Publish508C90(model with { SequenceState = state }, bodySource)
                : model with { CurrentBodyBoneMatrices = null };
        }

        void SelectPose(byte pose)
        {
            if (resource.AnimationFrames.IsDefault || pose >= resource.AnimationFrames.Length)
            {
                throw new InvalidDataException($"Native pose{pose} lacks its original animation frame-count byte.");
            }
            byte speed = (byte)(state.PoseFlags109 & ~3);
            if ((model.Flags2 & 8) == 0 && sequence == state.Default2)
            {
                uint status = model.Flags8 ?? throw new InvalidDataException("Native pose speed requires its projected status flags.");
                speed = (byte)((speed & ~3) | ((status & 0x8000) != 0 ? 1 : (status & 0x4000) != 0 ? 2 : 0));
            }
            byte limit = (speed & 1) != 0 ? unchecked((byte)(2 * resource.AnimationFrames[pose] - 1)) : resource.AnimationFrames[pose];
            NativeBattlePoseProgress? linked = null;
            if (!resource.LinkedAnimationFrames.IsDefaultOrEmpty)
            {
                if (pose >= resource.LinkedAnimationFrames.Length)
                {
                    throw new InvalidDataException($"Native linked weapon pose{pose} lacks its own animation frame-count byte.");
                }
                linked = new(pose, 0, (speed & 1) != 0 ? unchecked((byte)(2 * resource.LinkedAnimationFrames[pose] - 1)) : resource.LinkedAnimationFrames[pose], (byte)(((state.LinkedPose?.Flags ?? 0) & ~0xF) | (speed & 3)));
            }
            state = state with { Pose = pose, PoseFrame114 = 0, PoseLimit115 = limit, PoseFlags109 = (byte)(speed & ~0xC), LinkedPose = linked };
            AdvancePose();
        }

        byte DefaultSequence()
        {
            if (model.Index >= 3)
            {
                return state.Default2;
            }
            uint status = model.Flags8 ?? throw new InvalidDataException("Native player standing sequence requires its projected status flags.");
            return (status & 2) != 0 ? (byte)3 : (status & 0x1000) != 0 ? (byte)2 : (status & 0x1000000) != 0 ? (byte)16 : (status & 0x800000) != 0 ? (byte)19 : (status & 0x200000) != 0 ? (byte)29 : (status & 0x41021) != 0 ? (byte)2 : (byte)1;
        }

        void SwitchSequence(byte selected)
        {
            position = resource.SequenceStart(selected);
            sequence = selected;
            executingSequence = selected;
        }

        void Execute()
        {
            NativeBattleScriptExpressionState expression = new();
            for (int instruction = 0; instruction < 100_000; instruction++)
            {
                int origin = position;
                byte opcode = ReadByte();
                if (opcode >= 0xC0)
                {
                    NativeBattleScriptExpressions.Execute(opcode, origin, ref position, expression, ReadByte, ReadVariable, WriteVariable);
                    continue;
                }
                if (opcode < 0x80)
                {
                    SelectPose(opcode);
                    flags |= 0xA;
                    return;
                }
                switch (opcode)
                {
                    case 0x80:
                        (inputs.AdvanceTexture ?? throw new NotSupportedException("Native80 requires its actual50C950 mutable texture descriptor owner."))(model, ReadByte());
                        break;
                    case 0x82:
                        model = model with { Flags2 = (ushort)(RequireFlags2() | 0x40) };
                        break;
                    case 0x81:
                        (inputs.ScheduleModelGroupFade ?? throw new NotSupportedException("Native81 requires its original model-group fade task."))(ReadByte());
                        break;
                    case 0x84:
                        (inputs.ScheduleStageFade ?? throw new NotSupportedException("Native84 requires its shared stage fade producer."))(ReadByte());
                        break;
                    case 0x85:
                        model = model with { Flags0 = (ushort)(model.Flags0 & ~0x10) };
                        break;
                    case 0x86:
                        model = model with { Flags0 = (ushort)(model.Flags0 | 0x10) };
                        break;
                    case >= 0x87 and <= 0x8F:
                    case 0xB3:
                        break;
                    case 0x90:
                        model = (inputs.BindModelRing90 ?? throw new NotSupportedException("Native90 requires its actual linked-model pointer bank."))(model, ReadByte());
                        break;
                    case 0x91:
                        cues.Add(new(NativeBattleSequenceCueKind.Text, ReadByte(), ReadByte()));
                        break;
                    case 0x92:
                        model = (inputs.RebindActionGeometry ?? throw new NotSupportedException("Native92 requires its original50D300 geometry producer."))(model with { Sequence = sequence, ResourceFlags44 = flags, SequenceState = state }, ReadByte());
                        state = model.SequenceState ?? throw new InvalidDataException("Native92 lost its current sequence owner.");
                        flags = model.ResourceFlags44 ?? throw new InvalidDataException("Native92 lost its resource WORD44.");
                        break;
                    case 0x93:
                        byte modelSelector = ReadByte();
                        byte modelSequence = ReadByte();
                        ApplySelectedModelSequence(opcode, modelSelector, modelSequence);
                        break;
                    case 0xAF:
                    case 0xBF:
                        ApplySelectedModelSequence(opcode, 0, ReadByte());
                        break;
                    case 0x94:
                        model = model with { Flags0 = (ushort)(model.Flags0 ^ 0x20) };
                        break;
                    case 0x95:
                        model = model with { Transform = RequireTransform() with { Position28 = RequireTransform().Position28 with { X = RequireTransform().Initial20.X, Z = RequireTransform().Initial20.Z } } };
                        break;
                    case 0x96:
                        (inputs.ScheduleCameraShake ?? throw new NotSupportedException("Native96 requires its shared auxiliary camera translation producer."))(ReadByte(), ReadByte(), ReadByte());
                        break;
                    case 0x99:
                    case 0xB1:
                        byte particleBone = ReadByte();
                        NativeBattleAuxiliaryProgram particleProgram = new(resource.SequenceSection, position);
                        (inputs.ScheduleSoundParticle ?? throw new NotSupportedException("Native B1/99 requires its shared506BF0 auxiliary task producer."))(particleProgram, opcode == 0xB1, particleBone);
                        position = NativeBattleAuxiliaryState.SkipSoundParticleProgram(resource.SequenceSection, position);
                        break;
                    case 0x9A:
                        state = state with { Alternate43 = ReadByte() };
                        break;
                    case 0x9B:
                        byte variableTextureSelector = ReadByte();
                        int variableTextureVariant = ReadVariable(ReadByte());
                        (inputs.ApplyTexture ?? throw new NotSupportedException("Native9B requires its actual50C780 texture rectangle copy owner."))(variableTextureSelector, variableTextureVariant);
                        break;
                    case 0x9C:
                        model = model with { Flags2 = (ushort)(RequireFlags2() | 1) };
                        break;
                    case 0x9D:
                        cues.Add(new(NativeBattleSequenceCueKind.SuspendParticipant));
                        break;
                    case 0x9E:
                    case 0xAD:
                    case 0xAE:
                        byte motionFirst = ReadByte();
                        byte motionSecond = opcode == 0xAE ? ReadByte() : (byte)255;
                        short motionFraction = opcode == 0x9E ? (short)0 : unchecked((short)(ReadByte() | ReadByte() << 8));
                        byte motionDuration = ReadByte();
                        model = (inputs.ScheduleModelMotion ?? throw new NotSupportedException("Native model motion requires its shared auxiliary task producer."))(model with { Sequence = sequence, ResourceFlags44 = flags, SequenceState = state }, opcode, motionFirst, motionSecond, motionFraction, motionDuration);
                        break;
                    case 0x9F:
                        NativeBattleAuxiliaryProgram textureProgram = new(resource.SequenceSection, position);
                        (inputs.ScheduleTextureAnimation ?? throw new NotSupportedException("Native9F requires its shared506C10/5057D0 texture animation task owner."))(textureProgram, sequence);
                        model = model with { Flags0 = (ushort)(model.Flags0 & ~0x400) };
                        (inputs.ApplyTexture ?? throw new NotSupportedException("Native9F requires the actual50C780 texture rectangle copy owner."))(0, 0);
                        position = NativeBattleAuxiliaryState.SkipSoundParticleProgram(resource.SequenceSection, position);
                        break;
                    case 0xA0:
                        byte pose = ReadByte();
                        if (state.Pose != pose || (flags & 4) != 0)
                        {
                            SelectPose(pose);
                            flags |= 2;
                        }
                        break;
                    case 0xA1:
                        return;
                    case 0xA2:
                        cues.Add(new(NativeBattleSequenceCueKind.RestoreStanding));
                        RestoreStanding();
                        byte next = state.Pending3 != 0 && resource.Contains(state.Pending3) ? state.Pending3 : DefaultSequence();
                        state = state with { Pending3 = 0 };
                        SwitchSequence(next);
                        break;
                    case 0xA3:
                        state = state with { Default2 = sequence };
                        if (state.Pending3 != 0 && resource.Contains(state.Pending3))
                        {
                            byte pending = state.Pending3;
                            state = state with { Pending3 = 0 };
                            SwitchSequence(pending);
                        }
                        else
                        {
                            flags |= 0x40;
                            state = state with { Pending3 = 0 };
                        }
                        break;
                    case 0xA4:
                        flags |= 4;
                        break;
                    case 0xA5:
                        (inputs.ScheduleModelEffect ?? throw new NotSupportedException("NativeA5 requires its actual571840 shared module-pool producer."))(ReadByte());
                        break;
                    case 0xA6:
                        byte deformationGroup = ReadByte();
                        short deformationX = unchecked((short)(ReadByte() | ReadByte() << 8));
                        short deformationY = unchecked((short)(ReadByte() | ReadByte() << 8));
                        short deformationZ = unchecked((short)(ReadByte() | ReadByte() << 8));
                        short deformationAngle = unchecked((short)(ReadByte() | ReadByte() << 8));
                        model = (inputs.ScheduleModelDeformation ?? throw new NotSupportedException("NativeA6 requires its original shared deformation producer."))(model with { Sequence = sequence, ResourceFlags44 = flags, SequenceState = state }, deformationGroup, new(deformationX, deformationY, deformationZ), deformationAngle);
                        break;
                    case 0xA7:
                        SwitchSequence(ReadByte());
                        break;
                    case 0xA8:
                        model = NativeBattleGraphicsModels.StartService(model, ReadByte());
                        break;
                    case 0xA9:
                        //508F60 resets the decoded pose packet; its frame counter advances at the next504290 callback.
                        return;
                    case 0xAA:
                        if (inputs.ApplyActionContacts is { } applyContacts)
                        {
                            model = applyContacts(model with { Sequence = sequence, ResourceFlags44 = flags, SequenceState = state });
                            state = model.SequenceState ?? throw new InvalidDataException("Native AA lost its resource-header sequence owner.");
                            flags = model.ResourceFlags44 ?? throw new InvalidDataException("Native AA lost its resource WORD44.");
                            sequence = model.Sequence ?? throw new InvalidDataException("Native AA lost its selected sequence.");
                        }
                        else
                        {
                            cues.Add(new(NativeBattleSequenceCueKind.ActionCallback));
                        }
                        break;
                    case 0xAB:
                        int start = resource.SequenceStart(executingSequence);
                        BattleNativeRenzokukenBlock physicalBlock = NativeBattleRenzokukenRules.ReadBlock(resource.SequenceSection.AsSpan(), origin, resource.AnimationFrames);
                        BattleNativeRenzokukenBlock block = physicalBlock with { EndOffset = physicalBlock.EndOffset - start };
                        cues.Add(new(NativeBattleSequenceCueKind.TriggerWindow, sequence, origin - start) { TriggerWindow = block });
                        position = physicalBlock.EndOffset;
                        break;
                    case 0xAC:
                        byte restored = ReadByte();
                        if (restored == 0)
                        {
                            restored = state.Pending3 != 0 && resource.Contains(state.Pending3) ? state.Pending3 : DefaultSequence();
                        }
                        cues.Add(new(NativeBattleSequenceCueKind.RestoreStanding, 1));
                        RestoreStanding();
                        resource = inputs.OriginalResource ?? throw new InvalidDataException("Native AC requires its original resource-pointer owner.");
                        model = model with { ResourceMagicId = null, ActiveStagedResourceRow100 = null, LinkedStagedResourceRow = null, ActiveResourceRow100 = model.ActiveStagedResourceRow100 is not null ? model.OriginalResourceRow104 ?? throw new NotSupportedException("Native AC requires its actual original resource row104 before restoring a staged row.") : model.ActiveResourceRow100, Flags2 = (ushort)(RequireFlags2() & ~8) };
                        state = state with { Pending3 = 0, Callback42 = 0 };
                        SwitchSequence(restored);
                        //AC returns a null PC;504290 immediately re-enters the restored selected sequence in this callback.
                        flags = 0;
                        break;
                    case 0xB7:
                        model = (inputs.ApplyCurrentContact ?? throw new NotSupportedException("Native B7 requires its50A6C0 current physical contact receipt."))(model with { Sequence = sequence, ResourceFlags44 = flags, SequenceState = state });
                        state = model.SequenceState ?? throw new InvalidDataException("Native B7 lost its sequence owner.");
                        flags = model.ResourceFlags44 ?? throw new InvalidDataException("Native B7 lost its resource WORD44.");
                        sequence = model.Sequence ?? throw new InvalidDataException("Native B7 lost its selected sequence.");
                        break;
                    case 0xB2:
                        model = (inputs.ApplyNextContact ?? throw new NotSupportedException("Native B2 requires its50A690 physical contact receipt and pointer advance."))(model with { Sequence = sequence, ResourceFlags44 = flags, SequenceState = state });
                        state = model.SequenceState ?? throw new InvalidDataException("Native B2 lost its sequence owner.");
                        flags = model.ResourceFlags44 ?? throw new InvalidDataException("Native B2 lost its resource WORD44.");
                        sequence = model.Sequence ?? throw new InvalidDataException("Native B2 lost its selected sequence.");
                        break;
                    case 0xB0:
                    case 0xB4:
                        NativeBattleAuxiliaryProgram contactProgram = new(resource.SequenceSection, position);
                        (NativeBattleGraphicsModel contactModel, int contactOffset) = ((opcode == 0xB4 ? inputs.ScheduleContactEffect : inputs.ScheduleModelParticle) ?? throw new NotSupportedException($"Native {opcode:X2} requires its original505900 graphical producer and contact ownership."))(model with { Sequence = sequence, ResourceFlags44 = flags, SequenceState = state }, contactProgram);
                        if (contactOffset < position || contactOffset > resource.SequenceSection.Length)
                        {
                            throw new InvalidDataException("Native particle cursor exceeds its original bounded program.");
                        }
                        model = contactModel;
                        state = model.SequenceState ?? throw new InvalidDataException("Native particle callback lost its resource-header sequence owner.");
                        flags = model.ResourceFlags44 ?? throw new InvalidDataException("Native particle callback lost its resource WORD44.");
                        sequence = model.Sequence ?? throw new InvalidDataException("Native particle callback lost its selected sequence.");
                        position = contactOffset;
                        break;
                    case 0x97:
                    case 0x98:
                    case 0xB5:
                    case 0xB6:
                    case 0xB8:
                        byte sound = ReadByte();
                        byte attribute = ReadByte();
                        NativeBattleSequenceCue soundCue = new(opcode is 0x98 or 0xB8 ? NativeBattleSequenceCueKind.SystemSound : NativeBattleSequenceCueKind.ActorSound, sound, attribute, (attribute & 2) != 0 ? ReadByte() << 8 : 0) { OverrideSoundOnContact4 = opcode is 0x97 or 0x98 };
                        cues.Add(soundCue);
                        inputs.PlaySound?.Invoke(model with { Sequence = sequence, ResourceFlags44 = flags, SequenceState = state }, soundCue);
                        break;
                    case 0xB9:
                        state = state with { Wait1 = unchecked((byte)(ReadByte() - 1)) };
                        return;
                    case 0xBA:
                        AdvancePose();
                        break;
                    case 0xBB:
                        cues.Add(new(NativeBattleSequenceCueKind.CommandTitle));
                        break;
                    case 0xBC:
                        ReadByte();
                        break;
                    case 0xBD:
                        byte textureSelector = ReadByte();
                        byte textureVariant = ReadByte();
                        (inputs.ApplyTexture ?? throw new NotSupportedException("Native BD requires its actual50C780 texture rectangle copy owner."))(textureSelector, textureVariant);
                        break;
                    case 0xBE:
                        (inputs.ApplyTexture ?? throw new NotSupportedException("Native BE requires its actual50C780 texture reset owner."))(0, 0);
                        byte blink = ReadByte();
                        model = model with { Flags0 = (ushort)((model.Flags0 & ~0x400) | (blink != 0 ? 0x400 : 0)) };
                        break;
                    default:
                        throw new NotSupportedException($"Native model{model.Index} sequence{sequence} opcode{opcode:X2} requires its original graphical service.");
                }
            }
            throw new InvalidDataException("Native graphics sequence exceeded the bounded instruction budget without a callback yield.");

            void ApplySelectedModelSequence(byte opcode, byte selector, byte selectedSequence)
            {
                model = (inputs.SelectModelSequence ?? throw new NotSupportedException($"Native {opcode:X2} requires its505C00 physical model sequence selector."))(model with { Sequence = sequence, ResourceFlags44 = flags, SequenceState = state }, opcode, selector, selectedSequence);
                state = model.SequenceState ?? throw new InvalidDataException("Native model selector lost its sequence owner.");
                flags = model.ResourceFlags44 ?? throw new InvalidDataException("Native model selector lost its resource WORD44.");
                sequence = model.Sequence ?? throw new InvalidDataException("Native model selector lost its selected sequence.");
            }

            int ReadVariable(byte variable)
            {
                if (variable < 8)
                {
                    return state.Locals[variable] ?? throw new InvalidDataException("Native sequence reads an uninitialized local WORD.");
                }
                if (variable >= 120)
                {
                    return (alternatePass ? state.AlternateScratch48 : state.Scratch)[127 - variable] ?? throw new InvalidDataException("Native sequence reads an uninitialized scratch WORD.");
                }
                return variable switch
                {
                    8 => flags,
                    9 => state.PoseFrame114 ?? throw new InvalidDataException("Native sequence lacks its current pose frame."),
                    10 => state.PoseLimit115 ?? throw new InvalidDataException("Native sequence lacks its current pose limit."),
                    11 => state.Default2,
                    12 => unchecked((short)(inputs.GlobalRandom ?? throw new InvalidDataException("Native sequence lacks534AA0 random state."))()),
                    13 => state.Header36,
                    14 => state.Header38,
                    15 => state.Header40,
                    20 => model.LinkedTransform?.Position.X ?? 0,
                    21 => model.LinkedTransform?.Position.Y ?? 0,
                    22 => model.LinkedTransform?.Position.Z ?? 0,
                    23 => (model.Rotation14 ?? throw new InvalidDataException("Native sequence lacks its model rotationWORD14.")) & 0xFFF,
                    27 => model.Index,
                    28 => model.Index >= 3 ? 2048 : ((model.Rotation14 ?? throw new InvalidDataException("Native sequence lacks its model rotationWORD14.")) & 0xFFF) <= 2048 ? 0 : 4096,
                    31 => unchecked((int)(model.Flags8 ?? throw new InvalidDataException("Native sequence lacks projected model flags."))),
                    34 => inputs.EntryAdvantage ?? throw new InvalidDataException("Native sequence lacks48AFD0 entry advantage."),
                    35 => (inputs.CameraTasks ?? throw new InvalidDataException("Native sequence lacks actual camera task flags.")),
                    36 => RequireTransform().Scale154,
                    37 => RequireTransform().Scale148.X,
                    38 => RequireTransform().Scale148.Y,
                    39 => RequireTransform().Scale148.Z,
                    42 => RequireTransform().Scale38,
                    44 => RequireTransform().Initial20.Z,
                    55 => model.Flags0 & 4,
                    _ when variable is 13 or 14 or 15 or 16 or 17 or 18 or 19 or 24 or 25 or 26 or 36 or 37 or 38 or 39 or 40 or 41 or 42 or 44 or 46 or 51 or 53 or 54 => inputs.ReadModelExternal is { } readModel ? readModel(model with { Sequence = sequence, ResourceFlags44 = flags, SequenceState = state }, variable) : (inputs.ReadExternal ?? throw new NotSupportedException($"Native sequence variable{variable:X2} requires its original owner."))(variable),
                    _ => 0
                };
            }

            void WriteVariable(byte variable, int value)
            {
                if (variable < 8)
                {
                    state = state with { Locals = state.Locals.SetItem(variable, unchecked((short)value)) };
                }
                else if (variable >= 120)
                {
                    state = alternatePass ? state with { AlternateScratch48 = state.AlternateScratch48.SetItem(127 - variable, unchecked((short)value)) } : state with { Scratch = state.Scratch.SetItem(127 - variable, unchecked((short)value)) };
                }
                else
                {
                    switch (variable)
                    {
                        case 8:
                            flags = unchecked((ushort)value);
                            break;
                        case 11:
                            state = state with { Default2 = unchecked((byte)value) };
                            break;
                        case 13:
                            state = state with { Header36 = unchecked((short)value) };
                            break;
                        case 14:
                            state = state with { Header38 = unchecked((short)value) };
                            break;
                        case 15:
                            state = state with { Header40 = unchecked((short)value) };
                            break;
                        case 20:
                        case 21:
                        case 22:
                            if (model.LinkedTransform is { } linkedTransform)
                            {
                                NativeBattleVector linkedPosition = linkedTransform.Position;
                                linkedPosition = variable switch
                                {
                                    20 => linkedPosition with { X = unchecked((short)value) },
                                    21 => linkedPosition with { Y = unchecked((short)value) },
                                    _ => linkedPosition with { Z = unchecked((short)value) }
                                };
                                model = model with { LinkedTransform = linkedTransform with { Position = linkedPosition } };
                            }
                            break;
                        case 23:
                            model = model with { Rotation14 = unchecked((short)value) };
                            break;
                        case 29:
                            state = state with { Geometry4 = unchecked((byte)value) };
                            break;
                        case 30:
                            state = state with { Geometry5 = unchecked((byte)value) };
                            break;
                        case 32:
                            uint bit = 1u << (value >= 0 ? value - 1 : -1 - value);
                            state = state with { MeshMask124 = value >= 0 ? state.MeshMask124 | bit : state.MeshMask124 & ~bit };
                            break;
                        case 36:
                            model = model with { Transform = RequireTransform() with { Scale154 = unchecked((short)value) } };
                            break;
                        case 37:
                            model = model with { Transform = RequireTransform() with { Scale148 = RequireTransform().Scale148 with { X = unchecked((short)value) } } };
                            break;
                        case 38:
                            model = model with { Transform = RequireTransform() with { Scale148 = RequireTransform().Scale148 with { Y = unchecked((short)value) } } };
                            break;
                        case 39:
                            model = model with { Transform = RequireTransform() with { Scale148 = RequireTransform().Scale148 with { Z = unchecked((short)value) } } };
                            break;
                        case 42:
                            model = model with { Transform = RequireTransform() with { Scale38 = unchecked((short)value) } };
                            break;
                        case 43:
                            model = model with { SequenceByte145 = unchecked((byte)value) };
                            break;
                        case 44:
                            model = model with { Transform = RequireTransform() with { Initial20 = RequireTransform().Initial20 with { Z = unchecked((short)value) }, Position28 = RequireTransform().Position28 with { Z = unchecked((short)value) } } };
                            break;
                        case 33 or 45 or 48 or 49 or 50 or 52:
                            model = (inputs.WriteModelExternal ?? throw new NotSupportedException($"Native sequence write{variable:X2} requires its original target model owner."))(model, variable, value);
                            break;
                        case 18 or 19 or 47:
                            (inputs.WriteExternal ?? throw new NotSupportedException($"Native sequence write{variable:X2} requires its original owner."))(variable, value);
                            break;
                    }
                }
            }
        }

        byte ReadByte()
        {
            if ((uint)position >= resource.SequenceSection.Length)
            {
                throw new InvalidDataException("Native sequence cursor or operand exceeds its original bounded program.");
            }
            return resource.SequenceSection[position++];
        }

        ushort RequireFlags2() => model.Flags2 ?? throw new InvalidDataException("Native graphics service requires model Flags2.");
        NativeBattleModelTransform RequireTransform() => model.Transform ?? throw new NotSupportedException("Native model expression requires its original transform WORD owners.");

        void RestoreStanding()
        {
            model = model with { Rotation12 = 0, Rotation14 = model.Index >= 3 ? (short)2048 : (short)0, Rotation16 = 0 };
        }
    }
}
