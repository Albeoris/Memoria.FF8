using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleHoly175Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public sealed record NativeBattleHoly175TaskStep(NativeBattleHoly175Frame Frame, NativeBattleModulePoolTask Task, int Result);
public sealed record NativeBattleHoly175Sprite31Row(int Index, ushort Frame, short Scale, short Radius, short Height, short Angle);
public sealed record NativeBattleHoly175Sprite31DrawStep(NativeBattleHoly175Frame Frame, short NextOffset);
public abstract record NativeBattleHoly175Operation;
public sealed record NativeBattleHoly175InitializeTextures : NativeBattleHoly175Operation;
public sealed record NativeBattleHoly175CameraControl50A730 : NativeBattleHoly175Operation;
public sealed record NativeBattleHoly175Capture : NativeBattleHoly175Operation;
public sealed record NativeBattleHoly175DrawTask(NativeBattleModulePoolTask Task) : NativeBattleHoly175Operation;
public sealed record NativeBattleHoly175Sprite4Draw(int Index, ushort Frame, short Scale, NativeBattleVector Position, short Age) : NativeBattleHoly175Operation;
public sealed record NativeBattleHoly175StageFlags(byte ClearMask, byte SetMask) : NativeBattleHoly175Operation;
public sealed record NativeBattleHoly175StageFlash(int Amount) : NativeBattleHoly175Operation;
public sealed record NativeBattleHoly175UploadTim : NativeBattleHoly175Operation;
public sealed record NativeBattleHoly175Sound(ImmutableArray<byte> Position) : NativeBattleHoly175Operation;
public sealed record NativeBattleHoly175Contact(int Contact) : NativeBattleHoly175Operation;
public sealed record NativeBattleHoly175LoadCommon(uint DestinationAddress, uint CompletionWordAddress) : NativeBattleHoly175Operation;
public sealed record NativeBattleHoly175Inputs(uint GraphicsFlags, NativeBattleActionPacketsState Packets, Func<int, ImmutableArray<byte>> ReadPositionRaw8, Func<int, short> ReadActorScale38, Func<int, ushort> ReadModelFlags0, Func<NativeBattleHoly175Frame, NativeBattleModulePoolTask, NativeBattleHoly175TaskStep> AdvanceModelColors, Func<NativeBattleHoly175Frame, int, uint, NativeBattleHoly175Frame> AdvanceWarp, Func<NativeBattleHoly175Frame, NativeBattleModulePoolTask, NativeBattleHoly175Sprite31Row, NativeBattleHoly175Sprite31DrawStep> DrawSprite31, Func<NativeBattleHoly175Frame, NativeBattleHoly175Operation, NativeBattleHoly175Frame> Execute);

public static class NativeBattleHoly175Controller
{
    public const uint MainDescriptor = 0x23745A0;
    public const uint MainBank = 0x2374580;
    public const uint ChildDescriptor = 0x2376870;
    public const uint ChildBank = 0x2376888;
    public const uint ArenaAddress = NativeBattleEffectNodeMemory.ArenaAddress;

