using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>A bounded sequence of ordinary ready-menu selections, without overriding crisis or random state.</summary>
public sealed record BattleDiagnosticLimitSelection(byte MinimumCrisisLevel, byte MaximumCrisisLevel, int MaximumSelections)
{
    public void Validate()
    {
        if (MinimumCrisisLevel is < 1 or > 4 || MaximumCrisisLevel < MinimumCrisisLevel || MaximumCrisisLevel > 4 || MaximumSelections is < 1 or > 256)
        {
            throw new InvalidDataException("Diagnostic Limit selection requires a crisis range within1..4 and a finite1..256 ordinary menu selections.");
        }
    }
}

public sealed record BattleDiagnosticLimitSelectionReceipt(string ActorId, int CommandOwner, byte Command, bool Accepted, ImmutableArray<byte> CrisisSelections, NativeBattleRandomState? RandomBefore, NativeBattleRandomState? RandomAfter);

public sealed record BattleDiagnosticBorrowedLimitReceipt(string ActorId, int ActorModel, int CommandOwner, byte Command, ushort LearnedBlueMagic, byte NativeSequence, ImmutableArray<int> KernelSections, string ResourcePolicy);

public sealed partial class BattleSession
{
    /// <summary>Initial definition adaptation only; all subsequent commands use ordinary native Limit admission.</summary>
    public static (BattleDefinition Definition, BattleDiagnosticBorrowedLimitReceipt Receipt) PrepareDiagnosticBlueMagic(BattleDefinition source, string actorId, BattleNativeLimitDefinition originalOwner)
    {
        originalOwner.Validate();
        BattleCombatantDefinition actor = source.Combatants.Single(candidate => candidate.Id == actorId);
        if (!actor.IsPlayer || actor.ModelId != 0 || actor.NativeLimit is not { CharacterId: 0, Command: 5 } || actor.NativePlayerStatRules is { CharacterId: not 0 } || originalOwner.CharacterId != 3 || originalOwner.Command != 15)
        {
            throw new NotSupportedException("Borrowed Blue Magic requires Squall's unchanged actor and the original Quistis command15 owner.");
        }
        NativeLimitProgress progress = source.LimitProgress ?? throw new NotSupportedException("Borrowed Blue Magic requires the initialized saved learning owner.");
        progress.Validate();
        if (progress.BlueMagicFlags == 0 || source.NativeCommandSequences.Length != 39)
        {
            throw new NotSupportedException("Borrowed Blue Magic requires learned spells and the original39-command sequence table.");
        }
        for (ushort actionId = 0; actionId < 16; actionId++)
        {
            if ((progress.BlueMagicFlags & (1 << actionId)) == 0)
            {
                continue;
            }
            for (byte crisis = 1; crisis <= 4; crisis++)
            {
                BattleNativeAction[] matches = source.NativeActions.Where(action => action.Command == 15 && action.ActionId == actionId && action.CrisisLevel == crisis).ToArray();
                if (matches.Length != 1 || matches[0] is not { CharacterId: 3, KernelSection: 19 } native || native.KernelIndex != actionId || native.SupplementalKernelIndex != actionId * 4 + crisis - 1 || native.RawRecord.IsDefaultOrEmpty || native.SupplementalRecord.IsDefaultOrEmpty)
                {
                    throw new NotSupportedException($"Borrowed Blue Magic lacks its original kernel19/20 records for spell{actionId}, crisis{crisis}.");
                }
            }
        }
        BattleDefinition next = source with { Combatants = source.Combatants.Select(candidate => candidate.Id == actorId ? candidate with { NativeLimit = originalOwner } : candidate).ToArray() };
        BattleDiagnosticBorrowedLimitReceipt receipt = new(actorId, actor.ModelId, originalOwner.CharacterId, originalOwner.Command, progress.BlueMagicFlags, source.NativeCommandSequences[15], [19, 20, 29], "Original command/crisis/kernel records; initiating actor retains Squall body, weapon and sequence resources. Effect resources resolve normally and unsupported pose requirements fail explicitly.");
        return (next, receipt);
    }

    /// <summary>Uses the normal actor-selection path. Finisher choice remains owned by the later native invocation.</summary>
    public BattleDiagnosticLimitSelectionReceipt SelectDiagnosticLimit(string actorId, BattleDiagnosticLimitSelection selection)
    {
        selection.Validate();
        BattleCombatant actor = GetActor(actorId);
        BattleNativeLimitDefinition limit = actor.NativeLimit ?? throw new NotSupportedException("Diagnostic Limit selection requires the actor's original installed Limit definition.");
        if (!actor.IsPlayer || !CanChoosePlayerCommand(actorId) || PendingSlotsDeal is not null)
        {
            throw new InvalidOperationException("Diagnostic Limit selection requires a ready player without a pending Slots deal.");
        }
        NativeBattleRandomState before = random;
        ImmutableArray<byte>.Builder selections = ImmutableArray.CreateBuilder<byte>();
        bool accepted = false;
        for (int attempt = 0; attempt < selection.MaximumSelections; attempt++)
        {
            SelectPlayerCommandActor(actorId);
            byte crisis = GetNativeLimitLevel(actorId);
            selections.Add(crisis);
            if (crisis >= selection.MinimumCrisisLevel && crisis <= selection.MaximumCrisisLevel)
            {
                accepted = true;
                break;
            }
            if (definition.LimitOncePerTurn && actor.LimitTurnCrisis is not null)
            {
                break;
            }
        }
        return new(actorId, limit.CharacterId, limit.Command, accepted, selections.ToImmutable(), before, random);
    }

    /// <summary>Feeds the ordinary trigger input only while the original widget accepts a successful press.</summary>
    public bool TriggerDiagnosticGunbladeWindow()
    {
        return CanTriggerGunblade && GunbladeTriggerWindowOpen && TriggerGunblade();
    }
}
