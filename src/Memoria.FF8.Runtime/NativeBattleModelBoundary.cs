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

internal sealed class NativeBattleModelBoundary
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

    internal bool EncodeExactActor(BattleModelOwner owner, BattleActorModels models, out byte operand)
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

    internal bool EncodeExactTargetMask(BattleModelTargets targets, BattleActorModels models, out ushort mask)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(models);
        ushort encoded = 0;
        foreach (BattleModelOwner owner in targets.Enumerate())
        {
            if (!EncodeExactActor(owner, models, out byte operand))
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

    private void ValidateBindings(BattleActorModels models)
    {
        foreach (Cell cell in cells)
        {
            if (cell.Owner is { } owner && !models.Contains(owner))
            {
                throw new InvalidDataException("An original slot's bound owner is missing from its model pool.");
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
