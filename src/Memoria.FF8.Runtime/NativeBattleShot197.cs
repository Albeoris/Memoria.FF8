using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShot197Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public abstract record NativeBattleShot197Operation;
public sealed record NativeBattleShot197Bind(byte Actor) : NativeBattleShot197Operation;
public sealed record NativeBattleShot197Sequence(byte Actor, ushort Sequence) : NativeBattleShot197Operation;
public sealed record NativeBattleShot197Stage(int Intensity) : NativeBattleShot197Operation;
public sealed record NativeBattleShot197Contact : NativeBattleShot197Operation;
public sealed record NativeBattleShot197Sound(uint Descriptor, int Channel, int Volume) : NativeBattleShot197Operation;
public sealed record NativeBattleShot197Echo(byte Actor, NativeBattleBodyBoneMatrices Bones, int Geometry) : NativeBattleShot197Operation;
public sealed record NativeBattleShot197Sprite(uint Address, ushort Phase, NativeBattleVector Position, short Scale, int Depth, ushort Flags, NativeBattleActionMatrix? World = null, NativeBattleVector? ParentPosition = null) : NativeBattleShot197Operation;
public sealed record NativeBattleShot197ReloadCommon(uint CompletionAddress) : NativeBattleShot197Operation;
public sealed record NativeBattleShot197Mesh(uint Address, NativeBattleActionMatrix World, uint Flags, int? Fade, NativeBattleVector? CameraAnchor = null, int Depth = 0, bool BillboardOnly = false) : NativeBattleShot197Operation;
public sealed record NativeBattleShot197Inputs(uint GraphicsFlags, NativeBattleActionPacketsState Packets, Func<int, NativeBattleShot197Anchor> ReadMuzzle, Func<int, int> BoneCount, Func<int, int, int, NativeBattleVector> ReadBone, Func<int, short> ModelScale, Func<int, NativeBattleVector> ReadCenter, Func<int, NativeBattleBodyBoneMatrices> ReadBones, Func<NativeBattleShot197Frame, NativeBattleShot197Operation, NativeBattleShot197Frame> Execute);

/// <summary>5AED30/5AED60/5B17F0 and Armor Shot's original three physical callback pools.</summary>
public static class NativeBattleShot197
{
    public const uint Root = 0x2261A10;

