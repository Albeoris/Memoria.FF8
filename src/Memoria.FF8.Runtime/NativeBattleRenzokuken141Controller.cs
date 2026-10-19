using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokukenVariantController
{
    public static NativeBattleRenzokuken160Frame Enter61DE70(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleRenzokuken160Resources resources, NativeCrtRandomState random)
    {
        NativeBattleRenzokukenVariantProfile profile = NativeBattleRenzokukenVariantProfile.Renzokuken141;
        if (effect.Binding.Effect != profile.Effect || effect.Binding.Entry != profile.Entry || effect.Binding.Initializer != profile.Initializer || effect.Parameters is not { } parameters || resources.ActorResource.MagicId != profile.Effect)
        {
            throw new InvalidDataException("Native61DE70 requires its original141 binding, action parameters and embedded actor resource.");
        }
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int contact = header.FirstContactIndex ?? throw new NotSupportedException("Native61DE70 requires its physical first-contact pointer.");
        byte target = packets.ReadContact(contact).Primary.ActorIndex;
        bool initial = NativeBattleActionGraphics.Word(header, 4) == 0xFFFE;
        ImmutableDictionary<ushort, NativeBattleRenzokukenVariantState> states = memory.RenzokukenVariants ?? ImmutableDictionary<ushort, NativeBattleRenzokukenVariantState>.Empty;
        NativeBattleRenzokukenVariantState retained = states.GetValueOrDefault(profile.Effect) ?? new(NativeBattleRenzokuken160State.FromColdProcess(), default, default, null);
        NativeBattleRenzokukenVariantState state = retained with { Core = retained.Core.Enter(parameters.Actor78, parameters.HeaderIndex7C, target, initial), RetainedResources = resources };
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(profile.MainDescriptor, profile.MainBank, 16, 1);
        (main, int mainSlot) = main.Append(profile.RootCallback);
        main = main with { Slots = main.Slots.SetItem(mainSlot, main.Slots[mainSlot].WriteWord(12, 0)) };
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(profile.ChildDescriptor, profile.ChildBank, 36, 100);
        (children, int childSlot) = children.Append(profile.ControllerCallback);
        children = children with { Slots = children.Slots.SetItem(childSlot, children.Slots[childSlot].WriteWord(12, 0)) };
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native61DE70 requires its actual texture owner.");
        textures = textures.Register5099A0(resources.Animation);
        NativeBattleEffectInitializerAsset tim = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native61DE70 requires its original initializer TIM asset.");
        (textures, _) = textures.EnqueueTim505E30(new(tim.Name, tim.Data));
        return new(memory with { RenzokukenVariants = states.SetItem(profile.Effect, state), Textures = textures }, effect with { Pools = [main, children], ReturnedDescriptor = profile.MainDescriptor, DrawArenaOffset = 0 }, random);
    }

    public static NativeBattleRenzokuken160Frame Advance61F710(NativeBattleRenzokuken160Frame frame, Func<NativeBattleRenzokuken160Frame, NativeBattleEffectTaskPool, int, NativeBattleRenzokuken160ChildStep> advanceChild, Func<NativeBattleRenzokuken160Frame, NativeBattleRenzokuken160Frame> drawParticles)
    {
        NativeBattleRenzokukenVariantProfile profile = NativeBattleRenzokukenVariantProfile.Renzokuken141;
        if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != profile.MainDescriptor || frame.Effect.Pools[1].DescriptorAddress != profile.ChildDescriptor)
        {
            throw new InvalidDataException("Native61F710 lost its original task descriptors.");
        }
        NativeBattleEffectTaskPool main = frame.Effect.Pools[0];
        if (main.Head is not int root)
        {
            return frame with { Effect = frame.Effect with { ReturnedDescriptor = 0 } };
        }
        ushort counter = main.Slots[root].ReadWord(12);
        frame = WriteState(frame, ReadState(frame).Core with { DrawParity = counter & 1 });
        frame = frame with { Effect = frame.Effect with { DrawArenaOffset = (counter & 1) * 0x8000 } };
        NativeBattleEffectTaskStep children = frame.Effect.Pools[1].Advance((pool, index) =>
        {
            NativeBattleRenzokuken160ChildStep step = advanceChild(frame, pool, index);
            frame = step.Frame;
            return new(step.Pool, step.Result);
        });
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, children.Pool) } };
        frame = drawParticles(frame);
        NativeBattleEffectTaskStep advanced = main.Advance((pool, index) => new(pool with { Slots = pool.Slots.SetItem(index, pool.Slots[index].WriteWord(12, unchecked((ushort)(counter + 1)))) }, children.Survivors == 0 ? 2 : 0));
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, advanced.Pool), ReturnedDescriptor = advanced.Survivors == 0 ? 0u : profile.MainDescriptor } };
    }

    public static NativeBattleRenzokuken160ChildStep Advance61DF70(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        NativeBattleRenzokukenVariantProfile profile = NativeBattleRenzokukenVariantProfile.Renzokuken141;
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = unchecked((short)task.ReadWord(12));
        NativeBattleRenzokuken160State state = ReadState(frame).Core;
        if (counter == 0)
        {
            ushort flags = inputs.ReadModel(state.Actor).Flags0;
            task = task.WriteWord(14, (ushort)(flags & 0x800));
            Execute(new NativeBattleRenzokuken160ActorFlags((ushort)(flags | 0x800)));
        }
        if (counter == 1)
        {
            Execute(new NativeBattleRenzokuken160ActorResource(resources.ActorResource, state.InitialControl ? (byte)2 : (byte)1));
        }
        if (counter == 2)
        {
            if (inputs.AbSegments.IsDefaultOrEmpty)
            {
                throw new NotSupportedException("Native61DF70 requires the current unsigned AB byte bank.");
            }
            state = ReadState(frame).Core with { Duration = inputs.AbSegments.Sum(value => (int)value), AbSegments = inputs.AbSegments };
            Append(profile.TrailCallback, child => child.WriteWord(12, 0));
            byte[] history = state.TrailHistory.ToArray();
            for (int offset = 0; offset < history.Length; offset += 20)
            {
                BinaryPrimitives.WriteInt32LittleEndian(history.AsSpan(offset), 0);
            }
            frame = WriteState(frame, state with { TrailHistory = history.ToImmutableArray() });
            frame = frame with { Random = frame.Random.Draw(out ushort color) };
            Append(profile.SpiralCallback, child => child.WriteWord(12, 0).WriteWord(14, (ushort)(color % 24)));
            Append(profile.ContactCallback, child => child.WriteWord(12, 0));
        }
        state = ReadState(frame).Core;
        if (state.InitialControl && counter == state.Duration)
        {
            Execute(new NativeBattleRenzokuken160SharedTask(profile.SharedEmitterCallback, 0, state.Actor));
        }
        if (counter == 51)
        {
            (pool, int lateSlot) = pool.Append(0x61F590);
            frame = frame with { Random = frame.Random.Draw(out ushort scale) };
            short speed = unchecked((short)(scale % 2560 + 7168));
            pool = pool with { Slots = pool.Slots.SetItem(lateSlot, pool.Slots[lateSlot].WriteWord(12, 0).WriteWord(28, unchecked((ushort)(speed / 3))).WriteWord(30, unchecked((ushort)(speed / 3)))) };
            frame = Store(frame, ReadState(frame) with { LateOrigin = state.Origin, LateDirection = state.Direction });
        }
        if (counter <= 16)
        {
            Execute(new NativeBattleRenzokuken160Stage(counter << 7));
        }
        else if (counter >= state.Duration - 8 && !state.InitialControl)
        {
            Execute(new NativeBattleRenzokuken160Stage((state.Duration - counter) << 8));
        }
        if (counter <= 8 || counter >= state.Duration - 8)
        {
            int phase = counter <= 8 ? counter : state.Duration - counter;
            Execute(new NativeBattleRenzokuken160ActorColor(NativeBattleGteColorInterpolation.CodeConstants45F270.Interpolate(resources.DefaultModelColor, phase << 9, 0x4060F0)));
        }
        if (counter == 1)
        {
            Execute(new NativeBattleRenzokuken160Sound(resources.AppearanceSound, 0x8000, 128));
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        int result = unchecked((short)task.ReadWord(12)) <= state.Duration ? 0 : 2;
        if (result == 2)
        {
            if (!state.InitialControl)
            {
                Execute(new NativeBattleRenzokuken160Stage(0));
            }
            Execute(new NativeBattleRenzokuken160ActorColor(resources.DefaultModelColor));
            ushort flags = inputs.ReadModel(state.Actor).Flags0;
            Execute(new NativeBattleRenzokuken160ActorFlags((ushort)(task.ReadWord(14) | (flags & 0xF7FF))));
        }
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot] with { Payload = task.Payload }) }, result);

        void Execute(NativeBattleRenzokuken160Operation operation)
        {
            frame = inputs.Execute(frame, operation);
        }
        void Append(uint callback, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> initialize)
        {
            (pool, int child) = pool.Append(callback);
            pool = pool with { Slots = pool.Slots.SetItem(child, initialize(pool.Slots[child])) };
        }
    }
}
