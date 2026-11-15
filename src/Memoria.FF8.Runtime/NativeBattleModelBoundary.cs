using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public readonly record struct NativeBattleModelSlot
{
    private NativeBattleModelSlot(byte offset)
    {
        Offset = offset;
    }

    internal byte Offset { get; }
    public BattleSide Side => Offset < 3 ? BattleSide.Allies : BattleSide.Enemies;

    public static NativeBattleModelSlot Decode(byte operand)
    {
        if (operand >= NativeBattleModelBoundary.CellCount)
        {
            throw new ArgumentOutOfRangeException(nameof(operand));
        }
        return new(operand);
    }
}

public sealed partial class NativeBattleModelBoundary
{
    internal const int CellCount = 7;
    private const ushort AvailableTargetBits = (1 << CellCount) - 1;

    private sealed record Cell(BattleModelOwner? Owner, NativeBattleGraphicsModel? Retained);
    private readonly ImmutableArray<Cell> cells;

    internal NativeBattleModelBoundary(IEnumerable<NativeBattleGraphicsModel> originalStates)
    {
        ArgumentNullException.ThrowIfNull(originalStates);
        ImmutableArray<NativeBattleGraphicsModel> states = originalStates.Take(CellCount + 1).ToImmutableArray();
        if (states.Length != CellCount || states.Any(state => state is null))
        {
            throw new ArgumentException("The original model boundary requires seven retained model states.", nameof(originalStates));
        }
        cells = states.Select(state => new Cell(null, state)).ToImmutableArray();
    }

    private NativeBattleModelBoundary(ImmutableArray<Cell> cells)
    {
        this.cells = cells;
    }

    internal NativeBattleGraphicsModel ReadOriginalCell(NativeBattleModelSlot slot, BattleActorModels models)
    {
        ArgumentNullException.ThrowIfNull(models);
        Cell cell = cells[slot.Offset];
        return cell.Owner is { } owner ? models.Get(owner) : cell.Retained!;
    }

    internal ImmutableArray<NativeBattleGraphicsModel> CaptureOriginalCells(BattleActorModels models)
    {
        ArgumentNullException.ThrowIfNull(models);
        var captured = ImmutableArray.CreateBuilder<NativeBattleGraphicsModel>(CellCount);
        for (byte offset = 0; offset < CellCount; offset++)
        {
            captured.Add(ReadOriginalCell(NativeBattleModelSlot.Decode(offset), models));
        }
        return captured.MoveToImmutable();
    }

    internal NativeBattleModelBoundary CompleteUnboundActionCells50AED0()
    {
        ImmutableArray<Cell> updated = cells;
        for (int offset = 0; offset < cells.Length; offset++)
        {
            Cell cell = cells[offset];
            if (cell.Owner is null && cell.Retained is { } retained)
            {
                ushort flags = (ushort)(retained.Flags0 & 0x87FF);
                if (flags != retained.Flags0)
                {
                    updated = updated.SetItem(offset, cell with { Retained = retained with { Flags0 = flags } });
                }
            }
        }
        return updated == cells ? this : new(updated);
    }

    internal NativeBattleModelBoundary WriteUnboundPartyStatusMask(uint mask, uint value)
    {
        ImmutableArray<Cell> updated = cells;
        for (byte offset = 0; offset < CellCount; offset++)
        {
            NativeBattleModelSlot slot = NativeBattleModelSlot.Decode(offset);
            Cell cell = cells[offset];
            if (slot.Side == BattleSide.Allies && cell.Retained is { } retained && cell.Owner is null)
            {
                updated = updated.SetItem(offset, cell with { Retained = retained.WriteRetainedStatusMask(mask, value) });
            }
        }
        return updated == cells ? this : new(updated);
    }

    internal NativeBattleModelAliases CaptureAliases(BattleActorModels models)
    {
        ArgumentNullException.ThrowIfNull(models);
        ValidateBindings(models);
        var mappings = new List<(NativeBattleModelSlot Slot, BattleModelOwner Owner)>();
        for (byte offset = 0; offset < CellCount; offset++)
        {
            if (cells[offset].Owner is { } owner)
            {
                mappings.Add((NativeBattleModelSlot.Decode(offset), owner));
            }
        }
        return new(models.Roster, mappings);
    }

    internal ImmutableDictionary<NativeBattleModelSlot, NativeBattleGuardianModelVisibility> CaptureUnboundVisibility()
    {
        var results = ImmutableDictionary.CreateBuilder<NativeBattleModelSlot, NativeBattleGuardianModelVisibility>();
        for (byte index = 0; index < CellCount; index++)
        {
            if (cells[index] is { Owner: null, Retained: { } state })
            {
                results.Add(NativeBattleModelSlot.Decode(index), new(state.Flags0, state.Flags2));
            }
        }
        return results.ToImmutable();
    }

    internal NativeBattleModelBoundary RestoreUnboundVisibility(ImmutableDictionary<NativeBattleModelSlot, NativeBattleGuardianModelVisibility> visibility)
    {
        ImmutableArray<Cell> updated = cells;
        foreach ((NativeBattleModelSlot slot, NativeBattleGuardianModelVisibility saved) in visibility)
        {
            if (updated[slot.Offset] is { Owner: null, Retained: { } model } cell)
            {
                updated = updated.SetItem(slot.Offset, cell with { Retained = RestoreVisibility(model, saved) });
            }
        }
        return updated == cells ? this : new(updated);
    }

