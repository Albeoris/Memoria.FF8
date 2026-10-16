using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum NativeBattleEffectKind
{
    None,
    StatusReset506860,
    DamageText5069B0
}

/// <summary>Owned completion/linked-list fields of a32-byte1D98428 task; presentation coordinates have their own renderer owner.</summary>
public sealed record NativeBattleEffectTask(ushort Flags0, ushort State2, int? Next, NativeBattleEffectKind Kind)
{
    public byte ModelIndex { get; init; }
    public byte Frame { get; init; }
    public ImmutableArray<byte> Glyphs { get; init; } = [];
    public short HalfWidth { get; init; }
    public uint Color { get; init; }
}

public sealed record NativeBattleEffectsStep(NativeBattleEffectsState State, ImmutableArray<byte> ResetModelStatuses);

/// <summary>Exact20-slot allocator and published callback count from506BD0/508420/500900.</summary>
public sealed record NativeBattleEffectsState(ImmutableArray<NativeBattleEffectTask> Slots, int? Head, int? Tail, byte PublishedCount)
{
    public static NativeBattleEffectsState Clear()
    {
        //508300 clears all20*32 bytes, both descriptor links and initialBYTE1D96A88.
        return new(Enumerable.Range(0, 20).Select(_ => new NativeBattleEffectTask(0, 0, null, NativeBattleEffectKind.None)).ToImmutableArray(), null, null, 0);
    }

    public void Validate()
    {
        if (Slots.IsDefault || Slots.Length != 20 || Head is < 0 or >= 20 || Tail is < 0 or >= 20 || PublishedCount > 20 || Slots.Any(task => task is null || task.Next is < 0 or >= 20 || !Enum.IsDefined(task.Kind) || task.Glyphs.IsDefault || task.Glyphs.Length > 8 || (task.Flags0 & 1) != 0 && (task.Kind == NativeBattleEffectKind.None || task.ModelIndex >= 7 || task.Kind == NativeBattleEffectKind.DamageText5069B0 && task.Frame >= 10)))
        {
            throw new InvalidDataException("Native effect tasks require20 physical slots, valid callback payloads and separately published task count.");
        }
        HashSet<int> visited = [];
        for (int? next = Head; next is int index; next = Slots[index].Next)
        {
            if (!visited.Add(index) || (Slots[index].Flags0 & 1) == 0)
            {
                throw new InvalidDataException("Native effect callback links contain a cycle or a free physical slot.");
            }
        }
    }

    public NativeBattleEffectsState AppendStatusReset(byte modelIndex)
    {
        Validate();
        ValidateModel(modelIndex);
        int slot = FreeSlot();
        if (slot < 0)
        {
            //508360 logs failure, then506690 dereferences payload12; no successful native allocation exists here.
            throw new InvalidOperationException("Native status graphics effect exhausted its20 physical task slots.");
        }
        ImmutableArray<NativeBattleEffectTask> slots = Slots.SetItem(slot, Slots[slot] with { Flags0 = (ushort)(Slots[slot].Flags0 | 1), State2 = 0, Next = null, Kind = NativeBattleEffectKind.StatusReset506860, ModelIndex = modelIndex });
        if (Tail is int tail)
        {
            slots = slots.SetItem(tail, slots[tail] with { Next = slot });
        }
        return this with { Slots = slots, Head = Tail is null ? slot : Head, Tail = slot };
    }

    public NativeBattleEffectsState PrependDamageText(byte modelIndex, ushort contactFlags, ushort amount)
    {
        Validate();
        ValidateModel(modelIndex);
        if ((contactFlags & 1) != 0 && (contactFlags & 0x1000) == 0)
        {
            return this;
        }
        int slot = FreeSlot();
        if (slot < 0)
        {
            //5068B0 skips text when5083F0 cannot allocate; it does not displace another active physical task.
            return this;
        }
        byte[] glyphs;
        short halfWidth;
        if ((contactFlags & 0x400) != 0)
        {
            glyphs = [16, 0];
            halfWidth = 8;
        }
        else if ((contactFlags & 0x1000) != 0)
        {
            glyphs = [69, 0];
            halfWidth = 0;
        }
        else
        {
            glyphs = amount.ToString(System.Globalization.CultureInfo.InvariantCulture).Select(digit => (byte)(56 + digit - '0')).Append((byte)0).ToArray();
            halfWidth = checked((short)(4 * (glyphs.Length - 1)));
        }
        NativeBattleEffectTask task = Slots[slot] with { Flags0 = (ushort)(Slots[slot].Flags0 | 1), State2 = 0, Next = Head, Kind = NativeBattleEffectKind.DamageText5069B0, ModelIndex = modelIndex, Frame = 0, Glyphs = glyphs.ToImmutableArray(), HalfWidth = halfWidth, Color = (contactFlags & 0x100) != 0 ? 0x408040u : 0x808080u };
        //5083F0 deliberately leaves Tail unchanged, including an empty-list Tail0.
        return this with { Slots = Slots.SetItem(slot, task), Head = slot };
    }

    public NativeBattleEffectsStep Advance(IReadOnlyList<NativeBattleGraphicsModel> models)
    {
        Validate();
        if (models.Count != 7)
        {
            throw new InvalidDataException("Native effect callbacks require the seven physical graphics models.");
        }
        ImmutableArray<NativeBattleEffectTask> slots = Slots;
        ImmutableArray<byte>.Builder resetModels = ImmutableArray.CreateBuilder<byte>();
        int? head = Head;
        int? previous = null;
        int survivors = 0;
        for (int? next = Head; next is int index;)
        {
            NativeBattleEffectTask task = slots[index];
            bool release;
            if (task.Kind == NativeBattleEffectKind.StatusReset506860)
            {
                NativeBattleGraphicsModel model = models[task.ModelIndex];
                release = ((model.Flags8 ?? throw new NotSupportedException("Native506860 requires the reached graphics model DWORD8.")) & 0x1A) != 0 || ((model.ResourceFlags44 ?? throw new NotSupportedException("Native506860 requires its reached graphics resource WORD44.")) & 0x140) != 0;
                if (release)
                {
                    resetModels.Add(task.ModelIndex);
                }
            }
            else
            {
                byte frame = unchecked((byte)(task.Frame + 1));
                task = task with { Frame = frame };
                slots = slots.SetItem(index, task);
                release = frame >= 10;
            }
            next = task.Next;
            if (release)
            {
                slots = slots.SetItem(index, task with { Flags0 = 0 });
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
                previous = index;
                survivors++;
            }
        }
        return new(this with { Slots = slots, Head = head, Tail = previous, PublishedCount = checked((byte)survivors) }, resetModels.ToImmutable());
    }

    private int FreeSlot()
    {
        for (int index = 0; index < Slots.Length; index++)
        {
            if ((Slots[index].Flags0 & 1) == 0)
            {
                return index;
            }
        }
        return -1;
    }

    private static void ValidateModel(byte modelIndex)
    {
        if (modelIndex >= 7)
        {
            throw new ArgumentOutOfRangeException(nameof(modelIndex));
        }
    }
}
