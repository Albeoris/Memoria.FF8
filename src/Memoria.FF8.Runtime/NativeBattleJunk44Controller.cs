using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleJunk44Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleJunk44Controller
{
    public static NativeBattleJunk44Frame Initialize(NativeBattleJunk44Frame frame, NativeBattleJunk44Resources resources, NativeBattleJunk44Inputs inputs)
    {
        RequireBinding(frame);
        frame.State.Validate();
        resources.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.RootBankStorage is null)
        {
            throw new InvalidDataException("Junk activation requires its inactive physical root owner.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Junk requires its actual COMMON arena.");
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x2545100)?.ArenaOffset ?? throw new NotSupportedException("Junk requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Junk requires its retained texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        textures = textures.Register5099A0(resources.Camera);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2545150, 0x2545160, 20, 1);
        (root, _) = root.Append(0x6E7B90);
        frame = frame with { Memory = frame.Memory with { Textures = textures }, State = frame.State.WriteDword(0x2545100, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)tim)).WriteDword(0x2545104, 0x1D99A78).WriteDword(0x2545108, ModelAddress(Parameters(frame).Actor78)).WriteDword(0x2545180, NativeBattleEffectNodeMemory.ArenaAddress) with { RootBankStorage = null }, Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = 0x2545150, DrawArenaOffset = null } };
        return ClearDwords701200(frame, 0x254516C, 2);
    }

    public static NativeBattleJunk44Frame Advance(NativeBattleJunk44Frame frame, NativeBattleJunk44Resources resources, NativeBattleJunk44Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 5) || frame.State.RootBankStorage is not null)
        {
            throw new InvalidDataException("Junk requires its exclusive active module bank owners.");
        }
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleCureFamilyPool expected = Pools[index];
            NativeBattleEffectTaskPool actual = frame.Effect.Pools[index];
            if (actual.DescriptorAddress != expected.Descriptor || actual.BankAddress != expected.Bank || actual.Stride != expected.Stride || actual.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Junk module pool leaves its physical descriptor contract.");
            }
        }
        (frame, int survivors) = AdvancePool(frame, 0, resources, inputs);
        frame = frame with { Effect = frame.Effect with { ReturnedDescriptor = survivors == 0 ? 0u : 0x2545150u } };
        return survivors == 0 ? DetachBanks(frame) : frame;
    }

    private static (NativeBattleJunk44Frame Frame, int Survivors) AdvancePool(NativeBattleJunk44Frame frame, int index, NativeBattleJunk44Resources resources, NativeBattleJunk44Inputs inputs)
    {
        NativeBattleJunk44Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            current = WithPool(current, index, pool);
            (current, int result) = AdvanceTask(current, pool.BankAddress + (uint)(slot * pool.Stride), resources, inputs);
            return new(current.Effect.Pools[index], result);
        });
        return (WithPool(current, index, step.Pool), step.Survivors);
    }

    public static (NativeBattleJunk44Frame Frame, int Result) AdvanceTask(NativeBattleJunk44Frame frame, uint address, NativeBattleJunk44Resources resources, NativeBattleJunk44Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            0x6E7B90 => AdvanceRoot(frame, address, resources, inputs),
            0x6E7E00 => AdvanceMain(frame, address, resources, inputs),
            0x6E82C0 => AdvanceSprite6E82C0(frame, address, resources, inputs),
            0x6E84D0 => AdvanceDebris6E84D0(frame, address, resources, inputs),
            0x6E8810 => AdvanceProgramme6E8810(frame, address, resources, inputs),
            _ => throw new NotSupportedException("Junk reached an unsupported module callback.")
        };
    }

    public static NativeBattleJunk44Frame ClearDwords701200(NativeBattleJunk44Frame frame, uint address, int count) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, new byte[checked(count * 4)]), checked(count * 4));

    private static NativeBattleJunk44Frame InitializeSecondaryBanks(NativeBattleJunk44Frame frame)
    {
        if (frame.Effect.Pools.Length != 1)
        {
            throw new InvalidDataException("Junk secondary reset requires its inactive COMMON-backed banks.");
        }
        ImmutableArray<NativeBattleEffectTaskPool>.Builder pools = frame.Effect.Pools.ToBuilder();
        foreach (NativeBattleCureFamilyPool profile in Pools.Skip(1))
        {
            pools.Add(NativeBattleEffectTaskPool.Initialize(profile.Descriptor, profile.Bank, profile.Stride, profile.Capacity));
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Junk reset requires its actual COMMON owner.");
        return frame with { Memory = frame.Memory with { Arena = arena.MarkUnknown(0, 0x128C) }, Effect = frame.Effect with { Pools = pools.ToImmutable() } };
    }
}
