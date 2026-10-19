using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>One original508300 task: WORD flags/state, physical link, callback and retained suffix.</summary>
public sealed partial record NativeBattleModulePoolTask([property: JsonRequired] ushort Flags, [property: JsonRequired] ushort State, [property: JsonRequired] int? Next, [property: JsonRequired] uint Callback, [property: JsonRequired] ImmutableArray<byte> Payload)
{
    public ushort ReadWord(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(Payload.AsSpan().Slice(offset - 12));

    public NativeBattleModulePoolTask WriteWord(int offset, ushort value)
    {
        byte[] payload = Payload.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset - 12), value);
        return (this with { Payload = payload.ToImmutableArray() }).WithoutResourcePointers(offset, 2);
    }
}

public sealed record NativeBattleEffectTaskCallback(NativeBattleEffectTaskPool Pool, int Result);
public sealed record NativeBattleEffectTaskStep(NativeBattleEffectTaskPool Pool, int Survivors);

/// <summary>508300/508360/508420's module-local descriptor and physical task bank.</summary>
public sealed record NativeBattleEffectTaskPool([property: JsonRequired] uint DescriptorAddress, [property: JsonRequired] uint BankAddress, [property: JsonRequired] ushort Stride, [property: JsonRequired] ImmutableArray<NativeBattleModulePoolTask> Slots, [property: JsonRequired] int? Head, [property: JsonRequired] int? Tail)
{
    public static NativeBattleEffectTaskPool Initialize(uint descriptorAddress, uint bankAddress, ushort stride, ushort capacity)
    {
        if (stride < 12 || capacity == 0 || (descriptorAddress & 3) != 0 || (bankAddress & 3) != 0)
        {
            throw new ArgumentException("Native508300 requires its aligned descriptor/bank and physical task dimensions.");
        }
        _ = checked(bankAddress + (uint)stride * capacity);
        return new(descriptorAddress, bankAddress, stride, Enumerable.Range(0, capacity).Select(_ => new NativeBattleModulePoolTask(0, 0, null, 0, ImmutableArray.CreateRange(new byte[stride - 12]))).ToImmutableArray(), null, null);
    }

    public void Validate()
    {
        if (Stride < 12 || Slots.IsDefault || Slots.Length is < 1 or > 65535 || (DescriptorAddress & 3) != 0 || (BankAddress & 3) != 0 || Head is int head && (uint)head >= Slots.Length || Tail is int tail && (uint)tail >= Slots.Length || Slots.Any(task => task is null || task.Payload.IsDefault || task.Payload.Length != Stride - 12 || task.Next is int next && (uint)next >= Slots.Length || (task.Flags & 1) != 0 && task.Callback == 0))
        {
            throw new InvalidDataException("Native effect task pool lost its physical descriptor, stride, slots or callback.");
        }
        foreach (NativeBattleModulePoolTask task in Slots)
        {
            task.ValidateResourcePointers();
        }
        HashSet<int> seen = [];
        int? last = null;
        for (int? next = Head; next is int index; next = Slots[index].Next)
        {
            if (!seen.Add(index) || (Slots[index].Flags & 1) == 0)
            {
                throw new InvalidDataException("Native effect task links contain a cycle or an unallocated slot.");
            }
            last = index;
        }
        if (last != Tail)
        {
            throw new InvalidDataException("Native effect task descriptor tail contradicts its linked physical bank.");
        }
    }

    public (NativeBattleEffectTaskPool Pool, int Slot) Append(uint callback)
    {
        Validate();
        if (callback == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(callback));
        }
        int index = -1;
        for (int candidate = 0; candidate < Slots.Length; candidate++)
        {
            if ((Slots[candidate].Flags & 1) == 0)
            {
                index = candidate;
                break;
            }
        }
        if (index < 0)
        {
            throw new InvalidOperationException("Native508360 exhausted its original effect task bank.");
        }
        ImmutableArray<NativeBattleModulePoolTask> slots = Slots.SetItem(index, Slots[index] with { Flags = (ushort)(Slots[index].Flags | 1), State = 0, Next = null, Callback = callback });
        if (Tail is int tail)
        {
            slots = slots.SetItem(tail, slots[tail] with { Next = index });
        }
        return (this with { Slots = slots, Head = Tail is null ? index : Head, Tail = index }, index);
    }

    public NativeBattleEffectTaskStep Advance(Func<NativeBattleEffectTaskPool, int, NativeBattleEffectTaskCallback> callback)
    {
        Validate();
        NativeBattleEffectTaskPool pool = this;
        int? previous = null;
        int survivors = 0;
        for (int? next = Head; next is int index;)
        {
            NativeBattleEffectTaskCallback step = callback(pool, index);
            pool = step.Pool;
            if (pool.DescriptorAddress != DescriptorAddress || pool.BankAddress != BankAddress || pool.Stride != Stride || pool.Slots.Length != Slots.Length)
            {
                throw new InvalidDataException("Native callback replaced its physical effect task bank.");
            }
            //508420 reads Next after the callback, including children appended during this same pass.
            next = pool.Slots[index].Next;
            if ((step.Result & 2) != 0)
            {
                ImmutableArray<NativeBattleModulePoolTask> slots = pool.Slots.SetItem(index, pool.Slots[index] with { Flags = 0 });
                if (previous is int before)
                {
                    slots = slots.SetItem(before, slots[before] with { Next = next });
                }
                pool = pool with { Slots = slots, Head = previous is null ? next : pool.Head };
            }
            else
            {
                previous = index;
                survivors++;
            }
        }
        pool = pool with { Tail = previous };
        pool.Validate();
        return new(pool, survivors);
    }
}

/// <summary>The independent original task descriptors initialized by6472E0 and58D760.</summary>
public sealed record NativeBattleGuardianEffectPools([property: JsonRequired] NativeBattleEffectTaskPool Main, [property: JsonRequired] NativeBattleEffectTaskPool Children, [property: JsonRequired] NativeBattleEffectTaskPool? Particles)
{
    public static NativeBattleGuardianEffectPools Odin()
    {
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x24FD6C0, 0x24FD6A0, 16, 2);
        (main, _) = main.Append(0x64DD50);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x24FD990, 0x24FD9A0, 36, 100);
        (children, _) = children.Append(0x6474C0);
        return new(main, children, null);
    }

    public static NativeBattleGuardianEffectPools Gilgamesh()
    {
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x21FF4A8, 0x21FF468, 16, 2);
        (main, _) = main.Append(0x596B70);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x21FFD58, 0x21FFD68, 36, 100);
        (children, _) = children.Append(0x592300);
        return new(main, children, NativeBattleEffectTaskPool.Initialize(0x21FF458, 0x21FF3C8, 36, 4));
    }
}
