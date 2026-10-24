using static Memoria.FF8.Runtime.NativeBattleEffect166Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect166Controller
{
    public static NativeBattleEffect166Frame Initialize(NativeBattleEffect166Frame frame, NativeBattleEffect166Resources resources, NativeBattleEffect166Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame.State.Validate();
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Effect166 entry requires its inactive fixed banks.");
        }
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect166 requires its current parameters.");
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int actor = parameters.Actor78;
        int target = packets.ReadContact(header.FirstContactIndex ?? throw new NotSupportedException("Effect166 requires its actual first contact pointer.")).Primary.ActorIndex;
        int textureOffset = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect166 requires its actual executed initializer pointer.");
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
        for (int index = 0; index < SecondRecordCount; index++)
        {
            frame = WriteBytes(frame, SecondRecordsAddress + (uint)(index * SecondRecordStride), NativeBattleEffectArena.Clear(), 4);
        }
        frame = inputs.Execute(frame, new NativeBattleEffect166RegisterCamera5099A0(CameraAddress));
        return inputs.Execute(frame, new NativeBattleEffect166UploadTim505E30(source));
    }

    private static NativeBattleEffectTaskPool InitializeBank(NativeBattleEffect166Frame frame, int index)
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

    public static NativeBattleEffect166Frame Advance(NativeBattleEffect166Frame frame, NativeBattleEffect166Resources resources, NativeBattleEffect166Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 2)
        {
            throw new InvalidDataException("Effect166 requires its two exclusive active banks.");
        }
        for (int index = 0; index < 2; index++)
        {
            NativeBattleCureFamilyPool shape = Pools[index];
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            if (pool.DescriptorAddress != shape.Descriptor || pool.BankAddress != shape.Bank || pool.Stride != shape.Stride || pool.Slots.Length != shape.Capacity)
            {
                throw new InvalidDataException("Effect166 fixed bank dimensions changed.");
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

    public static (NativeBattleEffect166Frame Frame, int Survivors) AdvancePool(NativeBattleEffect166Frame frame, int index, NativeBattleEffect166Resources resources, NativeBattleEffect166Inputs inputs)
    {
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            frame = WithPool(frame, index, pool);
            NativeBattleEffect166Step advanced = AdvanceTask(frame, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            frame = advanced.Frame;
            return new(frame.Effect.Pools[index], advanced.Result);
        });
        return (WithPool(frame, index, step.Pool), step.Survivors);
    }

    public static NativeBattleEffect166Step AdvanceTask(NativeBattleEffect166Frame frame, uint address, NativeBattleEffect166Resources resources, NativeBattleEffect166Inputs inputs) => Read(frame, address).Callback switch
    {
        RootCallback => AdvanceRoot5E93A0(frame, address, resources, inputs),
        MainCallback => AdvanceMain5E4F60(frame, address, resources, inputs),
        CleanupCallback => AdvanceCleanup5E9410(frame, address, inputs),
        _ => NativeBattleEffect166Lower.Advance(frame, address, resources, inputs)
    };

    public static NativeBattleEffect166Step AdvanceRoot5E93A0(NativeBattleEffect166Frame frame, uint address, NativeBattleEffect166Resources resources, NativeBattleEffect166Inputs inputs)
    {
        uint bank = frame.State.ReadDword(ArenaGlobal) + ((Read(frame, address).ReadByte(12) & 1) != 0 ? 0xC000u : 0u);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, bank) };
        (frame, int survivors) = AdvancePool(frame, 1, resources, inputs);
        if (survivors == 0)
        {
            (frame, uint cleanup) = Append(frame, RootDescriptor, CleanupCallback);
            if (cleanup == 0)
            {
                throw new InvalidDataException("Effect166 requires its cleanup task slot.");
            }
            frame = Write(frame, cleanup, Read(frame, cleanup).WriteWord(12, 0).WriteWord(14, 0));
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1))));
        return new(frame, survivors == 0 ? 2 : 0);
    }

    public static NativeBattleEffect166Step AdvanceCleanup5E9410(NativeBattleEffect166Frame frame, uint address, NativeBattleEffect166Inputs inputs)
    {
        if (Read(frame, address).ReadWord(12) == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect166CommonTextureLoad508630(frame.State.ReadDword(ArenaGlobal), address + 14));
        }
        ushort completed = Read(frame, address).ReadWord(14);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1))));
        return new(frame, completed != 0 ? 2 : 0);
    }

    public static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)(actor * 156)) : throw new InvalidDataException("Effect166 requires an actual physical model.");
    private static void RequireBinding(NativeBattleEffect166Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect166 requires its selected entry.");
        }
    }
}
