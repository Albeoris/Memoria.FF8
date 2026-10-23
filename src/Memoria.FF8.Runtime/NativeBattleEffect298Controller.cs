using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect298Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect298Controller
{
    public static NativeBattleEffect298Frame Initialize(NativeBattleEffect298Frame frame, NativeBattleEffect298Resources resources, NativeBattleEffect298Inputs inputs)
    {
        RequireBinding(frame);
        frame.State.Validate();
        resources.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.RootBankStorage is null)
        {
            throw new InvalidDataException("Effect298 requires its exclusive inactive root bank.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect298 requires its actual COMMON arena.");
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect298 requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Effect298 requires its retained texture process.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, _) = root.Append(RootCallback);
        frame = frame with { Memory = frame.Memory with { Textures = textures.Register5099A0(resources.Camera) }, State = frame.State.WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(Parameters(frame).Actor78)).WriteDword(ArenaGlobal, ArenaAddress) with { RootBankStorage = null }, Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
        return ClearDwords701200(frame, RootBank + 12, 2);
    }

    public static NativeBattleEffect298Frame Advance(NativeBattleEffect298Frame frame, NativeBattleEffect298Resources resources, NativeBattleEffect298Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 5) || frame.State.RootBankStorage is not null)
        {
            throw new InvalidDataException("Effect298 requires its exclusive active task banks.");
        }
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleCureFamilyPool expected = Pools[index];
            NativeBattleEffectTaskPool actual = frame.Effect.Pools[index];
            if (actual.DescriptorAddress != expected.Descriptor || actual.BankAddress != expected.Bank || actual.Stride != expected.Stride || actual.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Effect298 task pool leaves its physical descriptor contract.");
            }
        }
        (frame, int survivors) = AdvancePool(frame, 0, resources, inputs);
        frame = frame with { Effect = frame.Effect with { ReturnedDescriptor = survivors == 0 ? 0u : RootDescriptor } };
        return survivors == 0 ? DetachBanks(frame) : frame;
    }

    private static (NativeBattleEffect298Frame Frame, int Survivors) AdvancePool(NativeBattleEffect298Frame frame, int index, NativeBattleEffect298Resources resources, NativeBattleEffect298Inputs inputs)
    {
        NativeBattleEffect298Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            current = WithPool(current, index, pool);
            (current, int result) = AdvanceTask(current, pool.BankAddress + (uint)(slot * pool.Stride), resources, inputs);
            return new(current.Effect.Pools[index], result);
        });
        return (WithPool(current, index, step.Pool), step.Survivors);
    }

    public static (NativeBattleEffect298Frame Frame, int Result) AdvanceTask(NativeBattleEffect298Frame frame, uint address, NativeBattleEffect298Resources resources, NativeBattleEffect298Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            RootCallback => AdvanceRoot(frame, address, resources, inputs),
            MainCallback => AdvanceMain(frame, address, resources, inputs),
            ScriptCallback => AdvanceScript670C60(frame, address, resources, inputs),
            0x671AB0 => AdvanceProgramme671AB0(frame, address, resources, inputs),
            0x671F20 => AdvanceSprite671F20(frame, address, resources, inputs),
            _ => throw new NotSupportedException("Effect298 reached an unsupported native task callback.")
        };
    }

    public static NativeBattleEffect298Frame ClearDwords701200(NativeBattleEffect298Frame frame, uint address, int count) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, new byte[checked(count * 4)]), checked(count * 4));

    private static NativeBattleEffect298Frame InitializeSecondaryBanks(NativeBattleEffect298Frame frame)
    {
        if (frame.Effect.Pools.Length != 1)
        {
            throw new InvalidDataException("Effect298 secondary reset requires its inactive COMMON-backed banks.");
        }
        NativeBattleEffectTaskPool[] reset = new NativeBattleEffectTaskPool[4];
        foreach (int index in new[] { 4, 3, 2, 1 })
        {
            NativeBattleCureFamilyPool profile = Pools[index];
            reset[index - 1] = NativeBattleEffectTaskPool.Initialize(profile.Descriptor, profile.Bank, profile.Stride, profile.Capacity);
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect298 reset requires its current COMMON owner.");
        return frame with { Memory = frame.Memory with { Arena = arena.MarkUnknown(0, IoOffset) }, Effect = frame.Effect with { Pools = frame.Effect.Pools.AddRange(reset) } };
    }
}
