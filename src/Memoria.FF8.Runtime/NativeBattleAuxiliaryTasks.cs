using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum NativeBattleAuxiliaryKind
{
    None,
    CameraBlend509930
}

public sealed record NativeBattleAuxiliaryTask([property: JsonRequired] ushort Flags, [property: JsonRequired] ushort State, [property: JsonRequired] int? Next, [property: JsonRequired] NativeBattleAuxiliaryKind Kind, [property: JsonRequired] ImmutableArray<byte> Payload);

public sealed record NativeBattleAuxiliaryStep(NativeBattleAuxiliaryState State, ushort? CameraBlend);

/// <summary>Shared506BF0 auxiliary list: sixteen44-byte slots, distinct from the twenty32-byte contact effects.</summary>
public sealed record NativeBattleAuxiliaryState([property: JsonRequired] ImmutableArray<NativeBattleAuxiliaryTask> Slots, [property: JsonRequired] int? Head, [property: JsonRequired] int? Tail)
{
    public static NativeBattleAuxiliaryState Clear()
    {
        return new(Enumerable.Range(0, 16).Select(_ => new NativeBattleAuxiliaryTask(0, 0, null, NativeBattleAuxiliaryKind.None, ImmutableArray.CreateRange(new byte[32]))).ToImmutableArray(), null, null);
    }

    public void Validate()
    {
        if (Slots.IsDefault || Slots.Length != 16 || Head is < 0 or >= 16 || Tail is < 0 or >= 16 || Slots.Any(task => task is null || task.Next is < 0 or >= 16 || !Enum.IsDefined(task.Kind) || task.Payload.IsDefault || task.Payload.Length != 32 || (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.None))
        {
            throw new InvalidDataException("Native auxiliary tasks require sixteen physical44-byte slots and their retained payloads.");
        }
        HashSet<int> visited = [];
        for (int? next = Head; next is int index; next = Slots[index].Next)
        {
            if (!visited.Add(index) || (Slots[index].Flags & 1) == 0)
            {
                throw new InvalidDataException("Native auxiliary task links contain a cycle or a free physical slot.");
            }
        }
    }

    public NativeBattleAuxiliaryState AppendCameraBlend(byte duration)
    {
        Validate();
        int slot = -1;
        for (int index = 0; index < Slots.Length; index++)
        {
            if ((Slots[index].Flags & 1) == 0)
            {
                slot = index;
                break;
            }
        }
        if (slot < 0)
        {
            //509810 dereferences506C10's result; the original has no successful allocation at exhaustion.
            throw new InvalidOperationException("Native camera blend exhausted the shared sixteen-slot auxiliary task pool.");
        }
        NativeBattleAuxiliaryTask task = Slots[slot];
        task = task with { Flags = (ushort)(task.Flags | 1), State = 0, Next = null, Kind = NativeBattleAuxiliaryKind.CameraBlend509930, Payload = task.Payload.SetItem(0, 0).SetItem(1, duration) };
        ImmutableArray<NativeBattleAuxiliaryTask> slots = Slots.SetItem(slot, task);
        if (Tail is int tail)
        {
            slots = slots.SetItem(tail, slots[tail] with { Next = slot });
        }
        return this with { Slots = slots, Head = Tail is null ? slot : Head, Tail = slot };
    }

    public NativeBattleAuxiliaryStep Advance()
    {
        Validate();
        ImmutableArray<NativeBattleAuxiliaryTask> slots = Slots;
        int? head = Head;
        int? previous = null;
        ushort? blend = null;
        for (int? next = Head; next is int index;)
        {
            NativeBattleAuxiliaryTask task = slots[index];
            byte frame = unchecked((byte)(task.Payload[0] + 1));
            byte duration = task.Payload[1];
            task = task with { Payload = task.Payload.SetItem(0, frame) };
            //509930 increments itsBYTE before signed IDIV; duration0 faults in the original too.
            int angle = (frame << 10) / duration;
            double value = Math.Sin(angle * (double)0.0015339808f) * (double)4096f + 4503601774854144d;
            blend = unchecked((ushort)((uint)BitConverter.DoubleToInt64Bits(value) + 0x80000000u));
            next = task.Next;
            if (frame >= duration)
            {
                slots = slots.SetItem(index, task with { Flags = 0 });
                if (previous is int retained)
                {
                    slots = slots.SetItem(retained, slots[retained] with { Next = next });
                }
                else
                {
                    head = next;
                }
            }
            else
            {
                slots = slots.SetItem(index, task);
                previous = index;
            }
        }
        return new(this with { Slots = slots, Head = head, Tail = previous }, blend);
    }
}
