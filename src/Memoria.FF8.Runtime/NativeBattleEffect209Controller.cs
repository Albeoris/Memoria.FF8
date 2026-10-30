using static Memoria.FF8.Runtime.NativeBattleEffect209Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect209Controller
{
    public static NativeBattleEffect209Frame Initialize(NativeBattleEffect209Frame frame, NativeBattleEffect209Resources resources, NativeBattleEffect209Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame.State.Validate();
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Effect209 entry requires its inactive fixed banks.");
        }
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect209 requires its current parameters.");
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int actor = parameters.Actor78;
        int target = packets.ReadContact(header.FirstContactIndex ?? throw new NotSupportedException("Effect209 requires its actual first contact pointer.")).Primary.ActorIndex;
        frame = frame with { State = frame.State.WriteDword(ArenaGlobal, CommonAddress).WriteDword(GuardianGlobal, GuardianAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, (uint)actor).WriteDword(TargetGlobal, (uint)target) };
        NativeBattleEffectTaskPool root = InitializeBank(frame, 0);
        (root, int rootSlot) = root.Append(RootCallback);
        root = root.WriteSlot(rootSlot, root.Slots[rootSlot].WriteWord(12, 0));
        NativeBattleEffectTaskPool child = InitializeBank(frame, 1);
        byte modelId = inputs.ReadModel(frame, actor).ModelId4 ?? throw new NotSupportedException("Effect209 requires its current caster model byte.");
        (child, int mainSlot) = child.Append(modelId == 0x85 ? AlternateCallback : MainCallback);
        child = child.WriteSlot(mainSlot, child.Slots[mainSlot].WriteWord(12, 0));
        frame = frame with { State = frame.State.DetachBank(0).DetachBank(1), Effect = frame.Effect with { Pools = [root, child], ReturnedDescriptor = RootDescriptor } };
        frame = inputs.Execute(frame, new NativeBattleEffect209RegisterCamera5099A0(CameraAddress));
        return inputs.Execute(frame, new NativeBattleEffect209UploadTim505E30(TimAddress));
    }

    private static NativeBattleEffectTaskPool InitializeBank(NativeBattleEffect209Frame frame, int index)
    {
        NativeBattleCureFamilyPool shape = Pools[index];
        NativeBattleEffectArena bank = frame.State.ReadBank(index);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, shape.Bank, shape.Stride, shape.Capacity);
        for (int slot = 0; slot < shape.Capacity; slot++)
        {
            int offset = slot * shape.Stride + 12;
            pool = NativeBattleEffectTaskMemory.WriteBankSpan(pool, offset, CopyBytes(bank, offset, NativeBattleEffectArena.Clear(), 0, shape.Stride - 12), shape.Stride - 12);
        }
        return pool;
    }

    public static NativeBattleEffect209Frame Advance(NativeBattleEffect209Frame frame, NativeBattleEffect209Resources resources, NativeBattleEffect209Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 2)
        {
            throw new InvalidDataException("Effect209 requires its two exclusive active banks.");
        }
        for (int index = 0; index < 2; index++)
        {
            NativeBattleCureFamilyPool shape = Pools[index];
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            if (pool.DescriptorAddress != shape.Descriptor || pool.BankAddress != shape.Bank || pool.Stride != shape.Stride || pool.Slots.Length != shape.Capacity)
            {
                throw new InvalidDataException("Effect209 fixed bank dimensions changed.");
            }
            pool.Validate();
        }
        (frame, int survivors) = AdvancePool(frame, 0, resources, inputs);
        if (survivors != 0)
        {
            return frame;
        }
        for (int index = 0; index < 2; index++)
        {
            frame = frame with { State = frame.State.AttachBank(index, NativeBattleEffectTaskMemory.CaptureBank(frame.Effect.Pools[index])) };
        }
        return frame with { Effect = frame.Effect with { Pools = [], ReturnedDescriptor = 0 } };
    }

    public static (NativeBattleEffect209Frame Frame, int Survivors) AdvancePool(NativeBattleEffect209Frame frame, int index, NativeBattleEffect209Resources resources, NativeBattleEffect209Inputs inputs)
    {
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            frame = WithPool(frame, index, pool);
            NativeBattleEffect209Step advanced = AdvanceTask(frame, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            frame = advanced.Frame;
            return new(frame.Effect.Pools[index], advanced.Result);
        });
        return (WithPool(frame, index, step.Pool), step.Survivors);
    }

    public static NativeBattleEffect209Step AdvanceTask(NativeBattleEffect209Frame frame, uint address, NativeBattleEffect209Resources resources, NativeBattleEffect209Inputs inputs)
    {
        frame = frame with { PresentationIdentity = frame.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(209, Read(frame, address)) : null };
        return Read(frame, address).Callback switch
        {
            RootCallback => AdvanceRoot639100(frame, address, resources, inputs),
            MainCallback => AdvanceMain6375D0(frame, address, resources, inputs),
            AlternateCallback => AdvanceAlternate638BD0(frame, address, resources, inputs),
            CleanupCallback => AdvanceCleanup639170(frame, address, inputs),
            _ => NativeBattleEffect209Lower.Advance(frame, address, resources, inputs)
        };
    }

    public static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)(actor * 156)) : throw new InvalidDataException("Effect209 requires an actual physical model.");
    private static void RequireBinding(NativeBattleEffect209Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect209 requires its selected entry.");
        }
    }
}
