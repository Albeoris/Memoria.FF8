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
        if (state.Alternate43 != 0)
        {
            throw new NotSupportedException("Native alternate sequence43 requires its separate scratch-bank owner.");
        }
        NativeBattleGraphicsModel model = source;
        List<NativeBattleSequenceCue> cues = [];
        int position = resource.SequenceStart(sequence) + state.Offset46;
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
            if ((uint)offset > ushort.MaxValue)
            {
                throw new InvalidDataException("Native sequence cursor exceeds its WORD owner.");
            }
            state = state with { Offset46 = (ushort)offset, Callback42 = unchecked((byte)(state.Callback42 + 1)) };
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
            byte frame = state.PoseFrame114 ?? throw new InvalidDataException("Native sequence reads a pose counter before its original selection.");
            byte limit = state.PoseLimit115 ?? throw new InvalidDataException("Native sequence reads a pose limit before its original selection.");
            if (frame >= limit)
            {
                return true;
            }
            int steps = (state.PoseFlags109 & 2) != 0 ? 2 : 1;
            byte interpolation = (byte)(4 * ((((state.PoseFlags109 >> 2) & 3) + 1) % 2));
            state = state with { PoseFrame114 = (byte)Math.Min(limit, frame + steps), PoseFlags109 = (byte)((state.PoseFlags109 & ~0xC) | interpolation) };
            return false;
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
            state = state with { Pose = pose, PoseFrame114 = 0, PoseLimit115 = limit, PoseFlags109 = (byte)(speed & ~0xC) };
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
        }

        void Execute()
        {
            int? accumulator = null;
            int?[] temporaries = new int?[128];
            for (int instruction = 0; instruction < 100_000; instruction++)
            {
                int origin = position;
                byte opcode = ReadByte();
                if (opcode >= 0xC0)
                {
                    if (opcode < 0xE4)
                    {
                        int operand = (opcode & 3) switch
                        {
                            0 => unchecked((short)(ReadByte() | ReadByte() << 8)),
                            1 => unchecked((sbyte)ReadByte()),
                            2 => ReadByte(),
                            _ => ReadVariable(ReadByte())
                        };
                        int current = (opcode & 0xFC) == 0xC0 ? 0 : RequireAccumulator();
                        accumulator = (opcode & 0xFC) switch
                        {
                            0xC0 => operand,
                            0xC4 => unchecked(current + operand),
                            0xC8 => unchecked(current - operand),
                            0xCC => unchecked(current * operand),
                            0xD0 => current / operand,
                            0xD4 => current & operand,
                            0xD8 => current | operand,
                            0xDC => current ^ operand,
                            0xE0 => current % operand,
                            _ => throw new InvalidDataException("Invalid native expression operation.")
                        };
                    }
                    else if (opcode == 0xE4)
                    {
                        accumulator = 0;
                    }
                    else if (opcode == 0xE5)
                    {
                        WriteVariable(ReadByte(), RequireAccumulator());
                    }
                    else if (opcode <= 0xF3)
                    {
                        int kind = (opcode - 0xE6) % 7;
                        int value = kind == 0 ? 0 : RequireAccumulator();
                        bool take = kind switch { 0 => true, 1 => value > 0, 2 => value >= 0, 3 => value == 0, 4 => value != 0, 5 => value <= 0, _ => value < 0 };
                        int relative = opcode < 0xED ? unchecked((sbyte)ReadByte()) : unchecked((short)(ReadByte() | ReadByte() << 8));
                        if (take)
                        {
                            position = origin + relative;
                        }
                    }
                    else
                    {
                        throw new NotSupportedException($"Native expression opcode{opcode:X2} has no reached contract.");
                    }
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
                    case 0x85:
                        model = model with { Flags0 = (ushort)(model.Flags0 & ~0x10) };
                        break;
                    case 0x86:
                        model = model with { Flags0 = (ushort)(model.Flags0 | 0x10) };
                        break;
                    case 0x91:
                        cues.Add(new(NativeBattleSequenceCueKind.Text, ReadByte(), ReadByte()));
                        break;
                    case 0x94:
                        model = model with { Flags0 = (ushort)(model.Flags0 ^ 0x20) };
                        break;
                    case 0x9C:
                        model = model with { Flags2 = (ushort)(RequireFlags2() | 0x40) };
                        break;
                    case 0x9D:
                        cues.Add(new(NativeBattleSequenceCueKind.SuspendParticipant));
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
                        cues.Add(new(NativeBattleSequenceCueKind.ActionCallback));
                        break;
                    case 0xAC:
                        byte restored = ReadByte();
                        if (restored == 0)
                        {
                            restored = state.Pending3 != 0 && resource.Contains(state.Pending3) ? state.Pending3 : DefaultSequence();
                        }
                        cues.Add(new(NativeBattleSequenceCueKind.RestoreStanding, 1));
                        model = model with { Flags2 = (ushort)(RequireFlags2() & ~8) };
                        state = state with { Pending3 = 0 };
                        SwitchSequence(restored);
                        flags = 1;
                        return;
                    case 0xB5:
                    case 0xB6:
                    case 0xB8:
                        byte sound = ReadByte();
                        byte attribute = ReadByte();
                        cues.Add(new(opcode == 0xB8 ? NativeBattleSequenceCueKind.SystemSound : NativeBattleSequenceCueKind.ActorSound, sound, attribute, (attribute & 2) != 0 ? ReadByte() << 8 : 0));
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
                    case 0xBD:
                        cues.Add(new(NativeBattleSequenceCueKind.Texture, ReadByte(), ReadByte()));
                        break;
                    case 0xBE:
                        byte blink = ReadByte();
                        cues.Add(new(NativeBattleSequenceCueKind.Texture, 0, 0));
                        model = model with { Flags0 = (ushort)((model.Flags0 & ~0x400) | (blink != 0 ? 0x400 : 0)) };
                        break;
                    default:
                        throw new NotSupportedException($"Native model{model.Index} sequence{sequence} opcode{opcode:X2} requires its original graphical service.");
                }
            }
            throw new InvalidDataException("Native graphics sequence exceeded the bounded instruction budget without a callback yield.");

            int RequireAccumulator() => accumulator ?? throw new InvalidDataException("Native sequence reads the unresolved initial expression accumulator.");

            int ReadVariable(byte variable)
            {
                if (variable >= 128)
                {
                    return temporaries[variable - 128] ?? throw new InvalidDataException("Native sequence reads an unwritten temporary DWORD.");
                }
                if (variable < 8)
                {
                    return state.Locals[variable] ?? throw new InvalidDataException("Native sequence reads an uninitialized local WORD.");
                }
                if (variable >= 120)
                {
                    return state.Scratch[127 - variable] ?? throw new InvalidDataException("Native sequence reads an uninitialized scratch WORD.");
                }
                return variable switch
                {
                    8 => unchecked((short)flags),
                    9 => state.PoseFrame114 ?? throw new InvalidDataException("Native sequence lacks its current pose frame."),
                    10 => state.PoseLimit115 ?? throw new InvalidDataException("Native sequence lacks its current pose limit."),
                    11 => state.Default2,
                    12 => unchecked((short)(inputs.GlobalRandom ?? throw new InvalidDataException("Native sequence lacks534AA0 random state."))()),
                    27 => model.Index,
                    31 => unchecked((short)(model.Flags8 ?? throw new InvalidDataException("Native sequence lacks projected model flags."))),
                    34 => inputs.EntryAdvantage ?? throw new InvalidDataException("Native sequence lacks48AFD0 entry advantage."),
                    35 => unchecked((short)(inputs.CameraTasks ?? throw new InvalidDataException("Native sequence lacks actual camera task flags."))),
                    55 => model.Flags0 & 4,
                    _ when variable is 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20 or 21 or 22 or 23 or 24 or 25 or 26 or 28 or 36 or 37 or 38 or 39 or 40 or 41 or 42 or 44 or 46 or 51 or 53 or 54 => (inputs.ReadExternal ?? throw new NotSupportedException($"Native sequence variable{variable:X2} requires its original owner."))(variable),
                    _ => 0
                };
            }

            void WriteVariable(byte variable, int value)
            {
                if (variable >= 128)
                {
                    temporaries[variable - 128] = value;
                }
                else if (variable < 8)
                {
                    state = state with { Locals = state.Locals.SetItem(variable, unchecked((short)value)) };
                }
                else if (variable >= 120)
                {
                    state = state with { Scratch = state.Scratch.SetItem(127 - variable, unchecked((short)value)) };
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
                        case 13 or 14 or 15 or 18 or 19 or 20 or 21 or 22 or 23 or 33 or 36 or 37 or 38 or 39 or 42 or 43 or 44 or 45 or 47 or 48 or 49 or 50 or 52:
                            (inputs.WriteExternal ?? throw new NotSupportedException($"Native sequence write{variable:X2} requires its original owner."))(variable, value);
                            break;
                    }
                }
            }
        }

        byte ReadByte()
        {
            if (position < resource.SequenceStart(sequence) || position >= resource.SequenceEnd(sequence))
            {
                throw new InvalidDataException("Native sequence cursor or operand exceeds its original bounded program.");
            }
            return resource.SequenceSection[position++];
        }

        ushort RequireFlags2() => model.Flags2 ?? throw new InvalidDataException("Native graphics service requires model Flags2.");
    }
}
