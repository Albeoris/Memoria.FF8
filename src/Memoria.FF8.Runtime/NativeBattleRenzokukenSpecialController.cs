using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokukenSpecialController
{
    public static NativeBattleRenzokuken160Frame Enter(NativeBattleRenzokukenSpecialProfile frameProfile, NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleRenzokuken160Resources resources, NativeCrtRandomState random)
    {
        NativeBattleRenzokukenSpecialProfile profile = frameProfile;
        if (effect.Binding.Effect != profile.Effect || effect.Binding.Entry != profile.Entry || effect.Binding.Initializer != profile.Initializer || effect.Parameters is not { } parameters || resources.ActorResource.MagicId != profile.Effect)
        {
            throw new InvalidDataException("Native special Renzokuken requires its original binding, action parameters and embedded actor resource.");
        }
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int contact = header.FirstContactIndex ?? throw new NotSupportedException("Native special Renzokuken entry requires its physical first-contact pointer.");
        byte target = packets.ReadContact(contact).Primary.ActorIndex;
        bool initial = NativeBattleActionGraphics.Word(header, 4) == 0xFFFE;
        ImmutableDictionary<ushort, NativeBattleRenzokukenSpecialState> states = memory.RenzokukenSpecials ?? ImmutableDictionary<ushort, NativeBattleRenzokukenSpecialState>.Empty;
        NativeBattleRenzokukenSpecialState retained = states.GetValueOrDefault(profile.Effect) ?? new(NativeBattleRenzokuken160State.FromColdProcess(), resources);
        NativeBattleRenzokukenSpecialState state = retained with { Core = retained.Core.Enter(parameters.Actor78, parameters.HeaderIndex7C, target, initial), Resources = resources };
        if (profile.Effect is 334 or 337 or 339 or 341 or 343 && state.ContactOffsets.IsEmpty)
        {
            state = state with { ContactOffsets = new NativeBattleVector[resources.ContactLeadFrames.Length].ToImmutableArray() };
        }
        if (profile.Effect == 161)
        {
            state = state with { TargetParticles = ResetMarkers(retained.TargetParticles.IsEmpty ? new byte[800].ToImmutableArray() : retained.TargetParticles, 20), BurstParticles = ResetMarkers(retained.BurstParticles.IsEmpty ? new byte[600].ToImmutableArray() : retained.BurstParticles, 20) };
        }
        if (profile.Effect != 161)
        {
            byte[] extra = retained.ExtraStorage.IsEmpty ? new byte[3200] : retained.ExtraStorage.ToArray();
            if (extra.Length != 3200)
            {
                throw new InvalidDataException("Native special Renzokuken extra pool lost its100 physical32-byte rows.");
            }
            for (int offset = 0; offset < extra.Length; offset += 32)
            {
                BinaryPrimitives.WriteInt32LittleEndian(extra.AsSpan(offset), 0);
            }
            state = state with { ExtraStorage = extra.ToImmutableArray() };
        }
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(profile.MainDescriptor, profile.MainBank, 16, 1);
        (main, int mainSlot) = main.Append(profile.RootCallback);
        main = main.WriteSlot(mainSlot, main.Slots[mainSlot].WriteWord(12, 0));
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(profile.ChildDescriptor, profile.ChildBank, 36, 100);
        (children, int childSlot) = children.Append(profile.ControllerCallback);
        children = children.WriteSlot(childSlot, children.Slots[childSlot].WriteWord(12, 0));
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native special Renzokuken entry requires its actual texture owner.");
        textures = textures.Register5099A0(resources.Animation);
        NativeBattleEffectInitializerAsset tim = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native special Renzokuken entry requires its original initializer TIM asset.");
        (textures, _) = textures.EnqueueTim505E30(new(tim.Name, tim.Data));
        return new(memory with { RenzokukenSpecials = states.SetItem(profile.Effect, state), Textures = textures }, effect with { Pools = [main, children], ReturnedDescriptor = profile.MainDescriptor, DrawArenaOffset = 0 }, random);
    }

    public static NativeBattleRenzokuken160Frame AdvanceRoot(NativeBattleRenzokuken160Frame frame, NativeBattleRenzokukenSpecialProfile profile, Func<NativeBattleRenzokuken160Frame, NativeBattleEffectTaskPool, int, NativeBattleRenzokuken160ChildStep> advanceChild, Func<NativeBattleRenzokuken160Frame, NativeBattleRenzokuken160Frame> drawParticles)
    {
        if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != profile.MainDescriptor || frame.Effect.Pools[1].DescriptorAddress != profile.ChildDescriptor)
        {
            throw new InvalidDataException("Native special Renzokuken root lost its original task descriptors.");
        }
        NativeBattleEffectTaskPool main = frame.Effect.Pools[0];
        if (main.Head is not int root)
        {
            return frame with { Effect = frame.Effect with { ReturnedDescriptor = 0 } };
        }
        ushort counter = main.Slots[root].ReadWord(12);
        frame = WriteState(frame, ReadState(frame).Core with { DrawParity = counter & 1 });
        frame = frame with { Effect = frame.Effect with { DrawArenaOffset = (counter & 1) * profile.PacketStride } };
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

    private static ImmutableArray<byte> ResetMarkers(ImmutableArray<byte> bank, int stride)
    {
        byte[] bytes = bank.ToArray();
        for (int offset = 0; offset < bytes.Length; offset += stride)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), -1);
        }
        return bytes.ToImmutableArray();
    }
}
