using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleShotgun281Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleShotgun281Controller
{
    public static NativeBattleShotgun281Frame Initialize(NativeBattleShotgun281Frame frame, NativeBattleShotgun281Resources resources, NativeBattleShotgun281Inputs inputs)
    {
        RequireBinding(frame);
        frame.State.Validate();
        resources.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.RootBankStorage is null)
        {
            throw new InvalidDataException("Shotgun activation requires its inactive physical root owner.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Shotgun requires its actual COMMON arena.");
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Shotgun requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Shotgun requires its retained texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        textures = textures.Register5099A0(resources.Camera);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, _) = root.Append(RootCallback);
        frame = frame with { Memory = frame.Memory with { Textures = textures }, State = frame.State.WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(Parameters(frame).Actor78)).WriteDword(ArenaGlobal, ArenaAddress) with { RootBankStorage = null }, Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
        return ClearDwords701200(frame, RootBank + 12, 2);
    }

    public static NativeBattleShotgun281Frame Advance(NativeBattleShotgun281Frame frame, NativeBattleShotgun281Resources resources, NativeBattleShotgun281Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 4) || frame.State.RootBankStorage is not null)
        {
            throw new InvalidDataException("Shotgun requires its exclusive active module bank owners.");
        }
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleCureFamilyPool expected = Pools[index];
            NativeBattleEffectTaskPool actual = frame.Effect.Pools[index];
            if (actual.DescriptorAddress != expected.Descriptor || actual.BankAddress != expected.Bank || actual.Stride != expected.Stride || actual.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Shotgun module pool leaves its physical descriptor contract.");
            }
        }
        (frame, int survivors) = AdvancePool(frame, 0, resources, inputs);
        frame = frame with { Effect = frame.Effect with { ReturnedDescriptor = survivors == 0 ? 0u : RootDescriptor } };
        return survivors == 0 ? DetachBanks(frame) : frame;
    }

    private static (NativeBattleShotgun281Frame Frame, int Survivors) AdvancePool(NativeBattleShotgun281Frame frame, int index, NativeBattleShotgun281Resources resources, NativeBattleShotgun281Inputs inputs)
    {
        NativeBattleShotgun281Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            current = WithPool(current, index, pool);
            (current, int result) = AdvanceTask(current, pool.BankAddress + (uint)(slot * pool.Stride), resources, inputs);
            return new(current.Effect.Pools[index], result);
        });
        return (WithPool(current, index, step.Pool), step.Survivors);
    }

    public static (NativeBattleShotgun281Frame Frame, int Result) AdvanceTask(NativeBattleShotgun281Frame frame, uint address, NativeBattleShotgun281Resources resources, NativeBattleShotgun281Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            RootCallback => AdvanceRoot(frame, address, resources, inputs),
            0x67DF80 => AdvanceMain(frame, address, resources, inputs),
            0x67E5F0 => AdvanceSprite67E5F0(frame, address, resources, inputs),
            0x67E6B0 => AdvanceProgramme67E6B0(frame, address, resources, inputs),
            _ => throw new NotSupportedException("Shotgun reached an unsupported module callback.")
        };
    }

    public static NativeBattleShotgun281Frame ClearDwords701200(NativeBattleShotgun281Frame frame, uint address, int count) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, new byte[checked(count * 4)]), checked(count * 4));

    private static NativeBattleShotgun281Frame InitializeSecondaryBanks(NativeBattleShotgun281Frame frame)
    {
        if (frame.Effect.Pools.Length != 1)
        {
            throw new InvalidDataException("Shotgun secondary reset requires its inactive COMMON-backed banks.");
        }
        ImmutableArray<NativeBattleEffectTaskPool>.Builder pools = frame.Effect.Pools.ToBuilder();
        foreach (NativeBattleCureFamilyPool profile in Pools.Skip(1))
        {
            pools.Add(NativeBattleEffectTaskPool.Initialize(profile.Descriptor, profile.Bank, profile.Stride, profile.Capacity));
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Shotgun reset requires its actual COMMON owner.");
        return frame with { Memory = frame.Memory with { Arena = arena.MarkUnknown(0, 0x1DE8) }, Effect = frame.Effect with { Pools = pools.ToImmutable() } };
    }
}
