using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEnemyMicroMissiles284Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEnemyMicroMissiles284Controller
{
    public static NativeBattleEnemyMicroMissiles284Frame Initialize(NativeBattleEnemyMicroMissiles284Frame frame, NativeBattleEnemyMicroMissiles284Resources resources, NativeBattleEnemyMicroMissiles284Inputs inputs)
    {
        RequireBinding(frame);
        frame.State.Validate();
        resources.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.RootBankStorage is null)
        {
            throw new InvalidDataException("Enemy Micro Missiles activation requires its inactive physical root owner.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Enemy Micro Missiles requires its actual COMMON arena.");
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Enemy Micro Missiles requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Enemy Micro Missiles requires its retained texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        textures = textures.Register5099A0(resources.Camera);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, _) = root.Append(RootCallback);
        frame = frame with { Memory = frame.Memory with { Textures = textures }, State = frame.State.WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(Parameters(frame).Actor78)).WriteDword(ArenaGlobal, ArenaAddress) with { RootBankStorage = null }, Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
        return ClearDwords701200(frame, RootBank + 12, 2);
    }

    public static NativeBattleEnemyMicroMissiles284Frame Advance(NativeBattleEnemyMicroMissiles284Frame frame, NativeBattleEnemyMicroMissiles284Resources resources, NativeBattleEnemyMicroMissiles284Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 4) || frame.State.RootBankStorage is not null)
        {
            throw new InvalidDataException("Enemy Micro Missiles requires its exclusive active module bank owners.");
        }
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleCureFamilyPool expected = Pools[index];
            NativeBattleEffectTaskPool actual = frame.Effect.Pools[index];
            if (actual.DescriptorAddress != expected.Descriptor || actual.BankAddress != expected.Bank || actual.Stride != expected.Stride || actual.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Enemy Micro Missiles module pool leaves its physical descriptor contract.");
            }
        }
        (frame, int survivors) = AdvancePool(frame, 0, resources, inputs);
        frame = frame with { Effect = frame.Effect with { ReturnedDescriptor = survivors == 0 ? 0u : RootDescriptor } };
        return survivors == 0 ? DetachBanks(frame) : frame;
    }

    private static (NativeBattleEnemyMicroMissiles284Frame Frame, int Survivors) AdvancePool(NativeBattleEnemyMicroMissiles284Frame frame, int index, NativeBattleEnemyMicroMissiles284Resources resources, NativeBattleEnemyMicroMissiles284Inputs inputs)
    {
        NativeBattleEnemyMicroMissiles284Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            current = WithPool(current, index, pool);
            (current, int result) = AdvanceTask(current, pool.BankAddress + (uint)(slot * pool.Stride), resources, inputs);
            return new(current.Effect.Pools[index], result);
        });
        return (WithPool(current, index, step.Pool), step.Survivors);
    }

    public static (NativeBattleEnemyMicroMissiles284Frame Frame, int Result) AdvanceTask(NativeBattleEnemyMicroMissiles284Frame frame, uint address, NativeBattleEnemyMicroMissiles284Resources resources, NativeBattleEnemyMicroMissiles284Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            RootCallback => AdvanceRoot(frame, address, resources, inputs),
            0x67BBB0 => AdvanceMissile67BBB0(frame, address, resources, inputs),
            0x67C300 => AdvanceImpact67C300(frame, address, resources, inputs),
            0x67C240 => AdvanceParticle67C240(frame, address, resources, inputs),
            _ => throw new NotSupportedException("Enemy Micro Missiles reached an unsupported module callback.")
        };
    }

    public static NativeBattleEnemyMicroMissiles284Frame ClearDwords701200(NativeBattleEnemyMicroMissiles284Frame frame, uint address, int count) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, new byte[checked(count * 4)]), checked(count * 4));

    private static NativeBattleEnemyMicroMissiles284Frame InitializeSecondaryBanks(NativeBattleEnemyMicroMissiles284Frame frame)
    {
        if (frame.Effect.Pools.Length != 1)
        {
            throw new InvalidDataException("Enemy Micro Missiles secondary reset requires its inactive COMMON-backed banks.");
        }
        ImmutableArray<NativeBattleEffectTaskPool>.Builder pools = frame.Effect.Pools.ToBuilder();
        NativeBattleCureFamilyPool missileProfile = Pools[3];
        NativeBattleEffectTaskPool missiles = NativeBattleEffectTaskPool.Initialize(missileProfile.Descriptor, missileProfile.Bank, missileProfile.Stride, missileProfile.Capacity);
        NativeBattleCureFamilyPool impactProfile = Pools[2];
        NativeBattleEffectTaskPool impacts = NativeBattleEffectTaskPool.Initialize(impactProfile.Descriptor, impactProfile.Bank, impactProfile.Stride, impactProfile.Capacity);
        NativeBattleCureFamilyPool particleProfile = Pools[1];
        pools.Add(NativeBattleEffectTaskPool.Initialize(particleProfile.Descriptor, particleProfile.Bank, particleProfile.Stride, particleProfile.Capacity));
        pools.Add(impacts);
        pools.Add(missiles);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Enemy Micro Missiles reset requires its actual COMMON owner.");
        return frame with { Memory = frame.Memory with { Arena = arena.MarkUnknown(0, 0x45A8) }, Effect = frame.Effect with { Pools = pools.ToImmutable() } };
    }
}
