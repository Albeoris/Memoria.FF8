using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect303Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect303Controller
{
    public static NativeBattleEffect303Frame Initialize(NativeBattleEffect303Frame frame, NativeBattleEffect303Resources resources, NativeBattleEffect303Inputs inputs)
    {
        RequireBinding(frame);
        frame.State.Validate();
        resources.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.RootBankStorage is null)
        {
            throw new InvalidDataException("Effect303 requires its exclusive inactive root bank.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect303 requires its actual COMMON arena.");
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect303 requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Effect303 requires its retained texture process.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        int header = Parameters(frame).HeaderIndex7C;
        if ((uint)header >= packets.Headers.Length)
        {
            throw new InvalidDataException("Effect303 requires its actual physical action header.");
        }
        int first = packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Effect303 requires its actual first contact pointer.");
        uint target = ModelAddress(packets.ReadContact(first).Primary.ActorIndex);
        uint caster = ModelAddress(Parameters(frame).Actor78);
        frame = frame with { Memory = frame.Memory with { Textures = textures }, State = frame.State.WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, caster).WriteDword(TargetGlobal, target).WriteDword(ArenaGlobal, ArenaAddress) };
        frame = inputs.Execute(frame, new NativeBattleEffect303CopyPackedPosition(caster, target));
        frame = inputs.Execute(frame, new NativeBattleEffect303SelectSequence505C00(target, 13));
        frame = inputs.Execute(frame, new NativeBattleEffect303EnterSequence508F90(target));
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, _) = root.Append(RootCallback);
        frame = frame with { State = frame.State with { RootBankStorage = null }, Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
        frame = ClearDwords701200(frame, RootBank + 12, 2);
        return frame with { Memory = frame.Memory with { Textures = (frame.Memory.Textures ?? throw new NotSupportedException("Effect303 lost its current texture process.")).Register5099A0(resources.Camera) } };
    }

    public static NativeBattleEffect303Frame Advance(NativeBattleEffect303Frame frame, NativeBattleEffect303Resources resources, NativeBattleEffect303Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 5) || frame.State.RootBankStorage is not null)
        {
            throw new InvalidDataException("Effect303 requires its exclusive active task banks.");
        }
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleCureFamilyPool expected = Pools[index];
            NativeBattleEffectTaskPool actual = frame.Effect.Pools[index];
            if (actual.DescriptorAddress != expected.Descriptor || actual.BankAddress != expected.Bank || actual.Stride != expected.Stride || actual.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Effect303 task pool leaves its physical descriptor contract.");
            }
        }
        (frame, int survivors) = AdvancePool(frame, 0, resources, inputs);
        frame = frame with { Effect = frame.Effect with { ReturnedDescriptor = survivors == 0 ? 0u : RootDescriptor } };
        return survivors == 0 ? DetachBanks(frame) : frame;
    }

    private static (NativeBattleEffect303Frame Frame, int Survivors) AdvancePool(NativeBattleEffect303Frame frame, int index, NativeBattleEffect303Resources resources, NativeBattleEffect303Inputs inputs)
    {
        NativeBattleEffect303Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            current = WithPool(current, index, pool);
            NativeBattlePilotVisualIdentity? previousIdentity = current.PilotIdentity;
            if (current.CapturePresentation)
            {
                current = current with { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(303, pool.Slots[slot]) };
            }
            (current, int result) = AdvanceTask(current, pool.BankAddress + (uint)(slot * pool.Stride), resources, inputs);
            current = current with { PilotIdentity = previousIdentity };
            return new(current.Effect.Pools[index], result);
        });
        return (WithPool(current, index, step.Pool), step.Survivors);
    }

    public static (NativeBattleEffect303Frame Frame, int Result) AdvanceTask(NativeBattleEffect303Frame frame, uint address, NativeBattleEffect303Resources resources, NativeBattleEffect303Inputs inputs) => Read(frame, address).Callback switch
    {
        RootCallback => AdvanceRoot(frame, address, resources, inputs),
        MainCallback => AdvanceMain(frame, address, resources, inputs),
        0x66C220 => AdvanceProgramme66C220(frame, address, resources, inputs),
        0x66C100 => AdvanceSprite66C100(frame, address, resources, inputs),
        0x66C080 => AdvanceSprite66C080(frame, address, resources, inputs),
        _ => throw new NotSupportedException("Effect303 reached an unsupported native task callback.")
    };

    public static NativeBattleEffect303Frame ClearDwords701200(NativeBattleEffect303Frame frame, uint address, int count) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, new byte[checked(count * 4)]), checked(count * 4));

    private static NativeBattleEffect303Frame InitializeSecondaryBanks(NativeBattleEffect303Frame frame)
    {
        if (frame.Effect.Pools.Length != 1)
        {
            throw new InvalidDataException("Effect303 secondary reset requires its inactive COMMON-backed banks.");
        }
        NativeBattleEffectTaskPool[] reset = new NativeBattleEffectTaskPool[4];
        foreach (int index in new[] { 1, 2, 3, 4 })
        {
            NativeBattleCureFamilyPool profile = Pools[index];
            reset[index - 1] = NativeBattleEffectTaskPool.Initialize(profile.Descriptor, profile.Bank, profile.Stride, profile.Capacity);
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect303 reset requires its current COMMON owner.");
        return frame with { Memory = frame.Memory with { Arena = arena.MarkUnknown(0, IoOffset) }, Effect = frame.Effect with { Pools = frame.Effect.Pools.AddRange(reset) } };
    }
}