    public static NativeBattleOriginalEffectState WriteCompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        NativeBattleEffectTaskPool pool = effect.Pools.Single(pool => pool.DescriptorAddress == Root);
        uint delta = unchecked(address - pool.BankAddress);
        if (delta % pool.Stride != 14 || delta / pool.Stride >= pool.Slots.Length)
        {
            throw new NotSupportedException("Native Armor Shot completion requires its actual root task BYTE14.");
        }
        int slot = checked((int)(delta / pool.Stride));
        int index = effect.Pools.IndexOf(pool);
        return effect with { Pools = effect.Pools.SetItem(index, pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteByte(14, value)) }) };
    }

    public static NativeBattleShot197Frame Enter(NativeBattleShot197Frame frame, NativeBattleActionPacketsState packets)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Armor Shot requires its original action parameters.");
        byte mode = parameters.Parameter80 ?? throw new NotSupportedException("Native Armor Shot requires its actual module Parameter80.");
        NativeBattleEffectTaskPool[] pools;
        NativeBattleShot197State state;
        if (mode == 0)
        {
            pools = [NativeBattleEffectTaskPool.Initialize(Root, 0x2260BC0, 16, 4), NativeBattleEffectTaskPool.Initialize(0x2261A20, 0x2260C00, 36, 100), NativeBattleEffectTaskPool.Initialize(0x22636F8, 0x2263708, 36, 100)];
            (pools[0], int root) = pools[0].Append(0x5B17F0);
            Put(0, root, pools[0].Slots[root].WriteWord(12, 0));
            (pools[1], int opening) = pools[1].Append(0x5B13C0);
            Put(1, opening, pools[1].Slots[opening].WriteWord(12, 0));
            state = (frame.Memory.Shot197 ?? throw new NotSupportedException("Native Armor Shot requires its retained original module globals; explicitly migrate the standalone snapshot.")).Enter(parameters.Actor78);
        }
        else
        {
            state = frame.Memory.Shot197 ?? throw new NotSupportedException("Native repeated Shot requires its actual retained module globals.");
            state.Validate();
            pools = frame.Effect.Pools.ToArray();
            RequirePools(pools);
            if (mode == 1)
            {
                (pools[0], int ending) = pools[0].Append(0x5B12A0);
                Put(0, ending, pools[0].Slots[ending].WriteWord(12, 0));
            }
            else if (mode == 2)
            {
                if (pools[1].Slots.Any(task => (task.Flags & 1) == 0))
                {
                    (pools[1], int shot) = pools[1].Append(0x5AEEC0);
                    NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
                    int first = header.FirstContactIndex ?? throw new InvalidDataException("Native Shot requires its physical header contact pointer.");
                    Put(1, shot, pools[1].Slots[shot].WriteWord(12, 0).WriteWord(14, unchecked((ushort)(state.ShotSerial % 100))).WriteWord(24, (ushort)(state.SequenceCursor + 3)).WriteWord(26, packets.ReadContact(first).Primary.ActorIndex));
                    state = state with { ShotSerial = unchecked(state.ShotSerial + 1), SequenceCursor = state.SequenceCursor < 2 ? state.SequenceCursor + 1 : 0 };
                }
            }
        }
        return frame with { Memory = frame.Memory with { Shot197 = state }, Effect = frame.Effect with { Pools = pools.ToImmutableArray(), ReturnedDescriptor = Root } };

        void Put(int pool, int slot, NativeBattleModulePoolTask task) => pools[pool] = pools[pool] with { Slots = pools[pool].Slots.SetItem(slot, task) };
    }

    public static NativeBattleShot197Frame Advance(NativeBattleShot197Frame frame, NativeBattleShot197Inputs inputs, NativeBattleShot197Resources resources)
    {
        NativeBattleEffectTaskPool[] pools = frame.Effect.Pools.ToArray();
        RequirePools(pools);
        NativeBattleShot197State state = frame.Memory.Shot197 ?? throw new NotSupportedException("Native Armor Shot lost its retained module globals.");
        state.Validate();
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        NativeBattleEffectTaskStep rootStep = pools[0].Advance((pool, index) =>
        {
            pools[0] = pool;
            NativeBattleModulePoolTask task = pool.Slots[index];
            int result;
            if (task.Callback == 0x5B17F0)
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
                    (pools[0], int cleanup) = pools[0].Append(0x5B18A0);
                    Put(0, cleanup, pools[0].Slots[cleanup].WriteWord(12, 0).WriteWord(14, 0));
                    task = task with { Next = pools[0].Slots[index].Next };
                }
                task = Increment(task);
                result = actions == 0 && particles == 0 && (state.Control & 2) != 0 ? 2 : 0;
            }
            else if (task.Callback == 0x5B12A0)
            {
                result = Ending(ref task);
            }
            else if (task.Callback == 0x5B18A0)
            {
                if (S(task, 12) == 1)
                {
                    Execute(new NativeBattleShot197ReloadCommon(checked(pools[0].BankAddress + (uint)(index * pools[0].Stride + 14))));
                }
                task = Increment(task);
                result = task.ReadWord(14) != 0 ? 2 : 0;
            }
            else
            {
                throw new NotSupportedException($"Native Armor Shot root callback{task.Callback:X} is not owned.");
            }
            Put(0, index, task);
            return new(pools[0], result);
        });
        pools[0] = rootStep.Pool;
        return frame with { Memory = frame.Memory with { Shot197 = state }, Effect = frame.Effect with { Pools = pools.ToImmutableArray(), ReturnedDescriptor = rootStep.Survivors == 0 ? 0u : Root } };

        int AdvancePool(int index)
        {
            NativeBattleEffectTaskStep step = pools[index].Advance((pool, slot) =>
            {
                pools[index] = pool;
                NativeBattleModulePoolTask task = pool.Slots[slot];
                int result = task.Callback switch
                {
                    0x5B13C0 => Opening(ref task),
                    0x5B14B0 => Fade(ref task, false),
                    0x5B1360 => Fade(ref task, true),
                    0x5B1500 => Echoes(ref task),
                    0x5AEEC0 => Shot(ref task),
                    0x5AF7E0 or 0x5AF950 or 0x5AFAC0 or 0x5B0160 or 0x5B0340 or 0x5B04C0 or 0x5B0660 => Mesh(ref task),
                    0x5AFF30 => Beam(ref task),
                    0x5AFBF0 => BodyPairs(ref task),
                    0x5B0780 => DirectionalBurst(ref task),
                    0x5B0B90 => TargetSmoke(ref task),
                    0x5B0E80 => Trail(ref task),
                    0x5B10C0 => TrailSmoke(ref task),
                    _ => throw new NotSupportedException($"Native Armor Shot child callback{task.Callback:X} is not owned.")
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
                Execute(new NativeBattleShot197Bind(state.Actor));
                Execute(new NativeBattleShot197Sequence(state.Actor, 1));
                Append(2, 0x5B14B0, child => child.WriteWord(12, 0).WriteWord(14, 8).WriteWord(24, 2048));
                Append(2, 0x5B1500, child => child.WriteWord(12, 0));
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
                Execute(new NativeBattleShot197Sequence(state.Actor, 2));
                Append(2, 0x5B1360, child => child.WriteWord(12, 0).WriteWord(14, 8).WriteWord(24, 2048));
                state = state with { StopClock = state.Clock };
            }
            if (S(task, 12) == 6)
            {
                Execute(new NativeBattleShot197Contact());
            }
            task = Increment(task);
            if (S(task, 12) < 14)
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
            Execute(new NativeBattleShot197Stage(descending ? amplitude - level : level));
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
                Execute(new NativeBattleShot197Stage(0));
            }
            return 2;
        }

        int Echoes(ref NativeBattleModulePoolTask task)
        {
            int counter = S(task, 12);
            NativeBattleBodyBoneMatrices current = inputs.ReadBones(state.Actor);
            if (current.Matrices.Length > 4)
            {
                throw new NotSupportedException("Native Armor Shot echo copy exceeds its actual128-byte restore scratch.");
            }
            byte[] particles = state.ParticleStorage.ToArray();
            for (int bone = 0; bone < current.Matrices.Length; bone++)
            {
                int offset = 7232 + bone * 32;
                NativeBattleActionMatrix matrix = current.Matrices[bone];
                for (int word = 0; word < 9; word++)
                {
                    W16(particles, offset + word * 2, matrix.Rotation[word]);
                }
                W32(particles, offset + 20, matrix.X);
                W32(particles, offset + 24, matrix.Y);
                W32(particles, offset + 28, matrix.Z);
            }
            state = state with { ParticleStorage = particles.ToImmutableArray() };
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
                Execute(new NativeBattleShot197Echo(state.Actor, snapshot ?? throw new InvalidDataException("Native active Shot echo lost its retained bone bytes."), geometry));
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
            int serial = S(task, 14);
            if (phase == 0)
            {
                if ((state.Control & 4) != 0)
                {
                    return 0;
                }
                Execute(new NativeBattleShot197Sequence(state.Actor, task.ReadWord(24)));
                state = state with { Control = state.Control | 4 };
                NativeBattleShot197Anchor anchor = inputs.ReadMuzzle(state.Actor);
                NativeBattleVector point = inputs.ReadCenter(S(task, 26));
                point = point with { Y = Math.Min(point.Y, anchor.Position.Y) };
                int dx = point.X - anchor.Position.X;
                int dy = point.Y - anchor.Position.Y;
                int dz = point.Z - anchor.Position.Z;
                int squared = unchecked(dx * dx + dy * dy + dz * dz);
                if (squared < 0)
                {
                    throw new NotSupportedException("Native Armor Shot reaches an unproved negative56BEC0 conversion.");
                }
                int distance = (int)Math.Sqrt(squared);
                var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(dx, dy, dz);
                anchor = anchor with { Direction = new(unchecked((short)direction.X), unchecked((short)direction.Y), unchecked((short)direction.Z)) };
                state = state with { Anchors = state.Anchors.SetItem(serial, anchor) };
                int scale = inputs.ModelScale(S(task, 26));
                task = WriteVector(task, 16, point).WriteWord(22, unchecked((ushort)(2000 * scale >> 12))).WriteWord(28, unchecked((ushort)(2000 * Math.Clamp(scale, 1536, 4352) >> 12)));
                int angle = Roll() % 4096;
                scale = (Roll() % 896 + 2688) / 3;
                AppendMesh(0x5AF7E0, serial, default, 0, angle, 0, scale, scale, 0);
                angle = Roll() % 4096;
                scale = (Roll() % 512 + 2560) / 3;
                AppendMesh(0x5AF950, serial, default, 0, angle, 0, scale, scale, 0);
                angle = Roll() % 4096;
                scale = Roll() % 1152 + 2432;
                AppendMesh(0x5AFAC0, serial, default, 0, angle, 0, scale, 0, 0);
                int impact = state.ImpactSerial;
                state = state with { ImpactSerial = unchecked(impact + 1) };
                Append(2, 0x5AFBF0, child => child.WriteWord(12, 0).WriteWord(14, unchecked((ushort)impact)));
                for (int ordinal = 1; ordinal <= 5; ordinal++)
                {
                    int step = unchecked(distance / 6 * ordinal);
                    NativeBattleVector location = new(unchecked((short)(anchor.Position.X + (unchecked(direction.X * step) >> 12))), unchecked((short)(anchor.Position.Y + (unchecked(direction.Y * step) >> 12))), unchecked((short)(anchor.Position.Z + (unchecked(direction.Z * step) >> 12))));
                    angle = Roll() % 4096;
                    int angularVelocity = Roll() % 220 + 120;
                    int envelope = 4096 - NativeBattleCameraMath.Sine(ordinal * 512 / 5);
                    scale = (unchecked(envelope * (Roll() % 896 + 3584)) >> 12) / 3;
                    int length = Roll() % 1536 + 4352;
                    AppendMesh(0x5B0160, serial, location, ordinal - 1, angle, angularVelocity, scale, scale, length);
                }
                Append(2, 0x5AFF30, child => child.WriteWord(12, 0).WriteWord(14, (ushort)serial).WriteWord(28, 1152).WriteWord(32, unchecked((ushort)(distance / 2))).WriteWord(34, unchecked((ushort)(distance / 2))));
            }
            if (phase == 1)
            {
                NativeBattleVector point = Vector(task, 16);
                int height = S(task, 22);
                int angle = Roll() % 4096;
                int scale = Roll() % 640 + 1280;
                AppendMesh(0x5B0340, serial, point, height, angle, 0, scale / 3, scale / 3, 0);
                angle = Roll() % 4096;
                int angularVelocity = Roll() % 260 + 160;
                scale = (scale + 768) / 4;
                AppendMesh(0x5B04C0, serial, point, height, angle, angularVelocity, scale, scale, 0);
            }
            if (phase == 6)
            {
                NativeBattleVector point = Vector(task, 16);
                int angle = Roll() % 4096;
                short scale = unchecked((short)(unchecked(((S(task, 28) << 12) / 2400) * (Roll() % 1152 + 2560)) >> 12));
                scale = unchecked((short)(scale / 3));
                AppendMesh(0x5B0660, serial, point, S(task, 22), angle, 0, scale, scale, 0);
                int impact = state.ImpactSerial;
                state = state with { ImpactSerial = unchecked(impact + 2) };
                Append(2, 0x5B0780, child => WriteVector(child.WriteWord(12, 0).WriteWord(14, unchecked((ushort)impact)), 16, point));
                ushort height = task.ReadWord(22);
                ushort target = task.ReadWord(26);
                Append(2, 0x5B0B90, child => child.WriteWord(12, 0).WriteWord(14, unchecked((ushort)(impact + 1))).WriteWord(22, height).WriteWord(26, target));
                for (int trail = 0; trail < 3; trail++)
                {
                    int lane = state.LaneSerial % 8;
                    state = state with { LaneSerial = unchecked(state.LaneSerial + 1) };
                    Append(2, 0x5B0E80, child => WriteVector(child.WriteWord(12, 0).WriteWord(22, height).WriteWord(24, (ushort)lane).WriteWord(26, target), 16, point));
                }
                Execute(new NativeBattleShot197Contact());
            }
            if (phase == 22)
            {
                Ready();
                state = state with { Control = state.Control & ~4 };
            }
            if (phase == 1)
            {
                Execute(new NativeBattleShot197Sound(resources.ShotSound, 2 - (serial & 1), 128));
            }
            task = Increment(task);
            return S(task, 12) >= 25 ? 2 : 0;
        }

        void AppendMesh(uint callback, int serial, NativeBattleVector point, int height, int angle, int angularVelocity, int scale, int velocity, int length)
        {
            Append(2, callback, child => WriteVector(child.WriteWord(12, 0).WriteWord(14, unchecked((ushort)serial)).WriteWord(22, unchecked((ushort)height)).WriteWord(24, unchecked((ushort)angle)).WriteWord(26, unchecked((ushort)angularVelocity)).WriteWord(28, unchecked((ushort)scale)).WriteWord(30, unchecked((ushort)velocity)).WriteWord(32, unchecked((ushort)length)), 16, point));
        }

        NativeBattleShot197Anchor Anchor(NativeBattleModulePoolTask task)
        {
            return state.Anchors[S(task, 14)] ?? throw new InvalidDataException("Native Armor Shot lost its original56-byte muzzle anchor.");
        }

        NativeBattleActionMatrix Align(NativeBattleVector axis, NativeBattleVector direction)
        {
            NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(axis.X, axis.Y, axis.Z, direction.X, direction.Y, direction.Z);
            return new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), 0, 0, 0, 4096);
        }

        int Mesh(ref NativeBattleModulePoolTask task)
        {
            uint callback = task.Callback;
            if (callback == 0x5B0160 && S(task, 22) > 0)
            {
                if (!paused)
                {
                    task = task.WriteWord(22, unchecked((ushort)(S(task, 22) - 1)));
                }
                return 0;
            }
            int phase = S(task, 12);
            short angle = S(task, 24);
            short scale = S(task, 28);
            NativeBattleShot197Anchor anchor = Anchor(task);
            bool initial = callback is 0x5AF7E0 or 0x5AF950 or 0x5AFAC0;
            NativeBattleVector point = initial ? anchor.Position : Vector(task, 16);
            NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CE30(callback is 0x5AF7E0 or 0x5AFAC0 ? new(0, angle, 0) : new(0, 0, angle));
            if (callback is 0x5AF7E0 or 0x5AF950)
            {
                rotation = NativeBattleGfMatrices.Multiply56C090(anchor.Rotation, rotation);
            }
            else if (!initial && callback != 0x5B0660)
            {
                rotation = NativeBattleGfMatrices.Multiply56C090(Align(resources.ReferenceAxes[callback == 0x5B0340 ? 1 : 0], anchor.Direction), rotation);
            }
            NativeBattleActionMatrix world = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale, scale, callback == 0x5B0160 ? S(task, 32) : scale) with { X = point.X, Y = callback == 0x5AFAC0 ? 0 : point.Y, Z = point.Z };
            int fadeStart = callback switch { 0x5AF7E0 or 0x5AFAC0 or 0x5B0340 => 2, 0x5B0160 => 4, _ => 6 };
            int fadeStep = callback switch { 0x5B0340 => 1024, 0x5B04C0 or 0x5B0660 => 682, _ => 512 };
            uint address = callback switch { 0x5AF7E0 => 0xD012B0, 0x5AF950 => 0xD03940, 0x5AFAC0 => 0xD02180, 0x5B0160 => 0xD04B88, 0x5B0340 => 0xD02570, 0x5B04C0 => 0xD03298, _ => 0xCFF2F8 };
            bool hybrid = callback is 0x5B0340 or 0x5B04C0 or 0x5B0660;
            if (callback == 0x5B0660)
            {
                world = world with { Rotation = [scale, 0, 0, 0, scale, 0, 0, 0, scale] };
            }
            Execute(new NativeBattleShot197Mesh(address, world, phase >= fadeStart ? 0xF3u : 0x33u, phase >= fadeStart ? (phase - fadeStart) * fadeStep : null, hybrid ? point : null, -S(task, 22), callback == 0x5B0660));
            if (paused)
            {
                return 0;
            }
            if (callback != 0x5AFAC0)
            {
                int velocity = S(task, 30);
                int divisor = callback switch { 0x5AF7E0 or 0x5B0660 => 3, 0x5AF950 => 5, _ => 4 };
                task = task.WriteWord(28, unchecked((ushort)(scale + velocity))).WriteWord(30, unchecked((ushort)(velocity - velocity / divisor)));
            }
            if (callback is 0x5B0160 or 0x5B04C0)
            {
                int angularVelocity = S(task, 26);
                task = task.WriteWord(24, unchecked((ushort)(angle + angularVelocity))).WriteWord(26, unchecked((ushort)(angularVelocity - angularVelocity / (callback == 0x5B04C0 ? 16 : 10))));
            }
            task = Increment(task);
            int duration = callback switch { 0x5AF7E0 or 0x5AFAC0 => 10, 0x5AF950 => 14, 0x5B0340 => 6, _ => 12 };
            return S(task, 12) >= duration ? 2 : 0;
        }

        int Beam(ref NativeBattleModulePoolTask task)
        {
            NativeBattleShot197Anchor anchor = Anchor(task);
            int phase = S(task, 12);
            short scale = S(task, 28);
            int length = S(task, 32);
            NativeBattleActionMatrix world = NativeBattlePoseMatrices.Scale56BEF0(Align(resources.ReferenceAxes[0], anchor.Direction), scale, scale, unchecked(length << 12) / 6000) with { X = anchor.Position.X, Y = anchor.Position.Y, Z = anchor.Position.Z };
            uint flags = phase >= 4 ? 0xF3u : 0x33u;
            int? fade = phase >= 4 ? (phase - 4) << 9 : null;
            Execute(new NativeBattleShot197Mesh(0xCFDD18, world, flags, fade));
            NativeBattleVector point = new(unchecked((short)(anchor.Position.X + (unchecked(length * anchor.Direction.X) >> 12))), unchecked((short)(anchor.Position.Y + (unchecked(length * anchor.Direction.Y) >> 12))), unchecked((short)(anchor.Position.Z + (unchecked(length * anchor.Direction.Z) >> 12))));
            scale = unchecked((short)(Roll() % 768 + 2048));
            world = new([scale, 0, 0, 0, scale, 0, 0, 0, scale], point.X, point.Y, point.Z, 4096);
            Execute(new NativeBattleShot197Mesh(0xCFEDE8, world, flags, fade, point, -(scale >> 3), true));
            if (paused)
            {
                return 0;
            }
            if (phase < 4)
            {
                task = task.WriteWord(32, unchecked((ushort)(length + S(task, 34))));
            }
            task = Increment(task);
            return S(task, 12) >= 12 ? 2 : 0;
        }

        int BodyPairs(ref NativeBattleModulePoolTask task)
        {
            int serial = S(task, 14) + 1;
            int bank = (S(task, 14) & 1) == 0 ? 0 : 3632;
            byte[] particles = state.ParticleStorage.ToArray();
            if (!paused && S(task, 12) <= 3)
            {
                for (int pair = 0; pair < 2; pair++)
                {
                    int offset = FreeParticle(particles, bank);
                    if (offset < 0)
                    {
                        break;
                    }
                    for (int bone = 29; bone <= 30; bone++)
                    {
                        int destination = offset + (bone - 29) * 24;
                        W32(particles, destination, serial);
                        W16(particles, destination + 4, 0);
                        W16(particles, destination + 6, Roll() % 640 + 800);
                        NativeBattleVector point = inputs.ReadBone(state.Actor, bone, 3072);
                        WriteParticlePoint(particles, destination + 8, new(unchecked((short)(point.X + Roll() % 50 - 25)), unchecked((short)(point.Y + Roll() % 50 - 25)), unchecked((short)(point.Z + Roll() % 50 - 25))));
                    }
                }
            }
            int active = 0;
            for (int slot = 0; slot < 150; slot++)
            {
                int offset = bank + slot * 24;
                if (I32(particles, offset) != serial)
                {
                    continue;
                }
                ushort phase = U16(particles, offset + 4);
                short scale = I16(particles, offset + 6);
                Execute(new NativeBattleShot197Sprite(0xCFDA74, phase, V(particles, offset + 8), scale, -(scale >> 4), 0));
                if (!paused)
                {
                    W16(particles, offset + 4, phase + 1);
                    if (SpriteEnded(0xCFDA74, phase))
                    {
                        W32(particles, offset, 0);
                    }
                    else
                    {
                        W16(particles, offset + 6, scale + (scale >> 4));
                        active++;
                    }
                }
            }
            state = state with { ParticleStorage = particles.ToImmutableArray() };
            if (paused)
            {
                return 0;
            }
            task = Increment(task);
            return active == 0 ? 2 : 0;
        }

        int DirectionalBurst(ref NativeBattleModulePoolTask task)
        {
            int serial = S(task, 14) + 1;
            int bank = (S(task, 14) & 1) == 0 ? 0 : 3632;
            byte[] particles = state.ParticleStorage.ToArray();
            NativeBattleVector origin = Vector(task, 16);
            int active = 0;
            for (int slot = 0; slot < 150; slot++)
            {
                int offset = bank + slot * 24;
                if (I32(particles, offset) != serial)
                {
                    continue;
                }
                int x = I16(particles, offset + 16);
                int y = I16(particles, offset + 18);
                int z = I16(particles, offset + 20);
                var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
                NativeBattleActionMatrix world = NativeBattlePoseMatrices.Scale56BEF0(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096), 4096, I16(particles, offset + 6), 4096);
                world = NativeBattleGfMatrices.Multiply56C090(Align(resources.ReferenceAxes[2], new(unchecked((short)direction.X), unchecked((short)direction.Y), unchecked((short)direction.Z))), world);
                NativeBattleVector position = V(particles, offset + 8);
                world = world with { X = position.X, Y = position.Y, Z = position.Z };
                ushort phase = U16(particles, offset + 4);
                Execute(new NativeBattleShot197Sprite(0xCFDC20, phase, default, 4096, 0, 0x208, world, origin));
                if (!paused)
                {
                    W16(particles, offset + 4, phase + 1);
                    if (SpriteEnded(0xCFDC20, phase))
                    {
                        W32(particles, offset, 0);
                    }
                    else
                    {
                        for (int axis = 0; axis < 3; axis++)
                        {
                            int velocity = I16(particles, offset + 16 + axis * 2);
                            W16(particles, offset + 8 + axis * 2, I16(particles, offset + 8 + axis * 2) + velocity);
                            W16(particles, offset + 16 + axis * 2, velocity - (velocity >> 2));
                        }
                        active++;
                    }
                }
            }
            if (paused)
            {
                return 0;
            }
            if (task.ReadWord(12) <= 2)
            {
                for (int spawn = 0; spawn < 8; spawn++)
                {
                    int offset = FreeParticle(particles, bank);
                    if (offset < 0)
                    {
                        break;
                    }
                    W32(particles, offset, serial);
                    W16(particles, offset + 4, 0);
                    W16(particles, offset + 6, Roll() % 4096 + 5120);
                    int x = Roll() % 4096 - 2048;
                    int y = Roll() % 4096 - 2048;
                    int z = Roll() % 4096 - 2048;
                    var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
                    int radius = Roll() % 400 + 300;
                    W16(particles, offset + 8, unchecked(direction.X * radius) >> 12);
                    W16(particles, offset + 10, unchecked(direction.Y * radius) >> 12);
                    W16(particles, offset + 12, unchecked(direction.Z * radius) >> 12);
                    int speed = Roll() % 260 + 220;
                    W16(particles, offset + 16, unchecked(direction.X * speed) >> 12);
                    W16(particles, offset + 18, unchecked(direction.Y * speed) >> 12);
                    W16(particles, offset + 20, unchecked(direction.Z * speed) >> 12);
                }
            }
            state = state with { ParticleStorage = particles.ToImmutableArray() };
            task = Increment(task);
            return S(task, 12) >= 4 && active == 0 ? 2 : 0;
        }

        int FreeParticle(byte[] bytes, int bank)
        {
            int slot = Enumerable.Range(0, 150).FirstOrDefault(index => I32(bytes, bank + index * 24) == 0, -1);
            return slot < 0 ? -1 : bank + slot * 24;
        }

        int TargetSmoke(ref NativeBattleModulePoolTask task)
        {
            int serial = S(task, 14) + 1;
            int bank = (S(task, 14) & 1) == 0 ? 0 : 3632;
            byte[] particles = state.ParticleStorage.ToArray();
            if (!paused && S(task, 12) <= 2)
            {
                int target = S(task, 26);
                int bones = inputs.BoneCount(target);
                for (int spawn = 0; spawn < 3; spawn++)
                {
                    int offset = FreeParticle(particles, bank);
                    if (offset < 0)
                    {
                        break;
                    }
                    W32(particles, offset, serial);
                    W16(particles, offset + 4, 0);
                    W16(particles, offset + 6, Roll() % 2560 + 1408);
                    int bone = bones == 0 ? 0 : Roll() % bones;
                    NativeBattleVector point = inputs.ReadBone(target, bone, Roll() % 4096);
                    WriteParticlePoint(particles, offset + 8, new(unchecked((short)(point.X + Roll() % 1000 - 500)), unchecked((short)(point.Y + Roll() % 1000 - 500)), unchecked((short)(point.Z + Roll() % 1000 - 500))));
                }
            }
            int active = 0;
            for (int slot = 0; slot < 150; slot++)
            {
                int offset = bank + slot * 24;
                if (I32(particles, offset) != serial)
                {
                    continue;
                }
                ushort phase = U16(particles, offset + 4);
                Execute(new NativeBattleShot197Sprite(0xCFD714, phase, V(particles, offset + 8), I16(particles, offset + 6), -S(task, 22), 0));
                if (!paused)
                {
                    W16(particles, offset + 4, phase + 1);
                    if (SpriteEnded(0xCFD714, phase))
                    {
                        W32(particles, offset, 0);
                    }
                    else
                    {
                        active++;
                    }
                }
            }
            state = state with { ParticleStorage = particles.ToImmutableArray() };
            if (paused)
            {
                return 0;
            }
            task = Increment(task);
            return active == 0 ? 2 : 0;
        }

        int Trail(ref NativeBattleModulePoolTask task)
        {
            if (paused)
            {
                return 0;
            }
            if (S(task, 12) == 0)
            {
                int target = S(task, 26);
                int bones = inputs.BoneCount(target);
                int bone = bones == 0 ? 0 : Roll() % bones;
                NativeBattleVector start = inputs.ReadBone(target, bone, Roll() % 4096);
                NativeBattleVector center = Vector(task, 16);
                var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(start.X - center.X, start.Y - center.Y, start.Z - center.Z);
                int radius = Roll() % 2500 + 1500;
                short x = unchecked((short)(start.X + (unchecked(radius * direction.X) >> 12)));
                short y = unchecked((short)(start.Y - ((Roll() % 1200 + 500) >> 1)));
                short z = unchecked((short)(start.Z + (unchecked(radius * direction.Z) >> 12)));
                NativeBattleVector end = new(x, y, z);
                NativeBattleVector middle = new(unchecked((short)((end.X + start.X) >> 1)), unchecked((short)(((start.Y + end.Y) >> 1) - Roll() % 1200 - 500)), unchecked((short)((start.Z + end.Z) >> 1)));
                int lane = S(task, 24);
                state = NativeBattleShot197Trail.Initialize(state, lane, start, middle, end);
                int serial = state.ImpactSerial;
                state = state with { ImpactSerial = unchecked(serial + 1) };
                Append(2, 0x5B10C0, child => child.WriteWord(12, 0).WriteWord(14, (ushort)lane).WriteWord(22, unchecked((ushort)serial)).WriteWord(24, 0).WriteWord(26, 5));
            }
            task = Increment(task);
            return S(task, 12) >= 10 ? 2 : 0;
        }

        int TrailSmoke(ref NativeBattleModulePoolTask task)
        {
            int serial = S(task, 22) + 1;
            int bank = (S(task, 22) & 1) == 0 ? 0 : 3632;
            byte[] particles = state.ParticleStorage.ToArray();
            int active = 0;
            for (int slot = 0; slot < 150; slot++)
            {
                int offset = bank + slot * 24;
                if (I32(particles, offset) != serial)
                {
                    continue;
                }
                ushort phase = unchecked((ushort)(2 * I16(particles, offset + 4) + 1));
                short scale = I16(particles, offset + 6);
                Execute(new NativeBattleShot197Sprite(0xCFD714, phase, V(particles, offset + 8), scale, -(scale >> 4), 0));
                if (!paused)
                {
                    W16(particles, offset + 4, I16(particles, offset + 4) + 1);
                    if (SpriteEnded(0xCFD714, phase))
                    {
                        W32(particles, offset, 0);
                    }
                    else
                    {
                        active++;
                    }
                }
            }
            if (paused)
            {
                return 0;
            }
            int velocity = S(task, 26);
            task = Increment(task).WriteWord(24, unchecked((ushort)(S(task, 24) + velocity))).WriteWord(26, unchecked((ushort)(velocity > 2 ? velocity - 1 : velocity)));
            int index = S(task, 24);
            if (index < 24)
            {
                for (int sample = index; sample >= index - S(task, 26); sample--)
                {
                    int offset = FreeParticle(particles, bank);
                    if (offset < 0)
                    {
                        break;
                    }
                    W32(particles, offset, serial);
                    W16(particles, offset + 4, 0);
                    W16(particles, offset + 6, Roll() % 512 + 1152);
                    WriteParticlePoint(particles, offset + 8, NativeBattleShot197Trail.ReadSample(state, S(task, 14), sample));
                }
            }
            state = state with { ParticleStorage = particles.ToImmutableArray() };
            return S(task, 12) >= 4 && active == 0 ? 2 : 0;
        }

        void WriteParticlePoint(byte[] bytes, int offset, NativeBattleVector point)
        {
            W16(bytes, offset, point.X);
            W16(bytes, offset + 2, point.Y);
            W16(bytes, offset + 4, point.Z);
        }


        bool SpriteEnded(uint address, ushort phase)
        {
            NativeBattleSearchSpriteResource resource = resources.Sprites.Single(sprite => sprite.Address == address);
            return new NativeBattleModuleResource(0, address, 0, 0, resource.Bytes).ProjectedFrame571C80(phase).NextOffset < 0;
        }
        void Ready() => frame = frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters! with { Parameter81 = 1 } } };
        void Execute(NativeBattleShot197Operation operation)
        {
            frame = frame with { Memory = frame.Memory with { Shot197 = state } };
            frame = inputs.Execute(frame, operation);
            state = frame.Memory.Shot197 ?? throw new InvalidDataException("Native Shot service lost its module state.");
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
        if (pools.Length != 3 || pools[0].DescriptorAddress != Root || pools[0].Stride != 16 || pools[0].Slots.Length != 4 || pools[1].DescriptorAddress != 0x2261A20 || pools[1].Stride != 36 || pools[1].Slots.Length != 100 || pools[2].DescriptorAddress != 0x22636F8 || pools[2].Stride != 36 || pools[2].Slots.Length != 100)
        {
            throw new InvalidDataException("Native Armor Shot lost its original three callback pools.");
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
