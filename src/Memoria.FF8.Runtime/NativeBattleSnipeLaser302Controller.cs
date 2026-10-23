using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleSnipeLaser302Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSnipeLaser302Controller
{
    public static NativeBattleSnipeLaser302Frame Initialize(NativeBattleSnipeLaser302Frame frame, NativeBattleSnipeLaser302Resources resources, NativeBattleSnipeLaser302Inputs inputs)
    {
        RequireBinding(frame);
        frame.State.Validate();
        resources.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.RootBankStorage is null)
        {
            throw new InvalidDataException("Snipe Laser activation requires its inactive physical root owner.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Snipe Laser requires its actual COMMON arena.");
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Snipe Laser requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Snipe Laser requires its retained texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        textures = textures.Register5099A0(resources.Camera);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, _) = root.Append(RootCallback);
        frame = frame with { Memory = frame.Memory with { Textures = textures }, State = frame.State.WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(Parameters(frame).Actor78)).WriteDword(ArenaGlobal, ArenaAddress) with { RootBankStorage = null }, Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
        return ClearDwords701200(frame, RootBank + 12, 2);
    }

    public static NativeBattleSnipeLaser302Frame Advance(NativeBattleSnipeLaser302Frame frame, NativeBattleSnipeLaser302Resources resources, NativeBattleSnipeLaser302Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 5) || frame.State.RootBankStorage is not null)
        {
            throw new InvalidDataException("Snipe Laser requires its exclusive active module bank owners.");
        }
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleCureFamilyPool expected = Pools[index];
            NativeBattleEffectTaskPool actual = frame.Effect.Pools[index];
            if (actual.DescriptorAddress != expected.Descriptor || actual.BankAddress != expected.Bank || actual.Stride != expected.Stride || actual.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Snipe Laser module pool leaves its physical descriptor contract.");
            }
        }
        (frame, int survivors) = AdvancePool(frame, 0, resources, inputs);
        frame = frame with { Effect = frame.Effect with { ReturnedDescriptor = survivors == 0 ? 0u : RootDescriptor } };
        return survivors == 0 ? DetachBanks(frame) : frame;
    }

    private static (NativeBattleSnipeLaser302Frame Frame, int Survivors) AdvancePool(NativeBattleSnipeLaser302Frame frame, int index, NativeBattleSnipeLaser302Resources resources, NativeBattleSnipeLaser302Inputs inputs)
    {
        NativeBattleSnipeLaser302Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            current = WithPool(current, index, pool);
            (current, int result) = AdvanceTask(current, pool.BankAddress + (uint)(slot * pool.Stride), resources, inputs);
            return new(current.Effect.Pools[index], result);
        });
        return (WithPool(current, index, step.Pool), step.Survivors);
    }

    public static (NativeBattleSnipeLaser302Frame Frame, int Result) AdvanceTask(NativeBattleSnipeLaser302Frame frame, uint address, NativeBattleSnipeLaser302Resources resources, NativeBattleSnipeLaser302Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            RootCallback => AdvanceRoot(frame, address, resources, inputs),
            0x66CD40 => AdvanceMain(frame, address, resources, inputs),
            0x66D670 => AdvanceProgramme66D670(frame, address, resources, inputs),
            0x66D6D0 => AdvanceParticle66D6D0(frame, address, resources, inputs),
            0x66D790 => AdvanceSprite66D790(frame, address, resources, inputs),
            _ => throw new NotSupportedException("Snipe Laser reached an unsupported module callback.")
        };
    }

    public static NativeBattleSnipeLaser302Frame ClearDwords701200(NativeBattleSnipeLaser302Frame frame, uint address, int count) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, new byte[checked(count * 4)]), checked(count * 4));

    private static NativeBattleSnipeLaser302Frame InitializeSecondaryBanks(NativeBattleSnipeLaser302Frame frame)
    {
        if (frame.Effect.Pools.Length != 1)
        {
            throw new InvalidDataException("Snipe Laser secondary reset requires its inactive COMMON-backed banks.");
        }
        ImmutableArray<NativeBattleEffectTaskPool>.Builder pools = frame.Effect.Pools.ToBuilder();
        NativeBattleEffectTaskPool[] reset = new NativeBattleEffectTaskPool[4];
        for (int index = 4; index > 0; index--)
        {
            NativeBattleCureFamilyPool profile = Pools[index];
            reset[index - 1] = NativeBattleEffectTaskPool.Initialize(profile.Descriptor, profile.Bank, profile.Stride, profile.Capacity);
        }
        pools.AddRange(reset);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Snipe Laser reset requires its actual COMMON owner.");
        return frame with { Memory = frame.Memory with { Arena = arena.MarkUnknown(0, 0x1588) }, Effect = frame.Effect with { Pools = pools.ToImmutable() } };
    }
}
