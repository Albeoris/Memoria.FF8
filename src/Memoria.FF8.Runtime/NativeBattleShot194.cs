using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShot194Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public abstract record NativeBattleShot194Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleShot194Bind(byte Actor) : NativeBattleShot194Operation;
public sealed record NativeBattleShot194Sequence(byte Actor, ushort Sequence) : NativeBattleShot194Operation;
public sealed record NativeBattleShot194Stage(int Intensity) : NativeBattleShot194Operation;
public sealed record NativeBattleShot194Contact : NativeBattleShot194Operation;
public sealed record NativeBattleShot194Sound(uint Descriptor, int Channel, int Volume) : NativeBattleShot194Operation;
public sealed record NativeBattleShot194Echo(byte Actor, NativeBattleBodyBoneMatrices Bones, int Geometry) : NativeBattleShot194Operation;
public sealed record NativeBattleShot194Sprite(uint Address, ushort Phase, NativeBattleVector Position, short Scale, int Depth, ushort Flags, NativeBattleActionMatrix? World = null) : NativeBattleShot194Operation;
public sealed record NativeBattleShot194ReloadCommon(uint CompletionAddress) : NativeBattleShot194Operation;
public sealed record NativeBattleShot194Quad(NativeBattleVector Position, short Scale, uint Color) : NativeBattleShot194Operation;
public sealed record NativeBattleShot194Mesh(uint Address, NativeBattleActionMatrix World, uint Flags, int? Fade, int? UOffset = null, bool Billboard = false, int Depth = 0) : NativeBattleShot194Operation;
public sealed record NativeBattleShot194Inputs(uint GraphicsFlags, NativeBattleActionPacketsState Packets, Func<int, NativeBattleShot194Anchor> ReadMuzzle, Func<int, int> BoneCount, Func<int, int, int, NativeBattleVector> ReadBone, Func<int, short> ModelScale, Func<int, NativeBattleVector> ReadCenter, Func<int, NativeBattleBodyBoneMatrices> ReadBones, Func<NativeBattleShot194Frame, NativeBattleShot194Operation, NativeBattleShot194Frame> Execute);

/// <summary>5B4C60/5B4C90/5B7080 and Flame Shot's original three physical callback pools.</summary>
public static class NativeBattleShot194
{
    public const uint Root = 0x2272A18;

