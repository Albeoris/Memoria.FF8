using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Party cache WORDs370/372 and summoned GF WORD24; separate from already-computed combatant HP.</summary>
public sealed record NativeBattlePartyPresentedVital([property: JsonRequired] ushort HitPoints, [property: JsonRequired] ushort MaximumHitPoints, [property: JsonRequired] ushort? GuardianForceHitPoints, [property: JsonRequired] ushort? GuardianForceMaximumHitPoints)
{
    [JsonRequired]
    public int DisplayedHitPointsQ12 { get; init; }

    [JsonRequired]
    public int? DisplayedGuardianForceHitPointsQ12 { get; init; }

    public NativeBattlePartyPresentedVital AdvanceHud4B0500(bool guardianForceActive)
    {
        int hp = AdvanceDisplayed(DisplayedHitPointsQ12, HitPoints, MaximumHitPoints);
        int? gf = DisplayedGuardianForceHitPointsQ12;
        if (guardianForceActive)
        {
            gf = AdvanceDisplayed(gf ?? throw new NotSupportedException("Native4B0500 requires the started GF HUD accumulator."), GuardianForceHitPoints ?? throw new NotSupportedException("Native4B0500 requires the started GF cache."), GuardianForceMaximumHitPoints!.Value);
        }
        return this with { DisplayedHitPointsQ12 = hp, DisplayedGuardianForceHitPointsQ12 = gf };
    }

    private static int AdvanceDisplayed(int current, ushort hitPoints, ushort maximumHitPoints)
    {
        int target = unchecked((short)hitPoints) << 12;
        int step = (unchecked((short)maximumHitPoints) << 12) / 96;
        if (step == 0)
        {
            step = 4096;
        }
        if (current < target)
        {
            return Math.Min(unchecked(current + step), target);
        }
        if (current > target)
        {
            return Math.Max(0, Math.Max(unchecked(current - step), target));
        }
        return current;
    }

    public NativeBattlePartyPresentedVital Apply(NativeBattleContactHalf half)
    {
        short amount = unchecked((short)half.Amount6);
        ushort hp = HitPoints;
        ushort? gfHp = GuardianForceHitPoints;
        if ((half.HitFlags3 & 0x20) != 0)
        {
            short remaining = unchecked((short)((gfHp ?? throw new NotSupportedException("Native493D80 requires the summoned GF's actual cached HP WORD.")) - amount));
            gfHp = remaining <= 0 ? (ushort)0 : unchecked((ushort)remaining);
        }
        else if ((half.HitFlags3 & 1) != 0)
        {
            short healed = unchecked((short)(hp + amount));
            hp = healed > unchecked((short)MaximumHitPoints) ? MaximumHitPoints : unchecked((ushort)healed);
        }
        else
        {
            short remaining = unchecked((short)(hp - amount));
            hp = remaining <= 0 ? (ushort)0 : unchecked((ushort)remaining);
        }
        if ((half.SavedStatuses4 & 1) != 0)
        {
            hp = 0;
        }
        return this with { HitPoints = hp, GuardianForceHitPoints = gfHp };
    }
}

public sealed record NativeBattlePartyPresentation([property: JsonRequired] ImmutableArray<NativeBattlePartyPresentedVital?> Rows)
{
    public void Validate()
    {
        if (Rows.IsDefault || Rows.Length != 3 || Rows.Any(row => row is not null && ((row.GuardianForceHitPoints is null) != (row.GuardianForceMaximumHitPoints is null) || (row.GuardianForceHitPoints is null) != (row.DisplayedGuardianForceHitPointsQ12 is null))))
        {
            throw new InvalidDataException("Native party presentation requires three physical cache rows and paired GF HP owners.");
        }
    }
}

public sealed record BattlePresentedVital(int HitPoints, int MaximumHitPoints);

public sealed partial class BattleSession
{
    private NativeBattlePartyPresentation? nativePartyPresentation;

    private int ReadNativeRecoverSourceHp(BattleCombatant actor)
    {
        //493450 case9 reads WORD1CFF172, before any later493D80 contact updates that cache.
        if (nativePartyPresentation is not null)
        {
            return RequireNativePartyPresentedRow(actor.NativeIndex).HitPoints;
        }
        //TODO(TD-009): Declared graphicsless components retain their existing HP projection.
        return actor.CurrentHp;
    }

