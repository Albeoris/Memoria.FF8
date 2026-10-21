namespace Memoria.FF8.Runtime;

public sealed partial record NativeCharacterVitals
{
    public NativeCharacterVitals RemoveAllMagicJunctions()
    {
        Validate();
        NativeCharacterVital[] records = Records.Select(record => record with { Junction = record.Junction with { MagicJunctions = new byte[20] } }).ToArray();
        NativeCharacterVitals next = this with { Records = records };
        for (int row = 0; row < records.Length; row++)
        {
            records[row] = records[row] with { HitPoints = ClampHitPoints(records[row].HitPoints, next.GetMagicMaximumHitPoints(row)) };
        }
        return next.DeepCopy();
    }

    public NativeCharacterVitals RemoveJunctions(int row, bool all)
    {
        Validate();
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)row, 8u);
        NativeCharacterVital[] records = Records.ToArray();
        NativeCharacterVital current = records[row];
        NativeCharacterJunction junction = all ? current.Junction.Reset() : current.Junction with { MagicJunctions = new byte[20] };
        current = current with { Junction = junction };
        records[row] = current with { HitPoints = ClampHitPoints(current.HitPoints, MagicMaximumHitPoints(current)) };
        return (this with { Records = records }).RebuildParty();
    }
}
