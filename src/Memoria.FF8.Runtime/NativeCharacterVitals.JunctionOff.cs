namespace Memoria.FF8.Runtime;

public sealed partial record NativeCharacterVitals
{
    public NativeCharacterVitals RemoveJunctions(int row, bool all)
    {
        Validate();
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)row, 8u);
        NativeCharacterVital[] records = Records.ToArray();
        NativeCharacterVital current = records[row];
        NativeCharacterJunction junction = all ? current.Junction.Reset() : current.Junction with { MagicJunctions = new byte[20] };
        current = current with { Junction = junction };
        records[row] = current with { HitPoints = ClampHitPoints(current.HitPoints, Definitions.CharacterMaximum(current)) };
        return (this with { Records = records }).RebuildParty();
    }
}
