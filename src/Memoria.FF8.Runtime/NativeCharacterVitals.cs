using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>A native HP WORD domain; an interval never supplies an invented exact HP value.</summary>
public sealed record NativeHpWord([property: JsonRequired] ushort Minimum, [property: JsonRequired] ushort Maximum)
{
    public bool IsZero => Maximum == 0;
    public bool IsPositive => Minimum > 0;
    public void Validate()
    {
        if (Minimum > Maximum)
        {
            throw new InvalidDataException("Native HP WORD domain is inverted.");
        }
    }
}

public sealed record NativeCharacterVital([property: JsonRequired] byte CharacterId, [property: JsonRequired] NativeHpWord HitPoints, [property: JsonRequired] ushort Status);

/// <summary>Eight original save records and three raw combat slots, initialized only by authentic New Game resources.</summary>
public sealed record NativeCharacterVitals
{
    public const string InitializationRule = "ff8-en-init-out-56da10-positive-hp-495960-v1";
    [JsonRequired]
    public required int Version { get; init; }
    [JsonRequired]
    public required string Rule { get; init; }
    [JsonRequired]
    public required string InitOutSha256 { get; init; }
    [JsonRequired]
    public required string KernelSha256 { get; init; }
    [JsonRequired]
    public required NativeCharacterVital[] Records { get; init; }
    [JsonRequired]
    public required byte[] CombatParty { get; init; }
    [JsonRequired]
    public required bool JunctionResetEnabled { get; init; }

    public void Validate()
    {
        static bool Hash(string value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
        if (Version != 1 || Rule != InitializationRule || !Hash(InitOutSha256) || !Hash(KernelSha256) || Records is not { Length: 8 } || CombatParty is not { Length: 3 } || Records.Where((record, index) => record is null || record.HitPoints is null || record.CharacterId != index).Any() || CombatParty.Any(slot => slot != 255 && slot >= 8))
        {
            throw new InvalidDataException("Native character vitals lack their original initialization, eight records or raw combat slots.");
        }
        foreach (NativeCharacterVital record in Records)
        {
            record.HitPoints.Validate();
        }
    }

    public NativeCharacterVitals RebuildParty()
    {
        Validate();
        NativeCharacterVital[] records = Records.ToArray();
        foreach (byte slot in CombatParty.Where(slot => slot != 255))
        {
            NativeCharacterVital current = records[slot];
            // TODO(TD-009): full junction/stat rebuilding is deferred; the authentic unjunctioned initial maximum is proved positive, but its exact value is not substituted
            if (current.HitPoints.IsPositive)
            {
                records[slot] = current with { HitPoints = new NativeHpWord(1, Math.Min(current.HitPoints.Maximum, (ushort)9999)) };
            }
        }
        return this with { Records = records, CombatParty = CombatParty.ToArray() };
    }

    public NativeCharacterVitals RepairDyingParty()
    {
        Validate();
        if (CombatParty.Where(slot => slot != 255).Any(slot => !Records[slot].HitPoints.IsZero && !Records[slot].HitPoints.IsPositive))
        {
            throw new NotSupportedException("DYING cannot choose between zero and positive native HP alternatives.");
        }
        NativeCharacterVital[] records = Records.ToArray();
        foreach (byte slot in CombatParty.Where(slot => slot != 255))
        {
            if (records[slot].HitPoints.IsZero)
            {
                records[slot] = records[slot] with { HitPoints = new NativeHpWord(1, 1), Status = (ushort)(records[slot].Status & 0xFFFE) };
            }
        }
        return this with { Records = records, CombatParty = CombatParty.ToArray() };
    }
}
