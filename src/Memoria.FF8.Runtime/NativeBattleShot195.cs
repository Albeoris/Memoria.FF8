using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShot195Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public abstract record NativeBattleShot195Operation;
public sealed record NativeBattleShot195Bind(byte Actor) : NativeBattleShot195Operation;
public sealed record NativeBattleShot195Sequence(byte Actor, ushort Sequence) : NativeBattleShot195Operation;
public sealed record NativeBattleShot195Stage(int Intensity) : NativeBattleShot195Operation;
public sealed record NativeBattleShot195Contact(bool All = false) : NativeBattleShot195Operation;
public sealed record NativeBattleShot195Sound(uint Descriptor, int Channel, int Volume) : NativeBattleShot195Operation;
public sealed record NativeBattleShot195Echo(byte Actor, NativeBattleBodyBoneMatrices Bones, int Geometry) : NativeBattleShot195Operation;
public sealed record NativeBattleShot195Sprite(uint Address, ushort Phase, NativeBattleVector Position, short Scale, int Depth, ushort Flags, NativeBattleActionMatrix? World = null) : NativeBattleShot195Operation;
public sealed record NativeBattleShot195Mesh(uint Address, NativeBattleActionMatrix World, uint Flags, int? Fade, NativeBattleVector? CameraAnchor = null, int Depth = 0) : NativeBattleShot195Operation;
public sealed record NativeBattleShot195Inputs(uint GraphicsFlags, NativeBattleActionPacketsState Packets, Func<int, NativeBattleShot195Anchor> ReadMuzzle, Func<int, int> BoneCount, Func<int, int, int, NativeBattleVector> ReadBone, Func<int, short> ModelScale, Func<int, NativeBattleVector> ReadCenter, Func<int, NativeBattleBodyBoneMatrices> ReadBones, Func<NativeBattleShot195Frame, NativeBattleShot195Operation, NativeBattleShot195Frame> Execute);

/// <summary>5B2A40/5B2A70/5B4BC0 and Canister Shot's original three physical callback pools.</summary>
public static class NativeBattleShot195
{
    public const uint Root = 0x226B7D0;

    public static NativeBattleShot195Frame Enter(NativeBattleShot195Frame frame, NativeBattleActionPacketsState packets)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Canister Shot requires its original action parameters.");
        byte mode = parameters.Parameter80 ?? throw new NotSupportedException("Native Canister Shot requires its actual module Parameter80.");
        NativeBattleEffectTaskPool[] pools;
        NativeBattleShot195State state;
        if (mode == 0)
        {
            pools = [NativeBattleEffectTaskPool.Initialize(Root, 0x226A980, 16, 4), NativeBattleEffectTaskPool.Initialize(0x226B7E0, 0x226A9C0, 36, 100), NativeBattleEffectTaskPool.Initialize(0x226EDB8, 0x226EDC8, 36, 100)];
            (pools[0], int root) = pools[0].Append(0x5B4BC0);
            Put(0, root, pools[0].Slots[root].WriteWord(12, 0));
            (pools[1], int opening) = pools[1].Append(0x5B4790);
            Put(1, opening, pools[1].Slots[opening].WriteWord(12, 0));
            state = (frame.Memory.Shot195 ?? throw new NotSupportedException("Native Canister Shot requires its retained original module globals; explicitly migrate the standalone snapshot.")).Enter(parameters.Actor78);
        }
        else
        {
            state = frame.Memory.Shot195 ?? throw new NotSupportedException("Native repeated Shot requires its actual retained module globals.");
            state.Validate();
            pools = frame.Effect.Pools.ToArray();
            RequirePools(pools);
            if (mode == 1)
            {
                (pools[0], int ending) = pools[0].Append(0x5B4670);
                Put(0, ending, pools[0].Slots[ending].WriteWord(12, 0));
            }
            else if (mode == 2)
            {
                if (pools[1].Slots.Any(task => (task.Flags & 1) == 0))
                {
                    (pools[1], int shot) = pools[1].Append(0x5B2BF0);
                    NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
                    int first = header.FirstContactIndex ?? throw new InvalidDataException("Native Shot requires its physical header contact pointer.");
                    Put(1, shot, pools[1].Slots[shot].WriteWord(12, 0).WriteWord(14, unchecked((ushort)(state.ShotSerial % 100))).WriteWord(24, (ushort)(state.SequenceCursor + 3)).WriteWord(26, packets.ReadContact(first).Primary.ActorIndex));
                    state = state with { ShotSerial = unchecked(state.ShotSerial + 1), SequenceCursor = state.SequenceCursor < 2 ? state.SequenceCursor + 1 : 0 };
                }
            }
        }
        return frame with { Memory = frame.Memory with { Shot195 = state }, Effect = frame.Effect with { Pools = pools.ToImmutableArray(), ReturnedDescriptor = Root } };

