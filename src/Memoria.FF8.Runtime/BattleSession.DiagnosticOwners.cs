namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    /// <summary>Bounded common-owner observations for stalled invocation diagnostics; no effect payload or snapshot is copied.</summary>
    public object ObserveDiagnosticInvocationOwners()
    {
        NativeBattleGraphicsState? graphics = NativeGraphics;
        return new
        {
            LifecycleId = DiagnosticLifecycleId,
            SimulationFrame = Frame,
            Callback = NativeHostCallbacks,
            LastInvocationCompletion = LastDiagnosticInvocationCompletion,
            LastInvocationAftercare = LastDiagnosticInvocationAftercare,
            InvocationAftercareProgress = DiagnosticInvocationAftercareProgress,
            LastInvocationPendingOwners = LastDiagnosticInvocationPendingOwners,
            Clock = nativeBattleClock,
            EffectIoResult = NativeEffectIoResult,
            CameraMask = NativeCamera?.Mask,
            SoundBusy = graphics is null ? (bool?)null : NativeOriginalSoundBusy(),
            Action = ActiveAction is { } action ? new { action.ActorId, action.TargetId, action.Ability, action.ActionId, action.Phase, action.NativeHeaderIndex, action.NativeQueuedCommand, action.NativeSequence } : null,
            GuardianCharges = guardianForceCasts.Values.Select(cast => new { cast.ActorId, GuardianForceId = cast.Command.ActionId, cast.RemainingTicks, cast.InitialTicks }).ToArray(),
            Commands = nativeCommandBanks.BoundCommands.Select(command => new { command.ActorId, command.TargetId, command.Ability, command.ActionId, command.NativeQueuedCommand, command.NativeHeaderIndex, command.NativeCallback }).ToArray(),
            Graphics = graphics is null ? null : new
            {
                graphics.Callback,
                graphics.Flags,
                graphics.ActionActorId,
                graphics.ActionCallbackReady,
                graphics.ActionGraphics,
                ModuleCount = graphics.ModuleEffects.PublishedCount,
                EffectCount = graphics.Effects.PublishedCount,
                Descriptors = graphics.Descriptors.Queue.Select(index => new { Index = index, Packet = graphics.Descriptors.Packets[index] }).ToArray(),
                Tasks = graphics.Descriptors.Tasks.Select((task, index) => new { Index = index, task.Flags, task.State, task.Kind }).Where(task => (task.Flags & 1) != 0).ToArray(),
                OriginalEffect = graphics.OriginalEffect is { } effect ? new { effect.RequestedEffect, effect.SelectedIndex, effect.Parameters, effect.ReturnedDescriptor } : null,
                Models = graphics.Models.Select(model => new
                {
                    model.Index, model.ModelId4, model.Kind5, model.Flags0, model.Flags8, model.ResourceFlags44, model.Sequence, model.SequenceFrame,
                    SequenceState = model.SequenceState is { } sequence ? new { sequence.Offset46, sequence.Wait1, sequence.Default2, sequence.Pending3, sequence.Callback42, sequence.Header40, sequence.Pose, sequence.PoseFlags109, sequence.PoseFrame114, sequence.PoseLimit115 } : null
                }).ToArray()
            }
        };
    }
}
