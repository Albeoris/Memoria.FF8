using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleMiniPulseCannon286Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMiniPulseCannon286Controller
{
    public static NativeBattleMiniPulseCannon286Frame Initialize(NativeBattleMiniPulseCannon286Frame frame, NativeBattleMiniPulseCannon286Resources resources, NativeBattleMiniPulseCannon286Inputs inputs)
    {
        RequireBinding(frame);
        frame.State.Validate();
        resources.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.RootBankStorage is null)
        {
            throw new InvalidDataException("Mini Pulse Cannon activation requires its inactive physical root owner.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Mini Pulse Cannon requires its actual COMMON arena.");
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Mini Pulse Cannon requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Mini Pulse Cannon requires its retained texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        textures = textures.Register5099A0(resources.Camera);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, _) = root.Append(RootCallback);
        frame = frame with { Memory = frame.Memory with { Textures = textures }, State = frame.State.WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(Parameters(frame).Actor78)).WriteDword(ArenaGlobal, ArenaAddress) with { RootBankStorage = null }, Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
        return ClearDwords701200(frame, RootBank + 12, 2);
    }

    public static NativeBattleMiniPulseCannon286Frame Advance(NativeBattleMiniPulseCannon286Frame frame, NativeBattleMiniPulseCannon286Resources resources, NativeBattleMiniPulseCannon286Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 3) || frame.State.RootBankStorage is not null)
        {
            throw new InvalidDataException("Mini Pulse Cannon requires its exclusive active module bank owners.");
        }
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleCureFamilyPool expected = Pools[index];
            NativeBattleEffectTaskPool actual = frame.Effect.Pools[index];
            if (actual.DescriptorAddress != expected.Descriptor || actual.BankAddress != expected.Bank || actual.Stride != expected.Stride || actual.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Mini Pulse Cannon module pool leaves its physical descriptor contract.");
            }
        }
        (frame, int survivors) = AdvancePool(frame, 0, resources, inputs);
        frame = frame with { Effect = frame.Effect with { ReturnedDescriptor = survivors == 0 ? 0u : RootDescriptor } };
        return survivors == 0 ? DetachBanks(frame) : frame;
    }

    private static (NativeBattleMiniPulseCannon286Frame Frame, int Survivors) AdvancePool(NativeBattleMiniPulseCannon286Frame frame, int index, NativeBattleMiniPulseCannon286Resources resources, NativeBattleMiniPulseCannon286Inputs inputs)
    {
        NativeBattleMiniPulseCannon286Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            current = WithPool(current, index, pool);
            (current, int result) = AdvanceTask(current, pool.BankAddress + (uint)(slot * pool.Stride), resources, inputs);
            return new(current.Effect.Pools[index], result);
        });
        return (WithPool(current, index, step.Pool), step.Survivors);
    }

    public static (NativeBattleMiniPulseCannon286Frame Frame, int Result) AdvanceTask(NativeBattleMiniPulseCannon286Frame frame, uint address, NativeBattleMiniPulseCannon286Resources resources, NativeBattleMiniPulseCannon286Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattlePilotVisualIdentity? parent = frame.PilotIdentity;
        frame = frame with { PilotIdentity = frame.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(286, task) : null };
        (NativeBattleMiniPulseCannon286Frame current, int result) = task.Callback switch
        {
            RootCallback => AdvanceRoot(frame, address, resources, inputs),
            0x67A070 => AdvanceMain(frame, address, resources, inputs),
            0x67A7C0 => AdvanceSprite67A7C0(frame, address, resources, inputs),
            _ => throw new NotSupportedException("Mini Pulse Cannon reached an unsupported module callback.")
        };
        return (current with { PilotIdentity = parent }, result);
    }

    public static NativeBattleMiniPulseCannon286Frame ClearDwords701200(NativeBattleMiniPulseCannon286Frame frame, uint address, int count) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, new byte[checked(count * 4)]), checked(count * 4));

    private static NativeBattleMiniPulseCannon286Frame InitializeSecondaryBanks(NativeBattleMiniPulseCannon286Frame frame)
    {
        if (frame.Effect.Pools.Length != 1)
        {
            throw new InvalidDataException("Mini Pulse Cannon secondary reset requires its inactive COMMON-backed banks.");
        }
        ImmutableArray<NativeBattleEffectTaskPool>.Builder pools = frame.Effect.Pools.ToBuilder();
        NativeBattleCureFamilyPool mainProfile = Pools[2];
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(mainProfile.Descriptor, mainProfile.Bank, mainProfile.Stride, mainProfile.Capacity);
        NativeBattleCureFamilyPool spriteProfile = Pools[1];
        pools.Add(NativeBattleEffectTaskPool.Initialize(spriteProfile.Descriptor, spriteProfile.Bank, spriteProfile.Stride, spriteProfile.Capacity));
        pools.Add(main);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Mini Pulse Cannon reset requires its actual COMMON owner.");
        return frame with { Memory = frame.Memory with { Arena = arena.MarkUnknown(0, 0xB54) }, Effect = frame.Effect with { Pools = pools.ToImmutable() } };
    }
}