        void Put(int pool, int slot, NativeBattleModulePoolTask task) => pools[pool] = pools[pool].WriteSlot(slot, task);
    }

    public static NativeBattleShot195Frame Advance(NativeBattleShot195Frame frame, NativeBattleShot195Inputs inputs, NativeBattleShot195Resources resources)
    {
        NativeBattleEffectTaskPool[] pools = frame.Effect.Pools.ToArray();
        RequirePools(pools);
        NativeBattleShot195State state = frame.Memory.Shot195 ?? throw new NotSupportedException("Native Canister Shot lost its retained module globals.");
        state.Validate();
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        NativeBattleEffectTaskStep rootStep = pools[0].Advance((pool, index) =>
        {
            pools[0] = pool;
            NativeBattleModulePoolTask task = pool.Slots[index];
            int result;
            if (task.Callback == 0x5B4BC0)
            {
                const int baseOffset = 0;
                frame = frame with { Effect = frame.Effect with { DrawArenaOffset = baseOffset + ((task.ReadWord(12) & 1) != 0 ? 0xE000 : 0) } };
                int actions = AdvancePool(1);
                int particles = AdvancePool(2);
                if (!paused)
                {
                    state = state with { Clock = unchecked(state.Clock + 1) };
                }
                task = Increment(task);
                result = actions == 0 && particles == 0 && (state.Control & 2) != 0 ? 2 : 0;
            }
            else if (task.Callback == 0x5B4670)
            {
                result = Ending(ref task);
            }
            else
            {
                throw new NotSupportedException($"Native Canister Shot root callback{task.Callback:X} is not owned.");
            }
            Put(0, index, task);
            return new(pools[0], result);
        });
        pools[0] = rootStep.Pool;
        return frame with { Memory = frame.Memory with { Shot195 = state }, Effect = frame.Effect with { Pools = pools.ToImmutableArray(), ReturnedDescriptor = rootStep.Survivors == 0 ? 0u : Root } };

        int AdvancePool(int index)
        {
            NativeBattleEffectTaskStep step = pools[index].Advance((pool, slot) =>
            {
                pools[index] = pool;
                NativeBattleModulePoolTask task = pool.Slots[slot];
                int result = task.Callback switch
                {
                    0x5B4790 => Opening(ref task),
                    0x5B4880 => Fade(ref task, false),
                    0x5B4730 => Fade(ref task, true),
                    0x5B48D0 => Echoes(ref task),
                    0x5B2BF0 => Shot(ref task),
                    0x5B32A0 => MuzzleSprite(ref task),
                    0x5B3340 or 0x5B3490 or 0x5B3620 or 0x5B44C0 or 0x5B3FF0 => Mesh(ref task),
                    0x5B37D0 => MuzzleSmoke(ref task),
                    0x5B3A80 => MuzzleSmoke(ref task),
                    0x5B4140 => MeshParticles(ref task),
                    0x5B3DC0 => TargetSprite(ref task),
                    0x5B3F60 => FinalSprite(ref task),
                    _ => throw new NotSupportedException($"Native Canister Shot child callback{task.Callback:X} is not owned.")
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
                Execute(new NativeBattleShot195Bind(state.Actor));
                Execute(new NativeBattleShot195Sequence(state.Actor, 1));
                Append(2, 0x5B4880, child => child.WriteWord(12, 0).WriteWord(14, 8).WriteWord(24, 2048));
                Append(2, 0x5B48D0, child => child.WriteWord(12, 0));
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
                Execute(new NativeBattleShot195Sequence(state.Actor, 2));
                Append(2, 0x5B4730, child => child.WriteWord(12, 0).WriteWord(14, 8).WriteWord(24, 2048));
                state = state with { StopClock = state.Clock };
            }
            if (S(task, 12) == 2)
            {
                Execute(new NativeBattleShot195Contact(true));
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
            Execute(new NativeBattleShot195Stage(descending ? amplitude - level : level));
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
                Execute(new NativeBattleShot195Stage(0));
            }
            return 2;
        }

        int Echoes(ref NativeBattleModulePoolTask task)
        {
            int counter = S(task, 12);
            NativeBattleBodyBoneMatrices current = inputs.ReadBones(state.Actor);
            if (current.Matrices.Length > 4)
            {
                throw new NotSupportedException("Native Canister Shot echo copy exceeds its actual128-byte restore scratch.");
            }
            byte[] particles = state.ParticleStorage.ToArray();
            for (int bone = 0; bone < current.Matrices.Length; bone++)
            {
                int offset = 13632 + bone * 32;
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
                Execute(new NativeBattleShot195Echo(state.Actor, snapshot ?? throw new InvalidDataException("Native active Shot echo lost its retained bone bytes."), geometry));
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
                Execute(new NativeBattleShot195Sequence(state.Actor, task.ReadWord(24)));
                state = state with { Control = state.Control | 4 };
                state = state with { Anchors = state.Anchors.SetItem(serial, inputs.ReadMuzzle(state.Actor)) };
                int scale = Roll() % 640 + 1536;
                Append(2, 0x5B32A0, child => child.WriteWord(12, 0).WriteWord(14, (ushort)serial).WriteWord(28, (ushort)scale));
                int angle = Roll() % 4096;
                scale = Roll() % 640 + 2048;
                Append(2, 0x5B3340, child => child.WriteWord(12, 0).WriteWord(14, (ushort)serial).WriteWord(24, (ushort)angle).WriteWord(28, (ushort)scale));
                int impact = state.ImpactSerial;
                state = state with { ImpactSerial = unchecked(impact + 2) };
                Append(2, 0x5B37D0, child => child.WriteWord(12, 0).WriteWord(14, (ushort)serial).WriteWord(22, unchecked((ushort)impact)));
                Append(2, 0x5B3A80, child => child.WriteWord(12, 0).WriteWord(14, (ushort)serial).WriteWord(22, unchecked((ushort)(impact + 1))));
                task = task.WriteWord(28, (ushort)(Roll() % 640 + 1920));
            }
            if (phase == 1)
            {
                for (int ring = 0; ring < 2; ring++)
                {
                    int angle = Roll() % 4096;
                    int angularVelocity = Roll() % 150 + 110;
                    if (ring == 0 && (angle & 1) != 0)
                    {
                        angularVelocity = -angularVelocity;
                    }
                    int scale = (Roll() % 640 + 768) / 4;
                    int y = ring * 2048;
                    Append(2, 0x5B3620, child => child.WriteWord(12, 0).WriteWord(14, (ushort)serial).WriteWord(24, (ushort)angle).WriteWord(26, unchecked((ushort)angularVelocity)).WriteWord(28, (ushort)scale).WriteWord(30, (ushort)scale).WriteWord(32, (ushort)y));
                }
            }
            if (task.ReadWord(12) <= 4 && (phase & 1) == 0)
            {
                int angle = Roll() % 4096;
                int angularVelocity = Roll() % 150 + 110;
                int scale = S(task, 28) / 3;
                Append(2, 0x5B3490, child => child.WriteWord(12, 0).WriteWord(14, (ushort)serial).WriteWord(24, (ushort)angle).WriteWord(26, (ushort)angularVelocity).WriteWord(28, (ushort)scale).WriteWord(30, (ushort)scale));
            }
            if (phase == 2)
            {
                int target = S(task, 26);
                int bones = inputs.BoneCount(target);
                for (int impact = 0; impact < 4; impact++)
                {
                    int bone = bones == 0 ? 0 : Roll() % bones;
                    int fraction = Roll() % 4096;
                    NativeBattleVector point = inputs.ReadBone(target, bone, fraction);
                    int height = unchecked(2000 * inputs.ModelScale(target)) >> 12;
                    int scale = Roll() % 640 + 1536;
                    int ordinal = impact;
                    Append(2, 0x5B3DC0, child => WriteVector(child.WriteWord(12, 0).WriteWord(14, (ushort)serial).WriteWord(22, unchecked((ushort)height)).WriteWord(26, (ushort)ordinal).WriteWord(28, (ushort)scale).WriteWord(34, 0), 16, point));
                    int angle = Roll() % 4096;
                    int angularVelocity = (Roll() % 190 + 185) * ((angle & 1) == 0 ? 1 : -1);
                    scale /= 5;
                    Append(2, 0x5B44C0, child => WriteVector(child.WriteWord(12, 0).WriteWord(14, (ushort)serial).WriteWord(22, unchecked((ushort)height)).WriteWord(24, (ushort)angle).WriteWord(26, unchecked((ushort)angularVelocity)).WriteWord(28, (ushort)scale).WriteWord(30, (ushort)scale).WriteWord(34, 0), 16, point));
                }
            }
            if (phase == 18)
            {
                Execute(new NativeBattleShot195Contact());
            }
            if (phase == 22)
            {
                Ready();
                state = state with { Control = state.Control & ~4 };
            }
            if (phase == 1)
            {
                Execute(new NativeBattleShot195Sound(resources.ShotSound, 2 - (serial & 1), 128));
            }
            task = Increment(task);
            return S(task, 12) >= 25 ? 2 : 0;
        }

        int MuzzleSprite(ref NativeBattleModulePoolTask task)
        {
            NativeBattleShot195Anchor anchor = Anchor(task);
            short scale = S(task, 28);
            Execute(new NativeBattleShot195Sprite(0xD099F0, task.ReadWord(12), anchor.Position, scale, -(scale >> 3), 0));
            if (paused)
            {
                return 0;
            }
            bool ended = SpriteEnded(0xD099F0, task.ReadWord(12));
            task = Increment(task);
            return ended ? 2 : 0;
        }

        int Mesh(ref NativeBattleModulePoolTask task)
        {
            if (task.Callback == 0x5B44C0 && Delay(ref task))
            {
                return 0;
            }
            uint callback = task.Callback;
            int phase = S(task, 12);
            NativeBattleShot195Anchor anchor = Anchor(task);
            short angle = S(task, 24);
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(callback == 0x5B3340 ? new(0, angle, 0) : callback == 0x5B3620 ? new(angle, S(task, 32), 0) : new(0, 0, callback == 0x5B3FF0 ? (short)0 : angle));
            if (callback != 0x5B3340)
            {
                NativeBattleActionMatrix basis = anchor.Rotation;
                if (callback == 0x5B3620)
                {
                    NativeBattleVector axis = resources.ReferenceAxes[1];
                    NativeBattleVector direction = anchor.Direction;
                    NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(axis.X, axis.Y, axis.Z, direction.X, direction.Y, direction.Z);
                    basis = new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), 0, 0, 0, 4096);
                }
                local = NativeBattleGfMatrices.Multiply56C090(basis, local);
            }
            short scale = S(task, 28);
            local = NativeBattlePoseMatrices.Scale56BEF0(local, scale, scale, scale) with { X = anchor.Position.X, Y = callback == 0x5B3340 ? 0 : anchor.Position.Y, Z = anchor.Position.Z };
            uint address = callback switch { 0x5B3340 => 0xD0A978, 0x5B3490 or 0x5B44C0 => 0xD0AFE0, 0x5B3620 => 0xD0B488, _ => 0xD09FB8 };
            int start = callback switch { 0x5B3340 or 0x5B44C0 => 0, 0x5B3490 => 2, 0x5B3620 => 6, _ => 4 };
            uint flags = phase >= start ? 0xF3u : 0x33u;
            int? fade = phase >= start ? (phase - start) * (callback == 0x5B44C0 ? 409 : callback == 0x5B3340 ? 819 : 512) : null;
            if (callback == 0x5B3340)
            {
                fade = phase == 0 ? 2048 : phase == 1 ? null : (phase - 2) * 819;
                flags = phase == 1 ? 0x33u : 0xF3u;
            }
            bool hybrid = callback is 0x5B44C0 or 0x5B3FF0;
            Execute(new NativeBattleShot195Mesh(address, local, flags, fade, hybrid ? Vector(task, 16) : null, -S(task, 22)));
            if (paused)
            {
                return 0;
            }
            if (callback != 0x5B3340)
            {
                int velocity = S(task, 30);
                int divisor = callback == 0x5B3620 ? 8 : callback == 0x5B3FF0 ? 4 : 5;
                task = task.WriteWord(28, unchecked((ushort)(scale + velocity))).WriteWord(30, unchecked((ushort)(velocity - velocity / divisor)));
                if (callback != 0x5B3FF0)
                {
                    int angularVelocity = S(task, 26);
                    task = task.WriteWord(24, unchecked((ushort)(angle + angularVelocity))).WriteWord(26, unchecked((ushort)(angularVelocity - angularVelocity / 10)));
                }
            }
            task = Increment(task);
            int duration = callback switch { 0x5B3340 => 7, 0x5B3620 => 14, 0x5B3FF0 => 12, _ => 10 };
            return S(task, 12) >= duration ? 2 : 0;
        }

        int TargetSprite(ref NativeBattleModulePoolTask task)
        {
            if (Delay(ref task))
            {
                return 0;
            }
            NativeBattleVector point = Vector(task, 16);
            int phase = S(task, 12);
            Execute(new NativeBattleShot195Sprite(0xD09E0C, task.ReadWord(12), point, S(task, 28), -S(task, 22), 0));
            if (paused)
            {
                return 0;
            }
            if (phase == 15)
            {
                ushort ordinal = task.ReadWord(26);
                ushort height = task.ReadWord(22);
                ushort serial = task.ReadWord(14);
                int scale = Roll() % 2048 + 3328;
                Append(2, 0x5B3F60, child => WriteVector(child.WriteWord(12, 0).WriteWord(22, height).WriteWord(26, ordinal).WriteWord(28, (ushort)scale), 16, point));
                scale = (Roll() % 2688 + 5248) / 3;
                Append(2, 0x5B3FF0, child => WriteVector(child.WriteWord(12, 0).WriteWord(14, serial).WriteWord(22, height).WriteWord(28, (ushort)scale).WriteWord(30, (ushort)scale), 16, point));
                int impact = state.ImpactSerial;
                state = state with { ImpactSerial = unchecked(impact + 1) };
                Append(2, 0x5B4140, child => WriteVector(child.WriteWord(12, 0).WriteWord(14, serial).WriteWord(22, height).WriteWord(32, unchecked((ushort)impact)), 16, point));
            }
            bool ended = SpriteEnded(0xD09E0C, task.ReadWord(12));
            task = Increment(task);
            return ended ? 2 : 0;
        }

        int FinalSprite(ref NativeBattleModulePoolTask task)
        {
            Execute(new NativeBattleShot195Sprite(0xD097CC, task.ReadWord(12), Vector(task, 16), S(task, 28), -S(task, 22), 8));
            if (paused)
            {
                return 0;
            }
            bool ended = SpriteEnded(0xD097CC, task.ReadWord(12));
            task = Increment(task);
            return ended ? 2 : 0;
        }

        bool Delay(ref NativeBattleModulePoolTask task)
        {
            short delay = S(task, 34);
            if (delay <= 0)
            {
                return false;
            }
            if (!paused)
            {
                task = task.WriteWord(34, unchecked((ushort)(delay - 1)));
            }
            return true;
        }

        NativeBattleShot195Anchor Anchor(NativeBattleModulePoolTask task)
        {
            return state.Anchors[S(task, 14)] ?? throw new InvalidDataException("Native Canister Shot requires its retained56-byte muzzle anchor.");
        }

        int MuzzleSmoke(ref NativeBattleModulePoolTask task)
        {
            bool pair = task.Callback == 0x5B3A80;
            int bank = (S(task, 14) & 1) == 0 ? 0 : 10032;
            int serial = S(task, 22) + 1;
            byte[] particles = state.ParticleStorage.ToArray();
            if (!paused && S(task, 12) <= 3)
            {
                for (int spawn = 0; spawn < 2; spawn++)
                {
                    int offset = FreeParticle(particles, bank, 150, 24);
                    if (offset < 0)
                    {
                        break;
                    }
                    if (pair)
                    {
                        for (int bone = 29; bone <= 30; bone++)
                        {
                            int physical = offset + (bone - 29) * 24;
                            W32(particles, physical, serial);
                            W16(particles, physical + 4, 0);
                            W16(particles, physical + 6, Roll() % 640 + 800);
                            NativeBattleVector point = inputs.ReadBone(state.Actor, bone, 3072);
                            WriteParticlePoint(particles, physical + 8, new(unchecked((short)(point.X + Roll() % 50 - 25)), unchecked((short)(point.Y + Roll() % 50 - 25)), unchecked((short)(point.Z + Roll() % 50 - 25))));
                        }
                    }
                    else
                    {
                        W32(particles, offset, serial);
                        W16(particles, offset + 4, 0);
                        W16(particles, offset + 6, Roll() % 640 + 1408);
                        NativeBattleVector point = Anchor(task).Position;
                        WriteParticlePoint(particles, offset + 8, new(unchecked((short)(point.X + Roll() % 240 - 120)), unchecked((short)(point.Y + Roll() % 240 - 120)), unchecked((short)(point.Z + Roll() % 240 - 120))));
                        W16(particles, offset + 18, -5 - Roll() % 10);
                    }
                }
            }
            uint address = pair ? 0xD09C60u : 0xD09AB4u;
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
                Execute(new NativeBattleShot195Sprite(address, phase, V(particles, offset + 8), scale, -(scale >> 4), pair ? (ushort)8 : (ushort)0));
                if (!paused)
                {
                    W16(particles, offset + 4, phase + 1);
                    if (SpriteEnded(address, phase))
                    {
                        W32(particles, offset, 0);
                    }
                    else
                    {
                        if (!pair)
                        {
                            W16(particles, offset + 10, I16(particles, offset + 10) + I16(particles, offset + 18));
                        }
                        else
                        {
                            W16(particles, offset + 6, scale + (scale >> 4));
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

        int MeshParticles(ref NativeBattleModulePoolTask task)
        {
            int bank = (S(task, 14) & 1) == 0 ? 6800 : 3600;
            int serial = S(task, 32);
            byte[] particles = state.ParticleStorage.ToArray();
            if (!paused && S(task, 12) <= 2)
            {
                for (int spawn = 0; spawn < 4; spawn++)
                {
                    int offset = FreeParticle(particles, bank, 100, 32);
                    if (offset < 0)
                    {
                        break;
                    }
                    W32(particles, offset, serial);
                    W16(particles, offset + 4, 0);
                    W16(particles, offset + 6, Roll() % 640 + 128);
                    WriteParticlePoint(particles, offset + 8, Vector(task, 16));
                    int x = Roll() % 4096 - 2048;
                    int y = Roll() % 4096 - 2048;
                    int z = Roll() % 4096 - 2048;
                    var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
                    int speed = Roll() % 400 + 140;
                    W16(particles, offset + 16, unchecked(speed * direction.X) >> 12);
                    W16(particles, offset + 18, unchecked(speed * direction.Y) >> 12);
                    W16(particles, offset + 20, unchecked(speed * direction.Z) >> 12);
                    int angle = Roll() % 4096;
                    W16(particles, offset + 24, angle);
                    W16(particles, offset + 28, angle >> 1);
                    int step = (Roll() % 390 + 90) * (((offset - bank) / 32 & 1) == 0 ? 1 : -1);
                    W16(particles, offset + 26, step);
                    W16(particles, offset + 30, Roll() % 320 + 80);
                }
            }
            int active = 0;
            for (int slot = 0; slot < 100; slot++)
            {
                int offset = bank + slot * 32;
                if (I32(particles, offset) != serial)
                {
                    continue;
                }
                int phase = I16(particles, offset + 4);
                short scale = I16(particles, offset + 6);
                NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CD50(new(I16(particles, offset + 24), 0, I16(particles, offset + 28)));
                NativeBattleVector point = V(particles, offset + 8);
                world = NativeBattlePoseMatrices.Scale56BEF0(world, scale, scale, scale) with { X = point.X, Y = point.Y, Z = point.Z };
                Execute(new NativeBattleShot195Mesh(0xD0AD68, world, phase >= 6 ? 195u : 12u, phase >= 6 ? (phase - 6) << 9 : null));
                if (!paused)
                {
                    W16(particles, offset + 4, phase + 1);
                    if (phase + 1 >= 14)
                    {
                        W32(particles, offset, 0);
                    }
                    else
                    {
                        for (int axis = 0; axis < 3; axis++)
                        {
                            int velocity = I16(particles, offset + 16 + axis * 2);
                            W16(particles, offset + 8 + axis * 2, I16(particles, offset + 8 + axis * 2) + velocity);
                            W16(particles, offset + 16 + axis * 2, velocity - (velocity >> 3));
                        }
                        W16(particles, offset + 24, I16(particles, offset + 24) + I16(particles, offset + 26));
                        W16(particles, offset + 28, I16(particles, offset + 28) + I16(particles, offset + 30));
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

        int FreeParticle(byte[] bytes, int bank, int count, int stride)
        {
            int slot = Enumerable.Range(0, count).FirstOrDefault(index => I32(bytes, bank + index * stride) == 0, -1);
            return slot < 0 ? -1 : bank + slot * stride;
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
        void Execute(NativeBattleShot195Operation operation)
        {
            frame = frame with { Memory = frame.Memory with { Shot195 = state } };
            frame = inputs.Execute(frame, operation);
            state = frame.Memory.Shot195 ?? throw new InvalidDataException("Native Shot service lost its module state.");
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
        if (pools.Length != 3 || pools[0].DescriptorAddress != Root || pools[0].Stride != 16 || pools[0].Slots.Length != 4 || pools[1].DescriptorAddress != 0x226B7E0 || pools[1].Stride != 36 || pools[1].Slots.Length != 100 || pools[2].DescriptorAddress != 0x226EDB8 || pools[2].Stride != 36 || pools[2].Slots.Length != 100)
        {
            throw new InvalidDataException("Native Canister Shot lost its original three callback pools.");
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
