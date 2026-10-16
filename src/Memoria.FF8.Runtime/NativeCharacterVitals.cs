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

public sealed record NativeCharacterVital([property: JsonRequired] byte CharacterId, [property: JsonRequired] NativeHpWord HitPoints, [property: JsonRequired] ushort Status, [property: JsonRequired] NativeCharacterJunction Junction, [property: JsonRequired] uint Experience, [property: JsonRequired] ushort HitPointBonus, [property: JsonRequired] byte Weapon, [property: JsonRequired] byte[] StatBonuses, [property: JsonRequired] NativeMagicStock[] MagicInventory, [property: JsonRequired] ushort MemberFlags, [property: JsonRequired] ushort KillCount)
{
    public ushort? DeathCount { get; init; }
    public ushort[]? GuardianForceCompatibility { get; init; }
    public bool Equals(NativeCharacterVital? other)
    {
        return other is not null && CharacterId == other.CharacterId && HitPoints == other.HitPoints && Status == other.Status && Junction == other.Junction && Experience == other.Experience && HitPointBonus == other.HitPointBonus && Weapon == other.Weapon && MemberFlags == other.MemberFlags && KillCount == other.KillCount && DeathCount == other.DeathCount && StatBonuses.AsSpan().SequenceEqual(other.StatBonuses) && MagicInventory.SequenceEqual(other.MagicInventory) && (GuardianForceCompatibility is null ? other.GuardianForceCompatibility is null : other.GuardianForceCompatibility is not null && GuardianForceCompatibility.AsSpan().SequenceEqual(other.GuardianForceCompatibility));
    }

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(CharacterId);
        hash.Add(HitPoints);
        hash.Add(Status);
        hash.Add(Junction);
        hash.Add(Experience);
        hash.Add(HitPointBonus);
        hash.Add(Weapon);
        hash.Add(MemberFlags);
        hash.Add(KillCount);
        hash.Add(DeathCount);
        foreach (byte bonus in StatBonuses)
        {
            hash.Add(bonus);
        }
        foreach (NativeMagicStock stock in MagicInventory)
        {
            hash.Add(stock);
        }
        return hash.ToHashCode();
    }
}

/// <summary>Eight original save records and three raw combat slots, initialized only by authentic New Game resources.</summary>
public sealed partial record NativeCharacterVitals
{
    public const string InitializationRule = "ff8-en-init-out-56da10-saved-stat-inputs-495ef0-v1";
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
    public required bool DreamEnabled { get; init; }
    [JsonRequired]
    public required NativeGuardianForceVital[] GuardianForces { get; init; }
    [JsonRequired]
    public required NativeStatDefinitions Definitions { get; init; }
    [JsonRequired]
    public required NativeGuardianForceMenuDefinitions MenuDefinitions { get; init; }
    [JsonRequired]
    public required byte[] DreamWeapons { get; init; }
    /// <summary>Physical event-bank bytes188..193, cleared by executed native52CA90 and overwritten by JUNCTION.</summary>
    [JsonRequired]
    public required byte[] JunctionPartyBackup { get; init; }

