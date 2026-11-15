using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

/// <summary>Explicit original-slot bindings for tests of the original battle boundary.</summary>
internal static class NativeBattleTypedFixture
{
    private static readonly BattleModelOwner[] owners =
    [
        new BattlePartyActorModel(new("fixture-party-0")),
        new BattlePartyActorModel(new("fixture-party-1")),
        new BattlePartyActorModel(new("fixture-party-2")),
        new BattleEnemyActorModel(new("fixture-enemy-3")),
        new BattleEnemyActorModel(new("fixture-enemy-4")),
        new BattleEnemyActorModel(new("fixture-enemy-5")),
        new BattleEnemyActorModel(new("fixture-enemy-6"))
    ];

    internal static BattleModelRoster OriginalRoster { get; } = new(owners);
    internal static NativeBattleModelAliases OriginalAliases { get; } = new(OriginalRoster, owners.Select((owner, slot) => (NativeBattleModelSlot.Decode((byte)slot), owner)));

    internal static BattleModelOwner OriginalOwner(int slot)
    {
        if ((uint)slot >= owners.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
        return owners[slot];
    }

    internal static NativeBattleActionPacketsState OriginalPackets()
    {
        return NativeBattleActionPacketsState.Create(OriginalRoster, NativeBattleRetainedActionPackets.Fresh(), OriginalAliases);
    }

    internal static (BattleActorModels Models, NativeBattleModelBoundary Boundary) OriginalModels(IEnumerable<(int Slot, NativeBattleGraphicsModel Model)> entries)
    {
        BattleActorModels models = new([]);
        NativeBattleModelBoundary boundary = NativeBattleModelBoundary.FromRetainedMemory(null);
        foreach ((int slot, NativeBattleGraphicsModel model) in entries)
        {
            BattleModelOwner owner = OriginalOwner(slot);
            if (models.Contains(owner))
            {
                throw new ArgumentException("The original test fixture already binds this slot.", nameof(entries));
            }
            (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode((byte)slot), owner, models);
            models = models.Set(owner, model);
        }
        return (models, boundary);
    }

    internal static NativeBattleEffectsState OriginalEffects()
    {
        return NativeBattleEffectsState.Clear(BattleContactTaskStorageContract.OriginalArena);
    }

    internal static NativeBattleEffectTaskActors OriginalTaskActors(int actor, int target)
    {
        return new(OriginalOwner(actor), OriginalOwner(target), OriginalAliases);
    }
}
