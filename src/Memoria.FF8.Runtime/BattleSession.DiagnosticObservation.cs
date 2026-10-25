namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticEffectDispatch(long Serial, long Callback, string? ActorId, BattleAbility? Ability, int? ActionId, ushort RequestedEffect, ushort ExecutedEffect);

public sealed partial class BattleSession
{
    private long diagnosticDispatchSerial;
    public event Action<BattleDiagnosticEffectDispatch>? DiagnosticEffectDispatched;

    private void ObserveDiagnosticEffectDispatch(NativeBattleGraphicsState graphics, NativeBattleOriginalEffectState effect)
    {
        diagnosticDispatchSerial++;
        DiagnosticEffectDispatched?.Invoke(new(diagnosticDispatchSerial, NativeHostCallbacks ?? graphics.Callback, ActiveAction?.ActorId ?? graphics.ActionActorId, ActiveAction?.Ability, ActiveAction?.ActionId, effect.RequestedEffect, effect.Binding.Effect));
    }

    /// <summary>Conservative normal-owner drain observation, without serialization or owner mutation.</summary>
    public IReadOnlyList<string> ObserveDiagnosticDrainBlockers()
    {
        NativeBattleGraphicsState graphics = NativeGraphics ?? throw new NotSupportedException("Diagnostic drain requires normal native graphical owners.");
        NativeBattleFileIoState files = NativeFileIo ?? throw new NotSupportedException("Diagnostic drain requires the actual file completion owner.");
        if (files.ActiveCount is null)
        {
            throw new NotSupportedException("Diagnostic drain cannot infer an unknown file scheduler count.");
        }
        List<string> blockers = [];
        if (ActiveAction is not null || actions.Count != 0 || nativeCommandBanks.BoundCommands.Any())
        {
            blockers.Add("action-or-command");
        }
        if (nativeTimedAction is not null || nativeActionCommitDepth != 0 || HasNativePendingReactions || pendingSlotsDeal is not null || nativeShotProcesses.Count != 0 || nativeDuelProcesses.Count != 0 || nativeRenzokukenProcesses.Count != 0)
        {
            blockers.Add("timed-input-or-mandatory-reaction");
        }
        if (!graphics.PendingContacts.IsEmpty || graphics.Descriptors.Queue.Length != 0 || graphics.Descriptors.Tasks.Any(task => (task.Flags & 1) != 0 && task.Kind != NativeBattleDescriptorTaskKind.Pump500CC0))
        {
            blockers.Add("contact-or-descriptor");
        }
        if (graphics.Effects.Head is not null || graphics.Effects.PublishedCount != 0 || graphics.Auxiliary.Head is not null || graphics.ModuleEffects.Head is not null)
        {
            blockers.Add("effect-or-auxiliary-task");
        }
        if (files.ActiveCount != 0 || NativeOriginalEffectBusy(graphics))
        {
            blockers.Add("file-camera-sound-or-effect-resource");
        }
        return blockers;
    }
}
