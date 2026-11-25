using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{

    /// <summary>Called at completed host/command boundaries. This composes presentation data, never a battle save.</summary>
    public NativeBattlePreparedPresentation? PreparedPresentation => Presentation?.PreparedPresentation;

    public void RefreshPreparedPresentation()
    {
        Presentation?.RefreshPreparedPresentationAtBoundary();
    }

    private NativeBattlePreparedActor ReadPreparedActor(BattleModelOwner owner, NativeBattleModelTextureBinding body, NativeBattleModelTextureBinding? linked)
    {
        BattleCombatant actor = GetActor(RequireCanonicalGraphicsActorId(owner));
        return new(actor.Id, actor.Name, actor.IsPlayer, actor.ModelId, actor.Costume, actor.WeaponId, actor.SceneSlot, GetGuardianFormVisual(actor.Id), body, linked);
    }

    private NativeBattlePreparedScanActor? ReadPreparedScanActor(BattleModelOwner owner)
    {
        if (!byId.TryGetValue(RequireCanonicalGraphicsActorId(owner), out BattleCombatant? actor))
        {
            return null;
        }
        bool hidden = (actor.NativeCombatantFlags & 0x2000) != 0;
        return new(actor.ModelId, actor.Name, actor.IsPlayer, actor.HasNativeDisplayNameOverride)
        {
            TargetData = new(NativeModelId(actor), actor.Level, ScanHp(actor.CurrentHp), ScanHp(actor.MaximumHp), EffectiveStat(actor, BattleStatId.Strength), EffectiveStat(actor, BattleStatId.Vitality), actor.Speed, actor.Magic, EffectiveStat(actor, BattleStatId.Spirit), actor.Evasion,
                hidden ? [] : [.. Enumerable.Range(0, 8).Select(element => EffectiveNativeElementDefense(actor, element))], !hidden && actor.NativeStatusResistances.Count == 40 ? [.. actor.NativeStatusResistances] : [])
        };

        int? ScanHp(int hp) => !hidden && hp < 100000 ? hp : null;
    }

    private NativeBattlePreparedScanExtra ReadPreparedScanExtra(BattleModelOwner owner)
    {
        return byId.TryGetValue(RequireCanonicalGraphicsActorId(owner), out BattleCombatant? actor)
            ? CaptureScanExtra(actor)
            : new(null, NativeBattlePreparedScanExtraFailure.MissingOwner);
    }

    private static NativeBattlePreparedScanExtra CaptureScanExtra(BattleCombatant actor)
    {
        NativeBattleScanByteOwner row = new(new byte[71].ToImmutableArray(), new bool[71].ToImmutableArray());
        if (actor.NativeDisplayNameIndex is not int index)
        {
            return new(row.WriteByte(0, 0), NativeBattlePreparedScanExtraFailure.None);
        }
        if (actor.NativeAi is null)
        {
            return new(null, NativeBattlePreparedScanExtraFailure.MissingName);
        }
        if ((uint)index >= actor.NativeAi.NameBuffers.Length)
        {
            return new(null, NativeBattlePreparedScanExtraFailure.InvalidIndex);
        }
        if (actor.NativeAi.NameBuffers[index] is not { } name)
        {
            return new(null, NativeBattlePreparedScanExtraFailure.MissingName);
        }
        return name.Length > 71
            ? new(null, NativeBattlePreparedScanExtraFailure.InvalidLength)
            : new(row.Write(0, name), NativeBattlePreparedScanExtraFailure.None);
    }

    private ImmutableDictionary<string, float> ReadPendingAllocationOpacities()
    {
        if (encounterAllocation is not { Phase: BattleEncounterAllocationPhase.Loading or BattleEncounterAllocationPhase.Ready } batch)
        {
            return ImmutableDictionary<string, float>.Empty;
        }
        return batch.ActorIds.ToImmutableDictionary(id => id, _ => 0f, StringComparer.Ordinal);
    }
}
