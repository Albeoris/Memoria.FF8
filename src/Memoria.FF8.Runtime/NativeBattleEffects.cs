using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum BattleContactTaskStorageContract
{
    OriginalArena,
    ExpandableArena
}

public enum NativeBattleEffectKind
{
    None,
    StatusReset506860,
    DamageText5069B0
}

/// <summary>Owned completion/linked-list fields of a32-byte1D98428 task; presentation coordinates have their own renderer owner.</summary>
public sealed record NativeBattleEffectTask([property: JsonRequired] ushort Flags0, [property: JsonRequired] ushort State2, [property: JsonRequired] int? Next, [property: JsonRequired] NativeBattleEffectKind Kind)
{
    [JsonRequired]
    public BattleModelOwner? ModelOwner { get; init; }
    [JsonRequired]
    public byte Frame { get; init; }
    [JsonRequired]
    public ImmutableArray<byte> Glyphs { get; init; } = [];
    [JsonRequired]
    public short HalfWidth { get; init; }
    [JsonRequired]
    public uint Color { get; init; }
    [JsonRequired]
    public NativeBattleDamageTextAnchor? TextAnchor { get; init; }
}

public sealed record NativeBattleEffectsStep(NativeBattleEffectsState State, ImmutableArray<BattleModelOwner> ResetModelStatuses);

/// <summary>Native contact graphics callback list and its separately published count.</summary>
public sealed record NativeBattleEffectsState([property: JsonRequired] ImmutableArray<NativeBattleEffectTask> Slots, [property: JsonRequired] int? Head, [property: JsonRequired] int? Tail, [property: JsonRequired] int PublishedCount, [property: JsonRequired] BattleContactTaskStorageContract StorageContract)
{
    private const int NativeSlotCount = 20;

    [JsonRequired]
    public ImmutableArray<NativeBattleDamageTextDraw> TextDraws { get; init; } = [];

    public static NativeBattleEffectsState Clear(BattleContactTaskStorageContract storageContract)
    {
        if (!Enum.IsDefined(storageContract))
        {
            throw new ArgumentOutOfRangeException(nameof(storageContract));
        }
        //508300 clears all20*32 bytes, both descriptor links and initialBYTE1D96A88.
        return new(Enumerable.Range(0, NativeSlotCount).Select(_ => new NativeBattleEffectTask(0, 0, null, NativeBattleEffectKind.None)).ToImmutableArray(), null, null, 0, storageContract);
    }

    private static NativeBattleEffectTask FreeTask { get; } = new(0, 0, null, NativeBattleEffectKind.None);

    public void Validate(BattleModelRoster roster)
    {
        ArgumentNullException.ThrowIfNull(roster);
        if (TextDraws.IsDefault || TextDraws.Any(draw => draw is null || draw.Glyphs.IsDefaultOrEmpty))
        {
            throw new InvalidDataException("Native damage text requires its last executed presentation commands.");
        }
        int count = Slots.IsDefault ? 0 : Slots.Length;
        if (!Enum.IsDefined(StorageContract) || count < NativeSlotCount || StorageContract == BattleContactTaskStorageContract.OriginalArena && count != NativeSlotCount || Head is < 0 || Head >= count || Tail is < 0 || Tail >= count || PublishedCount < 0 || PublishedCount > count || Slots.Any(task => !ValidTask(task, count, roster)))
        {
            throw new InvalidDataException("Contact effects require valid storage, owned callback payloads and a separately published task count.");
        }
        HashSet<int> visited = [];
        int? last = null;
        for (int? next = Head; next is int index; next = Slots[index].Next)
        {
            if (!visited.Add(index) || (Slots[index].Flags0 & 1) == 0)
            {
                throw new InvalidDataException("Native effect callback links contain a cycle or a free physical slot.");
            }
            last = index;
        }
        if (StorageContract == BattleContactTaskStorageContract.ExpandableArena && (Tail != last || visited.Count != Slots.Count(task => (task.Flags0 & 1) != 0)))
        {
            throw new InvalidDataException("Expandable contact tasks require every active slot to be reachable and the tail to identify the last slot.");
        }
    }

    public NativeBattleEffectsState AppendStatusReset(BattleModelOwner owner, BattleModelRoster roster)
    {
        Validate(roster);
        ValidateModel(owner, roster);
        (ImmutableArray<NativeBattleEffectTask> slots, int slot) = AllocateSlot();
        if (slot < 0)
        {
            //508360 logs failure, then506690 dereferences payload12; no successful native allocation exists here.
            throw new InvalidOperationException("Native status graphics effect exhausted its20 physical task slots.");
        }
        slots = slots.SetItem(slot, slots[slot] with { Flags0 = (ushort)(slots[slot].Flags0 | 1), State2 = 0, Next = null, Kind = NativeBattleEffectKind.StatusReset506860, ModelOwner = owner });
        if (Tail is int tail)
        {
            slots = slots.SetItem(tail, slots[tail] with { Next = slot });
        }
        return this with { Slots = slots, Head = Tail is null ? slot : Head, Tail = slot };
    }