    internal static NativeBattleGraphicsModel RestoreVisibility(NativeBattleGraphicsModel model, NativeBattleGuardianModelVisibility saved)
    {
        ushort flags = (ushort)((model.Flags0 & ~0xC) | (saved.Flags0 & 0xC));
        ushort? flags2 = model.Flags2 is ushort current ? (ushort)((current & ~4) | ((saved.Flags2 ?? 0) & 4)) : null;
        return model with { Flags0 = flags, Flags2 = flags2 };
    }

    internal BattleModelOwner? FindOriginalOwner(NativeBattleModelSlot slot, BattleActorModels models)
    {
        ArgumentNullException.ThrowIfNull(models);
        return cells[slot.Offset].Owner is { } owner && models.Contains(owner) ? owner : null;
    }

    internal BattleModelOwner DecodeLiteral(byte operand, BattleActorModels models)
    {
        ArgumentNullException.ThrowIfNull(models);
        return RequireBoundOwner(NativeBattleModelSlot.Decode(operand), models);
    }

    internal BattleModelTargets DecodeTargetMask(ushort mask, BattleActorModels models)
    {
        ArgumentNullException.ThrowIfNull(models);
        if ((mask & ~AvailableTargetBits) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mask));
        }
        var selected = ImmutableArray.CreateBuilder<BattleModelOwner>();
        for (byte offset = 0; offset < CellCount; offset++)
        {
            if ((mask & (1 << offset)) != 0)
            {
                selected.Add(RequireBoundOwner(NativeBattleModelSlot.Decode(offset), models));
            }
        }
        return BattleModelTargets.From(selected);
    }

    internal bool TryEncodeExactActor(BattleModelOwner owner, BattleActorModels models, out byte operand)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(models);
        if (models.Contains(owner))
        {
            for (byte offset = 0; offset < CellCount; offset++)
            {
                if (cells[offset].Owner == owner)
                {
                    operand = offset;
                    return true;
                }
            }
        }
        operand = default;
        return false;
    }

    internal bool TryEncodeExactTargetMask(BattleModelTargets targets, BattleActorModels models, out ushort mask)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(models);
        ushort encoded = 0;
        foreach (BattleModelOwner owner in targets.Enumerate())
        {
            if (!TryEncodeExactActor(owner, models, out byte operand))
            {
                mask = default;
                return false;
            }
            encoded |= (ushort)(1 << operand);
        }
        mask = encoded;
        return true;
    }

    internal (NativeBattleModelBoundary Boundary, BattleActorModels Models) BindOriginalSlot(NativeBattleModelSlot slot, BattleModelOwner owner, BattleActorModels models)
    {
        ArgumentNullException.ThrowIfNull(owner);
        NativeBattleGraphicsModel priorState = ReadOriginalCell(slot, models);
        ValidateBindings(models);
        if (owner.Side != slot.Side)
        {
            throw new ArgumentException("Model owner is on the wrong side for the original slot.", nameof(owner));
        }
        BattleModelOwner? priorOwner = cells[slot.Offset].Owner;
        if (priorOwner == owner)
        {
            return (this, models);
        }
        if (models.Contains(owner))
        {
            throw new ArgumentException("Model owner is already bound.", nameof(owner));
        }

        var updated = new NativeBattleModelBoundary(cells.SetItem(slot.Offset, new(owner, null)));
        IEnumerable<BattleModelEntry<BattleModelOwner>> entries = models.Entries.Where(entry => entry.Owner != priorOwner).Append(new(owner, priorState));
        return (updated, updated.OrderOriginalBindings(entries));
    }

    internal (NativeBattleModelBoundary Boundary, BattleActorModels Models) UnbindOriginalSlot(NativeBattleModelSlot slot, BattleActorModels models)
    {
        NativeBattleGraphicsModel state = ReadOriginalCell(slot, models);
        ValidateBindings(models);
        if (cells[slot.Offset].Owner is not { } owner)
        {
            return (this, models);
        }
        var updated = new NativeBattleModelBoundary(cells.SetItem(slot.Offset, new(null, state)));
        return (updated, models.Remove(owner));
    }

    private BattleModelOwner RequireBoundOwner(NativeBattleModelSlot slot, BattleActorModels models)
    {
        if (cells[slot.Offset].Owner is not { } owner || !models.Contains(owner))
        {
            throw new InvalidDataException("The original slot has no current model owner.");
        }
        return owner;
    }

    internal void ValidateBindings(BattleActorModels models)
    {
        for (byte offset = 0; offset < cells.Length; offset++)
        {
            if (cells[offset].Owner is { } owner && (owner.Side != NativeBattleModelSlot.Decode(offset).Side || !models.Contains(owner)))
            {
                throw new InvalidDataException("An original slot requires its bound owner in the corresponding model pool.");
            }
        }
    }

    private BattleActorModels OrderOriginalBindings(IEnumerable<BattleModelEntry<BattleModelOwner>> entries)
    {
        BattleModelEntry<BattleModelOwner>[] ordered = entries.ToArray();
        Dictionary<BattleModelOwner, BattleModelEntry<BattleModelOwner>> byOwner = ordered.ToDictionary(entry => entry.Owner);
        var originals = ImmutableArray.CreateBuilder<BattleModelEntry<BattleModelOwner>>();
        foreach (Cell cell in cells)
        {
            if (cell.Owner is { } owner)
            {
                originals.Add(byOwner[owner]);
                byOwner.Remove(owner);
            }
        }
        return new(originals.Concat(ordered.Where(entry => byOwner.ContainsKey(entry.Owner))));
    }
}
