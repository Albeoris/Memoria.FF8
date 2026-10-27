using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShot188Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public abstract record NativeBattleShot188Operation;
public sealed record NativeBattleShot188Bind(byte Actor) : NativeBattleShot188Operation;
public sealed record NativeBattleShot188Sequence(byte Actor, ushort Sequence) : NativeBattleShot188Operation;
public sealed record NativeBattleShot188Stage(int Intensity) : NativeBattleShot188Operation;
public sealed record NativeBattleShot188Contact : NativeBattleShot188Operation;
public sealed record NativeBattleShot188Sound(uint Descriptor, int Channel, int Volume) : NativeBattleShot188Operation;
public sealed record NativeBattleShot188Echo(byte Actor, NativeBattleBodyBoneMatrices Bones, int Geometry) : NativeBattleShot188Operation;
public sealed record NativeBattleShot188Sprite(uint Address, ushort Phase, NativeBattleVector Position, short Scale, int Depth, ushort Flags, NativeBattleActionMatrix? World = null) : NativeBattleShot188Operation;
public sealed record NativeBattleShot188Mesh(uint Address, NativeBattleActionMatrix World, uint Flags, int? Fade) : NativeBattleShot188Operation;
public sealed record NativeBattleShot188Inputs(uint GraphicsFlags, NativeBattleActionPacketsState Packets, Func<int, NativeBattleShot188Anchor> ReadMuzzle, Func<int, int> BoneCount, Func<int, int, int, NativeBattleVector> ReadBone, Func<int, short> ModelScale, Func<int, NativeBattleBodyBoneMatrices> ReadBones, Func<NativeBattleShot188Frame, NativeBattleShot188Operation, NativeBattleShot188Frame> Execute);

/// <summary>5BE340/5BE370/5BF650 and Normal Shot's original three physical callback pools.</summary>
public static class NativeBattleShot188
{
    public const uint Root = 0x22B3898;

    public static NativeBattleShot188Frame Enter(NativeBattleShot188Frame frame, NativeBattleActionPacketsState packets)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Normal Shot requires its original action parameters.");
        byte mode = parameters.Parameter80 ?? throw new NotSupportedException("Native Normal Shot requires its actual module Parameter80.");
        NativeBattleEffectTaskPool[] pools;
        NativeBattleShot188State state;
        if (mode == 0)
        {
            pools = [NativeBattleEffectTaskPool.Initialize(Root, 0x22B2A48, 16, 4), NativeBattleEffectTaskPool.Initialize(0x22B38A8, 0x22B2A88, 36, 100), NativeBattleEffectTaskPool.Initialize(0x22B5580, 0x22B5590, 36, 100)];
            (pools[0], int root) = pools[0].Append(0x5BF650);
            Put(0, root, pools[0].Slots[root].WriteWord(12, 0));
            (pools[1], int opening) = pools[1].Append(0x5BF220);
            Put(1, opening, pools[1].Slots[opening].WriteWord(12, 0));
            state = (frame.Memory.Shot188 ?? throw new NotSupportedException("Native Normal Shot requires its retained original module globals; explicitly migrate the standalone snapshot.")).Enter(parameters.Actor78);
        }
        else
        {
            state = frame.Memory.Shot188 ?? throw new NotSupportedException("Native repeated Shot requires its actual retained module globals.");
            state.Validate();
            pools = frame.Effect.Pools.ToArray();
            RequirePools(pools);
            if (mode == 1)
            {
                (pools[0], int ending) = pools[0].Append(0x5BF100);
                Put(0, ending, pools[0].Slots[ending].WriteWord(12, 0));
            }
            else if (mode == 2)
            {
                if (pools[1].Slots.Any(task => (task.Flags & 1) == 0))
                {
                    (pools[1], int shot) = pools[1].Append(0x5BE4D0);
                    NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
                    int first = header.FirstContactIndex ?? throw new InvalidDataException("Native Shot requires its physical header contact pointer.");
                    Put(1, shot, pools[1].Slots[shot].WriteWord(12, 0).WriteWord(14, unchecked((ushort)(state.ShotSerial % 100))).WriteWord(24, (ushort)(state.SequenceCursor + 3)).WriteWord(26, packets.ReadContact(first).Primary.ActorIndex));
                    state = state with { ShotSerial = unchecked(state.ShotSerial + 1), SequenceCursor = state.SequenceCursor < 2 ? state.SequenceCursor + 1 : 0 };
                }
            }
        }
        return frame with { Memory = frame.Memory with { Shot188 = state }, Effect = frame.Effect with { Pools = pools.ToImmutableArray(), ReturnedDescriptor = Root } };

