namespace Memoria.FF8.Runtime;

/// <summary>A retained model selector whose lifetime was established at its writer.</summary>
public abstract record BattleRetainedModelReference
{
    private protected BattleRetainedModelReference()
    {
    }

    internal sealed record OriginalSlot(NativeBattleModelSlot Slot) : BattleRetainedModelReference;

    internal sealed record CanonicalOwner(BattleModelOwner Owner) : BattleRetainedModelReference;

    internal static BattleRetainedModelReference Capture(BattleModelOwner owner, NativeBattleModelAliases aliases)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return aliases.TryEncodeExact(owner, out byte alias) ? new OriginalSlot(NativeBattleModelSlot.Decode(alias)) : new CanonicalOwner(owner);
    }

    internal BattleModelOwner Resolve(BattleActorModels models, NativeBattleModelBoundary boundary)
    {
        return this switch
        {
            OriginalSlot original => boundary.DecodeLiteral(original.Slot.Offset, models),
            CanonicalOwner canonical when models.Contains(canonical.Owner) => canonical.Owner,
            CanonicalOwner => throw new InvalidDataException("The retained model selector names a retired owner."),
            _ => throw new InvalidDataException("The retained model selector has no binding contract.")
        };
    }

    internal BattleModelOwner Resolve(NativeBattleModelAliases aliases)
    {
        return this switch
        {
            OriginalSlot original => aliases.DecodeLiteral(original.Slot.Offset),
            CanonicalOwner canonical when aliases.Roster.Contains(canonical.Owner) => canonical.Owner,
            CanonicalOwner => throw new InvalidDataException("The retained model selector names a retired owner."),
            _ => throw new InvalidDataException("The retained model selector has no binding contract.")
        };
    }
}
