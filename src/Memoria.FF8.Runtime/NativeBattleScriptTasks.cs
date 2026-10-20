using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScriptTask([property: JsonRequired] ImmutableArray<byte> Bytes, [property: JsonRequired] ushort KnownBytes, [property: JsonRequired] BattleEvent? Message)
{
    public byte ReadByte(int offset)
    {
        RequireKnown(offset, 1);
        return Bytes[offset];
    }

    public ushort ReadWord(int offset)
    {
        RequireKnown(offset, 2);
        return BinaryPrimitives.ReadUInt16LittleEndian(Bytes.AsSpan().Slice(offset));
    }

    public uint ReadDword(int offset)
    {
        RequireKnown(offset, 4);
        return BinaryPrimitives.ReadUInt32LittleEndian(Bytes.AsSpan().Slice(offset));
    }

    public NativeBattleScriptTask WriteByte(int offset, byte value)
    {
        return this with { Bytes = Bytes.SetItem(offset, value), KnownBytes = (ushort)(KnownBytes | 1 << offset) };
    }

    public NativeBattleScriptTask WriteWord(int offset, ushort value)
    {
        byte[] bytes = Bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
        return this with { Bytes = bytes.ToImmutableArray(), KnownBytes = (ushort)(KnownBytes | 3 << offset) };
    }

    public NativeBattleScriptTask WriteDword(int offset, uint value)
    {
        byte[] bytes = Bytes.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        return this with { Bytes = bytes.ToImmutableArray(), KnownBytes = (ushort)(KnownBytes | 15 << offset) };
    }

    private void RequireKnown(int offset, int count)
    {
        int mask = ((1 << count) - 1) << offset;
        if (offset < 0 || offset + count > 16 || (KnownBytes & mask) != mask)
        {
            throw new NotSupportedException($"Native script task reads unowned retained bytes{offset}..{offset + count - 1}.");
        }
    }
}

public sealed record NativeBattleScriptTaskLink([property: JsonRequired] byte Previous, [property: JsonRequired] byte Next, [property: JsonRequired] byte Byte2, [property: JsonRequired] byte? Byte3);

/// <summary>482D10's sixteen4-byte links and sixteen retained16-byte script callback records.</summary>
public sealed record NativeBattleScriptTasks([property: JsonRequired] ImmutableArray<NativeBattleScriptTaskLink> Links, [property: JsonRequired] ImmutableArray<NativeBattleScriptTask> Slots, [property: JsonRequired] byte Tail)
{
    public static NativeBattleScriptTasks Clear()
    {
        //482D10 clears links0..2 and completedBYTE15 only. Other retained payload bytes remain explicitly unknown.
        return new(Enumerable.Range(0, 16).Select(_ => new NativeBattleScriptTaskLink(0, 0, 0, null)).ToImmutableArray(), Enumerable.Range(0, 16).Select(_ => new NativeBattleScriptTask(ImmutableArray.CreateRange(new byte[16]), 0x8000, null)).ToImmutableArray(), 255);
    }

    public void Validate()
    {
        if (Links.IsDefault || Links.Length != 16 || Slots.IsDefault || Slots.Length != 16 || Tail != 255 && Tail >= 16 || Links.Any(link => link is null || link.Previous != 255 && link.Previous >= 16 || link.Next != 255 && link.Next >= 16) || Slots.Any(task => task is null || task.Bytes.IsDefault || task.Bytes.Length != 16 || (task.KnownBytes & 0x8000) == 0))
        {
            throw new InvalidDataException("Native script callbacks require their sixteen physical link/payload records.");
        }
        for (int index = 0; index < 16; index++)
        {
            if (Allocated(index))
            {
                _ = Slots[index].ReadDword(0);
            }
        }
    }

    public bool Allocated(int index)
    {
        return Links[index].Previous != 0 || Links[index].Next != 0;
    }

    public (NativeBattleScriptTasks State, int Slot) Allocate(uint callback)
    {
        Validate();
        int slot = 0;
        while (slot < 16 && Allocated(slot))
        {
            slot++;
        }
        //482C90 deliberately chooses0 if all sixteen slots are occupied; retain that native alias rather than growing a list.
        if (slot == 16)
        {
            slot = 0;
        }
        ImmutableArray<NativeBattleScriptTaskLink> links = Links.SetItem(slot, Links[slot] with { Previous = Tail, Next = 255, Byte2 = 0 });
        if (Tail != 255)
        {
            links = links.SetItem(Tail, links[Tail] with { Next = (byte)slot });
        }
        return (this with { Links = links, Tail = (byte)slot, Slots = Slots.SetItem(slot, Slots[slot].WriteDword(0, callback)) }, slot);
    }

    public NativeBattleScriptTasks AppendWait(ushort callbacks)
    {
        (NativeBattleScriptTasks state, int slot) = Allocate(0x48ACD0);
        return state with { Slots = state.Slots.SetItem(slot, state.Slots[slot].WriteWord(8, callbacks)) };
    }

    public NativeBattleScriptTasks AppendMessage(BattleEvent message, bool delayed, ushort callbacks)
    {
        (NativeBattleScriptTasks state, int slot) = Allocate(delayed ? 0x48AC90u : 0x48AC60u);
        NativeBattleScriptTask task = state.Slots[slot] with { KnownBytes = (ushort)(state.Slots[slot].KnownBytes & ~0xF0), Message = message };
        if (delayed)
        {
            task = task.WriteWord(8, callbacks);
        }
        return state with { Slots = state.Slots.SetItem(slot, task) };
    }

    public NativeBattleScriptTasks Advance(Func<NativeBattleScriptTasks, int, NativeBattleScriptTasks> callback, Action<int>? observeRelease = null)
    {
        Validate();
        NativeBattleScriptTasks state = this;
        int head = -1;
        for (int slot = 0; slot < 16; slot++)
        {
            if (state.Links[slot].Previous == 255)
            {
                head = slot;
                break;
            }
        }
        if (head >= 0)
        {
            HashSet<int> visited = [];
            for (int index = head; index != 255;)
            {
                if (!visited.Add(index))
                {
                    throw new NotSupportedException("Native script task links repeat during one callback pass.");
                }
                state = callback(state, index);
                if (state.Tail == 255)
                {
                    break;
                }
                index = state.Links[index].Next;
            }
        }
        //482DC0 scans physical completion bytes only after482D50 has invoked every linked callback.
        for (int slot = 0; slot < 16; slot++)
        {
            if (state.Slots[slot].ReadByte(15) == 1)
            {
                observeRelease?.Invoke(slot);
                state = state with { Slots = state.Slots.SetItem(slot, state.Slots[slot].WriteByte(15, 0)) };
                state = state.Release(slot);
            }
        }
        return state;
    }

    private NativeBattleScriptTasks Release(int slot)
    {
        NativeBattleScriptTaskLink link = Links[slot];
        ImmutableArray<NativeBattleScriptTaskLink> links = Links;
        if (link.Previous != 255)
        {
            links = links.SetItem(link.Previous, links[link.Previous] with { Next = link.Next });
        }
        byte tail = Tail;
        if (link.Next == 255)
        {
            tail = link.Previous;
        }
        else
        {
            links = links.SetItem(link.Next, links[link.Next] with { Previous = link.Previous });
        }
        links = links.SetItem(slot, links[slot] with { Previous = 0, Next = 0, Byte2 = 0 });
        return this with { Links = links, Tail = tail };
    }
}
