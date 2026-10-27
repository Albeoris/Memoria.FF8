using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRayBomb10Controller
{
    public static NativeBattleRayBomb10Frame Initialize627E60(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleRayBomb10Resources resources, NativeCrtRandomState random)
    {
        NativeBattleRayBomb10State retained = memory.RayBomb10 ?? throw new NotSupportedException("Native627E60 requires its retained process globals.");
        retained.Validate();
        if (effect.Binding.Entry != 0x627E60)
        {
            throw new InvalidDataException("Native Ray-Bomb requires its original627E60 entry.");
        }
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new NotSupportedException("Native627E60 requires its actual action parameters.");
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x24C91F8, 0x24C91D8, 0x10, 2);
        (root, int rootSlot) = root.Append(0x629730);
        root = root.WriteSlot(rootSlot, root.Slots[rootSlot].WriteWord(12, 0));
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x24C9988, 0x24C9998, 0x24, 100);
        (children, int director) = children.Append(0x627F10);
        children = children.WriteSlot(director, children.Slots[director].WriteWord(12, 0));
        byte[] particles = retained.Particles.ToArray();
        for (int index = 0; index < 80; index++)
        {
            particles.AsSpan(index * 24, 4).Clear();
        }
        retained = retained with { Particles = particles.ToImmutableArray(), Context = 0x1D99A78, Caster = parameters.Actor78, ArenaBase = checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)memory.Cursor) };
        int texture = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x24CA7F8)?.ArenaOffset ?? throw new NotSupportedException("Native627E60 requires its loaded24CA7F8 TIM pointer.");
        NativeBattleEffectTextureState textures = (memory.Textures ?? throw new NotSupportedException("Native627E60 requires its actual texture registration owner.")).Register5099A0(resources.Camera);
        (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native627E60 requires its loaded magic arena."), texture);
        memory = memory with { RayBomb10 = retained, Textures = textures };
        return new(memory, effect with { Pools = [root, children], ReturnedDescriptor = 0x24C91F8 }, random);
    }

    public static NativeBattleRayBomb10Frame Advance629730(NativeBattleRayBomb10Frame frame, NativeBattleRayBomb10Resources resources, NativeBattleRayBomb10Inputs inputs)
    {
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        Execution execution = new(frame, resources, inputs);
        NativeBattleEffectTaskStep root = frame.Effect.Pools[0].Advance((pool, index) =>
        {
            execution.SetPool(0, pool);
            NativeBattleModulePoolTask task = pool.Slots[index];
            int result;
            if (task.Callback == 0x629730)
            {
                execution.Process = execution.Process with { PacketCursor = unchecked(execution.Process.ArenaBase + ((task.ReadWord(12) & 1) != 0 ? 0x8000u : 0)) };
                NativeBattleEffectTaskStep children = execution.Frame.Effect.Pools[1].Advance((childPool, slot) =>
                {
                    execution.SetPool(1, childPool);
                    int childResult = execution.Advance(slot);
                    return new(execution.Frame.Effect.Pools[1], childResult);
                });
                execution.SetPool(1, children.Pool);
                int dust = execution.ParticlePass();
                task = execution.Frame.Effect.Pools[0].Slots[index];
                execution.Write(0, index, Add(task, 12, 1));
                result = (children.Survivors | dust) != 0 ? 0 : 2;
            }
            else if (task.Callback == 0x6296F0)
            {
                result = execution.Restore(index);
            }
            else
            {
                throw new NotSupportedException("Native Ray-Bomb root queue requires its original root or restore callback.");
            }
            return new(execution.Frame.Effect.Pools[0], result);
        });
        execution.SetPool(0, root.Pool);
        if (root.Survivors == 0)
        {
            execution.Frame = execution.Frame with { Effect = execution.Frame.Effect with { ReturnedDescriptor = 0 } };
        }
        return execution.Flush();
    }

    public static NativeBattleOriginalEffectState WriteCompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        if (effect.Binding.Entry != 0x627E60 || effect.Pools.Length != 2)
        {
            throw new NotSupportedException("Native Ray-Bomb completion requires its actual task banks.");
        }
        NativeBattleEffectTaskPool pool = effect.Pools[0];
        uint delta = unchecked(address - pool.BankAddress);
        if (delta >= pool.Slots.Length * pool.Stride || delta % pool.Stride != 14)
        {
            throw new NotSupportedException("Native Ray-Bomb completion does not address its restore task BYTE14.");
        }
        int slot = checked((int)(delta / pool.Stride));
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.Callback != 0x6296F0)
        {
            throw new NotSupportedException("Native Ray-Bomb completion lost its restore callback owner.");
        }
        task = task.WriteWord(14, (ushort)((task.ReadWord(14) & 0xFF00) | value));
        return effect with { Pools = effect.Pools.SetItem(0, pool.WriteSlot(slot, task)) };
    }

    private sealed partial class Execution(NativeBattleRayBomb10Frame frame, NativeBattleRayBomb10Resources resources, NativeBattleRayBomb10Inputs inputs)
    {
        public NativeBattleRayBomb10Frame Frame { get; set; } = frame;
        private readonly byte[] particles = (frame.Memory.RayBomb10 ?? throw new NotSupportedException("Native Ray-Bomb requires its retained particle globals.")).Particles.ToArray();
        private readonly byte?[] saved = frame.Memory.RayBomb10!.SavedActors.ToArray();
        public NativeBattleRayBomb10State Process
        {
            get => Frame.Memory.RayBomb10 ?? throw new NotSupportedException("Native Ray-Bomb lost its process globals.");
            set => Frame = Frame with { Memory = Frame.Memory with { RayBomb10 = value } };
        }
        private bool DrawOnly => (inputs.GraphicsFlags & 0x201) != 0;
        public void SetPool(int pool, NativeBattleEffectTaskPool state) => Frame = Frame with { Effect = Frame.Effect with { Pools = Frame.Effect.Pools.SetItem(pool, state) } };
        public void Write(int pool, int slot, NativeBattleModulePoolTask task) => SetPool(pool, Frame.Effect.Pools[pool].WriteSlot(slot, task));
        private NativeBattleModulePoolTask Read(int slot) => Frame.Effect.Pools[1].Slots[slot];
        private void Put(int slot, NativeBattleModulePoolTask task) => Write(1, slot, task);
        private void Execute(NativeBattleRayBomb10Operation operation) => Frame = inputs.Execute(Frame, operation);
        private int Rand()
        {
            NativeCrtRandomState random = Frame.Random.Draw(out ushort value);
            Frame = Frame with { Random = random };
            return value;
        }
        private int Append(uint callback, int pool = 1)
        {
            (NativeBattleEffectTaskPool next, int slot) = Frame.Effect.Pools[pool].Append(callback);
            SetPool(pool, next);
            return slot;
        }
        public NativeBattleRayBomb10Frame Flush()
        {
            Process = Process with { Particles = particles.ToImmutableArray(), SavedActors = saved.ToImmutableArray() };
            return Frame;
        }
        public int Restore(int index)
        {
            NativeBattleModulePoolTask task = Frame.Effect.Pools[0].Slots[index];
            if (Word(task, 12) == 0)
            {
                task = task.WriteWord(14, 0);
                Write(0, index, task);
                uint completion = Frame.Effect.Pools[0].BankAddress + (uint)(index * Frame.Effect.Pools[0].Stride) + 14;
                Execute(new NativeBattleRayBomb10Restore(unchecked(Process.ArenaBase + 0x10000), completion));
            }
            task = Frame.Effect.Pools[0].Slots[index];
            bool done = Word(task, 14) != 0;
            Write(0, index, Add(task, 12, 1));
            return done ? 2 : 0;
        }
        private int FreeParticle()
        {
            for (int index = 0; index < 80; index++)
            {
                if (P32(index, 0) == 0)
                {
                    return index;
                }
            }
            return -1;
        }
        private short P16(int index, int offset) => BinaryPrimitives.ReadInt16LittleEndian(particles.AsSpan(index * 24 + offset, 2));
        private uint P32(int index, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(particles.AsSpan(index * 24 + offset, 4));
        private void P16(int index, int offset, int value) => BinaryPrimitives.WriteInt16LittleEndian(particles.AsSpan(index * 24 + offset, 2), unchecked((short)value));
        private void P32(int index, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(particles.AsSpan(index * 24 + offset, 4), value);
        private NativeBattleModuleAnchor ParticlePoint(int index) => new(P16(index, 8), P16(index, 10), P16(index, 12), P16(index, 14));
        public int ParticlePass()
        {
            int alive = 0;
            for (int index = 0; index < 80; index++)
            {
                if ((P32(index, 0) & 1) == 0)
                {
                    continue;
                }
                short scale = P16(index, 6);
                NativeBattleModuleSpriteFrame image = Sprite(0xDEABB8, P16(index, 4), ParticlePoint(index), scale, -(scale >> 4));
                if (DrawOnly)
                {
                    continue;
                }
                P16(index, 4, P16(index, 4) + 1);
                if (image.NextOffset < 0)
                {
                    P32(index, 0, 0);
                    continue;
                }
                for (int lane = 0; lane < 3; lane++)
                {
                    P16(index, 8 + 2 * lane, P16(index, 8 + 2 * lane) + P16(index, 16 + 2 * lane));
                    short velocity = P16(index, 16 + 2 * lane);
                    P16(index, 16 + 2 * lane, velocity - (velocity >> 4));
                }
                alive++;
            }
            return alive;
        }
        private NativeBattleModuleSpriteFrame Sprite(uint resourceAddress, short phase, NativeBattleModuleAnchor point, int scale, int depth, ushort flags = 0, uint color = 0x808080, short rotation = 0, ImmutableArray<short> world = default)
        {
            NativeBattleModuleResource resource = resources.Sprites.Single(item => item.Address == resourceAddress);
            NativeBattleModuleSpriteFrame image = resource.ProjectedFrame571C80(phase);
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = image, Phase = unchecked((ushort)phase), Anchor = point, Scale = scale, DepthOffset = depth, SpriteFlags = flags, SpriteColor = color, Rotation = rotation, SpriteWorldRotation = world.IsDefault ? [] : world };
            Execute(new NativeBattleRayBomb10Sprite(draw));
            return image;
        }
        private NativeBattleActionMatrix Billboard(NativeBattleModuleAnchor point, short scale, int depth)
        {
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Anchor = point, Scale = scale, DepthOffset = depth, SpriteFlags = 0 };
            return NativeBattlePoseMatrices.ModuleSpriteCamera(draw, inputs.Projection.View ?? throw new NotSupportedException("Native Ray-Bomb requires its actual current view."), inputs.CameraRoll);
        }
        private static NativeBattleActionMatrix World(NativeBattleModulePoolTask task, NativeBattleVector angles, int x, int y, int z)
        {
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CD50(angles) with { X = Word(task, 16), Y = Word(task, 18), Z = Word(task, 20) };
            return NativeBattlePoseMatrices.Scale56BEF0(matrix, x, y, z);
        }
        private static NativeBattleModuleAnchor Position(NativeBattleModulePoolTask task) => new(Word(task, 16), Word(task, 18), Word(task, 20), Word(task, 22));
    }
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)(task.ReadWord(offset) + value)));
}
