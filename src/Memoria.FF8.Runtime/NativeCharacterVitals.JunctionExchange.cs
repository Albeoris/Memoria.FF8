namespace Memoria.FF8.Runtime;

public sealed partial record NativeCharacterVitals
{
    /// <summary>4CB4A0 exchanges both saved junction packages; the menu then rebuilds all eight rows through4BFD40.</summary>
    public NativeCharacterVitals ExchangeJunctions(int sourceRow, int targetRow)
    {
        Validate();
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)sourceRow, (uint)Records.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)targetRow, (uint)Records.Length);
        if (sourceRow == targetRow)
        {
            throw new ArgumentException("Junction exchange requires distinct saved character rows.", nameof(targetRow));
        }
        if (MagicSystem.Reservations.Length != 0)
        {
            throw new InvalidOperationException("Junction exchange requires all pending magic casts to finish.");
        }
        if (!MagicSystem.SharedMagicStock && (sourceRow == 0 || targetRow == 0) && MagicSystem.RecordZeroExtension.Length != 0)
        {
            throw new NotSupportedException("Junction exchange cannot move an extended personal magic bank into a thirty-two-slot character record.");
        }
        NativeCharacterVitals next = DeepCopy();
        NativeCharacterVital source = next.Records[sourceRow];
        NativeCharacterVital target = next.Records[targetRow];
        next.Records[sourceRow] = source with { Junction = target.Junction };
        next.Records[targetRow] = target with { Junction = source.Junction };
        if (!MagicSystem.SharedMagicStock)
        {
            next.Records[sourceRow] = next.Records[sourceRow] with { MagicInventory = target.MagicInventory };
            next.Records[targetRow] = next.Records[targetRow] with { MagicInventory = source.MagicInventory };
            // Spent casts follow the exchanged personal stocks, so exchanging never refills them.
            (next.MagicSystem.PersonalFieldMasks[sourceRow], next.MagicSystem.PersonalFieldMasks[targetRow]) = (next.MagicSystem.PersonalFieldMasks[targetRow], next.MagicSystem.PersonalFieldMasks[sourceRow]);
            (next.MagicSystem.PersonalBattleMasks[sourceRow], next.MagicSystem.PersonalBattleMasks[targetRow]) = (next.MagicSystem.PersonalBattleMasks[targetRow], next.MagicSystem.PersonalBattleMasks[sourceRow]);
        }
        for (int row = 0; row < next.Records.Length; row++)
        {
            next = next.RebuildMenuRow(row);
        }
        next.ValidateMagicSystemConsistency();
        return next;
    }
}
