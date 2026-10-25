namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticEnemyAbility(int Model, int SceneSlot, int LevelBand, int AbilitySlot, byte Command, ushort ActionId, byte Sequence, byte TargetSelector)
{
    public bool UseTurnScript { get; init; }

    public void Validate()
    {
        if (Model is < 0 or > 238 || SceneSlot is < 0 or > 7 || LevelBand is < 0 or > 2 || AbilitySlot < (UseTurnScript ? -1 : 0) || AbilitySlot > 15 || ActionId == 0)
        {
            throw new InvalidDataException("Diagnostic enemy selection requires an original model, scene slot, level band and nonempty ability row.");
        }
    }
}

public sealed partial class BattleSession
{
    private BattleCombatant RequireDiagnosticEnemy(string actorId, BattleDiagnosticEnemyAbility selection)
    {
        selection.Validate();
        BattleCombatant actor = GetActor(actorId);
        NativeBattleAiDefinition ai = actor.NativeAi ?? throw new NotSupportedException("Diagnostic enemy selection requires its actual initialized AI bank.");
        if (!diagnosticHoldEnemyCommands || actor.IsPlayer || actor.ModelId != selection.Model || actor.SceneSlot != selection.SceneSlot || ai.LevelBand != selection.LevelBand || !selection.UseTurnScript && ai.Abilities.Length <= 16 * ai.LevelBand + selection.AbilitySlot)
        {
            throw new InvalidOperationException("Diagnostic enemy selection does not match the actual held owner, model, scene slot or level band.");
        }
        if (!selection.UseTurnScript)
        {
            NativeBattleAiAbility row = ai.Abilities[16 * ai.LevelBand + selection.AbilitySlot];
            if (row.Command != selection.Command || row.Id != selection.ActionId || row.Sequence != selection.Sequence)
            {
                throw new InvalidOperationException("Diagnostic enemy selection differs from its original ability row.");
            }
        }
        return actor;
    }

    public bool CanSubmitDiagnosticEnemyAbility(string actorId, BattleDiagnosticEnemyAbility selection)
    {
        BattleCombatant actor = RequireDiagnosticEnemy(actorId, selection);
        return Outcome == BattleOutcome.Active && NativeEntryReady && actor.IsReady && !actor.IsBusy && CanNativeBattleActorAct(actor) && (actor.NativeCombatantFlags & 0xC3) == 1;
    }

    /// <summary>Selects an existing legal row through the same preparation, targeting and packet producer as native AI.</summary>
    public void SubmitDiagnosticEnemyAbility(string actorId, BattleDiagnosticEnemyAbility selection, ushort expectedEffect)
    {
        BattleCombatant actor = RequireDiagnosticEnemy(actorId, selection);
        if (!CanSubmitDiagnosticEnemyAbility(actorId, selection))
        {
            throw new InvalidOperationException("Diagnostic enemy action requires its normally ready, living, active owner.");
        }
        BattleNativeAction declared = FindNativeCommandAction(actor, selection.Command, selection.ActionId);
        if (declared.MagicId != expectedEffect)
        {
            throw new InvalidOperationException("Diagnostic enemy row does not resolve to the requested native effect.");
        }
        if (selection.UseTurnScript)
        {
            if (HasNativeUiServiceClock)
            {
                QueueNativeCallback(actor, 1, 1);
                actor.NativeCombatantFlags &= ~0x200u;
            }
            else
            {
                actor.Atb = 0;
                QueueNativeCallback(actor, 1);
            }
            return;
        }
        byte command = 0;
        ushort action = 0;
        byte sequence = 0;
        PrepareNativeAiAbility(actor, selection.AbilitySlot, ref command, ref action, ref sequence);
        BattleNativeAction native = FindNativeCommandAction(actor, command, action);
        if (native.MagicId != expectedEffect)
        {
            throw new InvalidOperationException("Diagnostic enemy row does not resolve to the requested native effect.");
        }
        ushort targets = ResolveNativeAiTargetMask(actor, selection.TargetSelector);
        QueuePreparedNativeAiCommand(actor, command, action, sequence, targets);
    }
}
