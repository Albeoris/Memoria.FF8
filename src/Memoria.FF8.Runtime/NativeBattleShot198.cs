using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShot198Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public abstract record NativeBattleShot198Operation;
public sealed record NativeBattleShot198Bind(byte Actor) : NativeBattleShot198Operation;
public sealed record NativeBattleShot198Sequence(byte Actor, ushort Sequence) : NativeBattleShot198Operation;
public sealed record NativeBattleShot198Stage(int Intensity) : NativeBattleShot198Operation;
public sealed record NativeBattleShot198Contact : NativeBattleShot198Operation;
public sealed record NativeBattleShot198Sound(uint Descriptor, int Channel, int Volume) : NativeBattleShot198Operation;
public sealed record NativeBattleShot198Echo(byte Actor, NativeBattleBodyBoneMatrices Bones, int Geometry) : NativeBattleShot198Operation;
public sealed record NativeBattleShot198Sprite(uint Address, ushort Phase, NativeBattleVector Position, short Scale, int Depth, ushort Flags, NativeBattleActionMatrix? World = null, NativeBattleVector? ParentPosition = null) : NativeBattleShot198Operation;
public sealed record NativeBattleShot198Mesh(uint Address, NativeBattleActionMatrix World, uint Flags, int? Fade, NativeBattleVector? CameraAnchor = null, int Depth = 0, bool BillboardOnly = false, int Phase = 0) : NativeBattleShot198Operation;
public sealed record NativeBattleShot198Inputs(uint GraphicsFlags, NativeBattleActionPacketsState Packets, Func<int, NativeBattleShot198Anchor> ReadMuzzle, Func<int, int> BoneCount, Func<int, int, int, NativeBattleVector> ReadBone, Func<int, short> ModelScale, Func<int, short> ReadMaximumY, Func<int, NativeBattleVector> ReadCenter, Func<int, NativeBattleBodyBoneMatrices> ReadBones, Func<NativeBattleShot198Frame, NativeBattleShot198Operation, NativeBattleShot198Frame> Execute);

/// <summary>5AA3E0/5AA410/5AEC90 and Hyper Shot's original three physical callback pools.</summary>
public static class NativeBattleShot198
{
    public const uint Root = 0x225B768;

    public static NativeBattleShot198Frame Enter(NativeBattleShot198Frame frame, NativeBattleActionPacketsState packets)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Hyper Shot requires its original action parameters.");
        byte mode = parameters.Parameter80 ?? throw new NotSupportedException("Native Hyper Shot requires its actual module Parameter80.");
        NativeBattleEffectTaskPool[] pools;
        NativeBattleShot198State state;
        if (mode == 0)
        {
            pools = [NativeBattleEffectTaskPool.Initialize(Root, 0x225A918, 16, 4), NativeBattleEffectTaskPool.Initialize(0x225B778, 0x225A958, 36, 100), NativeBattleEffectTaskPool.Initialize(0x225DDB0, 0x225DDC0, 36, 100)];
            (pools[0], int root) = pools[0].Append(0x5AEC90);
            Put(0, root, pools[0].Slots[root].WriteWord(12, 0));
            (pools[1], int opening) = pools[1].Append(0x5AE860);
            Put(1, opening, pools[1].Slots[opening].WriteWord(12, 0));
            state = (frame.Memory.Shot198 ?? throw new NotSupportedException("Native Hyper Shot requires its retained original module globals; explicitly migrate the standalone snapshot.")).Enter(parameters.Actor78);
        }
        else
        {
            state = frame.Memory.Shot198 ?? throw new NotSupportedException("Native repeated Shot requires its actual retained module globals.");
            state.Validate();
            pools = frame.Effect.Pools.ToArray();
            RequirePools(pools);
            if (mode == 1)
            {
                (pools[0], int ending) = pools[0].Append(0x5AE740);
                Put(0, ending, pools[0].Slots[ending].WriteWord(12, 0));
            }
            else if (mode == 2)
            {
                if (pools[1].Slots.Any(task => (task.Flags & 1) == 0))
                {
                    (pools[1], int shot) = pools[1].Append(0x5AA570);
                    NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
                    int first = header.FirstContactIndex ?? throw new InvalidDataException("Native Shot requires its physical header contact pointer.");
                    Put(1, shot, pools[1].Slots[shot].WriteWord(12, 0).WriteWord(14, unchecked((ushort)(state.ShotSerial % 100))).WriteWord(24, (ushort)(state.SequenceCursor + 3)).WriteWord(26, packets.ReadContact(first).Primary.ActorIndex));
                    state = state with { ShotSerial = unchecked(state.ShotSerial + 1), SequenceCursor = state.SequenceCursor < 2 ? state.SequenceCursor + 1 : 0 };
                }
            }
        }
        return frame with { Memory = frame.Memory with { Shot198 = state }, Effect = frame.Effect with { Pools = pools.ToImmutableArray(), ReturnedDescriptor = Root } };

