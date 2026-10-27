using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeUltima149(NativeBattleGraphicsState graphics) => new NativeUltima149Runner(this, graphics).Initialize();
    private NativeBattleGraphicsState AdvanceNativeUltima149(NativeBattleGraphicsState graphics) => new NativeUltima149Runner(this, graphics).Advance();

    private sealed partial class NativeUltima149Runner
    {
        private readonly BattleSession owner;
        private NativeBattleGraphicsState graphics;
        private NativeBattleOriginalEffectState effect;
        private readonly NativeBattleUltima149Resources resources;
        private NativeBattleUltima149State state;
        private NativeBattleEffectTaskPool[] pools;
        private NativeCrtRandomState random;
        private NativeBattleEffectMemory Memory
        {
            get => owner.NativeEffectMemory ?? throw new NotSupportedException("Native Ultima requires its retained effect memory.");
            set => owner.NativeEffectMemory = value;
        }
        private NativeBattleCameraProjection Projection => owner.NativeCamera?.Projection ?? throw new NotSupportedException("Native Ultima requires its current projection.");
        private NativeBattleActionMatrix View => Projection.View ?? throw new NotSupportedException("Native Ultima requires its current view matrix.");
        private NativeBattleSummonColors Colors => owner.definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Native Ultima requires its current GTE color rules.");
        private bool DrawOnly => (graphics.Flags & 0x201) != 0;

        public NativeUltima149Runner(BattleSession owner, NativeBattleGraphicsState graphics)
        {
            this.owner = owner;
            this.graphics = graphics;
            effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Ultima requires its original action owner.");
            resources = effect.Binding.Ultima149Resources ?? throw new NotSupportedException("Native Ultima requires its complete original resources.");
            state = Memory.Ultima149 ?? throw new NotSupportedException("Native Ultima requires its retained module globals.");
            state.Validate();
            pools = effect.Pools.ToArray();
            random = owner.nativeCrtRandom ?? throw new NotSupportedException("Native Ultima requires its shared CRT stream.");
        }

        public NativeBattleGraphicsState Initialize()
        {
            NativeBattleEffectTextureState textures = (Memory.Textures ?? throw new NotSupportedException("Native Ultima requires its current texture queue.")).Register5099A0(resources.Camera);
            owner.NativeCamera = owner.NativeCamera!.Apply50A730().RegisterControlResource(textures.Animation);
            NativeBattleEffectInitializerAsset asset = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Ultima requires its original TIM allocation.");
            (textures, _) = textures.EnqueueTim505E30(new(asset.Name, asset.Data));
            Memory = Memory with { Textures = textures };
            NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(0x2461EB0, 0x2461E90, 16, 2);
            (roots, int rootSlot) = roots.Append(0x612000);
            roots = roots.WriteSlot(rootSlot, roots.Slots[rootSlot].WriteWord(12, 0));
            NativeBattleEffectTaskPool tasks = NativeBattleEffectTaskPool.Initialize(0x2463180, 0x2463190, 36, 100);
            (tasks, int director) = tasks.Append(0x60F770);
            tasks = tasks.WriteSlot(director, tasks.Slots[director].WriteWord(12, 0).WriteWord(14, 0));
            pools = [roots, tasks];
            state = state.Initialize();
            effect = effect with { ReturnedDescriptor = 0x2461EB0, DrawArenaOffset = null };
            return Publish();
        }

        public NativeBattleGraphicsState Advance()
        {
            if (pools.Length != 2 || pools[0].DescriptorAddress != 0x2461EB0 || pools[1].DescriptorAddress != 0x2463180)
            {
                throw new InvalidDataException("Native Ultima lost its original root/effect banks.");
            }
            NativeBattleEffectTaskStep roots = pools[0].Advance((pool, slot) =>
            {
                pools[0] = pool;
                NativeBattleModulePoolTask task = pool.Slots[slot];
                int result;
                if (task.Callback == 0x612000)
                {
                    effect = effect with { DrawArenaOffset = (Word(task, 12) & 1) != 0 ? 0xC000 : 0 };
                    NativeBattleEffectTaskStep children = pools[1].Advance((current, childSlot) =>
                    {
                        pools[1] = current;
                        NativeBattleModulePoolTask child = current.Slots[childSlot];
                        int code = Step(ref child);
                        child = child.UpdateHeader(child.Flags, child.State, pools[1].Slots[childSlot].Next, child.Callback);
                        Put(1, childSlot, child);
                        return new(pools[1], code);
                    });
                    pools[1] = children.Pool;
                    if (children.Survivors == 0)
                    {
                        int wait = Allocate(0, 0x612070);
                        Put(0, wait, pools[0].Slots[wait].WriteWord(12, 0).WriteWord(14, 0));
                    }
                    task = task.WriteWord(12, unchecked((ushort)(Word(task, 12) + 1)));
                    result = children.Survivors == 0 ? 2 : 0;
                }
                else if (task.Callback == 0x612070)
                {
                    if (Word(task, 12) == 1)
                    {
                        uint completion = checked(pools[0].BankAddress + (uint)slot * 16 + 14);
                        (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(NativeBattleEffectNodeMemory.ArenaAddress, completion, (address, value) =>
                        {
                            int targetSlot = checked((int)((address - pools[0].BankAddress) / 16));
                            Put(0, targetSlot, pools[0].Slots[targetSlot].WriteByte(14, value));
                        });
                        graphics = graphics with { Auxiliary = auxiliary };
                    }
                    result = Word(task, 14) != 0 ? 2 : 0;
                    task = task.WriteWord(12, unchecked((ushort)(Word(task, 12) + 1)));
                }
                else
                {
                    throw new NotSupportedException("Native Ultima root bank selected an unrelated callback.");
                }
                task = task.UpdateHeader(task.Flags, task.State, pools[0].Slots[slot].Next, task.Callback);
                Put(0, slot, task);
                return new(pools[0], result);
            });
            pools[0] = roots.Pool;
            effect = effect with { ReturnedDescriptor = roots.Survivors == 0 ? 0u : 0x2461EB0u };
            return Publish();
        }

        private int Step(ref NativeBattleModulePoolTask task)
        {
            return task.Callback switch
            {
                0x60F770 => Director(ref task),
                0x60FC90 => Orbit(ref task),
                0x60FF20 => Streaks(ref task),
                0x610310 => Shards(ref task),
                0x610700 => Energy(ref task),
                0x610840 => Ring(ref task),
                0x6113A0 => Core(ref task),
                0x6115D0 => Dust(ref task),
                0x6118A0 => Sparks(ref task),
                0x611C20 => SideFade(ref task, true),
                0x611E10 => SideFade(ref task, false),
                _ => throw new NotSupportedException("Native Ultima effect bank selected an unrelated callback.")
            };
        }

        private NativeBattleGraphicsState Publish()
        {
            Memory = Memory with { Ultima149 = state };
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
        private void Put(int bank, int slot, NativeBattleModulePoolTask task) => pools[bank] = pools[bank].WriteSlot(slot, task);
        private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
        private static NativeBattleVector Position(NativeBattleModulePoolTask task) => new(Word(task, 16), Word(task, 18), Word(task, 20));
        private void Append(NativeBattleGfGeometryBatch batch)
        {
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(Memory, effect.DrawArenaOffset ?? throw new NotSupportedException("Native Ultima requires its selected packet arena."), graphics.PrimitiveFrame!.Pending, batch);
            Memory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
        private void Mesh(NativeBattleStaticMeshResource mesh, NativeBattleActionMatrix matrix, uint flags, uint? far = null, int? fade = null, byte depthShift = 2, ImmutableArray<byte> vertices = default)
        {
            Append(NativeBattleModulePackets.EmitBatch572200(mesh.Bytes, 0, vertices, matrix, Projection, new(flags, far, fade, null, null, null, 17, depthShift), Colors));
        }
    }

    private bool TryWriteNativeUltima149Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        if (graphics.OriginalEffect is not { Binding.Effect: 149 } effect)
        {
            return false;
        }
        uint delta = unchecked(address - 0x2461E90u);
        if (delta >= 32 || delta % 16 != 14)
        {
            return false;
        }
        NativeBattleEffectTaskPool pool = effect.Pools[0];
        int slot = (int)(delta / 16);
        if (pool.Slots[slot].Callback != 0x612070 || (pool.Slots[slot].Flags & 1) == 0)
        {
            throw new InvalidDataException("Native Ultima texture completion has no live returned wait task.");
        }
        pool = pool.WriteSlot(slot, pool.Slots[slot].WriteByte(14, value));
        updated = graphics with { OriginalEffect = effect with { Pools = effect.Pools.SetItem(0, pool) } };
        return true;
    }
}