    public NativeBattleEffectsState PrependDamageText(BattleModelOwner owner, ushort contactFlags, int amount, BattleModelRoster roster)
    {
        Validate(roster);
        ValidateModel(owner, roster);
        if ((contactFlags & 1) != 0 && (contactFlags & 0x1000) == 0)
        {
            return this;
        }
        (ImmutableArray<NativeBattleEffectTask> slots, int slot) = AllocateSlot();
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
        NativeBattleEffectTask task = slots[slot] with { Flags0 = (ushort)(slots[slot].Flags0 | 1), State2 = 0, Next = Head, Kind = NativeBattleEffectKind.DamageText5069B0, ModelOwner = owner, Frame = 0, Glyphs = glyphs.ToImmutableArray(), HalfWidth = halfWidth, Color = (contactFlags & 0x100) != 0 ? 0x408040u : 0x808080u, TextAnchor = null };
        //5083F0 leaves Tail unchanged. Expandable storage establishes both links on its first prepend.
        return this with { Slots = slots.SetItem(slot, task), Head = slot, Tail = StorageContract == BattleContactTaskStorageContract.ExpandableArena && Head is null ? slot : Tail };
    }

    public NativeBattleEffectsStep Advance(BattleActorModels models, Func<BattleModelOwner, NativeBattleDamageTextAnchor>? projectTextAnchor = null)
    {
        ArgumentNullException.ThrowIfNull(models);
        Validate(models.Roster);
        ImmutableArray<NativeBattleEffectTask> slots = Slots;
        ImmutableArray<BattleModelOwner>.Builder resetModels = ImmutableArray.CreateBuilder<BattleModelOwner>();
        ImmutableArray<NativeBattleDamageTextDraw>.Builder textDraws = ImmutableArray.CreateBuilder<NativeBattleDamageTextDraw>();
        int? head = Head;
        int? previous = null;
        int survivors = 0;
        for (int? next = Head; next is int index;)
        {
            NativeBattleEffectTask task = slots[index];
            BattleModelOwner owner = task.ModelOwner!;
            bool release;
            if (task.Kind == NativeBattleEffectKind.StatusReset506860)
            {
                NativeBattleGraphicsModel model = models.Get(owner);
                release = ((model.Flags8 ?? throw new NotSupportedException("Native506860 requires the reached graphics model DWORD8.")) & 0x1A) != 0 || ((model.ResourceFlags44 ?? throw new NotSupportedException("Native506860 requires its reached graphics resource WORD44.")) & 0x140) != 0;
                if (release)
                {
                    resetModels.Add(owner);
                }
            }
            else
            {
                if (task.Frame == 0)
                {
                    task = task with { TextAnchor = (projectTextAnchor ?? throw new NotSupportedException("Native5069B0 requires its first callback model/camera projection."))(owner) };
                }
                textDraws.Add(NativeBattleDamageText.Layout5069B0(task));
                byte frame = unchecked((byte)(task.Frame + 1));
                task = task with { Frame = frame };
                slots = slots.SetItem(index, task);
                release = frame >= 10;
            }
            next = task.Next;
            if (release)
            {
                slots = slots.SetItem(index, task with { Flags0 = 0, Kind = NativeBattleEffectKind.None, ModelOwner = null });
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
        return new(this with { Slots = slots, Head = head, Tail = previous, PublishedCount = survivors, TextDraws = textDraws.ToImmutable() }, resetModels.ToImmutable());
    }

    private (ImmutableArray<NativeBattleEffectTask> Slots, int Slot) AllocateSlot()
    {
        for (int index = 0; index < Slots.Length; index++)
        {
            if ((Slots[index].Flags0 & 1) == 0)
            {
                return (Slots, index);
            }
        }
        if (StorageContract == BattleContactTaskStorageContract.OriginalArena)
        {
            return (Slots, -1);
        }
        int nextCapacity = checked(Slots.Length * 2);
        return (Slots.AddRange(Enumerable.Repeat(FreeTask, nextCapacity - Slots.Length)), Slots.Length);
    }

    private static bool ValidTask(NativeBattleEffectTask task, int count, BattleModelRoster roster)
    {
        if (task is null || task.Next is < 0 || task.Next >= count || !Enum.IsDefined(task.Kind) || task.Glyphs.IsDefault || task.Glyphs.Length > 8)
        {
            return false;
        }
        if ((task.Flags0 & 1) == 0)
        {
            return task.Kind == NativeBattleEffectKind.None && task.ModelOwner is null;
        }
        return task.Kind != NativeBattleEffectKind.None && task.ModelOwner is { } owner && roster.Contains(owner) && (task.Kind != NativeBattleEffectKind.DamageText5069B0 || task.Frame < 10);
    }

    private static void ValidateModel(BattleModelOwner owner, BattleModelRoster roster)
    {
        if (!roster.Contains(owner))
        {
            throw new ArgumentOutOfRangeException(nameof(owner));
        }
    }
}
