using static Memoria.FF8.Runtime.NativeBattleEffect174Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect174Controller
{
    public static NativeBattleEffect174Frame Initialize(NativeBattleEffect174Frame frame, NativeBattleEffect174Resources resources, NativeBattleEffect174Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame.State.Validate();
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Effect174 entry requires its inactive fixed banks.");
        }
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect174 requires its current parameters.");
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int actor = parameters.Actor78;
        int target = packets.ReadContact(header.FirstContactIndex ?? throw new NotSupportedException("Effect174 requires its actual first contact pointer.")).Primary.ActorIndex;
        int textureOffset = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect174 requires its actual executed initializer pointer.");
        uint source = checked(CommonAddress + (uint)textureOffset);
        frame = frame with { State = frame.State.WriteDword(InitializerGlobal, source).WriteDword(ArenaGlobal, CommonAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, (uint)actor).WriteDword(TargetGlobal, (uint)target) };
        NativeBattleEffectTaskPool root = InitializeBank(frame, 0);
        NativeBattleEffectTaskPool child = InitializeBank(frame, 1);
        (root, int rootSlot) = root.Append(RootCallback);
        root = root with { Slots = root.Slots.SetItem(rootSlot, root.Slots[rootSlot].WriteWord(12, 0)) };
        (child, int mainSlot) = child.Append(MainCallback);
        child = child with { Slots = child.Slots.SetItem(mainSlot, child.Slots[mainSlot].WriteWord(12, 0)) };
        frame = frame with { State = frame.State.DetachBank(0).DetachBank(1), Effect = frame.Effect with { Pools = [root, child], ReturnedDescriptor = RootDescriptor } };
        for (int index = 0; index < FirstRecordCount; index++)
        {
            frame = WriteBytes(frame, FirstRecordsAddress + (uint)(index * FirstRecordStride), NativeBattleEffectArena.Clear(), 4);
        }
        frame = inputs.Execute(frame, new NativeBattleEffect174RegisterCamera5099A0(CameraAddress));
        return inputs.Execute(frame, new NativeBattleEffect174UploadTim505E30(source));
    }

    private static NativeBattleEffectTaskPool InitializeBank(NativeBattleEffect174Frame frame, int index)
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

    public static NativeBattleEffect174Frame Advance(NativeBattleEffect174Frame frame, NativeBattleEffect174Resources resources, NativeBattleEffect174Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 2)
        {
            throw new InvalidDataException("Effect174 requires its two exclusive active banks.");
        }
        for (int index = 0; index < 2; index++)
        {
            NativeBattleCureFamilyPool shape = Pools[index];
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            if (pool.DescriptorAddress != shape.Descriptor || pool.BankAddress != shape.Bank || pool.Stride != shape.Stride || pool.Slots.Length != shape.Capacity)
            {
                throw new InvalidDataException("Effect174 fixed bank dimensions changed.");
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

    public static (NativeBattleEffect174Frame Frame, int Survivors) AdvancePool(NativeBattleEffect174Frame frame, int index, NativeBattleEffect174Resources resources, NativeBattleEffect174Inputs inputs)
    {
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            frame = WithPool(frame, index, pool);
            NativeBattleEffect174Step advanced = AdvanceTask(frame, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            frame = advanced.Frame;
            return new(frame.Effect.Pools[index], advanced.Result);
        });
        return (WithPool(frame, index, step.Pool), step.Survivors);
    }

    public static NativeBattleEffect174Step AdvanceTask(NativeBattleEffect174Frame frame, uint address, NativeBattleEffect174Resources resources, NativeBattleEffect174Inputs inputs) => Read(frame, address).Callback switch
    {
        RootCallback => AdvanceRoot5D7440(frame, address, resources, inputs),
        MainCallback => AdvanceMain5D4660(frame, address, resources, inputs),
        CleanupCallback => AdvanceCleanup5D7400(frame, address, inputs),
        _ => NativeBattleEffect174Lower.Advance(frame, address, resources, inputs)
    };

    public static NativeBattleEffect174Step AdvanceRoot5D7440(NativeBattleEffect174Frame frame, uint address, NativeBattleEffect174Resources resources, NativeBattleEffect174Inputs inputs)
    {
        uint bank = frame.State.ReadDword(ArenaGlobal) + ((Read(frame, address).ReadByte(12) & 1) != 0 ? 0xC000u : 0u);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, bank) };
        (frame, int survivors) = AdvancePool(frame, 1, resources, inputs);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1))));
        return new(frame, survivors == 0 ? 2 : 0);
    }

    public static NativeBattleEffect174Step AdvanceCleanup5D7400(NativeBattleEffect174Frame frame, uint address, NativeBattleEffect174Inputs inputs)
    {
        if (Read(frame, address).ReadWord(12) == 1)
        {
            frame = Write(frame, address, Read(frame, address).WriteWord(14, 0));
            frame = inputs.Execute(frame, new NativeBattleEffect174AppendCommonTextureLoad508630(frame.State.ReadDword(ArenaGlobal) + 0x20000u, address + 14));
        }
        bool completed = Read(frame, address).ReadWord(14) != 0;
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1))));
        return new(frame, completed ? 2 : 0);
    }

    public static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)(actor * 156)) : throw new InvalidDataException("Effect174 requires an actual physical model.");
    private static void RequireBinding(NativeBattleEffect174Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect174 requires its selected entry.");
        }
    }
}
