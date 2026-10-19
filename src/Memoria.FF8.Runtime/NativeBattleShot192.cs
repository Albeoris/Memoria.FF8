using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShot192Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public abstract record NativeBattleShot192Operation;
public sealed record NativeBattleShot192Bind(byte Actor) : NativeBattleShot192Operation;
public sealed record NativeBattleShot192Sequence(byte Actor, ushort Sequence) : NativeBattleShot192Operation;
public sealed record NativeBattleShot192Stage(int Intensity) : NativeBattleShot192Operation;
public sealed record NativeBattleShot192Contact : NativeBattleShot192Operation;
public sealed record NativeBattleShot192Sound(uint Descriptor, int Channel, int Volume) : NativeBattleShot192Operation;
public sealed record NativeBattleShot192Echo(byte Actor, NativeBattleBodyBoneMatrices Bones, int Geometry) : NativeBattleShot192Operation;
public sealed record NativeBattleShot192Sprite(uint Address, ushort Phase, NativeBattleVector Position, short Scale, int Depth, ushort Flags, NativeBattleActionMatrix? World = null, uint Color = 0x808080) : NativeBattleShot192Operation;
public sealed record NativeBattleShot192ReloadCommon(uint CompletionAddress) : NativeBattleShot192Operation;
public sealed record NativeBattleShot192Mesh(uint Address, NativeBattleActionMatrix World, uint Flags, int? Fade) : NativeBattleShot192Operation;
public sealed record NativeBattleShot192Inputs(uint GraphicsFlags, NativeBattleActionPacketsState Packets, Func<int, NativeBattleShot192Anchor> ReadMuzzle, Func<int, int> BoneCount, Func<int, int, int, NativeBattleVector> ReadBone, Func<int, short> ModelScale, Func<int, NativeBattleVector> ReadCenter, Func<int, NativeBattleBodyBoneMatrices> ReadBones, Func<NativeBattleShot192Frame, NativeBattleShot192Operation, NativeBattleShot192Frame> Execute);

/// <summary>5B96F0/5B9720/5BB1B0 and Scatter Shot's original three physical callback pools.</summary>
public static class NativeBattleShot192
{
    public const uint Root = 0x227C198;