    public void Validate()
    {
        static bool Hash(string value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
        if (Version != 1 || Rule != InitializationRule || !Hash(InitOutSha256) || !Hash(KernelSha256) || Records is not { Length: 8 } || CombatParty is not { Length: 3 } || Records.Where((record, index) => record is null || record.HitPoints is null || record.CharacterId > 10 || record.StatBonuses is not { Length: 6 } || record.MagicInventory is not { Length: 32 } || record.MagicInventory.Any(stock => stock is null) || record.GuardianForceCompatibility is { Length: not 16 }).Any() || CombatParty.Any(slot => slot != 255 && slot >= 8))
        {
            throw new InvalidDataException("Native character vitals lack their original initialization, eight records or raw combat slots.");
        }
        if (Definitions is null || MenuDefinitions is null || GuardianForces is not { Length: 16 } || GuardianForces.Any(row => row is null || row.LearnedBank is null || row.AbilityProgress.IsDefault || row.AbilityProgress.Length != 21 || row.ForgottenAbilities > 0xFFFFFF) || DreamWeapons is not { Length: 3 } || JunctionPartyBackup is not { Length: 6 })
        {
            throw new InvalidDataException("Native saved-stat owner lacks current GF records, dream weapons or immutable kernel inputs.");
        }
        Definitions.Validate();
        MenuDefinitions.Validate();
        foreach (NativeCharacterVital record in Records)
        {
            record.HitPoints.Validate();
            if (record.Junction is null)
            {
                throw new InvalidDataException("Native character junction state requires its authentic New Game template.");
            }
            record.Junction.Validate();
        }
    }

    public NativeCharacterVitals DeepCopy()
    {
        return this with { Records = Records.Select(record => record with { Junction = record.Junction with { MagicJunctions = record.Junction.MagicJunctions.ToArray() }, StatBonuses = record.StatBonuses.ToArray(), MagicInventory = record.MagicInventory.ToArray(), GuardianForceCompatibility = record.GuardianForceCompatibility?.ToArray() }).ToArray(), CombatParty = CombatParty.ToArray(), GuardianForces = GuardianForces.ToArray(), DreamWeapons = DreamWeapons.ToArray(), JunctionPartyBackup = JunctionPartyBackup.ToArray() };
    }

    /// <summary>Native522110 writes the HP WORD at its wrapped full-DWORD physical row address.</summary>
    public NativeCharacterVitals WithSavedHitPoints(int rawRow, int rawHitPoints)
    {
        Validate();
        uint offset = unchecked((uint)rawRow * 152u);
        if (offset > 7u * 152u || offset % 152u != 0)
        {
            throw new NotSupportedException("SETHP destination is outside the eight owned saved HP WORDs; no identity lookup or low-byte row truncation is applied.");
        }
        int row = (int)(offset / 152u);
        ushort hitPoints = unchecked((ushort)rawHitPoints);
        NativeCharacterVital[] records = Records.ToArray();
        records[row] = records[row] with { HitPoints = new NativeHpWord(hitPoints, hitPoints) };
        return (this with { Records = records }).DeepCopy();
    }

    public NativeCharacterVitals ResetGuardianForces(int rawCharacter)
    {
        Validate();
        // Native52CC20 compares the low BYTE with all eight saved identities;
        // its missing FF result is zero-extended and clamped to record7 by4ACE50.
        return ResetGuardianForcesAtRow(FindPhysicalRow(unchecked((byte)rawCharacter)));
    }

    public NativeCharacterVitals ResetGuardianForcesAtRow(int rawRow)
    {
        Validate();
        // Native4ACE50 clamps its physical signed-DWORD row to0..7.
        int index = Math.Clamp(rawRow, 0, 7);
        NativeCharacterVital selected = Records[index];
        NativeCharacterVital[] records = Records.ToArray();
        records[index] = selected with { Junction = selected.Junction.Reset() };
        byte[] temporaryParty = CombatParty.ToArray();
        temporaryParty[0] = (byte)index;
        NativeCharacterVitals temporary = (this with { Records = records, CombatParty = temporaryParty }).RebuildParty();
        ushort maximum = temporary.Definitions.CharacterMaximum(temporary.Records[index]);
        records = temporary.Records.ToArray();
        records[index] = records[index] with { HitPoints = new NativeHpWord(maximum, maximum), Status = (ushort)(selected.Status & 0x0080) };
        NativeCharacterVitals restored = (temporary with { Records = records, CombatParty = CombatParty.ToArray() }).RebuildParty();
        return restored.DeepCopy();
    }

    private static NativeHpWord ClampHitPoints(NativeHpWord current, ushort maximum)
    {
        // Native495960 compares the signed maximum WORD against unsigned saved HP.
        int signedMaximum = unchecked((short)maximum);
        return new NativeHpWord(signedMaximum < current.Minimum ? maximum : current.Minimum, signedMaximum < current.Maximum ? maximum : current.Maximum);
    }

    public NativeCharacterVitals RebuildParty()
    {
        Validate();
        NativeCharacterVital[] records = Records.ToArray();
        foreach (byte slot in CombatParty.Where(slot => slot != 255))
        {
            NativeCharacterVital current = records[slot];
            ushort maximum = Definitions.CharacterMaximum(current);
            records[slot] = current with { HitPoints = ClampHitPoints(current.HitPoints, maximum) };
        }
        NativeGuardianForceVital[] guardianForces = GuardianForces.ToArray();
        for (int id = 0; id < guardianForces.Length; id++)
        {
            NativeGuardianForceVital current = guardianForces[id];
            if ((current.Flags & 1) != 0)
            {
                ushort maximum = Definitions.GuardianForceMaximum(id, current);
                guardianForces[id] = current with { MaximumHitPoints = maximum, HitPoints = unchecked((short)maximum) < current.HitPoints ? maximum : current.HitPoints };
            }
        }
        return (this with { Records = records, GuardianForces = guardianForces }).DeepCopy();
    }

    public NativeCharacterVitals AcquireGuardianForce(int id)
    {
        Validate();
        if ((uint)id >= GuardianForces.Length)
        {
            throw new NotSupportedException("Native GF acquisition requires an owned saved GF row.");
        }
        NativeGuardianForceVital[] rows = GuardianForces.ToArray();
        rows[id] = rows[id] with { Flags = (byte)(rows[id].Flags | 1) };
        return (this with { GuardianForces = rows }).DeepCopy();
    }

    public NativeCharacterVitals Rest()
    {
        Validate();
        NativeCharacterVitals next = DeepCopy();
        for (int row = 0; row < Records.Length; row++)
        {
            if ((next.Records[row].MemberFlags & 1) == 0 || DreamEnabled && !CombatParty.Contains((byte)row))
            {
                continue;
            }
            next = (next with { CombatParty = [(byte)row, 255, 255] }).RebuildParty();
            ushort maximum = next.Definitions.CharacterMaximum(next.Records[row]);
            NativeCharacterVital[] records = next.Records.ToArray();
            records[row] = records[row] with { HitPoints = new NativeHpWord(maximum, maximum), Status = (ushort)(records[row].Status & 0x80) };
            next = (next with { Records = records, CombatParty = CombatParty.ToArray() }).RebuildParty();
        }
        NativeGuardianForceVital[] guardianForces = next.GuardianForces.ToArray();
        for (int id = 0; id < guardianForces.Length; id++)
        {
            NativeGuardianForceVital row = guardianForces[id];
            if ((row.Flags & 1) != 0)
            {
                ushort maximum = row.MaximumHitPoints ?? throw new NotSupportedException("REST requires the actual earlier495D80 producer of the acquired GF maximum.");
                guardianForces[id] = row with { HitPoints = maximum };
            }
        }
        return (next with { GuardianForces = guardianForces }).DeepCopy();
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
