using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEvilEye282Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEvilEye282Controller
{
    public static NativeBattleEvilEye282Frame Initialize(NativeBattleEvilEye282Frame frame, NativeBattleEvilEye282Resources resources, NativeBattleEvilEye282Inputs inputs)
    {
        RequireBinding(frame);
        frame.State.Validate();
        resources.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.RootBankStorage is null)
        {
            throw new InvalidDataException("Evil-Eye activation requires its inactive physical root owner.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Evil-Eye requires its actual COMMON arena.");
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Evil-Eye requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Evil-Eye requires its retained texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        textures = textures.Register5099A0(resources.Camera);
        frame = DrawRandom(frame, out ushort seed);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, _) = root.Append(RootCallback);
        frame = frame with { Memory = frame.Memory with { Textures = textures }, State = frame.State.WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(Parameters(frame).Actor78)).WriteDword(ArenaGlobal, ArenaAddress).WriteDword(SeedGlobal, seed) with { RootBankStorage = null }, Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
        return ClearDwords701200(frame, RootBank + 12, 2);
    }

    public static NativeBattleEvilEye282Frame Advance(NativeBattleEvilEye282Frame frame, NativeBattleEvilEye282Resources resources, NativeBattleEvilEye282Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 4) || frame.State.RootBankStorage is not null)
        {
            throw new InvalidDataException("Evil-Eye requires its exclusive active module bank owners.");
        }
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleCureFamilyPool expected = Pools[index];
            NativeBattleEffectTaskPool actual = frame.Effect.Pools[index];
            if (actual.DescriptorAddress != expected.Descriptor || actual.BankAddress != expected.Bank || actual.Stride != expected.Stride || actual.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Evil-Eye module pool leaves its physical descriptor contract.");
            }
        }
        (frame, int survivors) = AdvancePool(frame, 0, resources, inputs);
        frame = frame with { Effect = frame.Effect with { ReturnedDescriptor = survivors == 0 ? 0u : RootDescriptor } };
        return survivors == 0 ? DetachBanks(frame) : frame;
    }

    private static (NativeBattleEvilEye282Frame Frame, int Survivors) AdvancePool(NativeBattleEvilEye282Frame frame, int index, NativeBattleEvilEye282Resources resources, NativeBattleEvilEye282Inputs inputs)
    {
        NativeBattleEvilEye282Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            current = WithPool(current, index, pool);
            (current, int result) = AdvanceTask(current, pool.BankAddress + (uint)(slot * pool.Stride), resources, inputs);
            return new(current.Effect.Pools[index], result);
        });
        return (WithPool(current, index, step.Pool), step.Survivors);
    }

    public static (NativeBattleEvilEye282Frame Frame, int Result) AdvanceTask(NativeBattleEvilEye282Frame frame, uint address, NativeBattleEvilEye282Resources resources, NativeBattleEvilEye282Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            RootCallback => AdvanceRoot(frame, address, resources, inputs),
            0x67D680 => AdvanceMain(frame, address, resources, inputs),
            0x67DC60 => AdvanceSprite67DC60(frame, address, resources, inputs),
            0x67DB20 => AdvanceProgramme67DB20(frame, address, resources, inputs),
            _ => throw new NotSupportedException("Evil-Eye reached an unsupported module callback.")
        };
    }

    public static NativeBattleEvilEye282Frame ClearDwords701200(NativeBattleEvilEye282Frame frame, uint address, int count) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, new byte[checked(count * 4)]), checked(count * 4));

    private static NativeBattleEvilEye282Frame InitializeSecondaryBanks(NativeBattleEvilEye282Frame frame)
    {
        if (frame.Effect.Pools.Length != 1)
        {
            throw new InvalidDataException("Evil-Eye secondary reset requires its inactive COMMON-backed banks.");
        }
        ImmutableArray<NativeBattleEffectTaskPool>.Builder pools = frame.Effect.Pools.ToBuilder();
        foreach (NativeBattleCureFamilyPool profile in Pools.Skip(1))
        {
            pools.Add(NativeBattleEffectTaskPool.Initialize(profile.Descriptor, profile.Bank, profile.Stride, profile.Capacity));
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Evil-Eye reset requires its actual COMMON owner.");
        return frame with { Memory = frame.Memory with { Arena = arena.MarkUnknown(0, 0x142C) }, Effect = frame.Effect with { Pools = pools.ToImmutable() } };
    }
}
