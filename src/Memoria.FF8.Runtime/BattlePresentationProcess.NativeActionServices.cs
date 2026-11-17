using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private NativeBattleGraphicsState ApplyNativeActiveHeaderContacts(NativeBattleGraphicsState graphics, BattleModelOwner? sequenceOwner = null)
    {
        BattleActionHeaderReference index = nativeActionPackets.ActiveHeader ?? throw new InvalidDataException("Native50A670 requires its actual active header pointer.");
        NativeBattleContactOrigin origin = sequenceOwner is not null && sequenceOwner == nativeActionPackets.ReadHeader(index).Source ? NativeBattleContactOrigin.SourceModelSequence : NativeBattleContactOrigin.Ordinary;
        return ApplyNativeHeaderContacts(graphics, index, origin);
    }

    private NativeBattleGraphicsState ApplyNativeHeaderContacts(NativeBattleGraphicsState graphics, BattleActionHeaderReference header, NativeBattleContactOrigin origin = NativeBattleContactOrigin.Ordinary)
    {
        //506BA0 reads this physical header range. The temporary projection is consumed here, never persisted as another contact owner.
        var contacts = nativeActionPackets.ReadHeaderContacts(header);
        NativeBattleGraphicsState applied = ConsumeNativeGraphicsContactsFrom(graphics with { PendingContacts = contacts }, origin);
        return applied with { PendingContacts = graphics.PendingContacts };
    }

    private bool NativeActionModelsBusy(NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context)
    {
        return NativeBattleActionGraphics.AreActionModelsBusy(graphics.ActorModels, graphics.NativeBindings, context);
    }

    private NativeBattleGraphicsState PrepareNativeHeaderGeometry(NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context)
    {
        NativeBattleActionHeaderSlot header = nativeActionPackets.ReadHeader(context.Header);
        BattleModelOwner source;
        if (header.Command == 0xF0)
        {
            BattleModelOwner? selected = null;
            foreach (BattleModelEntry<BattlePartyModelOwner> entry in graphics.ActorModels.Party.Entries)
            {
                if ((entry.State.Flags0 & 2) != 0)
                {
                    selected = entry.Owner;
                    if (entry.State.ModelId4 == 4)
                    {
                        break;
                    }
                }
            }
            source = selected ?? throw new NotSupportedException("Native50AFC0 command240 has no loaded party geometry source.");
        }
        else
        {
            source = context.ResolveActor(graphics);
        }
        BattleModelTargets selection = context.CoverActors.IsEmpty ? context.ResolveTargets(graphics) : context.ResolveCover(graphics);
        BattleModelTargets effective = selection;
        if (selection.IsEmpty && header.Command is 1 or 12 or 28)
        {
            effective = effective.Add(context.FirstTargetBinding?.Resolve(graphics.ActorModels, graphics.NativeBindings) ?? source);
        }
        return PrepareNativeActionGeometry(graphics, source, selection, effective);
    }

    private NativeBattleGraphicsState CompleteNativeHeaderGraphics(NativeBattleGraphicsState graphics)
    {
        NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native50AED0 requires its camera owner.")).CompleteAction();
        BattleActorModels models = graphics.ActorModels;
        foreach (BattleModelEntry<BattleModelOwner> entry in graphics.ActorModels.Entries)
        {
            NativeBattleGraphicsModel model = entry.State;
            models = models.Set(entry.Owner, model with { Flags0 = (ushort)(model.Flags0 & 0x87FF) });
        }
        return graphics with { ActorModels = models, NativeBindings = graphics.NativeBindings.CompleteUnboundActionCells50AED0() };
    }

    private NativeBattleGraphicsState SelectNativeHeaderSequence(NativeBattleGraphicsState graphics, BattleModelOwner index, int requestedSequence, NativeBattleCastingSequenceProvenance? presentationSource = null)
    {
        graphics = ScheduleNativeRingSequence505C00(graphics, index, requestedSequence);
        return graphics with { ModuleEffects = graphics.ModuleEffects.BindCastingSequence(index, presentationSource) };
    }

    private NativeBattleGraphicsState SelectNativeReadyHeaderSequence505CE0(NativeBattleGraphicsState graphics, BattleModelOwner index, int requestedSequence)
    {
        if (presentationForeignActors?.TryGetValue(index, out NativeBattleForeignActor? foreign) == true && foreign.NativeResource is null)
        {
            return SelectNativeHeaderSequence(graphics, index, requestedSequence);
        }
        return NativeBattleSequenceSelections.CanSelectReady505CE0(graphics.ActorModels.Get(index)) ? SelectNativeHeaderSequence(graphics, index, requestedSequence) : graphics;
    }

    private (NativeBattleGraphicsState Graphics, byte Phase, bool Complete) PollNativeOrdinaryGraphics(NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context, byte phase)
    {
        if (phase == 0)
        {
            (graphics, bool camera) = ChooseNativeActionCamera(graphics, context);
            context = context with { UseActionCamera = camera };
            graphics = graphics with { ActionGraphics = context };
            phase = 1;
        }
        if (phase == 1)
        {
            if (NativeActionModelsBusy(graphics, context))
            {
                return (graphics, phase, false);
            }
            phase = 2;
        }
        if (phase == 2)
        {
            graphics = PrepareNativeHeaderGeometry(graphics, context);
            NativeBattleGraphicsModel actor = graphics.ActorModels.Get(context.ResolveActor(graphics));
            actor = actor with { Flags8 = (actor.Flags8 ?? throw new InvalidDataException("Native50BB00 actor lacks its loaded DWORD8.")) & ~0x800000u };
            byte sequence = nativeActionPackets.ReadHeader(context.Header).Sequence;
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(context.ResolveActor(graphics), actor) };
            graphics = sequence != 0 ? SelectNativeHeaderSequence(graphics, context.ResolveActor(graphics), sequence) : ApplyNativeActiveHeaderContacts(graphics);
            graphics = StartNativeHeaderCamera(graphics, context);
            phase = 3;
        }
        NativeBattleGraphicsModel source = graphics.ActorModels.Get(context.ResolveActor(graphics));
        ushort flags = source.ResourceFlags44 ?? throw new NotSupportedException("Native50BB00 requires its source resource WORD44.");
        bool complete = ((flags & 0xC0) != 0 || (source.Flags0 & 2) == 0) && NativeCameraTaskMask == 0;
        if (complete)
        {
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(context.ResolveActor(graphics), source with { ResourceFlags44 = (ushort)(flags & ~0x80) }) };
            graphics = CompleteNativeHeaderGraphics(graphics);
        }
        return (graphics, phase, complete);
    }
}
