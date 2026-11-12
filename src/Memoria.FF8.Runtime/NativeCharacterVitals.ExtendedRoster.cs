using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeCharacterVitals
{
    public const int NativeRecordCount = 8;
    public const int ExtendedRecordCount = 11;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ExtendedRosterEnabled { get; init; }

    // The native JUNCTION backup contains only original physical row references.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? ExtendedRosterPartyBeforeDream { get; init; }

    public static bool IsExtendedRosterCharacter(int character) => character is >= 6 and < ExtendedRecordCount;

    public bool IsRosterMember(int row)
    {
        bool extended = ExtendedRosterEnabled && !DreamEnabled;
        if ((uint)row >= Records.Length || row >= NativeRecordCount && !extended)
        {
            return false;
        }
        return extended && IsExtendedRosterCharacter(row) || IsNativeRosterMember(row);
    }

    public byte GetBattleWeapon(int row)
    {
        NativeCharacterVital character = Records[row];
        return DreamEnabled && row < NativeRecordCount && character.CharacterId >= NativeRecordCount ? DreamWeapons[character.CharacterId - NativeRecordCount] : character.Weapon;
    }

    public byte[] ProjectNativeCombatParty(IReadOnlyList<int> fieldParty)
    {
        if (!ExtendedRosterEnabled || DreamEnabled)
        {
            return CombatParty.ToArray();
        }
        Queue<byte> originalMembers = new(fieldParty.Where(identity => (uint)identity < NativeRecordCount && !CombatParty.Contains((byte)identity)).Select(identity => (byte)identity).Distinct());
        byte[] projected = CombatParty.ToArray();
        for (int slot = 0; slot < projected.Length; slot++)
        {
            byte row = projected[slot];
            if (IsExtendedRosterCharacter(row) && (row >= NativeRecordCount || !fieldParty.Contains(row)))
            {
                projected[slot] = originalMembers.TryDequeue(out byte replacement) ? replacement : (byte)255;
            }
        }
        return projected;
    }

    public NativeCharacterVitals WithExtendedRoster(bool enabled)
    {
        Validate();
        if (enabled == ExtendedRosterEnabled)
        {
            return this;
        }
        NativeCharacterVitals next = this with { ExtendedRosterEnabled = enabled };
        if (enabled && Records.Length == NativeRecordCount)
        {
            NativeCharacterVital[] records = new NativeCharacterVital[ExtendedRecordCount];
            Records.CopyTo(records, 0);
            for (int row = NativeRecordCount; row < records.Length; row++)
            {
                records[row] = new((byte)row, new(1, 1), 0, new(0, 0, 0, new byte[20]), 0, 0, DreamWeapons[row - NativeRecordCount], new byte[6], Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray(), 0, 0)
                {
                    NativeByte90 = 0,
                    DeathCount = 0,
                    GuardianForceCompatibility = Enumerable.Repeat((ushort)500, 16).ToArray()
                };
            }
            next = next with { Records = records, MagicSystem = MagicSystem.WithRecordCount(records.Length) };
            for (int row = NativeRecordCount; row < records.Length; row++)
            {
                ushort hp = next.GetMagicMaximumHitPoints(row);
                records[row] = records[row] with { HitPoints = new(hp, hp) };
            }
        }
        if (!enabled)
        {
            NativeCharacterVital[] records = next.Records.ToArray();
            for (int row = 6; row < records.Length; row++)
            {
                NativeCharacterVital record = records[row];
                records[row] = record with { Junction = record.Junction.Reset() };
            }
            next = next with { Records = records };
            if (DreamEnabled || ExtendedRosterPartyBeforeDream is not null)
            {
                next = next with { ExtendedRosterPartyBeforeDream = RemoveModOnlyPartyMembers(ExtendedRosterPartyBeforeDream ?? JunctionPartyBackup.AsSpan(3).ToArray()) };
            }
            if (!DreamEnabled)
            {
                next = next with { CombatParty = RemoveModOnlyPartyMembers(CombatParty) };
            }
        }
        next.Validate();
        return next.DeepCopy();
    }

    public NativeCharacterVitals ForOriginalSaveExport()
    {
        Validate();
        if (Records.Length == NativeRecordCount)
        {
            return this;
        }
        if (MagicSystem.Reservations.Length != 0)
        {
            throw new NotSupportedException("Original FF8 export requires all reserved magic casts to finish first.");
        }
        byte[] backup = JunctionPartyBackup.ToArray();
        if (DreamEnabled)
        {
            RemoveModOnlyPartyMembers(ExtendedRosterPartyBeforeDream ?? backup.AsSpan(3).ToArray()).CopyTo(backup, 3);
        }
        NativeCharacterVitals native = this with
        {
            Records = Records[..NativeRecordCount],
            CombatParty = DreamEnabled ? CombatParty : RemoveModOnlyPartyMembers(CombatParty),
            ExtendedRosterEnabled = false,
            ExtendedRosterPartyBeforeDream = null,
            JunctionPartyBackup = backup,
            MagicSystem = MagicSystem with
            {
                AttackModes = MagicSystem.AttackModes[..NativeRecordCount],
                PersonalFieldMasks = MagicSystem.PersonalFieldMasks[..NativeRecordCount],
                PersonalBattleMasks = MagicSystem.PersonalBattleMasks[..NativeRecordCount]
            }
        };
        native.Validate();
        return native.DeepCopy();
    }

    internal static bool IsSupportedRecordCount(int count) => count is NativeRecordCount or ExtendedRecordCount;

    internal static bool IsValidRosterShape(NativeCharacterVital[] records, byte[] party, bool extendedRosterEnabled, bool dreamEnabled, byte[]? beforeDream)
    {
        return IsSupportedRecordCount(records.Length)
            && (!extendedRosterEnabled || records.Length == ExtendedRecordCount)
            && records.Skip(NativeRecordCount).Select((record, index) => record?.CharacterId == index + NativeRecordCount).All(valid => valid)
            && party is { Length: 3 }
            && party.All(row => row == 255 || row < (extendedRosterEnabled && !dreamEnabled ? records.Length : NativeRecordCount))
            && (beforeDream is null || records.Length == ExtendedRecordCount && beforeDream is { Length: 3 } && beforeDream.All(row => row == 255 || row < records.Length));
    }

    private bool IsNativeRosterMember(int row) => row < NativeRecordCount && (Records[row].MemberFlags & 1) != 0;

    private byte[] RemoveModOnlyPartyMembers(byte[] party)
    {
        byte[] remaining = party.Where(row => row < 6 || row < NativeRecordCount && IsNativeRosterMember(row)).ToArray();
        if (remaining.Length == 0)
        {
            return [0, 255, 255];
        }
        if (remaining.Length == party.Count(row => row != 255))
        {
            return party;
        }
        return [.. remaining, .. Enumerable.Repeat((byte)255, party.Length - remaining.Length)];
    }
}