    public static NativeBattleOriginalEffectState WriteCompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        NativeBattleEffectTaskPool pool = effect.Pools.Single(pool => pool.DescriptorAddress == Root);
        uint delta = unchecked(address - pool.BankAddress);
        if (delta % pool.Stride != 14 || delta / pool.Stride >= pool.Slots.Length)
        {
            throw new NotSupportedException("Native Scatter Shot completion requires its actual root task BYTE14.");
        }
        int slot = checked((int)(delta / pool.Stride));
        int index = effect.Pools.IndexOf(pool);
        return effect with { Pools = effect.Pools.SetItem(index, pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteByte(14, value)) }) };
    }

    public static NativeBattleShot192Frame Enter(NativeBattleShot192Frame frame, NativeBattleActionPacketsState packets)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Scatter Shot requires its original action parameters.");
        byte mode = parameters.Parameter80 ?? throw new NotSupportedException("Native Scatter Shot requires its actual module Parameter80.");
        NativeBattleEffectTaskPool[] pools;
        NativeBattleShot192State state;
        if (mode == 0)
        {
            pools = [NativeBattleEffectTaskPool.Initialize(Root, 0x227B348, 16, 4), NativeBattleEffectTaskPool.Initialize(0x227C1A8, 0x227B388, 36, 100), NativeBattleEffectTaskPool.Initialize(0x227FAA0, 0x227FAB0, 36, 100)];
            (pools[0], int root) = pools[0].Append(0x5BB1B0);
            Put(0, root, pools[0].Slots[root].WriteWord(12, 0));
            (pools[1], int opening) = pools[1].Append(0x5BAD80);
            Put(1, opening, pools[1].Slots[opening].WriteWord(12, 0));
            state = (frame.Memory.Shot192 ?? throw new NotSupportedException("Native Scatter Shot requires its retained original module globals; explicitly migrate the standalone snapshot.")).Enter(parameters.Actor78);
        }
        else
        {
            state = frame.Memory.Shot192 ?? throw new NotSupportedException("Native repeated Shot requires its actual retained module globals.");
            state.Validate();
            pools = frame.Effect.Pools.ToArray();
            RequirePools(pools);
            if (mode == 1)
            {
                (pools[0], int ending) = pools[0].Append(0x5BAC60);
                Put(0, ending, pools[0].Slots[ending].WriteWord(12, 0));
            }
            else if (mode == 2)
            {
                if (pools[1].Slots.Any(task => (task.Flags & 1) == 0))
                {
                    (pools[1], int shot) = pools[1].Append(0x5B9880);
                    NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
                    int first = header.FirstContactIndex ?? throw new InvalidDataException("Native Shot requires its physical header contact pointer.");
                    Put(1, shot, pools[1].Slots[shot].WriteWord(12, 0).WriteWord(14, unchecked((ushort)(state.ShotSerial % 100))).WriteWord(24, (ushort)(state.SequenceCursor + 3)).WriteWord(26, packets.ReadContact(first).Primary.ActorIndex));
                    state = state with { ShotSerial = unchecked(state.ShotSerial + 1), SequenceCursor = state.SequenceCursor < 2 ? state.SequenceCursor + 1 : 0 };
                }
            }
        }
        return frame with { Memory = frame.Memory with { Shot192 = state }, Effect = frame.Effect with { Pools = pools.ToImmutableArray(), ReturnedDescriptor = Root } };

        void Put(int pool, int slot, NativeBattleModulePoolTask task) => pools[pool] = pools[pool] with { Slots = pools[pool].Slots.SetItem(slot, task) };
    }

    public static NativeBattleShot192Frame Advance(NativeBattleShot192Frame frame, NativeBattleShot192Inputs inputs, NativeBattleShot192Resources resources)
    {
        NativeBattleEffectTaskPool[] pools = frame.Effect.Pools.ToArray();
        RequirePools(pools);
        NativeBattleShot192State state = frame.Memory.Shot192 ?? throw new NotSupportedException("Native Scatter Shot lost its retained module globals.");
        state.Validate();
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        NativeBattleEffectTaskStep rootStep = pools[0].Advance((pool, index) =>
        {
            pools[0] = pool;
            NativeBattleModulePoolTask task = pool.Slots[index];
            int result;
            if (task.Callback == 0x5BB1B0)
            {
                const int baseOffset = 0;
                frame = frame with { Effect = frame.Effect with { DrawArenaOffset = baseOffset + ((task.ReadWord(12) & 1) != 0 ? 0xC000 : 0) } };
                int actions = AdvancePool(1);
                int particles = AdvancePool(2);
                if (!paused)
                {
                    state = state with { Clock = unchecked(state.Clock + 1) };
                }
                if (particles == 0 && (state.Control & 2) != 0)
                {
                    (pools[0], int cleanup) = pools[0].Append(0x5BB260);
                    Put(0, cleanup, pools[0].Slots[cleanup].WriteWord(12, 0).WriteWord(14, 0));
                    task = task with { Next = pools[0].Slots[index].Next };
                }
                task = Increment(task);
                result = actions == 0 && particles == 0 && (state.Control & 2) != 0 ? 2 : 0;
            }
            else if (task.Callback == 0x5BAC60)
            {
                result = Ending(ref task);
            }
            else if (task.Callback == 0x5BB260)
            {
                if (S(task, 12) == 1)
                {
                    Execute(new NativeBattleShot192ReloadCommon(checked(pools[0].BankAddress + (uint)(index * pools[0].Stride + 14))));
                }
                task = Increment(task);
                result = task.ReadWord(14) != 0 ? 2 : 0;
            }
            else
            {
                throw new NotSupportedException($"Native Scatter Shot root callback{task.Callback:X} is not owned.");
            }
            Put(0, index, task);
            return new(pools[0], result);
        });
        pools[0] = rootStep.Pool;
        return frame with { Memory = frame.Memory with { Shot192 = state }, Effect = frame.Effect with { Pools = pools.ToImmutableArray(), ReturnedDescriptor = rootStep.Survivors == 0 ? 0u : Root } };

        int AdvancePool(int index)
        {
            NativeBattleEffectTaskStep step = pools[index].Advance((pool, slot) =>
            {
                pools[index] = pool;
                NativeBattleModulePoolTask task = pool.Slots[slot];
                int result = task.Callback switch
                {
                    0x5BAD80 => Opening(ref task),
                    0x5BAE70 => Fade(ref task, false),
                    0x5BAD20 => Fade(ref task, true),
                    0x5BAEC0 => Echoes(ref task),
                    0x5B9880 => Shot(ref task),
                    0x5B9EF0 or 0x5BA030 or 0x5BA240 => MuzzleMesh(ref task),
                    0x5BA1A0 or 0x5BA3A0 => MuzzleSprite(ref task),
                    0x5BA4B0 => Impact(ref task),
                    0x5BA800 => Ground(ref task),
                    0x5BA8C0 => GroundMesh(ref task),
                    0x5BAA00 => Dust(ref task),
                    _ => throw new NotSupportedException($"Native Scatter Shot child callback{task.Callback:X} is not owned.")
                };
                Put(index, slot, task);
                return new(pools[index], result);
            });
            pools[index] = step.Pool;
            return step.Survivors;
        }

        int Opening(ref NativeBattleModulePoolTask task)
        {
            if (paused)
            {
                return 0;
            }
            if (S(task, 12) == 0)
            {
                Execute(new NativeBattleShot192Bind(state.Actor));
                Execute(new NativeBattleShot192Sequence(state.Actor, 1));
                Append(2, 0x5BAE70, child => child.WriteWord(12, 0).WriteWord(14, 8).WriteWord(24, 2048));
                Append(2, 0x5BAEC0, child => child.WriteWord(12, 0));
                state = state with { StopClock = 999999, EchoActive = Enumerable.Repeat(false, 5).ToImmutableArray() };
            }
            task = Increment(task);
            if (S(task, 12) < 7)
            {
                return 0;
            }
            state = state with { Control = state.Control | 1 };
            Ready();
            return 2;
        }

        int Ending(ref NativeBattleModulePoolTask task)
        {
            if (paused || (state.Control & 4) != 0)
            {
                return 0;
            }
            if (S(task, 12) == 0)
            {
                Execute(new NativeBattleShot192Sequence(state.Actor, 2));
                Append(2, 0x5BAD20, child => child.WriteWord(12, 0).WriteWord(14, 8).WriteWord(24, 2048));
                state = state with { StopClock = state.Clock };
            }
            if (S(task, 12) == 4)
            {
                Execute(new NativeBattleShot192Contact());
            }
            task = Increment(task);
            if (S(task, 12) < 12)
            {
                return 0;
            }
            state = state with { Control = state.Control | 2 };
            Ready();
            return 2;
        }

        int Fade(ref NativeBattleModulePoolTask task, bool descending)
        {
            int amplitude = S(task, 24);
            int level = amplitude / S(task, 14) * S(task, 12);
            Execute(new NativeBattleShot192Stage(descending ? amplitude - level : level));
            if (paused)
            {
                return 0;
            }
            task = Increment(task);
            if (S(task, 12) < S(task, 14))
            {
                return 0;
            }
            if (descending)
            {
                Execute(new NativeBattleShot192Stage(0));
            }
            return 2;
        }

        int Echoes(ref NativeBattleModulePoolTask task)
        {
            int counter = S(task, 12);
            NativeBattleBodyBoneMatrices current = inputs.ReadBones(state.Actor);
            state = state with { Echoes = state.Echoes.SetItem(counter % 5, current), EchoActive = state.EchoActive.SetItem(counter % 5, true) };
            int history = counter > 0 ? (counter - 1) % 5 : 0;
            int total = counter < state.StopClock ? 5 : unchecked(state.StopClock - counter + 5);
            for (int geometry = 0; geometry < total; geometry++)
            {
                NativeBattleBodyBoneMatrices? snapshot = state.Echoes[history];
                if (!state.EchoActive[history])
                {
                    break;
                }
                Execute(new NativeBattleShot192Echo(state.Actor, snapshot ?? throw new InvalidDataException("Native active Shot echo lost its retained bone bytes."), geometry));
                history = history > 0 ? history - 1 : 4;
            }
            if (paused)
            {
                return 0;
            }
            task = Increment(task);
            return S(task, 12) >= state.StopClock + 5 ? 2 : 0;
        }

        int Shot(ref NativeBattleModulePoolTask task)
        {
            if (paused || (state.Control & 1) == 0)
            {
                return 0;
            }
            int phase = S(task, 12);
            if (phase == 0)
            {
                if ((state.Control & 4) != 0)
                {
                    return 0;
                }
                Execute(new NativeBattleShot192Sequence(state.Actor, task.ReadWord(24)));
                state = state with { Control = state.Control | 4 };
            }
            if (phase == 8)
            {
                Ready();
                state = state with { Control = state.Control & ~4 };
            }
            int serial = S(task, 14);
            if (phase == 0)
            {
                NativeBattleShot192Anchor origin = inputs.ReadMuzzle(state.Actor);
                state = state with { Anchors = state.Anchors.SetItem(serial, origin) };
                int angle = Roll() % 4096;
                int scale = Roll() % 1280 + 2304;
                Append(2, 0x5B9EF0, child => child.WriteWord(12, 0).WriteWord(14, (ushort)serial).WriteWord(24, (ushort)angle).WriteWord(28, (ushort)scale));
                angle = Roll() % 4096;
                scale = (short)(Roll() % 1536 + 2048) / 3;
                Append(2, 0x5BA030, child => child.WriteWord(12, 0).WriteWord(14, (ushort)serial).WriteWord(24, (ushort)angle).WriteWord(28, (ushort)scale).WriteWord(30, (ushort)scale));
                scale = Roll() % 1792 + 1024;
                Append(2, 0x5BA1A0, child => child.WriteWord(12, 0).WriteWord(14, (ushort)serial).WriteWord(28, (ushort)scale));
                Append(2, 0x5BA3A0, child => child.WriteWord(12, 0).WriteWord(14, (ushort)serial).WriteWord(28, unchecked((ushort)(6656 * scale >> 12))));
                scale = (Roll() % 768 + 2560) / 3;
                Append(2, 0x5BA240, child => child.WriteWord(12, 0).WriteWord(14, (ushort)serial).WriteWord(28, (ushort)scale).WriteWord(30, (ushort)scale));
            }
            if (phase == 1)
            {
                NativeBattleActionHeaderSlot header = inputs.Packets.Headers[frame.Effect.Parameters!.HeaderIndex7C];
                int first = header.FirstContactIndex ?? throw new InvalidDataException("Native Scatter Shot requires its original contact array.");
                int count = header.ReadByte(16);
                for (int contact = 0; contact < count; contact++)
                {
                    int target = inputs.Packets.ReadContact(first + contact).Primary.ActorIndex;
                    int bones = inputs.BoneCount(target);
                    int bone = bones == 0 ? 0 : Roll() % bones;
                    int angle = Roll() % 4096;
                    NativeBattleVector point = inputs.ReadBone(target, bone, angle);
                    int scale = Roll() % 3072 + 2048;
                    int impactSerial = state.ImpactSerial;
                    state = state with { ImpactSerial = unchecked(impactSerial + 1) };
                    short height = unchecked((short)(2000 * inputs.ModelScale(target) >> 12));
                    Append(2, 0x5BA4B0, child => WriteVector(child.WriteWord(12, 0).WriteWord(14, unchecked((ushort)impactSerial)).WriteWord(22, unchecked((ushort)height)).WriteWord(24, (ushort)(scale & 1)).WriteWord(28, (ushort)scale), 16, point));
                    if ((Roll() & 1) != 0)
                    {
                        bone = bones == 0 ? 0 : Roll() % bones;
                        angle = Roll() % 4096;
                        point = inputs.ReadBone(target, bone, angle);
                        scale = Roll() % 3072 + 2048;
                        impactSerial = state.ImpactSerial;
                        state = state with { ImpactSerial = unchecked(impactSerial + 1) };
                        Append(2, 0x5BA4B0, child => WriteVector(child.WriteWord(12, 0).WriteWord(14, unchecked((ushort)impactSerial)).WriteWord(22, unchecked((ushort)height)).WriteWord(24, (ushort)(scale & 1)).WriteWord(28, (ushort)scale), 16, point));
                    }
                    else
                    {
                        impactSerial = state.ImpactSerial;
                        state = state with { ImpactSerial = unchecked(impactSerial + 1) };
                        NativeBattleVector center = inputs.ReadCenter(target);
                        center = new(unchecked((short)(center.X + Roll() % (2 * height) - height)), 0, unchecked((short)(center.Z + Roll() % (2 * height) - height)));
                        Append(2, 0x5BA800, child => WriteVector(child.WriteWord(12, 0).WriteWord(14, unchecked((ushort)impactSerial)).WriteWord(22, unchecked((ushort)height)).WriteWord(24, unchecked((ushort)(center.X & 1))), 16, center));
                    }
                }
                Execute(new NativeBattleShot192Contact());
                Execute(new NativeBattleShot192Sound(resources.ShotSound, 2 - ((serial & 1) != 0 ? 1 : 0), 128));
            }
            task = Increment(task);
            return S(task, 12) >= 10 ? 2 : 0;
        }

        int MuzzleMesh(ref NativeBattleModulePoolTask task)
        {
            NativeBattleShot192Anchor anchor = state.Anchors[S(task, 14)] ?? throw new InvalidDataException("Native Shot muzzle child lost its original anchor.");
            NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CE30(new(0, 0, task.Callback == 0x5BA240 ? (short)0 : S(task, 24)));
            NativeBattleActionMatrix combined = NativeBattleGfMatrices.Multiply56C090(anchor.Rotation, rotation);
            combined = NativeBattlePoseMatrices.Scale56BEF0(combined, S(task, 28), S(task, 28), S(task, 28)) with { X = anchor.Position.X, Y = anchor.Position.Y, Z = anchor.Position.Z };
            int phase = S(task, 12);
            bool ring = task.Callback == 0x5BA030;
            bool cone = task.Callback == 0x5BA240;
            int? fade = phase >= 1 ? ring ? 1365 * phase - 1365 : cone ? 682 * phase - 682 : 3072 : null;
            Execute(new NativeBattleShot192Mesh(ring ? 0xD17768u : cone ? 0xD17DC0u : 0xD17220u, combined, phase >= 1 ? 0xF3u : 0x33u, fade));
            if (paused)
            {
                return 0;
            }
            if (ring || cone)
            {
                int velocity = S(task, 30);
                task = task.WriteWord(28, unchecked((ushort)(S(task, 28) + velocity))).WriteWord(30, unchecked((ushort)(velocity - velocity / (ring ? 3 : 4))));
            }
            task = Increment(task);
            return S(task, 12) >= (ring ? 4 : cone ? 7 : 2) ? 2 : 0;
        }

        int MuzzleSprite(ref NativeBattleModulePoolTask task)
        {
            NativeBattleShot192Anchor anchor = state.Anchors[S(task, 14)] ?? throw new InvalidDataException("Native Shot sprite child lost its original anchor.");
            bool aligned = task.Callback == 0x5BA3A0;
            int scale = S(task, 28);
            uint resource = aligned ? 0xD16C30u : 0xD16B58u;
            Execute(new NativeBattleShot192Sprite(resource, task.ReadWord(12), anchor.Position, (short)scale, aligned ? 0 : -(scale >> 3), aligned ? (ushort)8 : (ushort)0, aligned ? NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(1024, 0, 0)), scale, scale, scale) with { X = anchor.Position.X, Y = 0, Z = anchor.Position.Z } : null));
            if (paused)
            {
                return 0;
            }
            bool ended = SpriteEnded(resource, task.ReadWord(12));
            task = Increment(task);
            return ended ? 2 : 0;
        }

        int Impact(ref NativeBattleModulePoolTask task)
        {
            if (S(task, 24) > 0)
            {
                if (!paused)
                {
                    task = task.WriteWord(24, unchecked((ushort)(S(task, 24) - 1)));
                }
                return 0;
            }
            int phase = S(task, 12);
            int serial = S(task, 14);
            int baseOffset = (serial & 1) != 0 ? 300 * 24 : 0;
            if (phase < 16)
            {
                Execute(new NativeBattleShot192Sprite(0xD16CA4, task.ReadWord(12), Vector(task, 16), S(task, 28), -S(task, 22), 8));
            }
            byte[] bytes = state.ImpactParticles.ToArray();
            int active = 0;
            for (int particle = 0; particle < 300; particle++)
            {
                int offset = baseOffset + particle * 24;
                if (I32(bytes, offset) != serial + 1)
                {
                    continue;
                }
                ushort particlePhase = U16(bytes, offset + 4);
                Execute(new NativeBattleShot192Sprite(0xD17074, particlePhase, V(bytes, offset + 8), I16(bytes, offset + 6), -S(task, 22), 8));
                if (paused)
                {
                    continue;
                }
                W16(bytes, offset + 4, particlePhase + 1);
                if (SpriteEnded(0xD17074, particlePhase))
                {
                    W32(bytes, offset, 0);
                    continue;
                }
                for (int axis = 0; axis < 3; axis++)
                {
                    int velocity = I16(bytes, offset + 16 + axis * 2);
                    W16(bytes, offset + 8 + axis * 2, I16(bytes, offset + 8 + axis * 2) + velocity);
                    W16(bytes, offset + 16 + axis * 2, velocity - (velocity >> 2));
                }
                active++;
            }
            if (!paused && phase == 0)
            {
                for (int spawn = 0; spawn < 10; spawn++)
                {
                    int free = Enumerable.Range(0, 300).FirstOrDefault(index => I32(bytes, baseOffset + index * 24) == 0, -1);
                    if (free < 0)
                    {
                        break;
                    }
                    int offset = baseOffset + free * 24;
                    W32(bytes, offset, serial + 1);
                    W16(bytes, offset + 4, 0);
                    W16(bytes, offset + 6, Roll() % 1152 + 256);
                    NativeBattleVector position = Vector(task, 16);
                    W16(bytes, offset + 8, position.X + Roll() % 300 - 150);
                    W16(bytes, offset + 10, position.Y + Roll() % 300 - 150);
                    W16(bytes, offset + 12, position.Z + Roll() % 300 - 150);
                    W16(bytes, offset + 14, S(task, 22));
                    var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(Roll() % 4096 - 2048, Roll() % 4096 - 2048, Roll() % 4096 - 2048);
                    int speed = Roll() % 400 + 180;
                    W16(bytes, offset + 16, unchecked(direction.X * speed) >> 12);
                    W16(bytes, offset + 18, unchecked(direction.Y * speed) >> 12);
                    W16(bytes, offset + 20, unchecked(direction.Z * speed) >> 12);
                }
            }
            state = state with { ImpactParticles = bytes.ToImmutableArray() };
            if (paused)
            {
                return 0;
            }
            task = Increment(task);
            return S(task, 12) >= 4 && active == 0 ? 2 : 0;
        }

        int Ground(ref NativeBattleModulePoolTask task)
        {
            if (S(task, 24) > 0)
            {
                if (!paused)
                {
                    task = task.WriteWord(24, unchecked((ushort)(S(task, 24) - 1)));
                }
                return 0;
            }
            NativeBattleVector point = Vector(task, 16);
            ushort height = task.ReadWord(22);
            int angle = Roll() % 4096;
            int scale = (Roll() % 1536 + 1792) / 4;
            Append(2, 0x5BA8C0, child => WriteVector(child.WriteWord(12, 0).WriteWord(22, height).WriteWord(24, (ushort)angle).WriteWord(28, (ushort)scale).WriteWord(30, (ushort)scale), 16, point));
            ushort serial = task.ReadWord(14);
            Append(2, 0x5BAA00, child => WriteVector(child.WriteWord(12, 0).WriteWord(14, serial).WriteWord(22, height), 16, point));
            return 2;
        }

        int GroundMesh(ref NativeBattleModulePoolTask task)
        {
            int scale = S(task, 28);
            NativeBattleVector point = Vector(task, 16);
            NativeBattleActionMatrix world = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, S(task, 24), 0)), scale, scale, scale) with { X = point.X, Y = point.Y, Z = point.Z };
            int phase = S(task, 12);
            Execute(new NativeBattleShot192Mesh(0xD17B58, world, phase >= 4 ? 0xF3u : 0x33u, phase >= 4 ? (phase - 4) << 10 : null));
            if (paused)
            {
                return 0;
            }
            int velocity = S(task, 30);
            task = Increment(task).WriteWord(28, unchecked((ushort)(scale + velocity))).WriteWord(30, unchecked((ushort)(velocity - velocity / 5)));
            return S(task, 12) >= 8 ? 2 : 0;
        }

        int Dust(ref NativeBattleModulePoolTask task)
        {
            int serial = S(task, 14);
            int baseOffset = (serial & 1) != 0 ? 300 * 24 : 0;
            byte[] bytes = state.ImpactParticles.ToArray();
            int active = 0;
            for (int particle = 0; particle < 300; particle++)
            {
                int offset = baseOffset + particle * 24;
                if (I32(bytes, offset) != serial)
                {
                    continue;
                }
                ushort phase = U16(bytes, offset + 4);
                short scale = I16(bytes, offset + 6);
                Execute(new NativeBattleShot192Sprite(0xD16EC8, phase, V(bytes, offset + 8), scale, -(scale >> 4), 4, Color: 0x404040));
                if (paused)
                {
                    continue;
                }
                W16(bytes, offset + 4, phase + 1);
                if (SpriteEnded(0xD16EC8, phase))
                {
                    W32(bytes, offset, 0);
                    continue;
                }
                W16(bytes, offset + 6, scale + 32);
                foreach (int axis in new[] { 0, 2 })
                {
                    int velocity = I16(bytes, offset + 16 + axis * 2);
                    W16(bytes, offset + 8 + axis * 2, I16(bytes, offset + 8 + axis * 2) + velocity);
                    W16(bytes, offset + 16 + axis * 2, velocity - (velocity >> 3));
                }
                active++;
            }
            if (!paused && S(task, 12) == 0)
            {
                for (int spawn = 0; spawn < 10; spawn++)
                {
                    int free = Enumerable.Range(0, 300).FirstOrDefault(index => I32(bytes, baseOffset + index * 24) == 0, -1);
                    if (free < 0)
                    {
                        break;
                    }
                    int offset = baseOffset + free * 24;
                    W32(bytes, offset, serial);
                    W16(bytes, offset + 4, 0);
                    W16(bytes, offset + 6, Roll() % 1536 + 2048);
                    NativeBattleVector point = Vector(task, 16);
                    W16(bytes, offset + 8, point.X);
                    W16(bytes, offset + 10, point.Y);
                    W16(bytes, offset + 12, point.Z);
                    W16(bytes, offset + 14, S(task, 22));
                    int angle = Roll() % 4096;
                    int cosine = NativeBattleCameraMath.Cosine(angle);
                    int sine = NativeBattleCameraMath.Sine(angle);
                    int radius = Roll() % 50 + 50;
                    int upper = 300 * 24 + free * 24;
                    W16(bytes, upper + 8, I16(bytes, upper + 8) + (cosine * radius >> 12));
                    W16(bytes, upper + 12, I16(bytes, upper + 12) + (sine * radius >> 12));
                    int speed = Roll() % 30 + 20;
                    W16(bytes, upper + 16, cosine * speed >> 12);
                    W16(bytes, upper + 20, sine * speed >> 12);
                }
            }
            state = state with { ImpactParticles = bytes.ToImmutableArray() };
            if (paused)
            {
                return 0;
            }
            task = Increment(task);
            return S(task, 12) >= 4 && active == 0 ? 2 : 0;
        }

        bool SpriteEnded(uint address, ushort phase)
        {
            NativeBattleSearchSpriteResource resource = resources.Sprites.Single(sprite => sprite.Address == address);
            return new NativeBattleModuleResource(0, address, 0, 0, resource.Bytes).ProjectedFrame571C80(phase).NextOffset < 0;
        }
        void Ready() => frame = frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters! with { Parameter81 = 1 } } };
        void Execute(NativeBattleShot192Operation operation)
        {
            frame = frame with { Memory = frame.Memory with { Shot192 = state } };
            frame = inputs.Execute(frame, operation);
            state = frame.Memory.Shot192 ?? throw new InvalidDataException("Native Shot service lost its module state.");
        }
        int Roll()
        {
            NativeCrtRandomState random = frame.Random.Draw(out ushort result);
            frame = frame with { Random = random };
            return result;
        }
        void Append(int pool, uint callback, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> initialize)
        {
            (pools[pool], int slot) = pools[pool].Append(callback);
            Put(pool, slot, initialize(pools[pool].Slots[slot]));
        }
        void Put(int pool, int slot, NativeBattleModulePoolTask task) => pools[pool] = pools[pool] with { Slots = pools[pool].Slots.SetItem(slot, task) };
    }

    private static void RequirePools(NativeBattleEffectTaskPool[] pools)
    {
        if (pools.Length != 3 || pools[0].DescriptorAddress != Root || pools[0].Stride != 16 || pools[0].Slots.Length != 4 || pools[1].DescriptorAddress != 0x227C1A8 || pools[1].Stride != 36 || pools[1].Slots.Length != 100 || pools[2].DescriptorAddress != 0x227FAA0 || pools[2].Stride != 36 || pools[2].Slots.Length != 100)
        {
            throw new InvalidDataException("Native Scatter Shot lost its original three callback pools.");
        }
    }
    private static short S(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleModulePoolTask Increment(NativeBattleModulePoolTask task) => task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
    private static NativeBattleVector Vector(NativeBattleModulePoolTask task, int offset) => new(S(task, offset), S(task, offset + 2), S(task, offset + 4));
    private static NativeBattleModulePoolTask WriteVector(NativeBattleModulePoolTask task, int offset, NativeBattleVector point) => task.WriteWord(offset, unchecked((ushort)point.X)).WriteWord(offset + 2, unchecked((ushort)point.Y)).WriteWord(offset + 4, unchecked((ushort)point.Z));
    private static short I16(byte[] bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
    private static ushort U16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
    private static int I32(byte[] bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset));
    private static NativeBattleVector V(byte[] bytes, int offset) => new(I16(bytes, offset), I16(bytes, offset + 2), I16(bytes, offset + 4));
    private static void W16(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), unchecked((short)value));
    private static void W32(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), value);
}
