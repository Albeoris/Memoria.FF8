using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{

    private void ObserveDiagnosticEffectDispatch(NativeBattleGraphicsState graphics, NativeBattleOriginalEffectState effect)
    {
        if (services?.ObserveEffect is not { } observe)
        {
            return;
        }
        NativeBattleActionHeaderSlot? header = effect.Parameters is { } entered ? nativeActionPackets.ReadHeader(entered.Header) : null;
        string? actorId = header?.ReflectedCaster is { } caster ? ReadGraphicsPresentationId(caster) :
            effect.Parameters is { } parameters ? ReadGraphicsPresentationId(parameters.Source) : graphics.ActionActorId;
        int? index = OriginalDiagnosticHeaderIndex(effect.Parameters?.Header);
        long? generation = index is int slot && diagnosticHeaderGenerations is { } generations && generations.TryGetValue(slot, out long current) ? current : null;
        observe(this, new(effect, header, actorId, FrameCallback, OriginalDiagnosticSource(effect.Parameters),
            OriginalDiagnosticTargets(effect.Parameters), index, generation));
    }

    internal void AppendDiagnosticGraphicsBlockers(List<string> blockers)
    {
        NativeBattleGraphicsState graphics = NativeGraphics ?? throw new NotSupportedException("Diagnostic drain requires normal native graphical owners.");
        NativeBattleFileIoState files = NativeFileIo ?? throw new NotSupportedException("Diagnostic drain requires the actual file completion owner.");
        if (files.ActiveCount is null)
        {
            throw new NotSupportedException("Diagnostic drain cannot infer an unknown file scheduler count.");
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
    }
}