        void Put(int pool, int slot, NativeBattleModulePoolTask task) => pools[pool] = pools[pool].WriteSlot(slot, task);
    }

    public static NativeBattleShot188Frame Advance(NativeBattleShot188Frame frame, NativeBattleShot188Inputs inputs, NativeBattleShot188Resources resources)
    {
        NativeBattleEffectTaskPool[] pools = frame.Effect.Pools.ToArray();
        RequirePools(pools);
        NativeBattleShot188State state = frame.Memory.Shot188 ?? throw new NotSupportedException("Native Normal Shot lost its retained module globals.");
        state.Validate();
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        NativeBattleEffectTaskStep rootStep = pools[0].Advance((pool, index) =>
        {
            pools[0] = pool;
            NativeBattleModulePoolTask task = pool.Slots[index];
            int result;
            if (task.Callback == 0x5BF650)
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
            else if (task.Callback == 0x5BF100)
            {
                result = Ending(ref task);
            }
            else
            {
                throw new NotSupportedException($"Native Normal Shot root callback{task.Callback:X} is not owned.");
            }
            Put(0, index, task);
            return new(pools[0], result);
        });
        pools[0] = rootStep.Pool;
        return frame with { Memory = frame.Memory with { Shot188 = state }, Effect = frame.Effect with { Pools = pools.ToImmutableArray(), ReturnedDescriptor = rootStep.Survivors == 0 ? 0u : Root } };

        int AdvancePool(int index)
        {
            NativeBattleEffectTaskStep step = pools[index].Advance((pool, slot) =>
            {
                pools[index] = pool;
                NativeBattleModulePoolTask task = pool.Slots[slot];
                int result = task.Callback switch
                {
                    0x5BF220 => Opening(ref task),
                    0x5BF310 => Fade(ref task, false),
                    0x5BF1C0 => Fade(ref task, true),
                    0x5BF360 => Echoes(ref task),
                    0x5BE4D0 => Shot(ref task),
                    0x5BE980 or 0x5BEAC0 => MuzzleMesh(ref task),
                    0x5BEC30 or 0x5BECD0 => MuzzleSprite(ref task),
                    0x5BEDE0 => Impact(ref task),
                    _ => throw new NotSupportedException($"Native Normal Shot child callback{task.Callback:X} is not owned.")
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
                Execute(new NativeBattleShot188Bind(state.Actor));
                Execute(new NativeBattleShot188Sequence(state.Actor, 1));
                Append(2, 0x5BF310, child => child.WriteWord(12, 0).WriteWord(14, 8).WriteWord(24, 2048));
                Append(2, 0x5BF360, child => child.WriteWord(12, 0));
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
                Execute(new NativeBattleShot188Sequence(state.Actor, 2));
                Append(2, 0x5BF1C0, child => child.WriteWord(12, 0).WriteWord(14, 8).WriteWord(24, 2048));
                state = state with { StopClock = state.Clock };
            }
            if (S(task, 12) == 1)
            {
                Execute(new NativeBattleShot188Contact());
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
            Execute(new NativeBattleShot188Stage(descending ? amplitude - level : level));
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
                Execute(new NativeBattleShot188Stage(0));
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
                Execute(new NativeBattleShot188Echo(state.Actor, snapshot ?? throw new InvalidDataException("Native active Shot echo lost its retained bone bytes."), geometry));
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
                Execute(new NativeBattleShot188Sequence(state.Actor, task.ReadWord(24)));
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
                NativeBattleShot188Anchor origin = inputs.ReadMuzzle(state.Actor);
                state = state with { Anchors = state.Anchors.SetItem(serial, origin) };
                int angle = Roll() % 4096;
                int scale = Roll() % 1280 + 2304;
                Append(2, 0x5BE980, child => child.WriteWord(12, 0).WriteWord(14, (ushort)serial).WriteWord(24, (ushort)angle).WriteWord(28, (ushort)scale));
                angle = Roll() % 4096;
                scale = (short)(Roll() % 1536 + 2048) / 3;
                Append(2, 0x5BEAC0, child => child.WriteWord(12, 0).WriteWord(14, (ushort)serial).WriteWord(24, (ushort)angle).WriteWord(28, (ushort)scale).WriteWord(30, (ushort)scale));
                scale = Roll() % 1792 + 1024;
                Append(2, 0x5BEC30, child => child.WriteWord(12, 0).WriteWord(14, (ushort)serial).WriteWord(28, (ushort)scale));
                Append(2, 0x5BECD0, child => child.WriteWord(12, 0).WriteWord(14, (ushort)serial).WriteWord(28, unchecked((ushort)(9 * scale << 9 >> 12))));
            }
            if (phase == 1)
            {
                int target = S(task, 26);
                int count = inputs.BoneCount(target);
                int bone = count == 0 ? 0 : Roll() % count;
                int impactSerial = state.ImpactSerial;
                state = state with { ImpactSerial = unchecked(impactSerial + 1) };
                int angle = Roll() % 4096;
                NativeBattleVector point = inputs.ReadBone(target, bone, angle);
                int scale = Roll() % 3072 + 2048;
                Append(2, 0x5BEDE0, child => WriteVector(child.WriteWord(12, 0).WriteWord(14, unchecked((ushort)impactSerial)).WriteWord(22, unchecked((ushort)(2000 * inputs.ModelScale(target) >> 12))).WriteWord(28, (ushort)scale), 16, point));
                Execute(new NativeBattleShot188Contact());
                Execute(new NativeBattleShot188Sound(resources.ShotSound, 2 - ((serial & 1) != 0 ? 1 : 0), 128));
            }
            task = Increment(task);
            return S(task, 12) >= 10 ? 2 : 0;
        }

        int MuzzleMesh(ref NativeBattleModulePoolTask task)
        {
            NativeBattleShot188Anchor anchor = state.Anchors[S(task, 14)] ?? throw new InvalidDataException("Native Shot muzzle child lost its original anchor.");
            NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CE30(new(0, 0, S(task, 24)));
            NativeBattleActionMatrix combined = NativeBattleGfMatrices.Multiply56C090(anchor.Rotation, rotation);
            combined = NativeBattlePoseMatrices.Scale56BEF0(combined, S(task, 28), S(task, 28), S(task, 28)) with { X = anchor.Position.X, Y = anchor.Position.Y, Z = anchor.Position.Z };
            int phase = S(task, 12);
            bool ring = task.Callback == 0x5BEAC0;
            int? fade = phase >= 1 ? ring ? 1365 * phase - 1365 : 3072 : null;
            Execute(new NativeBattleShot188Mesh(ring ? 0xD1D638u : 0xD1D0F0u, combined, phase >= 1 ? 0xF3u : 0x33u, fade));
            if (paused)
            {
                return 0;
            }
            if (ring)
            {
                int velocity = S(task, 30);
                task = task.WriteWord(28, unchecked((ushort)(S(task, 28) + velocity))).WriteWord(30, unchecked((ushort)(velocity - velocity / 3)));
            }
            task = Increment(task);
            return S(task, 12) >= (ring ? 4 : 2) ? 2 : 0;
        }

        int MuzzleSprite(ref NativeBattleModulePoolTask task)
        {
            NativeBattleShot188Anchor anchor = state.Anchors[S(task, 14)] ?? throw new InvalidDataException("Native Shot sprite child lost its original anchor.");
            bool aligned = task.Callback == 0x5BECD0;
            int scale = S(task, 28);
            uint resource = aligned ? 0xD1CB00u : 0xD1CA28u;
            Execute(new NativeBattleShot188Sprite(resource, task.ReadWord(12), anchor.Position, (short)scale, aligned ? 0 : -(scale >> 3), aligned ? (ushort)8 : (ushort)0, aligned ? NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(1024, 0, 0)), scale, scale, scale) with { X = anchor.Position.X, Y = 0, Z = anchor.Position.Z } : null));
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
            int phase = S(task, 12);
            int serial = S(task, 14);
            int baseOffset = (serial & 1) != 0 ? 150 * 24 : 0;
            if (phase < 16)
            {
                Execute(new NativeBattleShot188Sprite(0xD1CB74, task.ReadWord(12), Vector(task, 16), S(task, 28), -S(task, 22), 8));
            }
            byte[] bytes = state.ImpactParticles.ToArray();
            int active = 0;
            for (int particle = 0; particle < 150; particle++)
            {
                int offset = baseOffset + particle * 24;
                if (I32(bytes, offset) != serial + 1)
                {
                    continue;
                }
                ushort particlePhase = U16(bytes, offset + 4);
                Execute(new NativeBattleShot188Sprite(0xD1CF44, particlePhase, V(bytes, offset + 8), I16(bytes, offset + 6), -S(task, 22), 8));
                if (paused)
                {
                    continue;
                }
                W16(bytes, offset + 4, particlePhase + 1);
                if (SpriteEnded(0xD1CF44, particlePhase))
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
                for (int spawn = 0; spawn < 16; spawn++)
                {
                    int free = Enumerable.Range(0, 150).FirstOrDefault(index => I32(bytes, baseOffset + index * 24) == 0, -1);
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

        bool SpriteEnded(uint address, ushort phase)
        {
            NativeBattleSearchSpriteResource resource = resources.Sprites.Single(sprite => sprite.Address == address);
            return new NativeBattleModuleResource(0, address, 0, 0, resource.Bytes).ProjectedFrame571C80(phase).NextOffset < 0;
        }
        void Ready() => frame = frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters! with { Parameter81 = 1 } } };
        void Execute(NativeBattleShot188Operation operation)
        {
            frame = frame with { Memory = frame.Memory with { Shot188 = state } };
            frame = inputs.Execute(frame, operation);
            state = frame.Memory.Shot188 ?? throw new InvalidDataException("Native Shot service lost its module state.");
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
        if (pools.Length != 3 || pools[0].DescriptorAddress != Root || pools[0].Stride != 16 || pools[0].Slots.Length != 4 || pools[1].DescriptorAddress != 0x22B38A8 || pools[1].Stride != 36 || pools[1].Slots.Length != 100 || pools[2].DescriptorAddress != 0x22B5580 || pools[2].Stride != 36 || pools[2].Slots.Length != 100)
        {
            throw new InvalidDataException("Native Normal Shot lost its original three callback pools.");
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
