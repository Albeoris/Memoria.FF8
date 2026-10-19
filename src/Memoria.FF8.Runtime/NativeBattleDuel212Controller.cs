using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel212PrepareScene(int Slot) : NativeBattleDuel212Operation;
public sealed record NativeBattleDuel212UpdateScene : NativeBattleDuel212Operation;
public sealed record NativeBattleDuel212RestoreParty : NativeBattleDuel212Operation;
public sealed record NativeBattleDuel212CloseScene : NativeBattleDuel212Operation;

public static partial class NativeBattleDuel212Controller
{
    public static NativeBattleDuel212Frame Enter6A0D20(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleFileStaging staging, NativeBattleDuel212Resources resources, NativeCrtRandomState random)
    {
        if (effect.Binding is not { Effect: 212, Entry: 0x6A0D20, Initializer: 0x6A0D30 } || effect.Parameters is not { } parameters)
        {
            throw new InvalidDataException("Native Burning Rave requires its original entered binding.");
        }
        resources.Validate();
        NativeBattleDuel212State state = memory.Duel212 ?? throw new NotSupportedException("Native Burning Rave requires retained loader state; migrate the selected standalone explicitly.");
        if (!state.GlobalsInitialized)
        {
            staging = staging.Write(0x2510258, resources.ColdGlobals.AsSpan());
        }
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(Root, 0x2510248, 16, 1);
        (root, int slot) = root.Append(0x6A0DC0);
        root = root with { Slots = root.Slots.SetItem(slot, root.Slots[slot].WriteDword(12, 0)) };
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Burning Rave requires its original texture owner.");
        (textures, _) = textures.EnqueueTim505E30(resources.InitialTim);
        staging = Put(staging, 0x2510278, 0x20DFAB8);
        staging = Put(staging, 0x25101FC, 0x1D99A78);
        staging = Put(staging, 0x2510200, checked(0x1D972C0u + 156u * parameters.Actor78));
        state = new(true, parameters, resources);
        return new(memory with { Duel212 = state, Textures = textures }, effect with { Pools = [root], ReturnedDescriptor = Root }, staging, random);
    }

    public static NativeBattleDuel212Frame AdvanceRoot(NativeBattleDuel212Frame frame, NativeBattleDuel212Inputs inputs, Func<NativeBattleDuel212Frame, NativeBattleEffectTaskPool, int, NativeBattleDuel212Step> advanceChild, Func<bool> isReloadComplete)
    {
        NativeBattleEffectTaskPool root = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == Root);
        NativeBattleEffectTaskPool? target = frame.Effect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == Targets);
        NativeBattleEffectTaskPool? fragments = frame.Effect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == Fragments);
        NativeBattleEffectTaskPool? sprites = frame.Effect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == Sprites);
        NativeBattleEffectTaskStep step = root.Advance((current, slot) =>
        {
            NativeBattleModulePoolTask task = current.Slots[slot];
            if (task.Callback != 0x6A0DC0)
            {
                throw new NotSupportedException("Native Burning Rave requires its original root callback.");
            }
            bool second = task.ReadByte(15) != 0;
            int cursor = second ? 43744 : 109280;
            int end = second ? 109280 : 174816;
            frame = frame with { Staging = Put(Put(frame.Staging, 0x2510258, checked(0x20DFAB8u + (uint)cursor)), 0x251025C, checked(0x20DFAB8u + (uint)end)), Effect = frame.Effect with { DrawArenaOffset = cursor } };
            task = task.WriteByte(15, second ? (byte)0 : (byte)1);
            if (task.ReadWord(12) == 1)
            {
                if (task.ReadByte(14) == 0)
                {
                    task = task.WriteByte(14, 1);
                    target = NativeBattleEffectTaskPool.Initialize(Targets, 0x20DFAB8, 3280, 1);
                    fragments = NativeBattleEffectTaskPool.Initialize(Fragments, 0x20DFAB8 + 3280, 36, 64);
                    sprites = NativeBattleEffectTaskPool.Initialize(Sprites, 0x20DFAB8 + 5584, 24, 64);
                }
                if (target is null)
                {
                    throw new InvalidDataException("Native Burning Rave target pool was lost after initialization.");
                }
                (target, int child) = target.Append(0x6A1660);
                Publish(current with { Slots = current.Slots.SetItem(slot, task) });
                frame = inputs.Execute(frame, new NativeBattleDuel212PrepareScene(child));
                Refresh();
            }
            int targetsAlive = 1;
            int spritesAlive = 1;
            if (task.ReadByte(14) != 0)
            {
                target = Advance(target!, out targetsAlive);
                fragments = Advance(fragments!, out _);
                sprites = Advance(sprites!, out spritesAlive);
            }
            int result = 0;
            if (task.ReadByte(14) == 0 || targetsAlive != 0)
            {
                task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
            }
            else
            {
                Publish(current with { Slots = current.Slots.SetItem(slot, task) });
                frame = inputs.Execute(frame, new NativeBattleDuel212UpdateScene());
                Refresh();
                if (spritesAlive == 0 && isReloadComplete())
                {
                    if (task.ReadByte(14) == 1)
                    {
                        task = task.WriteByte(14, 2);
                        frame = inputs.Execute(frame, new NativeBattleDuel212CloseScene());
                        Refresh();
                    }
                    else if (task.ReadByte(14) == 2)
                    {
                        frame = inputs.Execute(frame, new NativeBattleDuel212RestoreParty());
                        Refresh();
                        result = 2;
                    }
                }
            }
            return new(current with { Slots = current.Slots.SetItem(slot, task) }, result);

            NativeBattleEffectTaskPool Advance(NativeBattleEffectTaskPool pool, out int survivors)
            {
                NativeBattleEffectTaskStep advanced = pool.Advance((active, childSlot) =>
                {
                    if (pool.DescriptorAddress == Targets)
                    {
                        target = active;
                    }
                    else if (pool.DescriptorAddress == Fragments)
                    {
                        fragments = active;
                    }
                    else
                    {
                        sprites = active;
                    }
                    Publish(current with { Slots = current.Slots.SetItem(slot, task) });
                    NativeBattleDuel212Step child = advanceChild(frame, active, childSlot);
                    frame = child.Frame;
                    Refresh();
                    return new(child.Pool, child.Result);
                });
                survivors = advanced.Survivors;
                return advanced.Pool;
            }
        });
        root = step.Pool;
        Publish(root);
        return frame with { Effect = frame.Effect with { ReturnedDescriptor = step.Survivors == 0 ? 0u : Root } };

        void Publish(NativeBattleEffectTaskPool activeRoot)
        {
            frame = frame with { Effect = frame.Effect with { Pools = new[] { activeRoot, target, fragments, sprites }.Where(pool => pool is not null).Select(pool => pool!).ToImmutableArray() } };
        }
        void Refresh()
        {
            target = frame.Effect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == Targets);
            fragments = frame.Effect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == Fragments);
            sprites = frame.Effect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == Sprites);
        }
    }

    private static NativeBattleFileStaging Put(NativeBattleFileStaging staging, uint address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return staging.Write(address, bytes);
    }
}