    public static NativeBattleHoly175Frame Initialize5D1AD0(NativeBattleHoly175Frame frame, NativeBattleHoly175Inputs inputs, NativeBattleHoly175Resources resources)
    {
        if (frame.Effect.Binding is not { Effect: 175, Entry: 0x5D1AD0, Initializer: 0x5D1AB0 } || frame.Effect.Parameters is not { } parameters || frame.Memory.Arena is null)
        {
            throw new InvalidDataException("Native Holy requires its actual entry, action parameters and arena owner.");
        }
        NativeBattleHoly175State state = (frame.Memory.Holy175 ?? throw new NotSupportedException("Native Holy requires its retained process owners.")).Materialize(resources);
        int actor = inputs.Packets.ReadContact(inputs.Packets.Headers[parameters.HeaderIndex7C].FirstContactIndex ?? throw new NotSupportedException("Native Holy requires its actual first header contact.")).Primary.ActorIndex;
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainBank, 0x10, 2);
        (main, int root) = main.Append(0x5D44C0);
        main = Put(main, root, main.Slots[root].WriteWord(0xC, 0));
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 0x24, 100);
        (children, int primary) = children.Append(0x5D1BD0);
        children = Put(children, primary, children.Slots[primary].WriteWord(0xC, 0).WriteWord(0xE, 0).WriteWord(0x20, (ushort)actor));
        state = state with { CasterActor = parameters.Actor78, SelectedActor = actor, Sprite31Rows = ClearActive(state.Sprite31Rows, 100), Sprite4Rows = ClearActive(state.Sprite4Rows, 80) };
        frame = frame with { Memory = frame.Memory with { Holy175 = state }, Effect = frame.Effect with { Pools = [main, children], ReturnedDescriptor = MainDescriptor } };
        frame = inputs.Execute(frame, new NativeBattleHoly175CameraControl50A730());
        if ((parameters.Flags79 & 1) == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleHoly175InitializeTextures());
        }
        return frame;
    }

    public static NativeBattleOriginalEffectState WriteCompletionWord(NativeBattleOriginalEffectState effect, uint address, ushort value)
    {
        NativeBattleEffectTaskPool main = effect.Pools.Single(pool => pool.DescriptorAddress == MainDescriptor);
        long delta = (long)address - MainBank - 0xE;
        if (delta < 0 || delta % main.Stride != 0 || delta / main.Stride >= main.Slots.Length)
        {
            throw new InvalidDataException("Native Holy completion requires its actual final-task WORD E.");
        }
        int slot = (int)(delta / main.Stride);
        NativeBattleModulePoolTask task = main.Slots[slot];
        if ((task.Flags & 1) == 0 || task.Callback != 0x5D4540)
        {
            throw new InvalidDataException("Native Holy completion requires its live final main task.");
        }
        return effect with { Pools = effect.Pools.SetItem(effect.Pools.IndexOf(main), Put(main, slot, task.WriteWord(0xE, value))) };
    }

    public static NativeBattleHoly175Frame Advance5D44C0(NativeBattleHoly175Frame frame, NativeBattleHoly175Inputs inputs, NativeBattleHoly175Resources resources)
    {
        frame.Effect.Validate();
        State().Validate();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Holy lost its action parameters.");
        NativeBattleEffectTaskPool main = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == MainDescriptor);
        NativeBattleEffectTaskPool children = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == ChildDescriptor);
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        NativeBattleEffectTaskStep result = main.Advance((pool, slot) =>
        {
            main = pool;
            NativeBattleModulePoolTask task = pool.Slots[slot];
            if (task.Callback == 0x5D4540)
            {
                if (task.ReadWord(0xC) == 1)
                {
                    Emit(new NativeBattleHoly175LoadCommon(ArenaAddress, MainBank + (uint)(slot * main.Stride) + 0xE));
                    task = main.Slots[slot];
                }
                ushort completion = task.ReadWord(0xE);
                task = Add(task, 0xC, 1);
                return new(Put(main, slot, task), completion != 0 ? 2 : 0);
            }
            if (task.Callback != 0x5D44C0)
            {
                throw new NotSupportedException("Native Holy main bank contains an unsupported callback.");
            }
            frame = frame with { Effect = frame.Effect with { DrawArenaOffset = (task.ReadWord(0xC) & 1) != 0 ? 0xC000 : 0 } };
            NativeBattleEffectTaskStep childStep = children.Advance(Child);
            children = childStep.Pool;
            if (childStep.Survivors == 0 && (parameters.Flags79 & 2) == 0)
            {
                (main, int final) = main.Append(0x5D4540);
                main = Put(main, final, main.Slots[final].WriteWord(0xC, 0).WriteWord(0xE, 0));
            }
            task = Add(task, 0xC, 1);
            return new(Put(main, slot, task), childStep.Survivors == 0 ? 2 : 0);
        });
        main = result.Pool;
        frame = Publish();
        return frame with { Effect = frame.Effect with { ReturnedDescriptor = result.Survivors == 0 ? 0u : MainDescriptor } };

        NativeBattleEffectTaskCallback Child(NativeBattleEffectTaskPool pool, int slot)
        {
            children = pool;
            NativeBattleModulePoolTask task = pool.Slots[slot];
            int phase = S(task, 0xC);
            int code;
            switch (task.Callback)
            {
                case 0x5D1BD0:
                    if (paused)
                    {
                        return new(children, 0);
                    }
                    code = Primary(ref task, phase);
                    break;
                case 0x5D2910:
                    if (phase < 50 && (State().WarpAlive ?? throw new NotSupportedException("Native Holy warp requires its actual alive DWORD.")) != 0)
                    {
                        if (phase < 10)
                        {
                            Emit(new NativeBattleHoly175Capture());
                        }
                        uint brightness = unchecked((uint)Math.Min(255, 128 + 12 * phase));
                        frame = inputs.AdvanceWarp(Publish(), phase, brightness | brightness << 8 | brightness << 16);
                        Reload();
                    }
                    if (paused)
                    {
                        return new(children, 0);
                    }
                    if (phase == 0)
                    {
                        Emit(new NativeBattleHoly175StageFlags(2, 0));
                    }
                    if (phase == 6)
                    {
                        AppendFades(false, 6);
                    }
                    task = Add(task, 0xC, 1);
                    code = S(task, 0xC) > 50 ? 2 : 0;
                    break;
                case 0x5D27C0:
                case 0x5D3030:
                    NativeBattleHoly175TaskStep colors = inputs.AdvanceModelColors(Publish(), task);
                    frame = colors.Frame;
                    Reload();
                    task = colors.Task;
                    code = colors.Result;
                    break;
                case 0x5D3180:
                    code = Sprite31(ref task, paused);
                    break;
                case 0x5D37B0:
                    code = Sprite4(ref task, paused);
                    break;
                case 0x5D3420:
                    NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CD50(new(S(task, 0x1C), 0, S(task, 0x20))) with { X = S(task, 0x10), Y = S(task, 0x12), Z = S(task, 0x14) };
                    SetState(State().WriteMatrix(task.ReadWord(0xE), matrix));
                    if (paused)
                    {
                        return new(children, 0);
                    }
                    if (phase == 0)
                    {
                        int baseAngle = Random() % 4096;
                        int radius = unchecked((short)(unchecked(inputs.ReadActorScale38(task.ReadWord(0x1A)) * 2200) >> 12));
                        radius = 3 * radius / 4;
                        for (int orbit = 0; orbit < 8; orbit++)
                        {
                            int born = Append(0x5D3590);
                            NativeBattleModulePoolTask child = children.Slots[born].WriteWord(0xE, task.ReadWord(0xE)).WriteWord(0x16, U(radius)).WriteWord(0x18, (ushort)((baseAngle + 512 * orbit) & 4095)).WriteWord(0x1A, 2048).WriteWord(0x1C, 2560).WriteWord(0x1E, 0).WriteWord(0x20, 4000).WriteWord(0x22, 600);
                            children = Put(children, born, child);
                        }
                    }
                    if (phase is >= 6 and <= 57)
                    {
                        task = Add(task, 0x1C, S(task, 0x1E));
                        task = Add(task, 0x20, S(task, 0x22));
                    }
                    task = Add(task, 0xC, 1);
                    code = S(task, 0xC) >= 75 ? 2 : 0;
                    break;
                case 0x5D3590:
                    Emit(new NativeBattleHoly175DrawTask(task));
                    if (paused)
                    {
                        return new(children, 0);
                    }
                    if (phase < 6)
                    {
                        task = Add(task, 0x20, -S(task, 0x22));
                        task = Add(task, 0x22, -(S(task, 0x22) / 20));
                    }
                    else if (phase <= 57)
                    {
                        if (phase == 6)
                        {
                            task = task.WriteWord(0x22, task.ReadWord(0x20));
                        }
                        task = Add(task, 0x1A, 120);
                        task = task.WriteWord(0x20, U(S(task, 0x22) + (unchecked(NativeBattleCameraMath.Sine(S(task, 0x1A)) * 400) >> 12)));
                        task = Add(task, 0x18, 60);
                    }
                    else if (phase <= 73)
                    {
                        int age = phase - 58;
                        if (age == 0)
                        {
                            task = task.WriteWord(0x22, task.ReadWord(0x20)).WriteWord(0x1A, U((S(task, 0x20) + S(task, 0x16)) / 8));
                        }
                        if (age >= 8)
                        {
                            task = Add(task, 0x22, -S(task, 0x1A));
                        }
                        task = task.WriteWord(0x20, U(S(task, 0x22) + (unchecked(NativeBattleCameraMath.Sine((age << 11) / 16) * 3000) >> 12)));
                        task = Add(task, 0x18, 30);
                    }
                    else if (phase == 74)
                    {
                        task = task.WriteWord(0x20, U(-S(task, 0x16)));
                    }
                    task = Add(task, 0xC, 1);
                    code = S(task, 0xC) >= 75 ? 2 : 0;
                    break;
                case 0x5D3E20:
                    if (S(task, 0xE) > 0)
                    {
                        return new(Put(children, slot, paused ? task : Add(task, 0xE, -1)), 0);
                    }
                    goto case 0x5D3A50;
                case 0x5D3A50:
                case 0x5D3B80:
                case 0x5D3CE0:
                case 0x5D3FB0:
                case 0x5D42B0:
                case 0x5D43B0:
                    Emit(new NativeBattleHoly175DrawTask(task));
                    if (paused)
                    {
                        return new(children, 0);
                    }
                    switch (task.Callback)
                    {
                        case 0x5D3A50:
                            task = Add(task, 0x18, S(task, 0x1A));
                            break;
                        case 0x5D3B80:
                            task = Add(task, 0x22, 1);
                            break;
                        case 0x5D3CE0:
                            task = Add(task, 0x1C, S(task, 0x1E));
                            task = Add(task, 0x1E, -(S(task, 0x1E) / 32));
                            break;
                        case 0x5D3E20:
                            if (phase >= 12)
                            {
                                task = Add(task, 0x18, S(task, 0x1A));
                                task = Add(task, 0x1A, S(task, 0x1A) >> 4);
                            }
                            break;
                        case 0x5D42B0:
                            task = Add(task, 0x18, S(task, 0x1A));
                            task = Add(task, 0x1C, S(task, 0x1E));
                            if (S(task, 0x1C) < 0)
                            {
                                task = task.WriteWord(0x1C, 0x7FFF);
                            }
                            task = Add(task, 0x1E, S(task, 0x1E) / 8);
                            break;
                        case 0x5D43B0:
                            if (phase >= 8)
                            {
                                task = Add(task, 0x1C, -S(task, 0x1E));
                                task = Add(task, 0x1E, -(S(task, 0x1E) / 6));
                            }
                            break;
                    }
                    task = Add(task, 0xC, 1);
                    int lifetime = task.Callback switch { 0x5D3CE0 => 16, 0x5D3E20 => 26, 0x5D3FB0 or 0x5D42B0 => 24, 0x5D43B0 => 25, _ => 58 };
                    code = S(task, 0xC) >= lifetime ? 2 : 0;
                    break;
                default:
                    throw new NotSupportedException("Native Holy child pool contains an unsupported callback.");
            }
            return new(Put(children, slot, task), code);
        }

        int Primary(ref NativeBattleModulePoolTask task, int phase)
        {
            int actor = task.ReadWord(0x20);
            int sequence = S(task, 0xE);
            if (phase == 0)
            {
                task = Anchor(task, inputs.ReadPositionRaw8(actor));
                SetState(State() with { WarpTextured = 1, WarpAlive = 1 });
            }
            if (phase == 3)
            {
                NativeBattleHoly175WarpInitializationResult grid = NativeBattleHoly175WarpInitialization.InitializeGrid5D2210(State().Warp, frame.Random);
                SetState(State() with { Warp = grid.State });
                frame = frame with { Random = grid.Random };
            }
            if (phase == 4)
            {
                NativeBattleHoly175WarpInitializationResult faces = NativeBattleHoly175WarpInitialization.InitializeFaces5D25D0(State().Warp, frame.Random);
                SetState(State() with { Warp = faces.State });
                frame = frame with { Random = faces.Random };
            }
            if (phase == 5)
            {
                Append(0x5D2910);
                SetState(State() with { SelectedActor = S(task, 0x20) });
            }
            if (phase == 1)
            {
                int born = Append(0x5D3180);
                children = Put(children, born, Anchor(children.Slots[born], RawAnchor(task)));
            }
            if (phase == 6)
            {
                int first = Append(0x5D3420);
                int rotationX = Random() % 50 + 60;
                int rotationZ = Random() % 40 + 30;
                NativeBattleModulePoolTask driver = Anchor(children.Slots[first], RawAnchor(task)).WriteWord(0xE, 0).WriteWord(0x1A, (ushort)actor).WriteWord(0x1C, 0).WriteWord(0x20, 0).WriteWord(0x1E, U(rotationX)).WriteWord(0x22, U(rotationZ));
                children = Put(children, first, driver);
                int second = Append(0x5D3420);
                driver = Anchor(children.Slots[second], RawAnchor(task)).WriteWord(0xE, 1).WriteWord(0x1A, (ushort)actor).WriteWord(0x1C, 0).WriteWord(0x20, 0).WriteWord(0x1E, U(-rotationX)).WriteWord(0x22, U(-rotationZ));
                children = Put(children, second, driver);
            }
            if (phase == 16)
            {
                Append(0x5D37B0);
            }
            if (phase == 30)
            {
                SetState(State() with { WarpTextured = 0 });
                Emit(new NativeBattleHoly175UploadTim());
            }
            if (phase == 31)
            {
                int born = Append(0x5D3A50);
                NativeBattleModulePoolTask child = Anchor(children.Slots[born], RawAnchor(task));
                child = Add(child, 0x12, 500).WriteWord(0x1C, 4096).WriteWord(0x18, (ushort)(Random() % 4096)).WriteWord(0x1A, (ushort)(Random() % 20 + 30));
                children = Put(children, born, child);
                for (int index = 0; index < 2; index++)
                {
                    born = Append(0x5D3B80);
                    child = Add(Anchor(children.Slots[born], RawAnchor(task)), 0x12, 600).WriteWord(0x1C, 4096).WriteWord(0x20, index == 0 ? (ushort)12 : (ushort)4).WriteWord(0x22, index == 0 ? (ushort)0 : (ushort)8);
                    children = Put(children, born, child);
                }
            }
            if (phase is >= 31 and <= 73 && phase % 6 == 1)
            {
                int born = Append(0x5D3CE0);
                children = Put(children, born, Add(Anchor(children.Slots[born], RawAnchor(task)), 0x12, 1500).WriteWord(0x1C, 0x2AA).WriteWord(0x1E, 0x2AA));
            }
            if (phase == 45)
            {
                NativeBattleEffectHeapAllocation delays = resources.DelayTable ?? throw new NotSupportedException("Native Holy requires its original delay-table rows.");
                if (delays.Data.IsDefault || delays.Data.Length != 48)
                {
                    throw new InvalidDataException("Native Holy requires twelve DWORD-spaced delay operands.");
                }
                int baseTheta = Random() % 4096;
                for (int index = 0; index < 12; index++)
                {
                    int born = Append(0x5D3E20);
                    int theta = (baseTheta + index * 4096 / 12) & 4095;
                    int delay = BinaryPrimitives.ReadInt16LittleEndian(delays.Data.AsSpan().Slice(index * 4)) * 3;
                    NativeBattleModulePoolTask child = Anchor(children.Slots[born], RawAnchor(task)).WriteWord(0xE, U(delay));
                    child = Add(child, 0x10, unchecked(NativeBattleCameraMath.Cosine(theta) * 2100) >> 12);
                    child = Add(child, 0x12, 1400);
                    child = Add(child, 0x14, unchecked(NativeBattleCameraMath.Sine(theta) * 2100) >> 12);
                    child = child.WriteWord(0x16, U(3072 - theta)).WriteWord(0x18, 0).WriteWord(0x1C, 4096).WriteWord(0x1A, (ushort)(Random() % 30 + 30));
                    children = Put(children, born, child);
                }
            }
            if (phase == 66)
            {
                int born = Append(0x5D3FB0);
                children = Put(children, born, Anchor(children.Slots[born], RawAnchor(task)));
            }
            if (phase == 78)
            {
                for (int index = 0; index < 2; index++)
                {
                    int born = Append(0x5D42B0);
                    NativeBattleModulePoolTask child = Anchor(children.Slots[born], RawAnchor(task)).WriteWord(0x18, (ushort)(Random() % 4096)).WriteWord(0x1A, U(index == 0 ? 40 : -40)).WriteWord(0x1C, 0).WriteWord(0x1E, index == 0 ? (ushort)320 : (ushort)640);
                    children = Put(children, born, child);
                }
            }
            if (phase == 90)
            {
                int born = Append(0x5D43B0);
                children = Put(children, born, Anchor(children.Slots[born], RawAnchor(task)).WriteWord(0x1C, 15360).WriteWord(0x1E, 2560).WriteWord(0x20, 15360));
            }
            if (phase >= 103)
            {
                if (phase == 103)
                {
                    Emit(new NativeBattleHoly175StageFlags(0, 2));
                    AppendFades(true, 8);
                }
                Emit(new NativeBattleHoly175StageFlash(unchecked((111 - phase) << 9)));
            }
            if (phase == 106)
            {
                Emit(new NativeBattleHoly175Contact(Header(sequence).FirstContactIndex ?? throw new NotSupportedException("Native Holy requires its selected header contact.")));
            }
            if (phase == 111)
            {
                int next = sequence + 1;
                if (next <= Header(0).ReadByte(17))
                {
                    int born = Append(0x5D1BD0);
                    int nextActor = inputs.Packets.ReadContact(Header(next).FirstContactIndex ?? throw new NotSupportedException("Native Holy requires its next header contact.")).Primary.ActorIndex;
                    children = Put(children, born, children.Slots[born].WriteWord(0xE, U(next)).WriteWord(0x20, (ushort)nextActor));
                }
            }
            if (phase == 1)
            {
                Emit(new NativeBattleHoly175Sound(inputs.ReadPositionRaw8(actor)));
            }
            task = Add(task, 0xC, 1);
            if (S(task, 0xC) <= 111)
            {
                return 0;
            }
            if (sequence == Header(0).ReadByte(17))
            {
                Emit(new NativeBattleHoly175StageFlash(0));
            }
            return 2;
        }

        int Sprite31(ref NativeBattleModulePoolTask task, bool isPaused)
        {
            int alive = 0;
            for (int row = 0; row < 100; row++)
            {
                NativeBattleEffectArena owner = State().Sprite31Rows;
                int offset = row * 24;
                if ((owner.Read(offset, 1)[0] & 1) == 0)
                {
                    continue;
                }
                NativeBattleHoly175Sprite31Row data = new(row, ReadWord(owner, offset + 4), ReadSigned(owner, offset + 6), ReadSigned(owner, offset + 8), ReadSigned(owner, offset + 10), ReadSigned(owner, offset + 12));
                NativeBattleHoly175Sprite31DrawStep drawn = inputs.DrawSprite31(Publish(), task, data);
                frame = drawn.Frame;
                Reload();
                if (isPaused)
                {
                    continue;
                }
                owner = State().Sprite31Rows;
                owner = owner.Write(offset + 4, Word(data.Frame + 1));
                if (drawn.NextOffset < 0)
                {
                    SetState(State() with { Sprite31Rows = owner.Write(offset, new byte[4]) });
                    continue;
                }
                owner = owner.Write(offset + 8, Word(data.Radius - ReadSigned(owner, offset + 0x10))).Write(offset + 10, Word(data.Height + ReadSigned(owner, offset + 0x12))).Write(offset + 12, Word(data.Angle + ReadSigned(owner, offset + 0x14)));
                SetState(State() with { Sprite31Rows = owner });
                alive++;
            }
            if (isPaused)
            {
                return 0;
            }
            int phase = S(task, 0xC);
            if (phase is >= 0 and <= 66)
            {
                for (int spawn = 0; spawn < 2; spawn++)
                {
                    int row = FreeRow(State().Sprite31Rows, 100);
                    if (row < 0)
                    {
                        break;
                    }
                    int scale = Random() % 768 + 384;
                    int radius = Random() % 8000 + 600;
                    int height = 200 - Random() % 3000;
                    int angle = Random() % 4096;
                    int duration = Random() % 14 + 31;
                    int angularVelocity = -10 - Random() % 30;
                    byte[] prefix = new byte[14];
                    BinaryPrimitives.WriteUInt32LittleEndian(prefix, 1);
                    Write(prefix, 6, scale);
                    Write(prefix, 8, radius);
                    Write(prefix, 10, height);
                    Write(prefix, 12, angle);
                    byte[] motion = new byte[6];
                    Write(motion, 0, radius / duration);
                    Write(motion, 2, (S(task, 0x12) - height) / duration);
                    Write(motion, 4, angularVelocity);
                    NativeBattlePilotVisualLifetimes.Allocated(State().Sprite31Rows, 0x5D34D0, row);
                    SetState(State() with { Sprite31Rows = State().Sprite31Rows.Write(row * 24, prefix).Write(row * 24 + 0x10, motion) });
                }
            }
            task = Add(task, 0xC, 1);
            return S(task, 0xC) >= 31 && alive == 0 ? 2 : 0;
        }

        int Sprite4(ref NativeBattleModulePoolTask task, bool isPaused)
        {
            int alive = 0;
            for (int row = 0; row < 80; row++)
            {
                NativeBattleEffectArena owner = State().Sprite4Rows;
                int offset = row * 24;
                if ((owner.Read(offset, 1)[0] & 1) == 0)
                {
                    continue;
                }
                ushort sprite = ReadWord(owner, offset + 4);
                short scale = ReadSigned(owner, offset + 6);
                NativeBattleVector position = new(ReadSigned(owner, offset + 8), ReadSigned(owner, offset + 10), ReadSigned(owner, offset + 12));
                short age = ReadSigned(owner, offset + 14);
                Emit(new NativeBattleHoly175Sprite4Draw(row, sprite, scale, position, age));
                if (isPaused)
                {
                    continue;
                }
                owner = State().Sprite4Rows;
                short nextAge = unchecked((short)(age + 1));
                owner = owner.Write(offset + 14, Word(nextAge));
                if (nextAge >= ReadSigned(owner, offset + 0x16))
                {
                    SetState(State() with { Sprite4Rows = owner.Write(offset, new byte[4]) });
                    continue;
                }
                short velocity = ReadSigned(owner, offset + 0x12);
                owner = owner.Write(offset + 10, Word(position.Y + velocity)).Write(offset + 0x12, Word(velocity + (velocity >> 3)));
                SetState(State() with { Sprite4Rows = owner });
                alive++;
            }
            if (isPaused)
            {
                return 0;
            }
            int phase = S(task, 0xC);
            if (phase is >= 0 and <= 58)
            {
                for (int spawn = 0; spawn < 2; spawn++)
                {
                    int row = FreeRow(State().Sprite4Rows, 80);
                    if (row < 0)
                    {
                        break;
                    }
                    int sprite = Random() & 3;
                    int scale = Random() % 144 + 128;
                    int x = Random() % 340 - 170;
                    int y = Random() % 350 - 50;
                    int velocity = -1 - Random() % 10;
                    int duration = Random() % 10 + 10;
                    byte[] prefix = new byte[16];
                    BinaryPrimitives.WriteUInt32LittleEndian(prefix, 1);
                    Write(prefix, 4, sprite);
                    Write(prefix, 6, scale);
                    Write(prefix, 8, x);
                    Write(prefix, 10, y);
                    NativeBattlePilotVisualLifetimes.Allocated(State().Sprite4Rows, 0x5D44D0, row);
                    SetState(State() with { Sprite4Rows = State().Sprite4Rows.Write(row * 24, prefix).Write(row * 24 + 0x12, Word(velocity)).Write(row * 24 + 0x16, Word(duration)) });
                }
            }
            task = Add(task, 0xC, 1);
            return S(task, 0xC) >= 4 && alive == 0 ? 2 : 0;
        }

        void AppendFades(bool fadeIn, ushort duration)
        {
            int selected = State().SelectedActor ?? throw new NotSupportedException("Native Holy fade selection requires its retained target DWORD.");
            int first = selected >= 3 ? 0 : 3;
            int count = selected >= 3 ? 3 : 4;
            for (int actor = first; actor < first + count; actor++)
            {
                ushort flags = inputs.ReadModelFlags0(actor);
                bool admitted = fadeIn ? BinaryPrimitives.ReadUInt32LittleEndian(State().Visibility.Read(actor * 24, 4)) != 0 : (flags & 2) != 0 && (flags & 4) == 0;
                if (!fadeIn)
                {
                    SetState(State() with { Visibility = State().Visibility.Write(actor * 24, Dword(admitted ? 1u : 0)) });
                }
                if (admitted)
                {
                    int born = Append(fadeIn ? 0x5D27C0u : 0x5D3030u);
                    children = Put(children, born, children.Slots[born].WriteWord(0xE, (ushort)actor).WriteWord(0x18, duration).WriteWord(0x1A, (ushort)(flags & 0x800)));
                }
            }
        }

        int Append(uint callback)
        {
            (children, int slot) = children.Append(callback);
            children = Put(children, slot, children.Slots[slot].WriteWord(0xC, 0));
            return slot;
        }
        ushort Random()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
        NativeBattleActionHeaderSlot Header(int sequence) => inputs.Packets.Headers[parameters.HeaderIndex7C + sequence];
        NativeBattleHoly175State State() => frame.Memory.Holy175 ?? throw new NotSupportedException("Native Holy lost its retained process owners.");
        void SetState(NativeBattleHoly175State state) => frame = frame with { Memory = frame.Memory with { Holy175 = state } };
        NativeBattleHoly175Frame Publish() => frame with { Effect = frame.Effect with { Pools = [main, children] } };
        void Reload()
        {
            main = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == MainDescriptor);
            children = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == ChildDescriptor);
        }
        void Emit(NativeBattleHoly175Operation operation)
        {
            frame = inputs.Execute(Publish(), operation);
            Reload();
        }
    }

    private static NativeBattleEffectArena ClearActive(NativeBattleEffectArena owner, int count)
    {
        using NativeBattleEffectArenaWriter writer = new(owner);
        for (int index = 0; index < count; index++)
        {
            writer.Write(index * 24, new byte[4]);
        }
        return writer.Freeze();
    }
    private static int FreeRow(NativeBattleEffectArena owner, int count)
    {
        for (int index = 0; index < count; index++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(owner.Read(index * 24, 4)) == 0)
            {
                return index;
            }
        }
        return -1;
    }
    private static NativeBattleModulePoolTask Anchor(NativeBattleModulePoolTask task, ImmutableArray<byte> raw)
    {
        if (raw.IsDefault || raw.Length != 8)
        {
            throw new InvalidDataException("Native Holy requires its actual four-WORD world anchor.");
        }
        return task.WriteDword(0x10, BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan())).WriteDword(0x14, BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan().Slice(4)));
    }
    private static ImmutableArray<byte> RawAnchor(NativeBattleModulePoolTask task)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, task.ReadDword(0x10));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), task.ReadDword(0x14));
        return bytes.ToImmutableArray();
    }
    private static NativeBattleEffectTaskPool Put(NativeBattleEffectTaskPool pool, int slot, NativeBattleModulePoolTask task) => pool.WriteSlot(slot, task);
    private static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, U(S(task, offset) + value));
    private static ushort U(int value) => unchecked((ushort)value);
    private static short S(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static ushort ReadWord(NativeBattleEffectArena owner, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(owner.Read(offset, 2));
    private static short ReadSigned(NativeBattleEffectArena owner, int offset) => unchecked((short)ReadWord(owner, offset));
    private static byte[] Word(int value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, U(value));
        return bytes;
    }
    private static byte[] Dword(uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }
    private static void Write(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), U(value));
}
