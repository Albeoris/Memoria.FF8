using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeTornado146(NativeBattleGraphicsState graphics) => new NativeTornado146Runner(this, graphics).Initialize();
    private NativeBattleGraphicsState AdvanceNativeTornado146(NativeBattleGraphicsState graphics) => new NativeTornado146Runner(this, graphics).Advance();

    private sealed partial class NativeTornado146Runner
    {
        private readonly BattleSession owner;
        private NativeBattleGraphicsState graphics;
        private NativeBattleOriginalEffectState effect;
        private readonly NativeBattleTornado146Resources resources;
        private NativeBattleTornado146State state;
        private NativeBattleEffectTaskPool[] pools;
        private NativeCrtRandomState random;
        private NativeBattleEffectMemory Memory
        {
            get => owner.NativeEffectMemory ?? throw new NotSupportedException("Native Tornado requires its retained effect memory.");
            set => owner.NativeEffectMemory = value;
        }
        private NativeBattleCameraProjection Projection => owner.NativeCamera?.Projection ?? throw new NotSupportedException("Native Tornado requires its current projection.");
        private NativeBattleActionMatrix View => Projection.View ?? throw new NotSupportedException("Native Tornado requires its current view matrix.");
        private NativeBattleSummonColors Colors => owner.definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Native Tornado requires its current GTE color rules.");
        private bool DrawOnly => (graphics.Flags & 0x201) != 0;

        public NativeTornado146Runner(BattleSession owner, NativeBattleGraphicsState graphics)
        {
            this.owner = owner;
            this.graphics = graphics;
            effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Tornado requires its original action owner.");
            resources = effect.Binding.Tornado146Resources ?? throw new NotSupportedException("Native Tornado requires its complete original resources.");
            state = Memory.Tornado146 ?? throw new NotSupportedException("Native Tornado requires its retained module globals.");
            state.Validate();
            pools = effect.Pools.ToArray();
            random = owner.nativeCrtRandom ?? throw new NotSupportedException("Native Tornado requires its shared CRT stream.");
        }

        public NativeBattleGraphicsState Initialize()
        {
            NativeBattleEffectTextureState textures = (Memory.Textures ?? throw new NotSupportedException("Native Tornado requires its current texture queue.")).Register5099A0(resources.Camera);
            owner.NativeCamera = owner.NativeCamera!.RegisterControlResource(textures.Animation);
            int source = effect.ResourcePointers.Single(pointer => pointer.GlobalAddress == 0x2495228).ArenaOffset;
            (textures, _) = textures.EnqueueArenaTim505E30(Memory.Arena ?? throw new NotSupportedException("Native Tornado requires its loaded effect arena."), source);
            Memory = Memory with { Textures = textures };
            NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(0x24936E8, 0x24936C8, 16, 2);
            (roots, int rootSlot) = roots.Append(0x618560);
            roots = roots with { Slots = roots.Slots.SetItem(rootSlot, roots.Slots[rootSlot].WriteWord(12, 0)) };
            NativeBattleEffectTaskPool tasks = NativeBattleEffectTaskPool.Initialize(0x2493750, 0x2493760, 36, 100);
            (tasks, int director) = tasks.Append(0x614E10);
            tasks = tasks with { Slots = tasks.Slots.SetItem(director, tasks.Slots[director].WriteWord(12, 0).WriteWord(14, 0)) };
            pools = [roots, tasks];
            NativeBattleTornado146Work work = new(state);
            NativeBattleEffectParameters parameters = effect.Parameters ?? throw new NotSupportedException("Native Tornado requires its current cast parameters.");
            int first = owner.nativeActionPackets.Headers[parameters.HeaderIndex7C].FirstContactIndex ?? throw new NotSupportedException("Native Tornado requires the actual first contact pointer.");
            work.WriteDword(0x2491E3C, owner.nativeActionPackets.ReadContact(first).Primary.ActorIndex);
            work.WriteDword(0x2494AA0, parameters.Actor78);
            state = work.Freeze();
            effect = effect with { ReturnedDescriptor = 0x24936E8, DrawArenaOffset = null };
            return Publish();
        }

        public NativeBattleGraphicsState Advance()
        {
            if (pools.Length != 2 || pools[0].DescriptorAddress != 0x24936E8 || pools[1].DescriptorAddress != 0x2493750)
            {
                throw new InvalidDataException("Native Tornado lost its original root/effect banks.");
            }
            NativeBattleEffectTaskStep roots = pools[0].Advance((pool, slot) =>
            {
                pools[0] = pool;
                NativeBattleModulePoolTask task = pool.Slots[slot];
                int result;
                if (task.Callback == 0x618560)
                {
                    NativeBattleTornado146Work work = new(state);
                    uint parity = (uint)(Word(task, 12) & 1);
                    work.WriteDword(0x24AA624, parity);
                    work.WriteDword(0x24AA628, parity == 0 ? 0x249A624u : 0x24A2624u);
                    state = work.Freeze();
                    NativeBattleEffectTaskStep children = pools[1].Advance((current, childSlot) =>
                    {
                        pools[1] = current;
                        NativeBattleModulePoolTask child = current.Slots[childSlot];
                        int code = Step(ref child);
                        child = child with { Next = pools[1].Slots[childSlot].Next };
                        Put(1, childSlot, child);
                        return new(pools[1], code);
                    });
                    pools[1] = children.Pool;
                    task = task.WriteWord(12, unchecked((ushort)(Word(task, 12) + 1)));
                    result = children.Survivors == 0 ? 2 : 0;
                }
                else if (task.Callback == 0x618480)
                {
                    if (Word(task, 12) == 0)
                    {
                        task = task.WriteWord(14, 0);
                        Put(0, slot, task);
                        uint completion = checked(pools[0].BankAddress + (uint)slot * 16 + 14);
                        (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)effect.ResourcePointers.Single(pointer => pointer.GlobalAddress == 0x2495228).ArenaOffset), completion, (address, value) =>
                        {
                            int targetSlot = checked((int)((address - pools[0].BankAddress) / 16));
                            Put(0, targetSlot, pools[0].Slots[targetSlot].WriteByte(14, value));
                        });
                        graphics = graphics with { Auxiliary = auxiliary };
                        task = task with { Payload = pools[0].Slots[slot].Payload };
                    }
                    result = Word(task, 14) != 0 ? 2 : 0;
                    task = task.WriteWord(12, unchecked((ushort)(Word(task, 12) + 1)));
                }
                else
                {
                    throw new NotSupportedException("Native Tornado root bank selected an unrelated callback.");
                }
                task = task with { Next = pools[0].Slots[slot].Next };
                Put(0, slot, task);
                return new(pools[0], result);
            });
            pools[0] = roots.Pool;
            effect = effect with { ReturnedDescriptor = roots.Survivors == 0 ? 0u : 0x24936E8u };
            return Publish();
        }

        private int Step(ref NativeBattleModulePoolTask task)
        {
            return task.Callback switch
            {
                0x614E10 => Director(ref task),
                0x6155B0 => Piece(ref task),
                0x6164E0 => Ground(ref task),
                0x6171F0 => Funnel(ref task),
                0x617990 => Sparks(ref task),
                0x617C50 => Cloud(ref task),
                0x617DF0 => Lift(ref task),
                0x618280 => Debris(ref task),
                _ => throw new NotSupportedException("Native Tornado effect bank selected an unrelated callback.")
            };
        }

        private int Funnel(ref NativeBattleModulePoolTask task)
        {
            NativeBattleTornado146FunnelFrame result = NativeBattleTornado146Funnel.Advance6171F0(state, task, random, Projection, graphics.PrimitiveFrame!.Pending, graphics.Flags, owner.NativeFileStaging ?? throw new NotSupportedException("Native Tornado funnel requires its retained temporary stack."));
            state = result.State;
            task = result.Task;
            random = result.Random;
            owner.NativeFileStaging = result.Staging;
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = result.Ordering } };
            return result.Result;
        }

        private int Director(ref NativeBattleModulePoolTask task)
        {
            if (DrawOnly)
            {
                return 0;
            }
            int age = Word(task, 12);
            int action = Word(task, 14);
            if (age == 0)
            {
                NativeBattleModuleAnchor anchor = TargetAnchor(action);
                task = task.WriteWord(16, unchecked((ushort)anchor.X)).WriteWord(18, 0).WriteWord(20, unchecked((ushort)anchor.Z)).WriteWord(22, 0).WriteWord(34, 0);
                CaptureGround(Position(task));
                InitializeCraterGrid();
                state = NativeBattleTornado146Funnel.Initialize6152E0(state);
            }
            if (age == 18)
            {
                for (int index = 0; index < 15; index++)
                {
                    int slot = Allocate(1, 0x6155B0);
                    NativeBattleModulePoolTask child = CopyPosition(pools[1].Slots[slot].WriteWord(12, 0).WriteWord(14, (ushort)index), task);
                    child = child.WriteWord(22, unchecked((ushort)(-50 - Random() % 140 - index))).WriteWord(24, 0).WriteWord(26, (ushort)(Random() % 10 + 5)).WriteWord(28, 0).WriteWord(30, (ushort)(Random() % 40 + 20)).WriteWord(32, 0);
                    short spin = (short)(Random() % 60 + 30);
                    child = child.WriteWord(34, unchecked((ushort)((child.ReadWord(26) & 1) != 0 ? -spin : spin)));
                    Put(1, slot, child);
                }
                int ground = Allocate(1, 0x6164E0);
                Put(1, ground, CopyPosition(pools[1].Slots[ground].WriteWord(12, 0), task));
            }
            if (age == 1)
            {
                int funnel = Allocate(1, 0x6171F0);
                Put(1, funnel, CopyPosition(pools[1].Slots[funnel].WriteWord(12, 0), task));
                InitializeCloudDeltas();
                int sparks = Allocate(1, 0x617990);
                Put(1, sparks, CopyPosition(pools[1].Slots[sparks].WriteWord(12, 0), task));
                NativeBattleTornado146Work work = new(state);
                for (int index = 0; index < 60; index++)
                {
                    work.WriteDword(0x249A230u + (uint)index * 16, uint.MaxValue);
                }
                state = work.Freeze();
            }
            if (age is >= 2 and <= 70 && age % 12 == 2)
            {
                task = task.WriteWord(34, (ushort)(task.ReadWord(34) ^ 1));
                int cloud = Allocate(1, 0x617C50);
                Put(1, cloud, CopyPosition(pools[1].Slots[cloud].WriteWord(12, 0).WriteWord(14, task.ReadWord(34)).WriteWord(28, 12288), task));
            }
            if (age == 1)
            {
                int lift = Allocate(1, 0x617DF0);
                Put(1, lift, CopyPosition(pools[1].Slots[lift].WriteWord(12, 0).WriteWord(14, (ushort)action).WriteWord(24, 0), task));
            }
            NativeBattleEffectParameters parameters = effect.Parameters!;
            NativeBattleActionHeaderSlot first = owner.nativeActionPackets.Headers[parameters.HeaderIndex7C];
            if (action == first.ReadByte(17) && age == 78)
            {
                int wait = Allocate(0, 0x618480);
                Put(0, wait, pools[0].Slots[wait].WriteWord(12, 0));
            }
            if (age == 82)
            {
                NativeBattleActionHeaderSlot selected = owner.nativeActionPackets.Headers[checked(parameters.HeaderIndex7C + action)];
                int start = selected.FirstContactIndex ?? throw new NotSupportedException("Native Tornado contact group has no original contact pointer.");
                ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                graphics = owner.ConsumeNativeGraphicsContacts(graphics with { PendingContacts = Enumerable.Range(0, selected.ReadByte(16)).Select(index => owner.nativeActionPackets.ReadContact(start + index)).ToImmutableArray() });
                graphics = graphics with { PendingContacts = pending };
            }
            if (age == 83 && action + 1 <= first.ReadByte(17))
            {
                int next = Allocate(1, 0x614E10);
                Put(1, next, pools[1].Slots[next].WriteWord(12, 0).WriteWord(14, unchecked((ushort)(action + 1))));
            }
            if (age == 1)
            {
                NativeBattleModuleAnchor point = TargetAnchor(action);
                owner.PublishNativeModuleSound(new(resources.WindSound, 0, point));
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            return Word(task, 12) > 88 ? 2 : 0;
        }

        private NativeBattleModuleAnchor TargetAnchor(int action)
        {
            NativeBattleActionHeaderSlot header = owner.nativeActionPackets.Headers[checked(effect.Parameters!.HeaderIndex7C + action)];
            int count = header.ReadByte(16);
            int start = header.FirstContactIndex ?? throw new NotSupportedException("Native Tornado requires its selected contact group.");
            if (count == 0)
            {
                throw new InvalidDataException("Native6184C0 cannot divide by a zero original target count.");
            }
            short x = 0;
            short y = 0;
            short z = 0;
            short w = 0;
            for (int index = 0; index < count; index++)
            {
                int actor = owner.nativeActionPackets.ReadContact(start + index).Primary.ActorIndex;
                NativeBattleModuleAnchor anchor = NativeBattlePoseMatrices.ReadAnchor(graphics.Models[actor], PoseSource(actor));
                x = unchecked((short)(x + anchor.X));
                y = unchecked((short)(y + anchor.Y));
                z = unchecked((short)(z + anchor.Z));
                w = anchor.W;
            }
            return new((short)(x / count), (short)(y / count), (short)(z / count), w);
        }

        private NativeBattlePoseSource PoseSource(int actor) => owner.NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native Tornado requires the actor's actual pose source.");
        private static NativeBattleModulePoolTask CopyPosition(NativeBattleModulePoolTask target, NativeBattleModulePoolTask source) => target.WriteDword(16, source.ReadDword(16)).WriteDword(20, source.ReadDword(20));

        private NativeBattleGraphicsState Publish()
        {
            Memory = Memory with { Tornado146 = state };
            owner.nativeCrtRandom = random;
            effect = effect with { Pools = pools.ToImmutableArray() };
            return graphics with { OriginalEffect = effect };
        }
        private ushort Random()
        {
            random = random.Draw(out ushort value);
            return value;
        }
        private int Allocate(int bank, uint callback)
        {
            (pools[bank], int slot) = pools[bank].Append(callback);
            return slot;
        }
        private void Put(int bank, int slot, NativeBattleModulePoolTask task) => pools[bank] = pools[bank] with { Slots = pools[bank].Slots.SetItem(slot, task) };
        private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
        private static NativeBattleVector Position(NativeBattleModulePoolTask task) => new(Word(task, 16), Word(task, 18), Word(task, 20));
        private void Append(NativeBattleGfGeometryBatch batch)
        {
            NativeBattleTornado146Work work = new(state);
            uint physical = work.Dword(0x24AA628);
            int cursor = checked((int)(physical - NativeBattleTornado146State.WorkAddress));
            NativeBattleRegionalPacketWrite written = NativeBattleRegionalPacketArena.Append(state.Storage, NativeBattleTornado146State.PacketBank, cursor, NativeBattleTornado146State.WorkLength, graphics.PrimitiveFrame!.Pending, batch);
            state = state with { Storage = written.Arena };
            work = new(state);
            work.WriteDword(0x24AA628, checked(NativeBattleTornado146State.WorkAddress + (uint)written.Cursor));
            state = work.Freeze();
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }

        private void Mesh(NativeBattleStaticMeshResource mesh, NativeBattleActionMatrix matrix, uint flags, uint? far = null, int? fade = null, byte depthShift = 2, ImmutableArray<byte> vertices = default)
        {
            Append(NativeBattleModulePackets.EmitBatch572200(mesh.Bytes, 0, vertices, matrix, Projection, new(flags, far, fade, null, null, null, 17, depthShift), Colors));
        }
    }

    private bool TryWriteNativeTornado146Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        if (graphics.OriginalEffect is not { Binding.Effect: 146 } effect)
        {
            return false;
        }
        uint delta = unchecked(address - 0x24936C8u);
        if (delta >= 32 || delta % 16 != 14)
        {
            return false;
        }
        NativeBattleEffectTaskPool pool = effect.Pools[0];
        int slot = (int)(delta / 16);
        if (pool.Slots[slot].Callback != 0x618480 || (pool.Slots[slot].Flags & 1) == 0)
        {
            throw new InvalidDataException("Native Tornado texture completion has no live returned wait task.");
        }
        pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteByte(14, value)) };
        updated = graphics with { OriginalEffect = effect with { Pools = effect.Pools.SetItem(0, pool) } };
        return true;
    }
}

