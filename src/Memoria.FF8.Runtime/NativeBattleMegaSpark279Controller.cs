using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleMegaSpark279Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMegaSpark279Controller
{
    public static NativeBattleMegaSpark279Frame Initialize(NativeBattleMegaSpark279Frame frame, NativeBattleMegaSpark279Resources resources, NativeBattleMegaSpark279Inputs inputs)
    {
        RequireBinding(frame);
        frame.State.Validate();
        resources.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.RootBankStorage is null)
        {
            throw new InvalidDataException("Mega Spark activation requires its inactive physical root owner.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Mega Spark requires its actual COMMON arena.");
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Mega Spark requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Mega Spark requires its retained texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        textures = textures.Register5099A0(resources.Camera);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, _) = root.Append(RootCallback);
        frame = frame with { Memory = frame.Memory with { Textures = textures }, State = frame.State.WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(Parameters(frame).Actor78)).WriteDword(ArenaGlobal, ArenaAddress) with { RootBankStorage = null }, Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
        frame = DrawRandom(frame, out ushort seed);
        frame = Dword(frame, SeedGlobal, seed);
        return ClearDwords701200(frame, RootBank + 12, 2);
    }

    public static NativeBattleMegaSpark279Frame Advance(NativeBattleMegaSpark279Frame frame, NativeBattleMegaSpark279Resources resources, NativeBattleMegaSpark279Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 7) || frame.State.RootBankStorage is not null)
        {
            throw new InvalidDataException("Mega Spark requires its exclusive active module bank owners.");
        }
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleCureFamilyPool expected = Pools[index];
            NativeBattleEffectTaskPool actual = frame.Effect.Pools[index];
            if (actual.DescriptorAddress != expected.Descriptor || actual.BankAddress != expected.Bank || actual.Stride != expected.Stride || actual.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Mega Spark module pool leaves its physical descriptor contract.");
            }
        }
        (frame, int survivors) = AdvancePool(frame, 0, resources, inputs);
        frame = frame with { Effect = frame.Effect with { ReturnedDescriptor = survivors == 0 ? 0u : RootDescriptor } };
        return survivors == 0 ? DetachBanks(frame) : frame;
    }

    private static (NativeBattleMegaSpark279Frame Frame, int Survivors) AdvancePool(NativeBattleMegaSpark279Frame frame, int index, NativeBattleMegaSpark279Resources resources, NativeBattleMegaSpark279Inputs inputs)
    {
        NativeBattleMegaSpark279Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            current = WithPool(current, index, pool);
            (current, int result) = AdvanceTask(current, pool.BankAddress + (uint)(slot * pool.Stride), resources, inputs);
            return new(current.Effect.Pools[index], result);
        });
        return (WithPool(current, index, step.Pool), step.Survivors);
    }

    public static (NativeBattleMegaSpark279Frame Frame, int Result) AdvanceTask(NativeBattleMegaSpark279Frame frame, uint address, NativeBattleMegaSpark279Resources resources, NativeBattleMegaSpark279Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            RootCallback => AdvanceRoot(frame, address, resources, inputs),
            MainCallback => AdvanceMain(frame, address, resources, inputs),
            0x67FC30 => AdvanceRows67FC30(frame, address, resources, inputs),
            0x680520 => AdvanceAttachment680520(frame, address, resources, inputs),
            0x680170 => AdvanceProgramme680170(frame, address, resources, inputs),
            0x6800F0 => AdvanceSprite6800F0(frame, address, resources, inputs),
            _ => throw new NotSupportedException("Mega Spark reached an unsupported module callback.")
        };
    }

    public static NativeBattleMegaSpark279Frame ClearDwords701200(NativeBattleMegaSpark279Frame frame, uint address, int count) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, new byte[checked(count * 4)]), checked(count * 4));

    private static NativeBattleMegaSpark279Frame InitializeSecondaryBanks(NativeBattleMegaSpark279Frame frame)
    {
        if (frame.Effect.Pools.Length != 1)
        {
            throw new InvalidDataException("Mega Spark secondary reset requires its inactive COMMON-backed banks.");
        }
        ImmutableArray<NativeBattleEffectTaskPool>.Builder pools = frame.Effect.Pools.ToBuilder();
        NativeBattleEffectTaskPool[] reset = new NativeBattleEffectTaskPool[6];
        foreach (int index in new[] { 6, 5, 4, 2, 3, 1 })
        {
            NativeBattleCureFamilyPool profile = Pools[index];
            reset[index - 1] = NativeBattleEffectTaskPool.Initialize(profile.Descriptor, profile.Bank, profile.Stride, profile.Capacity);
        }
        pools.AddRange(reset);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Mega Spark reset requires its actual COMMON owner.");
        return frame with { Memory = frame.Memory with { Arena = arena.MarkUnknown(0, 0x3E68) }, Effect = frame.Effect with { Pools = pools.ToImmutable() } };
    }
}
