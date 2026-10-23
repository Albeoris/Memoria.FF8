using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleGravija214Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleGravija214Controller
{
    public static NativeBattleGravija214Frame Initialize(NativeBattleGravija214Frame frame, NativeBattleGravija214Resources resources, NativeBattleGravija214Inputs inputs)
    {
        RequireBinding(frame);
        frame.State.Validate();
        resources.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.RootBankStorage is null)
        {
            throw new InvalidDataException("Gravija activation requires its inactive physical root owner.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Gravija requires its actual COMMON arena.");
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Gravija requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Gravija requires its retained texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        textures = textures.Register5099A0(resources.Camera);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, _) = root.Append(RootCallback);
        frame = frame with { Memory = frame.Memory with { Textures = textures }, State = frame.State.CopyEntrySnapshot(resources).WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(Parameters(frame).Actor78)).WriteDword(ArenaGlobal, ArenaAddress) with { RootBankStorage = null }, Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
        return ClearDwords701200(frame, RootBank + 12, 2);
    }

    public static NativeBattleGravija214Frame Advance(NativeBattleGravija214Frame frame, NativeBattleGravija214Resources resources, NativeBattleGravija214Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 3) || frame.State.RootBankStorage is not null)
        {
            throw new InvalidDataException("Gravija requires its exclusive active module bank owners.");
        }
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleCureFamilyPool expected = Pools[index];
            NativeBattleEffectTaskPool actual = frame.Effect.Pools[index];
            if (actual.DescriptorAddress != expected.Descriptor || actual.BankAddress != expected.Bank || actual.Stride != expected.Stride || actual.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Gravija module pool leaves its physical descriptor contract.");
            }
        }
        (frame, int survivors) = AdvancePool(frame, 0, resources, inputs);
        frame = frame with { Effect = frame.Effect with { ReturnedDescriptor = survivors == 0 ? 0u : RootDescriptor } };
        return survivors == 0 ? DetachBanks(frame) : frame;
    }

    private static (NativeBattleGravija214Frame Frame, int Survivors) AdvancePool(NativeBattleGravija214Frame frame, int index, NativeBattleGravija214Resources resources, NativeBattleGravija214Inputs inputs)
    {
        NativeBattleGravija214Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            current = WithPool(current, index, pool);
            (current, int result) = AdvanceTask(current, pool.BankAddress + (uint)(slot * pool.Stride), resources, inputs);
            return new(current.Effect.Pools[index], result);
        });
        return (WithPool(current, index, step.Pool), step.Survivors);
    }

    public static (NativeBattleGravija214Frame Frame, int Result) AdvanceTask(NativeBattleGravija214Frame frame, uint address, NativeBattleGravija214Resources resources, NativeBattleGravija214Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            RootCallback => AdvanceRoot(frame, address, resources, inputs),
            0x69D870 => AdvanceMain(frame, address, resources, inputs),
            0x69DF00 => AdvanceSprite69DF00(frame, address, resources, inputs),
            _ => throw new NotSupportedException("Gravija reached an unsupported module callback.")
        };
    }

    public static NativeBattleOriginalEffectState WriteCompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        int index = -1;
        for (int candidate = 0; candidate < effect.Pools.Length; candidate++)
        {
            if (effect.Pools[candidate].DescriptorAddress == 0x2510128)
            {
                index = candidate;
                break;
            }
        }
        if (index < 0 || address != effect.Pools[index].BankAddress + 18)
        {
            throw new InvalidDataException("Gravija completion byte leaves its actual main task.");
        }
        NativeBattleEffectTaskPool pool = effect.Pools[index];
        return effect with { Pools = effect.Pools.SetItem(index, pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteByte(18, value)) }) };
    }

    public static NativeBattleGravija214Frame ClearDwords701200(NativeBattleGravija214Frame frame, uint address, int count) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, new byte[checked(count * 4)]), checked(count * 4));

    private static NativeBattleGravija214Frame InitializeSecondaryBanks(NativeBattleGravija214Frame frame)
    {
        if (frame.Effect.Pools.Length != 1)
        {
            throw new InvalidDataException("Gravija secondary reset requires its inactive COMMON-backed banks.");
        }
        ImmutableArray<NativeBattleEffectTaskPool>.Builder pools = frame.Effect.Pools.ToBuilder();
        foreach (NativeBattleCureFamilyPool profile in Pools.Skip(1))
        {
            pools.Add(NativeBattleEffectTaskPool.Initialize(profile.Descriptor, profile.Bank, profile.Stride, profile.Capacity));
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Gravija reset requires its actual COMMON owner.");
        return frame with { Memory = frame.Memory with { Arena = arena.MarkUnknown(0, 0xA24) }, Effect = frame.Effect with { Pools = pools.ToImmutable() } };
    }
}
