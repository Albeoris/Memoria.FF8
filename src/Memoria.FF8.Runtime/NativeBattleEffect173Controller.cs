using static Memoria.FF8.Runtime.NativeBattleEffect173Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect173Controller
{
    public static NativeBattleEffect173Frame Initialize(NativeBattleEffect173Frame frame, NativeBattleEffect173Resources resources, NativeBattleEffect173Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame.State.Validate();
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Effect173 entry requires its inactive fixed banks.");
        }
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect173 requires its current parameters.");
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int actor = parameters.Actor78;
        int target = packets.ReadContact(header.FirstContactIndex ?? throw new NotSupportedException("Effect173 requires its actual first contact pointer.")).Primary.ActorIndex;
        int textureOffset = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect173 requires its actual executed initializer pointer.");
        uint source = checked(CommonAddress + (uint)textureOffset);
        frame = frame with { State = frame.State.WriteDword(InitializerGlobal, source).WriteDword(ArenaGlobal, CommonAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, (uint)actor).WriteDword(TargetGlobal, (uint)target) };
        NativeBattleEffectTaskPool root = InitializeBank(frame, 0);
        NativeBattleEffectTaskPool child = InitializeBank(frame, 1);
        (root, int rootSlot) = root.Append(RootCallback);
        root = root.WriteSlot(rootSlot, root.Slots[rootSlot].WriteWord(12, 0));
        (child, int mainSlot) = child.Append(MainCallback);
        child = child.WriteSlot(mainSlot, child.Slots[mainSlot].WriteWord(12, 0));
        frame = frame with { State = frame.State.DetachBank(0).DetachBank(1), Effect = frame.Effect with { Pools = [root, child], ReturnedDescriptor = RootDescriptor } };
        for (int index = 0; index < FirstRecordCount; index++)
        {
            frame = WriteBytes(frame, FirstRecordsAddress + (uint)(index * FirstRecordStride), NativeBattleEffectArena.Clear(), 4);
        }
        frame = inputs.Execute(frame, new NativeBattleEffect173RegisterCamera5099A0(CameraAddress));
        return inputs.Execute(frame, new NativeBattleEffect173UploadTim505E30(source));
    }

    private static NativeBattleEffectTaskPool InitializeBank(NativeBattleEffect173Frame frame, int index)
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

    public static NativeBattleEffect173Frame Advance(NativeBattleEffect173Frame frame, NativeBattleEffect173Resources resources, NativeBattleEffect173Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is < 2 or > 3)
        {
            throw new InvalidDataException("Effect173 requires its two exclusive active banks.");
        }
        for (int index = 0; index < 2; index++)
        {
            NativeBattleCureFamilyPool shape = Pools[index];
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            if (pool.DescriptorAddress != shape.Descriptor || pool.BankAddress != shape.Bank || pool.Stride != shape.Stride || pool.Slots.Length != shape.Capacity)
            {
                throw new InvalidDataException("Effect173 fixed bank dimensions changed.");
            }
            pool.Validate();
        }
        (frame, int survivors) = AdvancePool(frame, 0, resources, inputs);
        if (survivors != 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length == 3)
        {
            frame = frame with { State = frame.State.AttachCommonQueue(ReadBytes(frame, CommonDescriptor, 8)) };
            NativeBattleEffectTaskTransfer transfer = NativeBattleEffectTaskMemory.DetachArenaBank(frame.Memory, frame.Effect, CommonDescriptor);
            frame = frame with { Memory = transfer.Memory, Effect = transfer.Effect };
        }
        for (int index = 0; index < 2; index++)
        {
            frame = frame with { State = frame.State.AttachBank(index, NativeBattleEffectTaskMemory.CaptureBank(frame.Effect.Pools[index])) };
        }
        return frame with { Effect = frame.Effect with { Pools = [], ReturnedDescriptor = 0 } };
    }

    public static (NativeBattleEffect173Frame Frame, int Survivors) AdvancePool(NativeBattleEffect173Frame frame, int index, NativeBattleEffect173Resources resources, NativeBattleEffect173Inputs inputs)
    {
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            frame = WithPool(frame, index, pool);
            NativeBattleEffect173Step advanced = AdvanceTask(frame, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            frame = advanced.Frame;
            return new(frame.Effect.Pools[index], advanced.Result);
        });
        return (WithPool(frame, index, step.Pool), step.Survivors);
    }

    public static NativeBattleEffect173Step AdvanceTask(NativeBattleEffect173Frame frame, uint address, NativeBattleEffect173Resources resources, NativeBattleEffect173Inputs inputs) => Read(frame, address).Callback switch
    {
        RootCallback => AdvanceRoot5D9BF0(frame, address, resources, inputs),
        MainCallback => AdvanceMain5D7570(frame, address, resources, inputs),
        CleanupCallback => AdvanceCleanup5D9C70(frame, address, inputs),
        0x5D90B0 => NativeBattleEffect173Programme.Advance90B0(frame, address, resources, inputs),
        _ => NativeBattleEffect173Lower.Advance(frame, address, resources, inputs)
    };

    public static NativeBattleEffect173Frame InitializeCommonBank(NativeBattleEffect173Frame frame)
    {
        if (frame.Effect.Pools.Length == 3)
        {
            frame = frame with { State = frame.State.AttachCommonQueue(ReadBytes(frame, CommonDescriptor, 8)) };
            NativeBattleEffectTaskTransfer transfer = NativeBattleEffectTaskMemory.DetachArenaBank(frame.Memory, frame.Effect, CommonDescriptor);
            frame = frame with { Memory = transfer.Memory, Effect = transfer.Effect };
        }
        frame.State.ReadCommonQueue().Read(0, 8);
        uint bankAddress = checked(frame.State.ReadDword(ArenaGlobal) + CommonBankOffset);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(CommonDescriptor, bankAddress, CommonStride, CommonCount);
        NativeBattleEffectArena bank = ReadBytes(frame, bankAddress, CommonStride * CommonCount);
        for (int slot = 0; slot < CommonCount; slot++)
        {
            int offset = slot * CommonStride + 12;
            pool = NativeBattleEffectTaskMemory.WriteBankSpan(pool, offset, CopyBytes(bank, offset, NativeBattleEffectArena.Clear(), 0, CommonStride - 12), CommonStride - 12);
        }
        return frame with { State = frame.State.DetachCommonQueue(), Effect = frame.Effect with { Pools = frame.Effect.Pools.Add(pool) }, Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, [pool]) };
    }

    private static NativeBattleEffect173Frame RestoreCommonBank(NativeBattleEffect173Frame frame)
    {
        uint bankAddress = checked(frame.State.ReadDword(ArenaGlobal) + CommonBankOffset);
        NativeBattleEffectArena queue = frame.State.ReadCommonQueue();
        NativeBattleEffectArena bytes = ReadBytes(frame, bankAddress, CommonStride * CommonCount);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(CommonDescriptor, bankAddress, CommonStride, CommonCount);
        pool = NativeBattleEffectTaskMemory.WriteBankSpan(pool, 0, bytes, CommonStride * CommonCount);
        pool = pool.UpdateLinks(Slot(System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(queue.Read(0, 4))), Slot(System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(queue.Read(4, 4))));
        pool.Validate();
        return frame with { State = frame.State.DetachCommonQueue(), Effect = frame.Effect with { Pools = frame.Effect.Pools.Add(pool) }, Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, [pool]) };

        int? Slot(uint pointer)
        {
            if (pointer == 0)
            {
                return null;
            }
            long offset = (long)pointer - bankAddress;
            if (offset < 0 || offset >= CommonStride * CommonCount || offset % CommonStride != 0)
            {
                throw new InvalidDataException("Effect173 retained COMMON link leaves its actual bank.");
            }
            return checked((int)(offset / CommonStride));
        }
    }

    public static NativeBattleEffect173Step AdvanceRoot5D9BF0(NativeBattleEffect173Frame frame, uint address, NativeBattleEffect173Resources resources, NativeBattleEffect173Inputs inputs)
    {
        uint bank = frame.State.ReadDword(ArenaGlobal) + ((Read(frame, address).ReadByte(12) & 1) != 0 ? 0xC000u : 0u);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, bank) };
        (frame, int survivors) = AdvancePool(frame, 1, resources, inputs);
        if (frame.Effect.Pools.Length == 2 && System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(frame.State.ReadCommonQueue().Read(0, 4)) != 0)
        {
            frame = RestoreCommonBank(frame);
        }
        if (frame.Effect.Pools.Length == 3)
        {
            (frame, _) = AdvancePool(frame, 2, resources, inputs);
        }
        if (survivors == 0)
        {
            (frame, uint cleanup) = Append(frame, RootDescriptor, CleanupCallback);
            frame = Write(frame, cleanup, Read(frame, cleanup).WriteWord(12, 0).WriteWord(14, 0));
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1))));
        return new(frame, survivors == 0 ? 2 : 0);
    }

    public static NativeBattleEffect173Step AdvanceCleanup5D9C70(NativeBattleEffect173Frame frame, uint address, NativeBattleEffect173Inputs inputs)
    {
        if (Read(frame, address).ReadWord(12) == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect173AppendCommonTextureLoad508630(frame.State.ReadDword(ArenaGlobal), address + 14));
        }
        bool completed = Read(frame, address).ReadWord(14) != 0;
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1))));
        return new(frame, completed ? 2 : 0);
    }

    public static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)(actor * 156)) : throw new InvalidDataException("Effect173 requires an actual physical model.");
    private static void RequireBinding(NativeBattleEffect173Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect173 requires its selected entry.");
        }
    }
}
