using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

/// <summary>Explicit original-slot bindings for tests of the original battle boundary.</summary>
internal static partial class NativeBattleTypedFixture
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

    internal static BattleSession CreateBattle(BattleDefinition definition, BattleAtbMode mode, NativeBattleGraphicsDefinition graphics)
    {
        BattleSession battle = new(definition, mode);
        battle.AttachPresentation(new(definition, graphics));
        return battle;
    }

    internal static BattleSession RestoreBattle(BattleSnapshot continuation)
    {
        BattlePresentationResources resources = continuation.PresentationContinuation?.Resources
            ?? throw new InvalidOperationException("The fixture requires a private in-process presentation continuation.");
        BattleSession battle = BattleSession.Restore(continuation);
        battle.AttachRestoredPresentation(resources, continuation);
        return battle;
    }

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

    internal static NativeBattleEffectsState OriginalEffects()
    {
        return NativeBattleEffectsState.Clear(BattleContactTaskStorageContract.OriginalArena);
    }

    internal static NativeBattleEffectTaskActors OriginalTaskActors(int actor, int target)
    {
        return new(OriginalOwner(actor), OriginalOwner(target), OriginalAliases);
    }
}
