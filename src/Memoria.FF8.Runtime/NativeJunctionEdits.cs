using System.Security.Cryptography;
using System.Text.Json;

namespace Memoria.FF8.Runtime;

public enum NativeJunctionEditOperation
{
    Assignments,
    ClearCharacterMagic,
    RemoveCharacterJunctions,
    ClearPartyMagic
}

public sealed record NativeJunctionEditRow(int Record, NativeCharacterJunction Junction);
public sealed record NativeJunctionEditPlan(int Version, string SourceFingerprint, NativeJunctionEditRow[] Rows, NativeJunctionEditOperation Operation = NativeJunctionEditOperation.Assignments, bool IsUndo = false);

/// <summary>Versioned assignment-only transactions shared by preview and state owners.</summary>
public static class NativeJunctionEdits
{
    public static string Fingerprint(NativeCharacterVitals source)
    {
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(source)));
    }

    public static NativeJunctionEditPlan Create(NativeCharacterVitals source, NativeCharacterVitals intended, NativeJunctionEditOperation operation = NativeJunctionEditOperation.Assignments, bool isUndo = false)
    {
        source.Validate();
        intended.Validate();
        NativeJunctionEditRow[] rows = Enumerable.Range(0, source.Records.Length).Where(row => source.Records[row].Junction != intended.Records[row].Junction).Select(row => new NativeJunctionEditRow(row, intended.Records[row].Junction with { MagicJunctions = intended.Records[row].Junction.MagicJunctions.ToArray() })).ToArray();
        return new(1, Fingerprint(source), rows, operation, isUndo);
    }

    public static void Validate(NativeJunctionEditPlan plan)
    {
        if (!Enum.IsDefined(plan.Operation) || plan.Version != 1 || plan.SourceFingerprint is not { Length: 64 } || !plan.SourceFingerprint.All(Uri.IsHexDigit) || plan.Rows is not { Length: > 0 and <= NativeCharacterVitals.ExtendedRecordCount } || plan.Rows.Any(row => row is null || (uint)row.Record >= NativeCharacterVitals.ExtendedRecordCount || row.Junction is null) || plan.Rows.Select(row => row.Record).Distinct().Count() != plan.Rows.Length)
        {
            throw new ArgumentException("Invalid junction edit version, source fingerprint or affected records.");
        }
        foreach (NativeJunctionEditRow row in plan.Rows)
        {
            row.Junction.Validate();
        }
    }

    public static NativeCharacterVitals Apply(NativeCharacterVitals source, NativeJunctionEditPlan plan)
    {
        Validate(plan);
        source.Validate();
        if (plan.Rows.Any(row => row.Record >= source.Records.Length) || !string.Equals(Fingerprint(source), plan.SourceFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Junction review is stale; review the current configuration before applying.");
        }
        ValidateOperationScope(source, plan);
        NativeCharacterVitals next = source.DeepCopy();
        foreach (NativeJunctionEditRow edit in plan.Rows)
        {
            NativeCharacterJunction prior = source.Records[edit.Record].Junction;
            if (plan.Operation == NativeJunctionEditOperation.Assignments && edit.Junction.MagicJunctions[19] != prior.MagicJunctions[19] || plan.Operation != NativeJunctionEditOperation.RemoveCharacterJunctions && (edit.Junction.Commands & 0xFF000000) != (prior.Commands & 0xFF000000))
            {
                throw new InvalidOperationException("A dashboard edit cannot change reserved junction fields.");
            }
            next.Records[edit.Record] = next.Records[edit.Record] with { Junction = edit.Junction with { MagicJunctions = edit.Junction.MagicJunctions.ToArray() } };
        }
        HashSet<int> guardianForces = [];
        HashSet<int> sharedSpells = [];
        for (int row = 0; row < next.Records.Length; row++)
        {
            NativeCharacterJunction junction = next.Records[row].Junction;
            foreach (int gf in junction.GuardianForceAssignments)
            {
                if ((next.GuardianForces[gf].Flags & 1) == 0 || !guardianForces.Add(gf))
                {
                    throw new InvalidOperationException("Junction edits require acquired GFs with unique ownership.");
                }
            }
            HashSet<int> localSpells = [];
            for (int slot = 0; slot < 19; slot++)
            {
                int spell = junction.MagicJunctions[slot];
                if (spell != 0 && (spell > 56 || !next.JunctionMagicSlotAvailable(row, slot) || next.GetMagicCount(row, spell) <= 0 || !localSpells.Add(spell) || next.MagicSystem.SharedMagicStock && !sharedSpells.Add(spell)))
                {
                    throw new InvalidOperationException("Junction edits require available magic positions, positive stock and unique spell ownership.");
                }
            }
            if (!plan.Rows.Any(edit => edit.Record == row))
            {
                continue;
            }
            IReadOnlySet<int> learned = next.JunctionLearnedAbilities(row);
            ValidateAbilities(junction.Commands, 3, true, learned);
            ValidateAbilities(junction.Abilities, next.JunctionAbilitySlots(row), false, learned);
        }
        return next.NormalizeJunctionEditRows(plan.Rows.Select(row => row.Record), normalizeStatus: plan.Operation == NativeJunctionEditOperation.Assignments);
    }

    private static void ValidateOperationScope(NativeCharacterVitals source, NativeJunctionEditPlan plan)
    {
        if (plan.Operation == NativeJunctionEditOperation.Assignments)
        {
            return;
        }
        if (plan.Operation != NativeJunctionEditOperation.ClearPartyMagic && plan.Rows.Length != 1)
        {
            throw new InvalidOperationException("A character Off operation must affect exactly one saved record.");
        }
        foreach (NativeJunctionEditRow edit in plan.Rows)
        {
            NativeCharacterJunction prior = source.Records[edit.Record].Junction;
            NativeCharacterJunction cleared = plan.IsUndo ? prior : edit.Junction;
            if (cleared.MagicJunctions.Any(spell => spell != 0))
            {
                throw new InvalidOperationException("An Off operation must clear all twenty original magic junction bytes.");
            }
            if (plan.Operation == NativeJunctionEditOperation.RemoveCharacterJunctions)
            {
                if (cleared.GuardianForces != 0 || cleared.Commands != 0 || cleared.Abilities != 0)
                {
                    throw new InvalidOperationException("Remove junctions must clear the character's GF, command and passive assignments.");
                }
            }
            else if (prior.GuardianForces != edit.Junction.GuardianForces || prior.Commands != edit.Junction.Commands || prior.Abilities != edit.Junction.Abilities)
            {
                throw new InvalidOperationException("Magic Off cannot modify GF, command or passive assignments.");
            }
        }
        if (plan.Operation == NativeJunctionEditOperation.ClearPartyMagic)
        {
            for (int row = 0; row < source.Records.Length; row++)
            {
                NativeCharacterJunction cleared = plan.IsUndo ? source.Records[row].Junction : plan.Rows.FirstOrDefault(edit => edit.Record == row)?.Junction ?? source.Records[row].Junction;
                if (cleared.MagicJunctions.Any(spell => spell != 0))
                {
                    throw new InvalidOperationException("Party magic Off must include every saved record with a magic assignment.");
                }
            }
        }
    }
    private static void ValidateAbilities(uint packed, int capacity, bool command, IReadOnlySet<int> learned)
    {
        HashSet<int> used = [];
        for (int slot = 0; slot < (command ? 3 : 4); slot++)
        {
            int ability = (byte)(packed >> (slot * 8));
            if (ability != 0 && (slot >= capacity || !learned.Contains(ability) || NativeCharacterVitals.IsGuardianForcePassiveAbility(ability) || (command ? ability is < 20 or >= 39 : ability is < 39 or >= 83) || !used.Add(ability)))
            {
                throw new InvalidOperationException("Junction edits require distinct learned commands/passives within the final capacity.");
            }
        }
    }

    /// <summary>Reverses assignments only. HP, stock, cast history and settings are never restored.</summary>
    public static NativeJunctionEditPlan CreateUndo(NativeCharacterVitals before, NativeCharacterVitals applied, NativeJunctionEditOperation operation = NativeJunctionEditOperation.Assignments)
    {
        NativeCharacterVitals intended = applied.DeepCopy();
        for (int row = 0; row < applied.Records.Length; row++)
        {
            if (before.Records[row].Junction != applied.Records[row].Junction)
            {
                intended.Records[row] = intended.Records[row] with { Junction = before.Records[row].Junction with { MagicJunctions = before.Records[row].Junction.MagicJunctions.ToArray() } };
            }
        }
        return Create(applied, intended, operation, isUndo: operation != NativeJunctionEditOperation.Assignments);
    }
}

public sealed partial record NativeCharacterVitals
{
    internal NativeCharacterVitals NormalizeJunctionEditRows(IEnumerable<int> rows, bool normalizeStatus)
    {
        foreach (int row in rows)
        {
            NativeCharacterVital record = Records[row];
            ushort maximum = GetMagicMaximumHitPoints(row);
            NativeHpWord hp = ClampHitPoints(record.HitPoints, maximum);
            Records[row] = record with { HitPoints = hp, Status = normalizeStatus ? MenuRowStatus(record.Status, hp, maximum) : record.Status };
        }
        return this;
    }
}
