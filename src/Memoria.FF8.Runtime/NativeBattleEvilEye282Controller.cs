using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEvilEye282Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEvilEye282Controller
{
    private const int SpritePoolIndex = 1;
    private const int ProgrammeBPoolIndex = 2;
    private const int ProgrammeAPoolIndex = 3;

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
        frame = frame with { Memory = frame.Memory with { Textures = textures }, State = frame.State.WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(ParametersGlobal, 0x1D99A78).WriteModel(CasterGlobal, Parameters(frame).Source, inputs.Packets.ModelAliases).WriteDword(ArenaGlobal, ArenaAddress).WriteDword(SeedGlobal, seed) with { RootBankStorage = null }, Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
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
        int count = 0;
        if (frame.Effect.Pools.Length == 4 && inputs.StorageContract == BattleContactTaskStorageContract.ExpandableArena)
        {
            NativeBattleEffectTaskPool main = frame.Effect.Pools[ProgrammeAPoolIndex];
            ValidatePool(main, Pools[ProgrammeAPoolIndex], Pools[ProgrammeAPoolIndex].Capacity);
            count = main.Slots[0].ReadContactCount(14);
        }
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            ValidatePool(frame.Effect.Pools[index], Pools[index], RequiredCapacity(index, count));
        }
        if (frame.Effect.Pools.Length == 4 && RequiredCapacity(SpritePoolIndex, count) > Pools[SpritePoolIndex].Capacity)
        {
            NativeBattleEffectTaskPool sprites = frame.Effect.Pools[SpritePoolIndex];
            NativeBattleEffectTaskPool targets = frame.Effect.Pools[ProgrammeBPoolIndex];
            ulong spritesEnd = sprites.BankAddress + (ulong)sprites.Stride * (uint)sprites.Slots.Length;
            ulong targetsEnd = targets.BankAddress + (ulong)targets.Stride * (uint)targets.Slots.Length;
            if (sprites.BankAddress < targetsEnd && targets.BankAddress < spritesEnd)
            {
                throw new InvalidDataException("Evil-Eye hosted module banks overlap.");
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

    private static NativeBattleEvilEye282Frame InitializeSecondaryBanks(NativeBattleEvilEye282Frame frame, BattleModelRoster? roster = null, int count = 0)
    {
        if (frame.Effect.Pools.Length != 1)
        {
            throw new InvalidDataException("Evil-Eye secondary reset requires its inactive COMMON-backed banks.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Evil-Eye reset requires its actual COMMON owner.");
        var layouts = new (uint Address, ushort Capacity)[Pools.Length];
        NativeBattleEffectMemory memory = frame.Memory;
        for (int index = 1; index < Pools.Length; index++)
        {
            NativeBattleCureFamilyPool profile = Pools[index];
            ushort capacity = RequiredCapacity(index, count);
            uint address = profile.Bank;
            if (capacity > profile.Capacity)
            {
                (memory, address) = NativeBattlePresentationTaskStorage.Reserve(memory, frame.Effect, roster!, profile.Stride, capacity);
            }
            layouts[index] = (address, capacity);
        }
        ImmutableArray<NativeBattleEffectTaskPool>.Builder pools = frame.Effect.Pools.ToBuilder();
        for (int index = 1; index < Pools.Length; index++)
        {
            NativeBattleCureFamilyPool profile = Pools[index];
            (uint address, ushort capacity) = layouts[index];
            pools.Add(NativeBattleEffectTaskPool.Initialize(profile.Descriptor, address, profile.Stride, capacity));
        }
        return frame with { Memory = memory with { Arena = arena.MarkUnknown(0, ScratchOffset) }, Effect = frame.Effect with { Pools = pools.ToImmutable() } };
    }

    private static ushort RequiredCapacity(int index, int count)
    {
        // Sprites advance on the birth frame and survive sixteen callbacks; each programme emits every fourth callback.
        int required = index switch
        {
            SpritePoolIndex => checked(count * 4),
            ProgrammeBPoolIndex => count,
            _ => 0
        };
        return checked((ushort)Math.Max(Pools[index].Capacity, required));
    }

    private static void ValidatePool(NativeBattleEffectTaskPool pool, NativeBattleCureFamilyPool profile, ushort capacity)
    {
        bool located = capacity == profile.Capacity
            ? pool.BankAddress == profile.Bank
            : NativeBattlePresentationTaskStorage.IsHostedBank(pool.BankAddress, profile.Stride, capacity);
        if (!located || pool.DescriptorAddress != profile.Descriptor || pool.Stride != profile.Stride || pool.Slots.Length != capacity)
        {
            throw new InvalidDataException("Evil-Eye module pool leaves its physical descriptor contract.");
        }
    }
}
