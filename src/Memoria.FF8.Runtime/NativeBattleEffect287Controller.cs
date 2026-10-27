using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect287Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect287Controller
{
    public static NativeBattleEffect287Frame Initialize(NativeBattleEffect287Frame frame, NativeBattleEffect287Resources resources, NativeBattleEffect287Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame.State.Validate();
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Effect287 entry requires its inactive root bank.");
        }
        frame = inputs.Execute(frame, new NativeBattleEffect287UploadTim505E30(FirstTimAddress));
        frame = inputs.Execute(frame, new NativeBattleEffect287UploadTim505E30(SecondTimAddress));
        frame = frame with { State = frame.State.WriteDword(ArenaGlobal, CommonAddress) };
        frame = frame with { Random = frame.Random.Draw(out ushort initialRandom), State = frame.State.WriteDword(RandomGlobal, initialRandom) };
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect287 requires its actual parameters.");
        frame = frame with { State = frame.State.WriteDword(CasterGlobal, ModelAddress(parameters.Actor78)).WriteDword(ParametersGlobal, 0x1D99A78) };
        NativeBattleEffectTaskPool root = CreatePool(frame, Pools[0], false);
        (root, _) = root.Append(RootCallback);
        frame = frame with { State = frame.State.DetachBank(0), Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor } };
        frame = ClearDwords701200(frame, RootBank + 12, 2);
        return inputs.Execute(frame, new NativeBattleEffect287RegisterCamera5099A0(CameraAddress));
    }

    public static NativeBattleEffect287Frame Advance(NativeBattleEffect287Frame frame, NativeBattleEffect287Resources resources, NativeBattleEffect287Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        (frame, int survivors) = AdvancePool(frame, 0, resources, inputs);
        if (survivors != 0)
        {
            return frame;
        }
        frame = frame with { State = frame.State.AttachBank(0, NativeBattleEffectTaskMemory.CaptureBank(frame.Effect.Pools[0])) };
        foreach (uint descriptor in frame.Effect.Pools.Skip(1).Select(pool => pool.DescriptorAddress).ToArray())
        {
            frame = DetachCommonBank(frame, descriptor);
        }
        return frame with { Effect = frame.Effect with { Pools = [], ReturnedDescriptor = 0 } };
    }

    public static (NativeBattleEffect287Frame Frame, int Survivors) AdvancePool(NativeBattleEffect287Frame frame, int index, NativeBattleEffect287Resources resources, NativeBattleEffect287Inputs inputs)
    {
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            frame = WithPool(frame, index, pool);
            NativeBattleEffect287Step result = AdvanceTask(frame, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            frame = result.Frame;
            return new(frame.Effect.Pools[index], result.Result);
        });
        return (WithPool(frame, index, step.Pool), step.Survivors);
    }

    public static NativeBattleEffect287Step AdvanceTask(NativeBattleEffect287Frame frame, uint address, NativeBattleEffect287Resources resources, NativeBattleEffect287Inputs inputs) => Read(frame, address).Callback switch
    {
        RootCallback => AdvanceRoot6F2F20(frame, address, resources, inputs),
        MainCallback => AdvanceMain6F31A0(frame, address, resources, inputs),
        _ => NativeBattleEffect287Lower.Advance(frame, address, resources, inputs)
    };

    private static NativeBattleEffectTaskPool CreatePool(NativeBattleEffect287Frame frame, NativeBattleCureFamilyPool shape, bool common)
    {
        uint bank = common ? checked(frame.State.ReadDword(ArenaGlobal) + shape.Bank) : shape.Bank;
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, bank, shape.Stride, shape.Capacity);
        NativeBattleEffectArena source = common ? frame.Memory.Arena ?? throw new NotSupportedException("Effect287 requires current COMMON.") : frame.State.ReadBank(0);
        int start = common ? checked((int)(bank - CommonAddress)) : 0;
        for (int slot = 0; slot < shape.Capacity; slot++)
        {
            int offset = slot * shape.Stride + 12;
            pool = NativeBattleEffectTaskMemory.WriteBankSpan(pool, offset, CopyBytes(source, start + offset, NativeBattleEffectArena.Clear(), 0, shape.Stride - 12), shape.Stride - 12);
        }
        return pool;
    }

    public static (NativeBattleEffect287Frame Frame, int Survivors) AdvanceDescriptor(NativeBattleEffect287Frame frame, uint descriptor, NativeBattleEffect287Resources resources, NativeBattleEffect287Inputs inputs)
    {
        int index = PoolIndex(frame, descriptor);
        if (index >= 0)
        {
            return AdvancePool(frame, index, resources, inputs);
        }
        if (descriptor == MeshDescriptor)
        {
            if (System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(frame.State.ReadMeshQueue().Read(0, 4)) != 0)
            {
                throw new NotSupportedException("Effect287 cannot schedule an inactive nonempty mesh queue.");
            }
            return (frame, 0);
        }
        throw new InvalidDataException("Effect287 requires its actual scheduled descriptor.");
    }

    public static NativeBattleEffect287Frame ClearMeshQueue(NativeBattleEffect287Frame frame)
    {
        int index = PoolIndex(frame, MeshDescriptor);
        if (index < 0)
        {
            throw new InvalidDataException("Effect287 requires its mesh pool at the native unlink.");
        }
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[index].UpdateLinks(frame.Effect.Pools[index].Head, null);
        pool = pool.UpdateLinks(null, pool.Tail);
        frame = WithPool(frame, index, pool);
        return DetachCommonBank(frame, MeshDescriptor);
    }

    private static NativeBattleEffect287Frame DetachCommonBank(NativeBattleEffect287Frame frame, uint descriptor)
    {
        NativeBattleEffectArena? queue = descriptor == MeshDescriptor ? MeshQueue(frame) : null;
        NativeBattleEffectTaskTransfer transfer = NativeBattleEffectTaskMemory.DetachArenaBank(frame.Memory, frame.Effect, descriptor);
        frame = frame with { Memory = transfer.Memory, Effect = transfer.Effect };
        return queue is null ? frame : frame with { State = frame.State.AttachMeshQueue(queue) };
    }

    private static NativeBattleEffectArena MeshQueue(NativeBattleEffect287Frame frame)
    {
        int index = PoolIndex(frame, MeshDescriptor);
        if (index < 0)
        {
            return frame.State.ReadMeshQueue();
        }
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
        uint head = pool.Head is int first ? checked(pool.BankAddress + (uint)(first * pool.Stride)) : 0;
        uint tail = pool.Tail is int last ? checked(pool.BankAddress + (uint)(last * pool.Stride)) : 0;
        return NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(head)).Write(4, BitConverter.GetBytes(tail));
    }

    private static int PoolIndex(NativeBattleEffect287Frame frame, uint descriptor)
    {
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            if (frame.Effect.Pools[index].DescriptorAddress == descriptor)
            {
                return index;
            }
        }
        return -1;
    }

    public static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)(actor * 156)) : throw new InvalidDataException("Effect287 requires an actual physical model row.");
    private static void RequireBinding(NativeBattleEffect287Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect287 requires its selected entry.");
        }
    }
}