    public static NativeBattleOriginalEffectState WriteCompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        NativeBattleEffectTaskPool pool = effect.Pools.Single(pool => pool.DescriptorAddress == Root);
        uint delta = unchecked(address - pool.BankAddress);
        if (delta % pool.Stride != 14 || delta / pool.Stride >= pool.Slots.Length)
        {
            throw new NotSupportedException("Native Flame Shot completion requires its actual root task BYTE14.");
        }
        int slot = checked((int)(delta / pool.Stride));
        int index = effect.Pools.IndexOf(pool);
        return effect with { Pools = effect.Pools.SetItem(index, pool.WriteSlot(slot, pool.Slots[slot].WriteByte(14, value))) };
    }

    public static NativeBattleShot194Frame Enter(NativeBattleShot194Frame frame, NativeBattleActionPacketsState packets)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Flame Shot requires its original action parameters.");
        byte mode = parameters.Parameter80 ?? throw new NotSupportedException("Native Flame Shot requires its actual module Parameter80.");
        NativeBattleEffectTaskPool[] pools;
        NativeBattleShot194State state;
        if (mode == 0)
        {
            pools = [NativeBattleEffectTaskPool.Initialize(Root, 0x2271BC8, 16, 4), NativeBattleEffectTaskPool.Initialize(0x2272A28, 0x2271C08, 36, 100), NativeBattleEffectTaskPool.Initialize(0x2274700, 0x2274710, 36, 100)];
            (pools[0], int root) = pools[0].Append(0x5B7080);
            Put(0, root, pools[0].Slots[root].WriteWord(12, 0));
            (pools[1], int opening) = pools[1].Append(0x5B6C50);
            Put(1, opening, pools[1].Slots[opening].WriteWord(12, 0));
            state = (frame.Memory.Shot194 ?? throw new NotSupportedException("Native Flame Shot requires its retained original module globals; explicitly migrate the standalone snapshot.")).Enter(parameters.Actor78);
        }
        else
        {
            state = frame.Memory.Shot194 ?? throw new NotSupportedException("Native repeated Shot requires its actual retained module globals.");
            state.Validate();
            pools = frame.Effect.Pools.ToArray();
            RequirePools(pools);
            if (mode == 1)
            {
                (pools[0], int ending) = pools[0].Append(0x5B6B30);
                Put(0, ending, pools[0].Slots[ending].WriteWord(12, 0));
            }
            else if (mode == 2)
            {
                if (pools[1].Slots.Any(task => (task.Flags & 1) == 0))
                {
                    (pools[1], int shot) = pools[1].Append(0x5B4DF0);
                    NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
                    int first = header.FirstContactIndex ?? throw new InvalidDataException("Native Shot requires its physical header contact pointer.");
                    Put(1, shot, pools[1].Slots[shot].WriteWord(12, 0).WriteWord(14, unchecked((ushort)(state.ShotSerial % 100))).WriteWord(24, (ushort)(state.SequenceCursor + 3)).WriteWord(26, packets.ReadContact(first).Primary.ActorIndex));
                    state = state with { ShotSerial = unchecked(state.ShotSerial + 1), SequenceCursor = state.SequenceCursor < 2 ? state.SequenceCursor + 1 : 0 };
                }
            }
        }
        return frame with { Memory = frame.Memory with { Shot194 = state }, Effect = frame.Effect with { Pools = pools.ToImmutableArray(), ReturnedDescriptor = Root } };

        void Put(int pool, int slot, NativeBattleModulePoolTask task) => pools[pool] = pools[pool].WriteSlot(slot, task);
    }

    public static NativeBattleShot194Frame Advance(NativeBattleShot194Frame frame, NativeBattleShot194Inputs inputs, NativeBattleShot194Resources resources)
    {
        NativeBattleEffectTaskPool[] pools = frame.Effect.Pools.ToArray();
        RequirePools(pools);
        NativeBattleShot194State state = frame.Memory.Shot194 ?? throw new NotSupportedException("Native Flame Shot lost its retained module globals.");
        state.Validate();
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        NativeBattlePilotVisualIdentity drawingIdentity = default;
        NativeBattleModulePoolTask drawingTask = null!;
        NativeBattleEffectTaskStep rootStep = pools[0].Advance((pool, index) =>
        {
            pools[0] = pool;
            NativeBattleModulePoolTask task = pool.Slots[index];
            int result;
            if (task.Callback == 0x5B7080)
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
                    (pools[0], int cleanup) = pools[0].Append(0x5B7130);
                    Put(0, cleanup, pools[0].Slots[cleanup].WriteWord(12, 0).WriteWord(14, 0));
                    task = task.UpdateHeader(task.Flags, task.State, pools[0].Slots[index].Next, task.Callback);
                }
                task = Increment(task);
                result = actions == 0 && particles == 0 && (state.Control & 2) != 0 ? 2 : 0;
            }
            else if (task.Callback == 0x5B6B30)
            {
                result = Ending(ref task);
            }
            else if (task.Callback == 0x5B7130)
            {
                if (S(task, 12) == 1)
                {
                    Execute(new NativeBattleShot194ReloadCommon(checked(pools[0].BankAddress + (uint)(index * pools[0].Stride + 14))));
                }
                task = Increment(task);
                result = task.ReadWord(14) != 0 ? 2 : 0;
            }
            else
            {
                throw new NotSupportedException($"Native Flame Shot root callback{task.Callback:X} is not owned.");
            }
            Put(0, index, task);
            return new(pools[0], result);
        });
        pools[0] = rootStep.Pool;
        return frame with { Memory = frame.Memory with { Shot194 = state }, Effect = frame.Effect with { Pools = pools.ToImmutableArray(), ReturnedDescriptor = rootStep.Survivors == 0 ? 0u : Root } };

        int AdvancePool(int index)
        {
            NativeBattleEffectTaskStep step = pools[index].Advance((pool, slot) =>
            {
                pools[index] = pool;
                NativeBattleModulePoolTask task = pool.Slots[slot];
                drawingTask = task;
                drawingIdentity = NativeBattlePilotVisualLifetimes.Identity(194, task);
                int result = task.Callback switch
                {
                    0x5B6C50 => Opening(ref task),
                    0x5B6D40 => Fade(ref task, false),
                    0x5B6BF0 => Fade(ref task, true),
                    0x5B6D90 => Echoes(ref task),
                    0x5B4DF0 => Shot(ref task, checked(pool.BankAddress + (uint)(slot * pool.Stride))),
                    0x5B5640 or 0x5B5840 or 0x5B64A0 or 0x5B6580 or 0x5B66F0 => Mesh(ref task),
                    0x5B57A0 or 0x5B59B0 => MuzzleSprite(ref task),
                    0x5B5AC0 => Trail(ref task),
                    0x5B5CF0 => TrailHead(ref task),
                    0x5B5DB0 => TrailSmoke(ref task),
                    0x5B5FB0 => TrailSparks(ref task),
                    0x5B6880 => Impact(ref task),
                    _ => throw new NotSupportedException($"Native Flame Shot child callback{task.Callback:X} is not owned.")
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
                Execute(new NativeBattleShot194Bind(state.Actor));
                Execute(new NativeBattleShot194Sequence(state.Actor, 1));
                Append(2, 0x5B6D40, child => child.WriteWord(12, 0).WriteWord(14, 8).WriteWord(24, 2048));
                Append(2, 0x5B6D90, child => child.WriteWord(12, 0));
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
                Execute(new NativeBattleShot194Sequence(state.Actor, 2));
                Append(2, 0x5B6BF0, child => child.WriteWord(12, 0).WriteWord(14, 8).WriteWord(24, 2048));
                state = state with { StopClock = state.Clock };
            }
            if (S(task, 12) == 4)
            {
                Execute(new NativeBattleShot194Contact());
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
            Execute(new NativeBattleShot194Stage(descending ? amplitude - level : level));
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
                Execute(new NativeBattleShot194Stage(0));
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
                Execute(new NativeBattleShot194Echo(state.Actor, snapshot ?? throw new InvalidDataException("Native active Shot echo lost its retained bone bytes."), geometry));
                history = history > 0 ? history - 1 : 4;
            }
            if (paused)
            {
                return 0;
            }
            task = Increment(task);
            return S(task, 12) >= state.StopClock + 5 ? 2 : 0;
        }

        int Shot(ref NativeBattleModulePoolTask task, uint physicalAddress)
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
                Execute(new NativeBattleShot194Sequence(state.Actor, task.ReadWord(24)));
                state = state with { Control = state.Control | 4 };
            }
            if (phase == 15)
            {
                Ready();
                state = state with { Control = state.Control & ~4 };
            }
            int serial = S(task, 14);
            if (phase == 0)
            {
                state = state with { Anchors = state.Anchors.SetItem(serial, inputs.ReadMuzzle(state.Actor)) };
                NativeBattleActionHeaderSlot header = inputs.Packets.Headers[frame.Effect.Parameters!.HeaderIndex7C];
                int first = header.FirstContactIndex ?? throw new InvalidDataException("Native Flame Shot requires its physical contact pointer.");
                int count = header.ReadByte(16);
                short x = 0;
                short y = 0;
                short z = 0;
                for (int target = 0; target < count; target++)
                {
                    NativeBattleVector center = inputs.ReadCenter(inputs.Packets.ReadContact(first + target).Primary.ActorIndex);
                    x = unchecked((short)(x + center.X));
                    y = unchecked((short)(y + center.Y));
                    z = unchecked((short)(z + center.Z));
                }
                NativeBattleVector point = new(unchecked((short)(x / count + Roll() % 2000 - 1000)), unchecked((short)(y / count + Roll() % 2000 - 1000)), 0);
                point = point with { Y = Math.Min(point.Y, (short)-500), Z = unchecked((short)(z / count + Roll() % 2000 - 1000)) };
                task = WriteVector(task, 16, point);
                int angle = Roll() % 4096;
                int scale = (Roll() % 1536 + 2816) / 3;
                AppendMesh(0x5B5640, serial, angle, 0, scale, scale, point);
                scale = Roll() % 1792 + 1024;
                Append(2, 0x5B57A0, child => child.WriteWord(12, 0).WriteWord(14, (ushort)serial).WriteWord(28, (ushort)scale));
                SpawnMuzzleRing(serial);
                int lane = state.LaneSerial % 8;
                state = state with { LaneSerial = unchecked(state.LaneSerial + 1) };
                Append(2, 0x5B5AC0, child => WriteVector(child.WriteWord(12, 0).WriteWord(14, (ushort)serial).WriteWord(24, (ushort)lane), 16, point));
            }
            if (phase == 1)
            {
                int scale = Roll() % 896 + 2816;
                Append(2, 0x5B59B0, child => child.WriteWord(12, 0).WriteWord(14, (ushort)serial).WriteWord(28, (ushort)scale));
            }
            if (task.ReadWord(12) <= 2)
            {
                SpawnMuzzleRing(serial);
            }
            NativeBattleVector targetPoint = Vector(task, 16);
            if (phase == 8)
            {
                int scale = (Roll() % 1536 + 5120) / 4;
                AppendMesh(0x5B64A0, serial, 0, 0, scale, scale, targetPoint);
            }
            if (phase == 9)
            {
                int angle = Roll() % 4096;
                short step = unchecked((short)((Roll() % 160 + 90) * ((angle & 1) == 0 ? 1 : -1)));
                int scale = (Roll() % 1024 + 2944) / 3;
                AppendMesh(0x5B6580, serial, angle, step, scale, scale, targetPoint);
                angle = Roll() % 4096;
                AppendMesh(0x5B6580, serial, angle, NativeBattleShot194Trail.SecondRingVelocity(step, physicalAddress), scale - (scale >> 4), scale, targetPoint);
                angle = Roll() % 4096;
                scale = (Roll() % 1024 + 4608) / 5;
                AppendMesh(0x5B66F0, serial, angle, 0, scale, scale, targetPoint);
                int impact = state.ImpactSerial;
                state = state with { ImpactSerial = unchecked(impact + 1) };
                Append(2, 0x5B6880, child => WriteVector(child.WriteWord(12, 0).WriteWord(14, (ushort)serial).WriteWord(22, unchecked((ushort)impact)), 16, targetPoint));
            }
            if (phase == 11)
            {
                Execute(new NativeBattleShot194Contact());
            }
            if (phase == 1)
            {
                Execute(new NativeBattleShot194Sound(resources.ShotSound, 2 - (serial & 1), 128));
            }
            task = Increment(task);
            return S(task, 12) >= 20 ? 2 : 0;
        }

        void SpawnMuzzleRing(int serial)
        {
            int angle = Roll() % 4096;
            int step = (Roll() % 110 + 60) * ((angle & 1) == 0 ? 1 : -1);
            int scale = (Roll() % 768 + 1536) / 3;
            AppendMesh(0x5B5840, serial, angle, step, scale, scale, default);
        }

        void AppendMesh(uint callback, int serial, int angle, int angularVelocity, int scale, int velocity, NativeBattleVector point)
        {
            Append(2, callback, child => WriteVector(child.WriteWord(12, 0).WriteWord(14, unchecked((ushort)serial)).WriteWord(24, unchecked((ushort)angle)).WriteWord(26, unchecked((ushort)angularVelocity)).WriteWord(28, unchecked((ushort)scale)).WriteWord(30, unchecked((ushort)velocity)), 16, point));
        }

        int Mesh(ref NativeBattleModulePoolTask task)
        {
            uint callback = task.Callback;
            int phase = S(task, 12);
            int angle = S(task, 24);
            int scale = S(task, 28);
            NativeBattleActionMatrix world;
            if (callback is 0x5B5640 or 0x5B5840)
            {
                NativeBattleShot194Anchor anchor = state.Anchors[S(task, 14)] ?? throw new InvalidDataException("Native Flame Shot lost its muzzle anchor.");
                world = NativeBattleGfMatrices.Multiply56C090(anchor.Rotation, NativeBattleGfMatrices.Euler56CE30(new(0, 0, (short)angle))) with { X = anchor.Position.X, Y = anchor.Position.Y, Z = anchor.Position.Z };
            }
            else
            {
                NativeBattleVector point = Vector(task, 16);
                world = NativeBattleGfMatrices.Euler56CE30(new(0, (short)angle, 0)) with { X = point.X, Y = point.Y, Z = point.Z };
            }
            world = NativeBattlePoseMatrices.Scale56BEF0(world, scale, scale, scale);
            uint resource = callback switch { 0x5B5640 => 0xD106DC, 0x5B5840 => 0xD11B54, 0x5B64A0 => 0xD0FB0C, 0x5B6580 => 0xD0DCE4, 0x5B66F0 => 0xD1283C, _ => throw new InvalidDataException("Native Flame Shot selected an unknown mesh.") };
            int fadeStart = callback switch { 0x5B64A0 => 1, 0x5B66F0 => 8, _ => 2 };
            int fadeStep = callback switch { 0x5B5640 => 1024, 0x5B5840 or 0x5B66F0 => 512, 0x5B64A0 => 682, _ => 341 };
            Execute(new NativeBattleShot194Mesh(resource, world, phase >= fadeStart ? 0xF3u : 0x33u, phase >= fadeStart ? (phase - fadeStart) * fadeStep : null, callback == 0x5B66F0 ? -12 * phase : null, callback == 0x5B64A0, -(scale >> 3)));
            if (paused)
            {
                return 0;
            }
            int velocity = S(task, 30);
            int divisor = callback switch { 0x5B64A0 => 6, 0x5B66F0 => 8, _ => 4 };
            if (callback is 0x5B5840 or 0x5B6580 or 0x5B66F0)
            {
                int angularVelocity = S(task, 26);
                task = task.WriteWord(24, unchecked((ushort)(angle + angularVelocity))).WriteWord(26, unchecked((ushort)(angularVelocity - (callback == 0x5B5840 ? angularVelocity >> 4 : angularVelocity / 12))));
            }
            task = Increment(task).WriteWord(28, unchecked((ushort)(scale + velocity))).WriteWord(30, unchecked((ushort)(velocity - velocity / divisor)));
            int duration = callback switch { 0x5B5640 => 6, 0x5B5840 => 10, 0x5B64A0 => 7, 0x5B6580 => 14, _ => 16 };
            return S(task, 12) >= duration ? 2 : 0;
        }

        int MuzzleSprite(ref NativeBattleModulePoolTask task)
        {
            NativeBattleShot194Anchor anchor = state.Anchors[S(task, 14)] ?? throw new InvalidDataException("Native Flame Shot lost its muzzle anchor.");
            bool ground = task.Callback == 0x5B59B0;
            int scale = S(task, 28);
            uint address = ground ? 0xD0DC34u : 0xD0DB5Cu;
            NativeBattleActionMatrix? world = ground ? NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(1024, 0, 0)), scale, scale, scale) with { X = anchor.Position.X, Y = 0, Z = anchor.Position.Z } : null;
            Execute(new NativeBattleShot194Sprite(address, task.ReadWord(12), anchor.Position, (short)scale, ground ? 0 : -(scale >> 3), ground ? (ushort)8 : (ushort)0, world));
            if (paused)
            {
                return 0;
            }
            bool ended = SpriteEnded(address, task.ReadWord(12));
            task = Increment(task);
            return ended ? 2 : 0;
        }

        int Trail(ref NativeBattleModulePoolTask task)
        {
            if (paused)
            {
                return 0;
            }
            if (S(task, 12) == 0)
            {
                int lane = S(task, 24);
                NativeBattleShot194Anchor anchor = state.Anchors[S(task, 14)] ?? throw new InvalidDataException("Native Flame Shot lost its trail muzzle anchor.");
                state = NativeBattleShot194Trail.Initialize5B5AC0(state, lane, anchor, Vector(task, 16), resources.ReferenceAxes.Single());
                int scale = Roll() % 384 + 1024;
                Append(2, 0x5B5CF0, child => child.WriteWord(12, 0).WriteWord(14, (ushort)lane).WriteWord(24, 0).WriteWord(26, 5).WriteWord(28, (ushort)scale));
                int serial = state.ImpactSerial;
                state = state with { ImpactSerial = unchecked(serial + 2) };
                Append(2, 0x5B5DB0, child => child.WriteWord(12, 0).WriteWord(14, (ushort)lane).WriteWord(22, unchecked((ushort)serial)).WriteWord(24, 0).WriteWord(26, 5));
                Append(2, 0x5B5FB0, child => child.WriteWord(12, 0).WriteWord(14, (ushort)lane).WriteWord(22, unchecked((ushort)(serial + 1))).WriteWord(24, 0).WriteWord(26, 5));
            }
            task = Increment(task);
            return S(task, 12) >= 18 ? 2 : 0;
        }

        int TrailHead(ref NativeBattleModulePoolTask task)
        {
            short scale = S(task, 28);
            Execute(new NativeBattleShot194Sprite(0xD0DCA8, 0, NativeBattleShot194Trail.ReadSample(state, S(task, 14), S(task, 24)), scale, -(scale >> 3), 0));
            if (paused)
            {
                return 0;
            }
            task = AdvanceTrailIndex(task);
            return S(task, 24) >= 24 ? 2 : 0;
        }

        NativeBattleModulePoolTask AdvanceTrailIndex(NativeBattleModulePoolTask task)
        {
            int velocity = S(task, 26);
            return Increment(task).WriteWord(24, unchecked((ushort)(S(task, 24) + velocity))).WriteWord(26, unchecked((ushort)(velocity > 2 ? velocity - 1 : velocity)));
        }

        int TrailSmoke(ref NativeBattleModulePoolTask task)
        {
            int lane = S(task, 14);
            int serial = S(task, 22) + 1;
            int bank = (lane & 1) * 3600;
            byte[] particles = state.ImpactParticles.ToArray();
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
                Execute(new NativeBattleShot194Sprite(0xD0D938, phase, V(particles, offset + 8), scale, -(scale >> 4), 0) { PilotIdentity = NativeBattleShotVisualLifetimes.Identity(194, drawingTask, drawingIdentity, offset) });
                if (!paused)
                {
                    W16(particles, offset + 4, I16(particles, offset + 4) + 1);
                    if (SpriteEnded(0xD0D938, phase))
                    {
                        W32(particles, offset, 0);
                    }
                    else
                    {
                        W16(particles, offset + 6, scale + (scale >> 5));
                        active++;
                    }
                }
            }
            if (paused)
            {
                return 0;
            }
            task = AdvanceTrailIndex(task);
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
                    NativeBattleShotVisualLifetimes.Allocated(194, drawingTask, offset);
                    W32(particles, offset, serial);
                    W16(particles, offset + 4, 0);
                    W16(particles, offset + 6, Roll() % 256 + 768);
                    WriteParticlePoint(particles, offset + 8, NativeBattleShot194Trail.ReadSample(state, lane, sample));
                }
            }
            state = state with { ImpactParticles = particles.ToImmutableArray() };
            return S(task, 12) >= 4 && active == 0 ? 2 : 0;
        }

        int TrailSparks(ref NativeBattleModulePoolTask task)
        {
            int lane = S(task, 14);
            int serial = S(task, 22) + 1;
            int bank = (lane & 1) * 3600;
            byte[] particles = state.ImpactParticles.ToArray();
            int drawn = 0;
            for (int slot = 0; slot < 150; slot++)
            {
                int offset = bank + slot * 24;
                if (I32(particles, offset) != serial)
                {
                    continue;
                }
                int phase = I16(particles, offset + 4);
                NativeBattleVector point = V(particles, offset + 8);
                if (!paused)
                {
                    W16(particles, offset + 4, phase + 1);
                    if (phase + 1 >= 8)
                    {
                        W32(particles, offset, 0);
                    }
                    else
                    {
                        for (int axis = 0; axis < 3; axis++)
                        {
                            int velocity = I16(particles, offset + 16 + axis * 2);
                            W16(particles, offset + 8 + axis * 2, I16(particles, offset + 8 + axis * 2) + velocity);
                            W16(particles, offset + 16 + axis * 2, velocity - (velocity >> 4));
                        }
                    }
                }
                int beforeDraw = frame.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native Flame Shot requires its actual packet cursor.");
                Execute(new NativeBattleShot194Quad(point, I16(particles, offset + 6), resources.TrailColors[phase]) { PilotIdentity = NativeBattleShotVisualLifetimes.Identity(194, drawingTask, drawingIdentity, offset) });
                if (frame.Effect.DrawArenaOffset != beforeDraw)
                {
                    drawn++;
                }
            }
            if (paused)
            {
                return 0;
            }
            task = AdvanceTrailIndex(task);
            int index = S(task, 24);
            if (index < 24)
            {
                for (int sample = index; sample >= index - S(task, 26); sample--)
                {
                    for (int spawn = 0; spawn < 4; spawn++)
                    {
                        int offset = FreeParticle(particles, bank);
                        if (offset < 0)
                        {
                            break;
                        }
                        NativeBattleShotVisualLifetimes.Allocated(194, drawingTask, offset);
                        W32(particles, offset, serial);
                        W16(particles, offset + 4, 0);
                        W16(particles, offset + 6, Roll() % 256 + 384);
                        NativeBattleVector position = NativeBattleShot194Trail.ReadSample(state, lane, sample);
                        WriteParticlePoint(particles, offset + 8, new(unchecked((short)(position.X + Roll() % 400 - 200)), unchecked((short)(position.Y + Roll() % 400 - 200)), unchecked((short)(position.Z + Roll() % 400 - 200))));
                        int before = sample == 23 ? sample - 1 : sample;
                        int after = sample == 23 ? sample : sample + 1;
                        NativeBattleVector a = NativeBattleShot194Trail.ReadSample(state, lane, before);
                        NativeBattleVector b = NativeBattleShot194Trail.ReadSample(state, lane, after);
                        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(b.X - a.X, b.Y - a.Y, b.Z - a.Z);
                        NativeBattleVector axis = resources.ReferenceAxes.Single();
                        NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(axis.X, axis.Y, axis.Z, direction.X, direction.Y, direction.Z);
                        NativeBattleActionMatrix rotation = new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), 0, 0, 0, 4096);
                        int angle = Roll() % 4096;
                        int sx = NativeBattleCameraMath.Sine(angle);
                        int sy = NativeBattleCameraMath.Cosine(angle);
                        int sz = Roll() % 2048;
                        var vector = NativeBattleGfMatrices.RotateLong56C600(rotation, sx, sy, sz);
                        int speed = Roll() % 35 + 15;
                        W16(particles, offset + 16, unchecked(speed * vector.X) >> 12);
                        W16(particles, offset + 18, unchecked(speed * vector.Y) >> 12);
                        W16(particles, offset + 20, unchecked(speed * vector.Z) >> 12);
                    }
                }
            }
            state = state with { ImpactParticles = particles.ToImmutableArray() };
            return S(task, 12) >= 8 && drawn == 0 ? 2 : 0;
        }

        int Impact(ref NativeBattleModulePoolTask task)
        {
            int bank = (S(task, 14) & 1) * 3600;
            int serial = S(task, 22) + 1;
            int phase = S(task, 12);
            byte[] particles = state.ImpactParticles.ToArray();
            if (!paused && phase <= 9)
            {
                for (int spawn = 0; spawn < 3; spawn++)
                {
                    int offset = FreeParticle(particles, bank);
                    if (offset < 0)
                    {
                        break;
                    }
                    NativeBattleShotVisualLifetimes.Allocated(194, drawingTask, offset);
                    W32(particles, offset, serial);
                    W16(particles, offset + 4, 0);
                    W16(particles, offset + 6, Roll() % 2304 + 2560);
                    int angle = Roll() % 4096;
                    int radius = 400 * (phase + 1);
                    NativeBattleVector point = Vector(task, 16);
                    int dx = unchecked(radius * NativeBattleCameraMath.Sine(angle)) >> 12;
                    int y = point.Y + Roll() % 1100 - 550;
                    int dz = unchecked(radius * NativeBattleCameraMath.Cosine(angle)) >> 12;
                    WriteParticlePoint(particles, offset + 8, new(unchecked((short)(point.X + dx)), unchecked((short)y), unchecked((short)(point.Z + dz))));
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
                ushort spritePhase = U16(particles, offset + 4);
                short scale = I16(particles, offset + 6);
                Execute(new NativeBattleShot194Sprite(0xD0D938, spritePhase, V(particles, offset + 8), scale, -(scale >> 4), 0) { PilotIdentity = NativeBattleShotVisualLifetimes.Identity(194, drawingTask, drawingIdentity, offset) });
                if (!paused)
                {
                    W16(particles, offset + 4, spritePhase + 1);
                    if (SpriteEnded(0xD0D938, spritePhase))
                    {
                        W32(particles, offset, 0);
                    }
                    else
                    {
                        active++;
                    }
                }
            }
            state = state with { ImpactParticles = particles.ToImmutableArray() };
            if (paused)
            {
                return 0;
            }
            task = Increment(task);
            return active == 0 ? 2 : 0;
        }

        int FreeParticle(byte[] bytes, int bank)
        {
            int slot = Enumerable.Range(0, 150).FirstOrDefault(index => I32(bytes, bank + index * 24) == 0, -1);
            return slot < 0 ? -1 : bank + slot * 24;
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
        void Execute(NativeBattleShot194Operation operation)
        {
            frame = frame with { Memory = frame.Memory with { Shot194 = state } };
            frame = inputs.Execute(frame, operation with { PilotIdentity = operation.PilotIdentity ?? drawingIdentity });
            state = frame.Memory.Shot194 ?? throw new InvalidDataException("Native Shot service lost its module state.");
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
        if (pools.Length != 3 || pools[0].DescriptorAddress != Root || pools[0].Stride != 16 || pools[0].Slots.Length != 4 || pools[1].DescriptorAddress != 0x2272A28 || pools[1].Stride != 36 || pools[1].Slots.Length != 100 || pools[2].DescriptorAddress != 0x2274700 || pools[2].Stride != 36 || pools[2].Slots.Length != 100)
        {
            throw new InvalidDataException("Native Flame Shot lost its original three callback pools.");
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
