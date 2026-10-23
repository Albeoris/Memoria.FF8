using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect283Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect283Controller
{
    public static NativeBattleEffect283Frame Initialize(NativeBattleEffect283Frame frame, NativeBattleEffect283Resources resources, NativeBattleEffect283Inputs inputs)
    {
        RequireBinding(frame);
        frame.State.Validate();
        resources.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.RootBankStorage is null)
        {
            throw new InvalidDataException("Effect283 requires its exclusive inactive root bank.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect283 requires its actual COMMON arena.");
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect283 requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Effect283 requires its retained texture process.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        uint caster = ModelAddress(Parameters(frame).Actor78);
        frame = frame with { Memory = frame.Memory with { Textures = textures }, State = frame.State.WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, caster).WriteDword(ArenaGlobal, ArenaAddress) };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, _) = root.Append(RootCallback);
        frame = frame with { State = frame.State with { RootBankStorage = null }, Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
        frame = ClearDwords701200(frame, RootBank + 12, 2);
        return frame with { Memory = frame.Memory with { Textures = (frame.Memory.Textures ?? throw new NotSupportedException("Effect283 lost its current texture process.")).Register5099A0(resources.Camera) } };
    }

    public static NativeBattleEffect283Frame Advance(NativeBattleEffect283Frame frame, NativeBattleEffect283Resources resources, NativeBattleEffect283Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 2) || frame.State.RootBankStorage is not null)
        {
            throw new InvalidDataException("Effect283 requires its exclusive active task banks.");
        }
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleCureFamilyPool expected = Pools[index];
            NativeBattleEffectTaskPool actual = frame.Effect.Pools[index];
            if (actual.DescriptorAddress != expected.Descriptor || actual.BankAddress != expected.Bank || actual.Stride != expected.Stride || actual.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Effect283 task pool leaves its physical descriptor contract.");
            }
        }
        (frame, int survivors) = AdvancePool(frame, 0, resources, inputs);
        frame = frame with { Effect = frame.Effect with { ReturnedDescriptor = survivors == 0 ? 0u : RootDescriptor } };
        return survivors == 0 ? DetachBanks(frame) : frame;
    }

    private static (NativeBattleEffect283Frame Frame, int Survivors) AdvancePool(NativeBattleEffect283Frame frame, int index, NativeBattleEffect283Resources resources, NativeBattleEffect283Inputs inputs)
    {
        NativeBattleEffect283Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            current = WithPool(current, index, pool);
            (current, int result) = AdvanceTask(current, pool.BankAddress + (uint)(slot * pool.Stride), resources, inputs);
            return new(current.Effect.Pools[index], result);
        });
        return (WithPool(current, index, step.Pool), step.Survivors);
    }

    public static (NativeBattleEffect283Frame Frame, int Result) AdvanceTask(NativeBattleEffect283Frame frame, uint address, NativeBattleEffect283Resources resources, NativeBattleEffect283Inputs inputs) => Read(frame, address).Callback switch
    {
        RootCallback => AdvanceRoot(frame, address, resources, inputs),
        MainCallback => AdvanceMain(frame, address, resources, inputs),
        _ => throw new NotSupportedException("Effect283 reached an unsupported native task callback.")
    };

    public static NativeBattleEffect283Frame ClearDwords701200(NativeBattleEffect283Frame frame, uint address, int count) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, new byte[checked(count * 4)]), checked(count * 4));

    private static NativeBattleEffect283Frame InitializeSecondaryBanks(NativeBattleEffect283Frame frame)
    {
        if (frame.Effect.Pools.Length != 1)
        {
            throw new InvalidDataException("Effect283 secondary reset requires its inactive COMMON-backed banks.");
        }
        NativeBattleCureFamilyPool profile = Pools[1];
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(profile.Descriptor, profile.Bank, profile.Stride, profile.Capacity);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect283 reset requires its current COMMON owner.");
        return frame with { Memory = frame.Memory with { Arena = arena.MarkUnknown(0, MainLength) }, Effect = frame.Effect with { Pools = frame.Effect.Pools.Add(main) } };
    }
}
