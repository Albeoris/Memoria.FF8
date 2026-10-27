using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed partial class NativeTheEnd85Runner
    {
        private NativeBattleTheEnd85LeafStep AdvanceLeaf(NativeBattleTheEnd85Frame current, uint address)
        {
            frame = current;
            NativeBattleModulePoolTask task = Task(address);
            switch (task.Callback)
            {
                case 0x784A60:
                    NativeBattleTheEnd85IoAdvance io = NativeBattleTheEnd85Io.Advance784A60(frame, task);
                    frame = io.Frame;
                    Put(address, io.Task);
                    if (io.Request is { } request)
                    {
                        NativeBattleEffectFileAsset file = resources.Files.SingleOrDefault(file => file.FileId == request.FileId) ?? throw new NotSupportedException("Native The End archive request has no supplied original file.");
                        NativeBattleTheEnd85IoQueued queued = NativeBattleTheEnd85Io.CopyAndQueue(frame, owner.NativeFileIo ?? throw new NotSupportedException("Native The End requires its asynchronous file owner."), file);
                        owner.NativeFileIo = queued.FileIo;
                        frame = queued.Frame;
                    }
                    return new(frame, io.Complete);
                case 0x784E30:
                    return Director(address);
                case 0x787E20:
                case 0x787FD0:
                case 0x7882E0:
                    NativeBattleTheEnd85EnvironmentStep environment = NativeBattleTheEnd85Environment.Advance(new(task, Context, owner.NativeStage ?? throw new NotSupportedException("Native The End requires its current stage owner."), graphics.Models), resources.DefaultColor);
                    owner.NativeStage = environment.Input.Stage;
                    graphics = graphics with { Models = environment.Input.Models };
                    PutContext(environment.Input.Context);
                    Put(address, environment.Input.Task);
                    if (environment.MusicGain is short gain)
                    {
                        NativeGlobalSoundProcess sound = owner.NativeGlobalSoundProcess ?? throw new NotSupportedException("Native The End requires its current music owner.");
                        owner.NativeGlobalSoundProcess = sound with { MusicPlayback = (sound.MusicPlayback ?? throw new NotSupportedException("Native The End requires its current music playback.")).SetGain46BB40(0, gain) };
                    }
                    return new(frame, environment.Complete);
                case 0x8DDC30:
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native The End overlay requires its current ordering table.");
                    NativeBattleFullscreenOverlayStep overlay = NativeBattleFullscreenOverlay.Advance8DDC30(new(frame.Memory, frame.Effect.Pools, primitive.Pending), address, frame.Memory.FullscreenOverlayPause2795914 ?? throw new NotSupportedException("Native The End requires its retained overlay pause."), resources.Overlay);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = overlay.Frame.Ordering } };
                    frame = frame with { Memory = overlay.Frame.Memory, Effect = frame.Effect with { Pools = overlay.Frame.Pools } };
                    return new(frame, overlay.Result == 2) { HierarchyReleased = overlay.Result == 2 };
                case 0x78C4C0:
                    return ScreenEmitter(address);
                case 0x78CE80:
                    NativeBattleTheEnd85TaskInput emitter = Input(address);
                    if (unchecked((sbyte)task.ReadByte(41)) == 0)
                    {
                        emitter = NativeBattleTheEnd85Particles.Spawn78CED0(emitter, TaskServices(address));
                        AcceptInput(address, emitter);
                    }
                    else if (unchecked((sbyte)task.ReadByte(41)) != 1)
                    {
                        throw new NotSupportedException("Native The End secondary emitter left its callback table.");
                    }
                    return FinishLeaf(address);
                case 0x785560:
                case 0x787B50:
                case 0x788A90:
                case 0x788B90:
                    return AcceptTask(address, NativeBattleTheEnd85Actors.Advance(Input(address), TaskServices(address)));
                case 0x788420:
                case 0x788580:
                case 0x78CFA0:
                    return AcceptTask(address, NativeBattleTheEnd85Particles.Advance(Input(address), TaskServices(address)));
                case 0x78D250:
                case 0x78D410:
                    return AcceptTask(address, NativeBattleTheEnd85ProgramTasks.Advance(Input(address), TaskServices(address)));
                default:
                    throw new NotSupportedException($"Native The End callback0x{task.Callback:X8} requires its actual current owner.");
            }
        }

        private NativeBattleTheEnd85TaskInput Input(uint address) => new(Task(address), Context, State, frame.Random);
        private void AcceptInput(uint address, NativeBattleTheEnd85TaskInput input)
        {
            State = input.State;
            PutContext(input.Context);
            frame = frame with { Random = input.Random };
            Put(address, input.Task);
        }
        private NativeBattleTheEnd85LeafStep AcceptTask(uint address, NativeBattleTheEnd85TaskStep step)
        {
            AcceptInput(address, step.Input);
            return new(frame, step.Complete);
        }
        private NativeBattleTheEnd85TaskServices TaskServices(uint address) => new(
            (input, descriptor, callback, size, initialize) =>
            {
                AcceptInput(address, input);
                Append(descriptor, callback, size, address, initialize);
                return Input(address);
            },
            (input, callback, parameter) =>
            {
                AcceptInput(address, input);
                int remaining = DrawTask(address, callback, parameter);
                return new(Input(address), remaining);
            },
            input =>
            {
                AcceptInput(address, input);
                NativeBattleModulePoolTask task = Task(address);
                NativeBattleGfAnimationSource source = Program(task.ReadDword(116));
                NativeBattleGfAnimationWorkspace workspace = new(task.ReadRawPayloadSpan().Slice(148 - 12).ToArray().ToImmutableArray());
                workspace = workspace.Initialize7016B0(source, checked((int)task.ReadDword(120)));
                Put(address, task.WriteRetainedBytes(12 + (148 - 12), workspace.Data.AsSpan()));
                return Input(address);
            },
            input =>
            {
                AcceptInput(address, input);
                AppendShadows();
                return Input(address);
            });

        private NativeBattleTheEnd85LeafStep Director(uint address)
        {
            NativeBattleModulePoolTask task = Task(address);
            int scriptOffset = checked((int)(State.Dword(0x25C1B60) - NativeBattleTheEnd85Io.ScriptAddress));
            ushort opcode = task.ReadByte(41) == 2 ? BinaryPrimitives.ReadUInt16LittleEndian((State.Script ?? throw new NotSupportedException("Native The End director requires its current script.")).AsSpan().Slice(scriptOffset, 2)) : (ushort)0;
            uint control = State.Dword(0x25C1C58);
            NativeBattleTheEnd85DirectorInput input = new(task, Context, State.Word(control + 358), opcode, State.Dword(0x25C21D4), State.Dword(0x25BC894));
            NativeBattleTheEnd85DirectorStep step = NativeBattleTheEnd85Director.Advance784E30(input, new(
                active =>
                {
                    Accept(active);
                    SpawnStage(address);
                    return Reload(active);
                },
                (active, sound) =>
                {
                    owner.PublishNativeModuleSound(new(resources.Sounds.TryGetValue(sound, out uint id) ? id : throw new NotSupportedException("Native The End sound descriptor is not supplied."), 0, null) { Pan = 128 });
                    return active;
                },
                (active, enabled) =>
                {
                    NativeBattleUiServices ui = graphics.UiServices ?? throw new NotSupportedException("Native The End requires its current UI flags.");
                    graphics = graphics with { UiServices = enabled ? ui.SetFlags(0x100, 0x200) : ui.SetFlags(0x200, 0x100) };
                    return active;
                },
                active =>
                {
                    Accept(active);
                    Append(0x25C19D8, 0x8DDC30, 64, address, child => child.WriteDword(52, resources.Overlay.Address).WriteWord(56, 0));
                    return Reload(active);
                }));
            Accept(step.Input);
            if (step.AdvanceScript16)
            {
                State = State.WriteDword(0x25C1B60, checked(State.Dword(0x25C1B60) + 16));
            }
            return new(frame, step.Complete);

            void Accept(NativeBattleTheEnd85DirectorInput active)
            {
                Put(address, active.Task);
                PutContext(active.Context);
                State = State.Write(control + 358, BitConverter.GetBytes(active.IoRelease358)).WriteDword(0x25C21D4, active.IoFreeze25C21D4).WriteDword(0x25BC894, active.Pause25BC894);
            }
            NativeBattleTheEnd85DirectorInput Reload(NativeBattleTheEnd85DirectorInput active) => active with { Task = Task(address), Context = Context };
        }

        private void SpawnStage(uint parent)
        {
            ushort stage = BinaryPrimitives.ReadUInt16LittleEndian(Context.AsSpan().Slice(64));
            switch (stage)
            {
                case 1:
                    foreach (uint callback in new uint[] { 0x787E20, 0x7882E0, 0x78D410, 0x787FD0 })
                    {
                        Append(0x25BC580, callback, 96, parent);
                    }
                    Append(0x25BC898, 0x788420, 228, parent);
                    break;
                case 2:
                    Append(0x25BC898, 0x78C4C0, 228, parent);
                    break;
                case 3:
                    Model(0x785560, 0x1718EE4, 32, 1, null);
                    Model(0x787B50, 0x171C42C, 80, 2, 0);
                    Model(0x787B50, 0x171C42C, 80, 2, 1);
                    break;
                case 4:
                    Append(0x25BC898, 0x78CE80, 228, parent);
                    break;
                case 5:
                    Append(0x25BC580, 0x78D250, 96, parent);
                    break;
            }
            void Model(uint callback, uint source, uint size, ushort delay, ushort? variant) => Append(0x25C0F40, callback, 732, parent, child =>
            {
                child = child.WriteDword(116, source).WriteDword(120, size).WriteWord(128, delay).WriteWord(130, 0);
                return variant is ushort value ? child.WriteWord(138, value) : child;
            });
        }

        private NativeBattleTheEnd85LeafStep ScreenEmitter(uint address)
        {
            NativeBattleModulePoolTask task = Task(address);
            switch (unchecked((sbyte)task.ReadByte(41)))
            {
                case 0:
                    task = task.WriteDword(48, 4096).WriteDword(52, 4096).WriteDword(56, 4096).WriteDword(76, 0x156C8BC).WriteWord(224, 128).WriteByte(41, 1);
                    break;
                case 1:
                    task = task.WriteWord(224, unchecked((ushort)Math.Max(108, (int)unchecked((short)(task.ReadWord(224) - 1)))));
                    if (BinaryPrimitives.ReadUInt16LittleEndian(Context.AsSpan().Slice(64)) >= 3)
                    {
                        task = task.WriteByte(38, (byte)(task.ReadByte(38) | 5)).WriteByte(41, 2);
                    }
                    break;
                case 2:
                    break;
                default:
                    throw new NotSupportedException("Native The End screen emitter left its callback table.");
            }
            Put(address, task);
            DrawTask(address, 0x78C530, unchecked((short)task.ReadWord(224)));
            return FinishLeaf(address);
        }

        private NativeBattleTheEnd85LeafStep FinishLeaf(uint address)
        {
            NativeBattleModulePoolTask task = Task(address);
            bool complete = (task.ReadByte(38) & 1) != 0 && task.ReadByte(40) == 0;
            Put(address, task.WriteWord(36, unchecked((ushort)(task.ReadWord(36) + 1))));
            return new(frame, complete);
        }
    }
}
