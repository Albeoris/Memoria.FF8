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
        BattleCombatant actor = combatants.Single(actor => actor.NativeIndex == 0 && actor.IsPlayer);
        NativeBattleAtbState gauge = actor.NativeAtb ?? throw new NotSupportedException("Preview ally requires its initialized native gauge owner.");
        gauge.Validate();
        actor.NativeAtb = gauge with { Current = gauge.Maximum };
    }

    /// <summary>Publishes one graphical carrier directly; no combat command, stock, formula or reward is evaluated.</summary>
    public void PublishDiagnosticEffectPreview(ushort effect)
    {
        if (!diagnosticEffectPreview || diagnosticEffectPreviewPublished || effect is < 1 or > 400)
        {
            throw new InvalidOperationException("Graphical preview requires one native effect publication in its own arena.");
        }
        RequireNativeEntryReady();
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        if (ObserveDiagnosticDrainBlockers().Count != 0 || graphics.ActorIds[0] is null || graphics.ActorIds[3] is null)
        {
            throw new InvalidOperationException("Graphical preview requires its initialized ally0/enemy3 resource owners.");
        }
        NativeBattleActionHeaderPreparation header = new(0, 2, 11, -1, 1, effect, null);
        (nativeActionPackets, int index) = nativeActionPackets.Prepare(header);
        (nativeActionPackets, _) = nativeActionPackets.AppendContact(new(new(3, 0, 1, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (nativeActionPackets, _) = nativeActionPackets.Publish(1);
        nativeActionPackets = nativeActionPackets.WritePlayerRepeatMode(index, 1);
        ObserveDiagnosticPreparedHeader(index);
        NativeGraphics = QueueNativeDescriptor(graphics, 104, checked(0x1D280C4u + 20u * (uint)index));
        diagnosticEffectPreviewPublished = true;
    }
}