        void Put(int pool, int slot, NativeBattleModulePoolTask task) => pools[pool] = pools[pool].WriteSlot(slot, task);
    }

    public static NativeBattleShot198Frame Advance(NativeBattleShot198Frame frame, NativeBattleShot198Inputs inputs, NativeBattleShot198Resources resources)
    {
        NativeBattleEffectTaskPool[] pools = frame.Effect.Pools.ToArray();
        RequirePools(pools);
        NativeBattleShot198State state = frame.Memory.Shot198 ?? throw new NotSupportedException("Native Hyper Shot lost its retained module globals.");
        state.Validate();
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        NativeBattleEffectTaskStep rootStep = pools[0].Advance((pool, index) =>
        {
            pools[0] = pool;
            NativeBattleModulePoolTask task = pool.Slots[index];
            int result;
            if (task.Callback == 0x5AEC90)
            {
                const int baseOffset = 0;
                frame = frame with { Effect = frame.Effect with { DrawArenaOffset = baseOffset + ((task.ReadWord(12) & 1) != 0 ? 0xC000 : 0) } };
                int actions = AdvancePool(1);
                int particles = AdvancePool(2);
                if (!paused)
                {
                    state = state with { Clock = unchecked(state.Clock + 1) };
                }
                task = Increment(task);
                result = actions == 0 && particles == 0 && (state.Control & 2) != 0 ? 2 : 0;
            }
            else if (task.Callback == 0x5AE740)
            {
                result = Ending(ref task);
            }
            else
            {
                throw new NotSupportedException($"Native Hyper Shot root callback{task.Callback:X} is not owned.");
            }
            Put(0, index, task);
            return new(pools[0], result);
        });
        pools[0] = rootStep.Pool;
        return frame with { Memory = frame.Memory with { Shot198 = state }, Effect = frame.Effect with { Pools = pools.ToImmutableArray(), ReturnedDescriptor = rootStep.Survivors == 0 ? 0u : Root } };

        int AdvancePool(int index)
        {
            NativeBattleEffectTaskStep step = pools[index].Advance((pool, slot) =>
            {
                pools[index] = pool;
                NativeBattleModulePoolTask task = pool.Slots[slot];
                int result = task.Callback switch
                {
                    0x5AE860 => Opening(ref task),
                    0x5AE950 => Fade(ref task, false),
                    0x5AE800 => Fade(ref task, true),
                    0x5AE9A0 => Echoes(ref task),
                    0x5AA570 => Shot(ref task),
                    0x5AABE0 or 0x5AC760 or 0x5AE590 => Mesh(ref task),
                    0x5AD7C0 => Beam(ref task),
                    0x5AD480 => BodyPairs(ref task),
                    0x5ADAD0 or 0x5ADE70 or 0x5AE240 => Particles(ref task),
                    _ => throw new NotSupportedException($"Native Hyper Shot child callback{task.Callback:X} is not owned.")
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
                Execute(new NativeBattleShot198Bind(state.Actor));
                Execute(new NativeBattleShot198Sequence(state.Actor, 1));
                Append(2, 0x5AE950, child => child.WriteWord(12, 0).WriteWord(14, 8).WriteWord(24, 2048));
                Append(2, 0x5AE9A0, child => child.WriteWord(12, 0));
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
                Execute(new NativeBattleShot198Sequence(state.Actor, 2));
                Append(2, 0x5AE800, child => child.WriteWord(12, 0).WriteWord(14, 8).WriteWord(24, 2048));
                state = state with { StopClock = state.Clock };
            }
            if (S(task, 12) == 10)
            {
                Execute(new NativeBattleShot198Contact());
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
            Execute(new NativeBattleShot198Stage(descending ? amplitude - level : level));
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
                Execute(new NativeBattleShot198Stage(0));
            }
            return 2;
        }

        int Echoes(ref NativeBattleModulePoolTask task)
        {
            int counter = S(task, 12);
            NativeBattleBodyBoneMatrices current = inputs.ReadBones(state.Actor);
            if (current.Matrices.Length > 4)
            {
                throw new NotSupportedException("Native Hyper Shot echo copy exceeds its actual128-byte restore scratch.");
            }
            byte[] particles = state.ParticleStorage.ToArray();
            for (int bone = 0; bone < current.Matrices.Length; bone++)
            {
                int offset = 9632 + bone * 32;
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
                Execute(new NativeBattleShot198Echo(state.Actor, snapshot ?? throw new InvalidDataException("Native active Shot echo lost its retained bone bytes."), geometry));
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
                Execute(new NativeBattleShot198Sequence(state.Actor, task.ReadWord(24)));
                state = state with { Control = state.Control | 4 };
                NativeBattleShot198Anchor anchor = inputs.ReadMuzzle(state.Actor);
                NativeBattleActionMatrix? retained = state.Anchors[serial]?.Rotation;
                anchor = anchor with { Rotation = anchor.Rotation with { X = retained?.X ?? 0, Y = retained?.Y ?? 0, Z = retained?.Z ?? 0 } };
                NativeBattleVector point = inputs.ReadCenter(S(task, 26));
                point = point with { Y = Math.Min(point.Y, anchor.Position.Y) };
                int dx = point.X - anchor.Position.X;
                int dy = point.Y - anchor.Position.Y;
                int dz = point.Z - anchor.Position.Z;
                int distance = Sqrt(unchecked(dx * dx + dy * dy + dz * dz));
                var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(dx, dy, dz);
                anchor = anchor with { Direction = new(unchecked((short)direction.X), unchecked((short)direction.Y), unchecked((short)direction.Z)) };
                state = state with { Anchors = state.Anchors.SetItem(serial, anchor) };
                int scale = inputs.ModelScale(S(task, 26));
                task = WriteVector(task, 16, point).WriteWord(22, unchecked((ushort)(2000 * scale >> 12))).WriteWord(28, unchecked((ushort)(2000 * Math.Clamp(scale, 1536, 4352) >> 12))).WriteWord(30, inputs.ReadMaximumY(S(task, 26)) > -500 ? (ushort)1 : (ushort)0);
                int angle = Roll() % 4096;
                scale = (Roll() % 896 + 3200) / 3;
                AppendMesh(0x5AABE0, serial, default, 0, angle, 0, scale, scale, 0);
                int impact = state.ImpactSerial;
                state = state with { ImpactSerial = unchecked(impact + 1) };
                Append(2, 0x5AD480, child => child.WriteWord(12, 0).WriteWord(14, unchecked((ushort)impact)));
                Append(2, 0x5AD7C0, child => child.WriteWord(12, 0).WriteWord(14, (ushort)serial).WriteWord(28, 13312).WriteWord(30, 332).WriteWord(32, unchecked((ushort)(distance / 2))).WriteWord(34, unchecked((ushort)(distance / 2))));
                impact = state.ImpactSerial;
                state = state with { ImpactSerial = unchecked(impact + 1) };
                Append(2, 0x5ADAD0, child => child.WriteWord(12, 0).WriteWord(14, (ushort)serial).WriteWord(22, unchecked((ushort)impact)));
            }
            if (phase <= 8 && phase % 3 == 0)
            {
                int angle = Roll() % 4096;
                int angularVelocity = Roll() % 150 + 110;
                int scale = (Roll() % 896 + 3200) / 3;
                AppendMesh(0x5AC760, serial, default, 0, angle, angularVelocity, scale, scale, 0);
            }
            if (phase == 2)
            {
                NativeBattleVector point = Vector(task, 16);
                ushort height = task.ReadWord(22);
                int impact = state.ImpactSerial;
                state = state with { ImpactSerial = unchecked(impact + 2) };
                Append(2, 0x5ADE70, child => WriteVector(child.WriteWord(12, 0).WriteWord(14, (ushort)serial).WriteWord(32, unchecked((ushort)impact)).WriteWord(22, height), 16, point));
                Append(2, 0x5AE240, child => WriteVector(child.WriteWord(12, 0).WriteWord(14, (ushort)serial).WriteWord(32, unchecked((ushort)(impact + 1))).WriteWord(22, height), 16, point));
            }
            if (phase >= 2 && phase <= 10 && phase % 3 == 0 && S(task, 30) != 0)
            {
                int scale = (unchecked(((S(task, 28) << 12) / 2200) * (Roll() % 896 + 2560)) >> 12) / 3;
                AppendMesh(0x5AE590, serial, Vector(task, 16), S(task, 22), 0, 0, scale, scale, 0);
            }
            if (phase == 10)
            {
                Execute(new NativeBattleShot198Contact());
            }
            if (phase == 22)
            {
                Ready();
                state = state with { Control = state.Control & ~4 };
            }
            if (phase == 1)
            {
                Execute(new NativeBattleShot198Sound(resources.ShotSound, 2 - (serial & 1), 128));
            }
            task = Increment(task);
            return S(task, 12) >= 25 ? 2 : 0;
        }

        void AppendMesh(uint callback, int serial, NativeBattleVector point, int height, int angle, int angularVelocity, int scale, int velocity, int length)
        {
            Append(2, callback, child => WriteVector(child.WriteWord(12, 0).WriteWord(14, unchecked((ushort)serial)).WriteWord(22, unchecked((ushort)height)).WriteWord(24, unchecked((ushort)angle)).WriteWord(26, unchecked((ushort)angularVelocity)).WriteWord(28, unchecked((ushort)scale)).WriteWord(30, unchecked((ushort)velocity)).WriteWord(32, unchecked((ushort)length)), 16, point));
        }

        NativeBattleShot198Anchor Anchor(NativeBattleModulePoolTask task)
        {
            return state.Anchors[S(task, 14)] ?? throw new InvalidDataException("Native Hyper Shot lost its original56-byte muzzle anchor.");
        }

        NativeBattleActionMatrix Align(NativeBattleVector axis, NativeBattleVector direction)
        {
            NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(axis.X, axis.Y, axis.Z, direction.X, direction.Y, direction.Z);
            return new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), 0, 0, 0, 4096);
        }

        int Mesh(ref NativeBattleModulePoolTask task)
        {
            NativeBattleShot198Anchor anchor = Anchor(task);
            int phase = S(task, 12);
            int size = S(task, 28);
            NativeBattleActionMatrix world;
            uint address;
            int fadeStart;
            int duration;
            if (task.Callback == 0x5AE590)
            {
                var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(anchor.Direction.X, 0, anchor.Direction.Z);
                NativeBattleVector direction = new(unchecked((short)normalized.X), unchecked((short)normalized.Y), unchecked((short)normalized.Z));
                NativeBattleVector point = Vector(task, 16);
                world = Align(resources.ReferenceAxes[1], direction) with { X = point.X - (S(task, 22) * direction.X >> 12), Y = 0, Z = point.Z - (S(task, 22) * direction.Z >> 12) };
                address = 0xCFAE64;
                fadeStart = 4;
                duration = 10;
            }
            else
            {
                world = NativeBattleGfMatrices.Multiply56C090(anchor.Rotation, NativeBattleGfMatrices.Euler56CE30(new(0, 0, S(task, 24)))) with { X = anchor.Position.X, Y = anchor.Position.Y, Z = anchor.Position.Z };
                bool scrolling = task.Callback == 0x5AABE0;
                address = scrolling ? 0xCF9834u : 0xCFA52Cu;
                fadeStart = scrolling ? 8 : 2;
                duration = scrolling ? 16 : 10;
            }
            world = NativeBattlePoseMatrices.Scale56BEF0(world, size, size, size);
            int? fade = phase >= fadeStart ? task.Callback == 0x5AE590 ? 682 * (phase - 4) : (phase - fadeStart) << 9 : null;
            Execute(new NativeBattleShot198Mesh(address, world, fade.HasValue ? 0xF3u : 0x33u, fade, Phase: phase));
            if (paused)
            {
                return 0;
            }
            int velocity = S(task, 30);
            task = task.WriteWord(28, unchecked((ushort)(size + velocity))).WriteWord(30, unchecked((ushort)(velocity - velocity / (task.Callback == 0x5AC760 ? 5 : 4))));
            if (task.Callback == 0x5AC760)
            {
                int angularVelocity = S(task, 26);
                task = task.WriteWord(24, unchecked((ushort)(S(task, 24) + angularVelocity))).WriteWord(26, unchecked((ushort)(angularVelocity - angularVelocity / 10)));
            }
            task = Increment(task);
            return S(task, 12) >= duration ? 2 : 0;
        }

        int Beam(ref NativeBattleModulePoolTask task)
        {
            NativeBattleShot198Anchor anchor = Anchor(task);
            int phase = S(task, 12);
            int size = S(task, 28);
            int length = S(task, 32);
            anchor = anchor with { Rotation = anchor.Rotation with { X = 600 * size >> 12, Z = length } };
            state = state with { Anchors = state.Anchors.SetItem(S(task, 14), anchor) };
            uint flags = phase >= 8 ? 0xF3u : 0x33u;
            int? fade = phase >= 8 ? (phase - 8) << 9 : null;
            NativeBattleActionMatrix world = NativeBattlePoseMatrices.Scale56BEF0(Align(resources.ReferenceAxes[0], anchor.Direction), size, size, unchecked(length << 12) / 6000) with { X = anchor.Position.X, Y = anchor.Position.Y, Z = anchor.Position.Z };
            Execute(new NativeBattleShot198Mesh(0xCF6394, world, flags, fade));
            var horizontal = NativeBattleActionGeometryRules.NormalizeVector56BC50(anchor.Direction.X, 0, anchor.Direction.Z);
            int y = anchor.Position.Y + (length * anchor.Direction.Y >> 12);
            int groundLength = Sqrt(unchecked(length * length - y * y));
            world = NativeBattlePoseMatrices.Scale56BEF0(Align(resources.ReferenceAxes[0], new(unchecked((short)horizontal.X), unchecked((short)horizontal.Y), unchecked((short)horizontal.Z))), size, size, unchecked(groundLength << 12) / 6000) with { X = anchor.Position.X, Y = 0, Z = anchor.Position.Z };
            Execute(new NativeBattleShot198Mesh(0xCF8D2C, world, flags, fade));
            if (paused)
            {
                return 0;
            }
            int velocity = S(task, 30);
            task = task.WriteWord(28, unchecked((ushort)(size - velocity))).WriteWord(30, unchecked((ushort)(velocity + velocity / 8)));
            if (phase < 4)
            {
                task = task.WriteWord(32, unchecked((ushort)(length + S(task, 34))));
            }
            task = Increment(task);
            return S(task, 12) >= 16 ? 2 : 0;
        }

        int BodyPairs(ref NativeBattleModulePoolTask task)
        {
            int serial = S(task, 14) + 1;
            int bank = (S(task, 14) & 1) == 0 ? 0 : 4832;
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
            for (int slot = 0; slot < 200; slot++)
            {
                int offset = bank + slot * 24;
                if (I32(particles, offset) != serial)
                {
                    continue;
                }
                ushort phase = U16(particles, offset + 4);
                short scale = I16(particles, offset + 6);
                Execute(new NativeBattleShot198Sprite(0xCF6054, phase, V(particles, offset + 8), scale, -(scale >> 4), 0));
                if (!paused)
                {
                    W16(particles, offset + 4, phase + 1);
                    if (SpriteEnded(0xCF6054, phase))
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

        int Particles(ref NativeBattleModulePoolTask task)
        {
            bool beam = task.Callback == 0x5ADAD0;
            bool smoke = task.Callback == 0x5AE240;
            int markerOffset = beam ? 22 : 32;
            int marker = S(task, markerOffset) + 1;
            int bank = (S(task, markerOffset) & 1) == 0 ? 0 : 4832;
            NativeBattleShot198Anchor anchor = Anchor(task);
            byte[] particles = state.ParticleStorage.ToArray();
            if (!paused && S(task, 12) <= (beam ? 5 : 12))
            {
                NativeBattleVector origin = beam ? anchor.Position : Vector(task, 16);
                if (!beam)
                {
                    origin = new(unchecked((short)(origin.X - (S(task, 22) * anchor.Direction.X >> 12))), unchecked((short)(origin.Y - (S(task, 22) * anchor.Direction.Y >> 12))), unchecked((short)(origin.Z - (S(task, 22) * anchor.Direction.Z >> 12))));
                }
                NativeBattleActionMatrix rotation = beam ? anchor.Rotation : Align(resources.ReferenceAxes[0], anchor.Direction);
                for (int particle = 0; particle < (smoke ? 2 : 8); particle++)
                {
                    int offset = FreeParticle(particles, bank);
                    if (offset < 0)
                    {
                        break;
                    }
                    W32(particles, offset, marker);
                    W16(particles, offset + 4, 0);
                    W16(particles, offset + 6, smoke ? Roll() % 4352 + 1664 : beam ? Roll() % 384 + 768 : Roll() % 640 + 1024);
                    NativeBattleVector point = origin;
                    int radius = 0;
                    if (beam)
                    {
                        if (rotation.Z == 0 || rotation.X == 0)
                        {
                            throw new InvalidDataException("Native Hyper Shot particle emitter requires the beam's preceding retained radius and length writes.");
                        }
                        int along = Roll() % rotation.Z;
                        point = new(unchecked((short)(point.X + (along * anchor.Direction.X >> 12))), unchecked((short)(point.Y + (along * anchor.Direction.Y >> 12))), unchecked((short)(point.Z + (along * anchor.Direction.Z >> 12))));
                        radius = Roll() % rotation.X;
                    }
                    int x = smoke ? Roll() % 1792 - 896 : Roll() % 4096 - 2048;
                    int y = smoke ? Roll() % 1792 - 896 : Roll() % 4096 - 2048;
                    int z = smoke ? -1024 - Roll() % 4608 : beam ? Roll() % 1664 - 832 : -128 - Roll() % 1024;
                    var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
                    var rotated = NativeBattleGfMatrices.RotateLong56C600(rotation, direction.X, direction.Y, direction.Z);
                    if (!beam)
                    {
                        radius = smoke ? Roll() % 300 + 100 : Roll() % 600 + 300;
                    }
                    WriteParticlePoint(particles, offset + 8, new(unchecked((short)(point.X + (rotated.X * radius >> 12))), unchecked((short)(point.Y + (rotated.Y * radius >> 12))), unchecked((short)(point.Z + (rotated.Z * radius >> 12)))));
                    int speed = smoke ? Roll() % 350 + 100 : beam ? Roll() % 85 + 15 : Roll() % 300 + 200;
                    W16(particles, offset + 16, rotated.X * speed >> 12);
                    W16(particles, offset + 18, rotated.Y * speed >> 12);
                    W16(particles, offset + 20, rotated.Z * speed >> 12);
                }
            }
            int active = 0;
            uint sprite = smoke ? 0xCF5E44u : 0xCF6200u;
            for (int slot = 0; slot < 200; slot++)
            {
                int offset = bank + slot * 24;
                if (I32(particles, offset) != marker)
                {
                    continue;
                }
                ushort phase = U16(particles, offset + 4);
                Execute(new NativeBattleShot198Sprite(sprite, phase, V(particles, offset + 8), I16(particles, offset + 6), smoke ? 0 : -S(task, 22), 0));
                if (!paused)
                {
                    W16(particles, offset + 4, phase + 1);
                    if (SpriteEnded(sprite, phase))
                    {
                        W32(particles, offset, 0);
                    }
                    else
                    {
                        for (int axis = 0; axis < 3; axis++)
                        {
                            int velocity = I16(particles, offset + 16 + axis * 2);
                            W16(particles, offset + 8 + axis * 2, I16(particles, offset + 8 + axis * 2) + velocity);
                            if (!beam)
                            {
                                W16(particles, offset + 16 + axis * 2, velocity - (velocity >> 2));
                            }
                        }
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

        int FreeParticle(byte[] bytes, int bank)
        {
            for (int slot = 0; slot < 200; slot++)
            {
                int offset = bank + slot * 24;
                if (I32(bytes, offset) == 0)
                {
                    return offset;
                }
            }
            return -1;
        }

        int Sqrt(int squared)
        {
            if (squared < 0)
            {
                throw new NotSupportedException("Native Hyper Shot reaches an unproved negative56BEC0 conversion.");
            }
            return (int)Math.Sqrt(squared);
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
        void Execute(NativeBattleShot198Operation operation)
        {
            frame = frame with { Memory = frame.Memory with { Shot198 = state } };
            frame = inputs.Execute(frame, operation);
            state = frame.Memory.Shot198 ?? throw new InvalidDataException("Native Shot service lost its module state.");
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
        void Put(int pool, int slot, NativeBattleModulePoolTask task) => pools[pool] = pools[pool].WriteSlot(slot, task);
    }

    private static void RequirePools(NativeBattleEffectTaskPool[] pools)
    {
        if (pools.Length != 3 || pools[0].DescriptorAddress != Root || pools[0].Stride != 16 || pools[0].Slots.Length != 4 || pools[1].DescriptorAddress != 0x225B778 || pools[1].Stride != 36 || pools[1].Slots.Length != 100 || pools[2].DescriptorAddress != 0x225DDB0 || pools[2].Stride != 36 || pools[2].Slots.Length != 100)
        {
            throw new InvalidDataException("Native Hyper Shot lost its original three callback pools.");
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
