namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    /// <summary>Bounded common-owner observations for stalled invocation diagnostics; no effect payload or snapshot is copied.</summary>
    internal object ObserveDiagnosticGraphicsOwners()
    {
        NativeBattleGraphicsState? graphics = NativeGraphics;
        return new
        {
            SimulationFrame = Frame,
            Callback = Graphics.Callback,
            EffectIoResult = NativeEffectIoResult,
            CameraMask = NativeCamera?.Mask,
            SoundBusy = graphics is null ? (bool?)null : NativeOriginalSoundBusy(),
            Effect101 = graphics?.OriginalEffect is { RequestedEffect: 101 } effect101 ? ObserveEffect101Owners(effect101) : null,
            ScriptedEffect = graphics?.OriginalEffect?.RequestedEffect is 77 or 211 ? ObserveScriptedEffectOwners() : null,
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
                Models = graphics.ActorModels.Entries.Select(entry => new
                {
                    ActorId = ReadGraphicsPresentationId(entry.Owner), Index = graphics.NativeBindings.TryEncodeExactActor(entry.Owner, graphics.ActorModels, out byte physical) ? (int?)physical : null,
                    entry.State.ModelId4, entry.State.Kind5, entry.State.Flags0, entry.State.Flags8, entry.State.ResourceFlags44, entry.State.Sequence, entry.State.SequenceFrame,
                    SequenceState = entry.State.SequenceState is { } sequence ? new { sequence.Offset46, sequence.Wait1, sequence.Default2, sequence.Pending3, sequence.Callback42, sequence.Header40, sequence.Pose, sequence.PoseFlags109, sequence.PoseFrame114, sequence.PoseLimit115 } : null
                }).ToArray()
            }
        };
    }

    internal bool ObserveNativeSoundBusy()
    {
        return NativeOriginalSoundBusy();
    }
}
