using static Memoria.FF8.Runtime.NativeBattleEffect66Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect66Controller
{
    public static NativeBattleEffect66Frame Initialize(NativeBattleEffect66Frame frame, NativeBattleEffect66Resources resources, NativeBattleEffect66Inputs inputs)
    {
        RequireBinding(frame);
        frame.State.Validate();
        resources.Validate();
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Effect66 entry requires its inactive fixed banks.");
        }
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect66 requires its current parameters.");
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int actor = parameters.Actor78;
        int target = packets.ReadContact(header.FirstContactIndex ?? throw new NotSupportedException("Effect66 requires its actual first contact pointer.")).Primary.ActorIndex;
        int textureOffset = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect66 requires its actual executed initializer pointer.");
        frame = frame with { State = frame.State.WriteDword(InitializerGlobal, checked(CommonAddress + (uint)textureOffset)).WriteDword(ArenaGlobal, CommonAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, (uint)actor).WriteDword(TargetGlobal, (uint)target) };
        NativeBattleEffectTaskPool root = InitializeBank(frame, 0);
        NativeBattleEffectTaskPool child = InitializeBank(frame, 1);
        (root, int rootSlot) = root.Append(RootCallback);
        root = root.WriteSlot(rootSlot, root.Slots[rootSlot].WriteWord(12, 0));
        (child, int mainSlot) = child.Append(MainCallback);
        child = child.WriteSlot(mainSlot, child.Slots[mainSlot].WriteWord(12, 0));
        frame = frame with { State = frame.State.DetachBank(0).DetachBank(1), Effect = frame.Effect with { Pools = [root, child], ReturnedDescriptor = RootDescriptor } };
        for (int index = 0; index < FirstPointCount; index++)
        {
            frame = WriteBytes(frame, FirstPointAddress + (uint)(index * PointStride), NativeBattleEffectArena.Clear(), 4);
        }
        for (int index = 0; index < SecondPointCount; index++)
        {
            frame = WriteBytes(frame, SecondPointAddress + (uint)(index * PointStride), NativeBattleEffectArena.Clear(), 4);
        }
        return WriteBytes(frame, CapturedWorldAddress, PackedArena(inputs.ReadWorld(frame, actor)), 32);
    }

    private static NativeBattleEffectTaskPool InitializeBank(NativeBattleEffect66Frame frame, int index)
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

    public static NativeBattleEffect66Frame Advance(NativeBattleEffect66Frame frame, NativeBattleEffect66Resources resources, NativeBattleEffect66Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 2)
        {
            throw new InvalidDataException("Effect66 requires its two exclusive active banks.");
        }
        for (int index = 0; index < 2; index++)
        {
            NativeBattleCureFamilyPool shape = Pools[index];
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            if (pool.DescriptorAddress != shape.Descriptor || pool.BankAddress != shape.Bank || pool.Stride != shape.Stride || pool.Slots.Length != shape.Capacity)
            {
                throw new InvalidDataException("Effect66 fixed bank dimensions changed.");
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

    public static (NativeBattleEffect66Frame Frame, int Survivors) AdvancePool(NativeBattleEffect66Frame frame, int index, NativeBattleEffect66Resources resources, NativeBattleEffect66Inputs inputs)
    {
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            frame = WithPool(frame, index, pool);
            NativeBattleEffect66Step advanced = AdvanceTask(frame, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            frame = advanced.Frame;
            return new(frame.Effect.Pools[index], advanced.Result);
        });
        return (WithPool(frame, index, step.Pool), step.Survivors);
    }

    public static NativeBattleEffect66Step AdvanceTask(NativeBattleEffect66Frame frame, uint address, NativeBattleEffect66Resources resources, NativeBattleEffect66Inputs inputs) => Read(frame, address).Callback switch
    {
        RootCallback => AdvanceRoot627DF0(frame, address, resources, inputs),
        MainCallback => AdvanceMain6226E0(frame, address, resources, inputs),
        0x622D30 => AdvanceEnvelope622D30(frame, address, inputs),
        0x622E10 or 0x622E60 => AdvanceWhite(frame, address, inputs),
        0x63E9C0 => AdvanceCamera63E9C0(frame, address, inputs),
        uint callback => NativeBattleEffect66Lower.Advance(frame, callback, address, resources, inputs)
    };

    public static NativeBattleEffect66Step AdvanceRoot627DF0(NativeBattleEffect66Frame frame, uint address, NativeBattleEffect66Resources resources, NativeBattleEffect66Inputs inputs)
    {
        uint bank = frame.State.ReadDword(ArenaGlobal) + ((Read(frame, address).ReadByte(12) & 1) != 0 ? 0x10000u : 0u);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, bank) };
        (frame, int survivors) = AdvancePool(frame, 1, resources, inputs);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1))));
        return new(frame, survivors == 0 ? 2 : 0);
    }

    public static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)(actor * 156)) : throw new InvalidDataException("Effect66 requires an actual physical model.");
    private static void RequireBinding(NativeBattleEffect66Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect66 requires its selected entry.");
        }
    }
}
