namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool diagnosticEffectPreview;
    private bool diagnosticEffectPreviewPublished;

    public void ConfigureDiagnosticEffectPreview()
    {
        if (Frame != 0 || ActiveAction is not null || actions.Count != 0 || nativeCommandBanks.BoundCommands.Any(command => command.NativeCallback?.Stage != 0))
        {
            throw new InvalidOperationException("Graphical preview must be configured before arena callbacks.");
        }
        diagnosticEffectPreview = true;
    }

    public void FillDiagnosticEffectPreviewAllyAtb()
    {
        if (!diagnosticEffectPreview)
        {
            throw new InvalidOperationException("Raw preview readiness belongs only to its graphical arena.");
        }
        RequireNativeEntryReady();
        BattleCombatant actor = combatants.Single(actor => actor.NativeActorIndex == 0 && actor.IsPlayer);
        NativeBattleAtbState gauge = actor.NativeAtb ?? throw new NotSupportedException("Preview ally requires its initialized native gauge owner.");
        gauge.Validate();
        actor.NativeAtb = gauge with { Current = gauge.Maximum };
    }

    /// <summary>Publishes one graphical carrier directly; no combat command, stock, formula or reward is evaluated.</summary>
    public void PublishDiagnosticEffectPreview(ushort effect)
    {
        PublishDiagnosticEffectPreviewHeader(effect, 2, 1);
    }

    public BattleNativeAction ResolveDiagnosticPreviewAbility(byte menuCommand, int abilityId)
    {
        byte nativeCommand = menuCommand switch { 2 => 2, 3 => 254, _ => throw new NotSupportedException("Ability preview requires native Magic or GF command metadata.") };
        int actionId = menuCommand == 3 ? checked(64 + abilityId) : abilityId;
        return definition.NativeActions.SingleOrDefault(action => action.Command == nativeCommand && action.ActionId == actionId) ?? throw new NotSupportedException("Selected ability has no installed original kernel action record.");
    }

    public void PublishDiagnosticAbilityPreview(byte menuCommand, int abilityId)
    {
        BattleNativeAction ability = ResolveDiagnosticPreviewAbility(menuCommand, abilityId);
        PublishDiagnosticEffectPreviewHeader(ability.MagicId, ability.Command, ability.ActionId);
    }

    private void PublishDiagnosticEffectPreviewHeader(ushort effect, byte command, ushort action)
    {
        if (!diagnosticEffectPreview || diagnosticEffectPreviewPublished || effect is < 1 or > 400)
        {
            throw new InvalidOperationException("Graphical preview requires one native effect publication in its own arena.");
        }
        RequireNativeEntryReady();
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        BattleModelOwner? source = graphics.NativeBindings.FindOriginalOwner(NativeBattleModelSlot.Decode(0), graphics.ActorModels);
        BattleModelOwner? target = graphics.NativeBindings.FindOriginalOwner(NativeBattleModelSlot.Decode(3), graphics.ActorModels);
        if (ObserveDiagnosticDrainBlockers().Count != 0 || source is null || target is null)
        {
            throw new InvalidOperationException("Graphical preview requires its initialized ally0/enemy3 resource owners.");
        }
        BattleCombatant actor = combatants.Single(actor => actor.NativeActorIndex == 0);
        byte sequence = ResolveNativeDispatchSequence(actor, command, 0, action);
        NativeBattleActionHeaderPreparation header = new(source, command, sequence, -1, action, effect, null);
        (nativeActionPackets, BattleActionHeaderReference index) = nativeActionPackets.Prepare(header);
        (nativeActionPackets, _) = nativeActionPackets.AppendContact(new(new(target, 0, 1, 0, 0, 0, 0), null));
        PublishNativeSingleActionHeader(1);
        ObserveDiagnosticPreparedHeader(index);
        NativeGraphics = QueueNativeActionDescriptor(graphics, index);
        diagnosticEffectPreviewPublished = true;
    }
}