    public BattlePresentedVital ReadPresentedVital(string actorId, int? guardianForceId = null)
    {
        BattleCombatant actor = GetActor(actorId);
        BattleGuardianForceVital? gf = guardianForceId is int id ? guardianForces[id] : null;
        if (nativePartyPresentation is null)
        {
            //TODO(TD-009): Declared graphicsless components retain their existing HUD projection.
            return new(gf?.HitPoints ?? actor.CurrentHp, gf?.MaximumHitPoints ?? actor.MaximumHp);
        }
        NativeBattlePartyPresentedVital row = RequireNativePartyPresentedRow(actor.NativeIndex);
        return gf is null ? new(unchecked((short)row.HitPoints), unchecked((short)row.MaximumHitPoints)) : new(unchecked((short)(row.GuardianForceHitPoints ?? throw new NotSupportedException("Native HUD lacks the actually started GF cache."))), unchecked((short)row.GuardianForceMaximumHitPoints!.Value));
    }

    public BattlePresentedVital ReadDisplayedVital(string actorId, int? guardianForceId = null)
    {
        BattlePresentedVital cached = ReadPresentedVital(actorId, guardianForceId);
        if (nativePartyPresentation is null)
        {
            return cached;
        }
        NativeBattlePartyPresentedVital row = RequireNativePartyPresentedRow(GetActor(actorId).NativeIndex);
        int displayed = guardianForceId is null ? row.DisplayedHitPointsQ12 : row.DisplayedGuardianForceHitPointsQ12 ?? throw new NotSupportedException("Native HUD lacks the actually started GF accumulator.");
        return cached with { HitPoints = displayed / 4096 };
    }

    private void AdvanceNativePresentedVitals4B0500()
    {
        if (nativePartyPresentation is null)
        {
            return;
        }
        ImmutableArray<NativeBattlePartyPresentedVital?> rows = nativePartyPresentation.Rows;
        foreach (BattleCombatant actor in combatants.Where(actor => actor.IsPlayer && actor.NativeIndex is >= 0 and < 3))
        {
            NativeBattlePartyPresentedVital row = RequireNativePartyPresentedRow(actor.NativeIndex);
            //48E620 clears the GF-panel flag when it removes the charge task, after any earlier logical cancellation.
            rows = rows.SetItem(actor.NativeIndex, row.AdvanceHud4B0500(guardianForceCasts.ContainsKey(actor.Id)));
        }
        nativePartyPresentation = nativePartyPresentation with { Rows = rows };
    }

    private void InitializeNativePartyPresentation()
    {
        if (definition.NativeGraphics is null)
        {
            return;
        }
        NativeBattlePartyPresentedVital?[] rows = new NativeBattlePartyPresentedVital?[3];
        foreach (BattleCombatant actor in combatants.Where(actor => actor.IsPlayer && actor.NativeIndex is >= 0 and < 3))
        {
            rows[actor.NativeIndex] = new(checked((ushort)actor.CurrentHp), checked((ushort)actor.MaximumHp), null, null) { DisplayedHitPointsQ12 = actor.CurrentHp << 12, DisplayedGuardianForceHitPointsQ12 = null };
        }
        nativePartyPresentation = new(rows.ToImmutableArray());
    }

    private void BeginNativePresentedGuardianForce(BattleCombatant actor, BattleGuardianForceVital gf)
    {
        if (nativePartyPresentation is null)
        {
            return;
        }
        NativeBattlePartyPresentedVital row = RequireNativePartyPresentedRow(actor.NativeIndex);
        nativePartyPresentation = nativePartyPresentation with { Rows = nativePartyPresentation.Rows.SetItem(actor.NativeIndex, row with { GuardianForceHitPoints = gf.HitPoints, GuardianForceMaximumHitPoints = gf.MaximumHitPoints, DisplayedGuardianForceHitPointsQ12 = gf.HitPoints << 12 }) };
    }

    private void PublishNativePresentedContact(NativeBattleContactHalf half)
    {
        if (half.ActorIndex >= 3)
        {
            return;
        }
        NativeBattlePartyPresentation state = nativePartyPresentation ?? throw new InvalidDataException("Native493D80 lacks the current party presentation cache.");
        NativeBattlePartyPresentedVital row = RequireNativePartyPresentedRow(half.ActorIndex);
        nativePartyPresentation = state with { Rows = state.Rows.SetItem(half.ActorIndex, row.Apply(half)) };
    }

    private NativeBattlePartyPresentedVital RequireNativePartyPresentedRow(int index)
    {
        return index is >= 0 and < 3 ? nativePartyPresentation!.Rows[index] ?? throw new NotSupportedException("Native493D80 lacks its actual party cache allocation.") : throw new InvalidDataException("Native party cache index is outside0..2.");
    }

    private void RestoreNativePartyPresentation(NativeBattlePartyPresentation? state)
    {
        if ((state is null) != (definition.NativeGraphics is null))
        {
            throw new InvalidDataException("Native party presentation cache is missing; explicitly migrate the standalone snapshot.");
        }
        state?.Validate();
        nativePartyPresentation = state;
    }
}
