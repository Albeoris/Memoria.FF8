namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticComponentAdmission(ushort Effect, byte Command, ushort Action, BattleModelOwner Actor, byte? OriginalActorIndex, int HeaderIndex, long Callback, bool ModuleLoaded);

public sealed partial class BattlePresentationProcess
{
    public NativeBattleEffectBinding ReadDiagnosticComponentResources(ushort effect)
    {
        if (Graphics.OriginalEffect is { ReturnedDescriptor: null } current && current.RequestedEffect == effect)
        {
            return current.Binding;
        }
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(effect, resources.NativeEffectCatalog);
        return NativeBattleEffectResourceResolver.MaterializeSelected(selected, nativeEffectResourceResolver).Binding;
    }

    public BattleDiagnosticComponentAdmission ReadDiagnosticComponentAdmission(ushort expectedEffect)
    {
        NativeBattleGraphicsState graphics = Graphics;
        if (!graphics.EntryReady || hostedPresentationInvocation is not { } invocation)
        {
            throw new InvalidDataException("Component preview requires completed entry and an admitted action.");
        }
        NativeBattleDescriptorState descriptors = graphics.Descriptors;
        NativeBattleDescriptorPacket descriptor = descriptors.Packets[invocation.DescriptorSlot];
        NativeBattleActionHeaderSlot header = NativeActionPackets.ReadHeader(invocation.Header);
        if (descriptor.Command != 104 || descriptor.Serial != invocation.DescriptorSerial || descriptor.Header != invocation.Header ||
            header.Effect != expectedEffect || !graphics.ActorModels.Contains(header.Source))
        {
            throw new InvalidDataException("The component descriptor and action header must identify the declared effect and actor.");
        }

        uint callback = NativeBattleActionGraphics.SelectService(header).Callback;
        if (callback == 0x50A9A0 && descriptors.Tasks.Any(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleDescriptorTaskKind.Action104 && task.PacketSlot == invocation.DescriptorSlot && task.Phase >= 6))
        {
            throw new InvalidDataException("The casting component has already entered its native effect.");
        }
        bool loaded = graphics.OriginalEffect is { ReturnedDescriptor: null };
        if (loaded)
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
            NativeBattleActionGraphicsState? context = graphics.ActionGraphics;
            bool contextMatches = effect.Parameters is { } parameters
                ? parameters.Header == invocation.Header && parameters.Source == header.Source
                : context is not null && context.Header == invocation.Header && context.ResolveActor(graphics) == header.Source && context.Callback == callback;
            bool taskMatches = descriptors.Tasks.Any(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleDescriptorTaskKind.Action104 &&
                task.PacketSlot == invocation.DescriptorSlot && (callback == 0x50BC20 && task.Phase is 1 or 2 || callback == 0x50B190 && task.Phase is 2 or 3 || callback == 0x50A9A0 && task.Phase is 3 or 4 or 5));
            if (!contextMatches || !taskMatches || !effect.Pools.IsEmpty || effect.RequestedEffect != expectedEffect || effect.Binding.Effect != expectedEffect)
            {
                throw new InvalidDataException("The loaded component has passed its unstarted invocation boundary.");
            }
        }
        else if (descriptors.Queue.Count(slot => descriptors.Packets[slot].Command == 104) != 1 || !descriptors.Queue.Contains(invocation.DescriptorSlot))
        {
            throw new InvalidDataException("Component preview requires one queued action104 descriptor.");
        }
        NativeBattleModelAliases aliases = graphics.NativeBindings.CaptureAliases(graphics.ActorModels);
        byte? originalActorIndex = aliases.TryEncodeExact(header.Source, out byte original) ? original : null;
        return new(expectedEffect, header.Command, header.Action, header.Source, originalActorIndex, invocation.Header.Ordinal, graphics.Callback, loaded);
    }
}
