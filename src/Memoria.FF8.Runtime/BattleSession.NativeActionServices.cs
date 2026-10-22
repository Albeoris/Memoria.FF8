using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState ApplyNativeActiveHeaderContacts(NativeBattleGraphicsState graphics)
    {
        int index = nativeActionPackets.ActiveHeaderIndex ?? throw new InvalidDataException("Native50A670 requires its actual active header pointer.");
        return ApplyNativeHeaderContacts(graphics, index);
    }

    private NativeBattleGraphicsState ApplyNativeHeaderContacts(NativeBattleGraphicsState graphics, int headerIndex)
    {
        //506BA0 reads this physical header range. The temporary projection is consumed here, never persisted as another contact owner.
        var contacts = nativeActionPackets.ReadHeaderContacts506BA0(headerIndex);
        NativeBattleGraphicsState applied = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = contacts });
        return applied with { PendingContacts = graphics.PendingContacts };
    }

    private bool NativeActionModelsBusy(NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context)
    {
        if (context.UseActionCamera ?? throw new InvalidDataException("Native action service lost its505F00 result."))
        {
            return graphics.Models.Any(model => (model.Flags0 & 2) != 0 && NativeBattleActionGraphics.IsModelBusy(model, 0x40));
        }
        if (NativeBattleActionGraphics.IsModelBusy(graphics.Models[context.ActorIndex], 0x40))
        {
            return true;
        }
        ushort mask = (ushort)(context.TargetMask | context.CoverMask);
        return graphics.Models.Any(model => (mask & 1 << model.Index) != 0 && NativeBattleActionGraphics.IsModelBusy(model, 0x140));
    }

    private NativeBattleGraphicsState PrepareNativeHeaderGeometry(NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context)
    {
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[context.HeaderIndex];
        int actorIndex = context.ActorIndex;
        if (header.ReadByte(1) == 0xF0)
        {
            int? selected = null;
            for (int index = 0; index < 3; index++)
            {
                if ((graphics.Models[index].Flags0 & 2) != 0)
                {
                    selected = index;
                    if (graphics.Models[index].ModelId4 == 4)
                    {
                        break;
                    }
                }
            }
            actorIndex = selected ?? throw new NotSupportedException("Native50AFC0 command240 has no loaded party geometry source.");
        }
        BattleCombatant actor = GraphicsActor(graphics, actorIndex);
        string target = context.FirstTarget is byte first ? GraphicsActor(graphics, first).Id : actor.Id;
        BattleAttackCommand geometry = new(actor.Id, target) { NativeTargetMask = context.CoverMask != 0 ? context.CoverMask : context.TargetMask, NativeQueuedCommand = header.ReadByte(1) };
        return PrepareNativeActionGeometry(graphics, actor, geometry);
    }

    private NativeBattleGraphicsState CompleteNativeHeaderGraphics(NativeBattleGraphicsState graphics)
    {
        NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native50AED0 requires its camera owner.")).CompleteAction();
        for (int index = 0; index < graphics.Models.Length; index++)
        {
            NativeBattleGraphicsModel model = graphics.Models[index];
            graphics = graphics with { Models = graphics.Models.SetItem(index, model with { Flags0 = (ushort)(model.Flags0 & 0x87FF) }) };
        }
        return graphics;
    }

    private NativeBattleGraphicsState SelectNativeHeaderSequence(NativeBattleGraphicsState graphics, byte index, int requestedSequence)
    {
        return ScheduleNativeRingSequence505C00(graphics, index, requestedSequence);
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
            NativeBattleGraphicsModel actor = graphics.Models[context.ActorIndex];
            actor = actor with { Flags8 = (actor.Flags8 ?? throw new InvalidDataException("Native50BB00 actor lacks its loaded DWORD8.")) & ~0x800000u };
            byte sequence = nativeActionPackets.Headers[context.HeaderIndex].ReadByte(2);
            graphics = graphics with { Models = graphics.Models.SetItem(context.ActorIndex, actor) };
            graphics = sequence != 0 ? SelectNativeHeaderSequence(graphics, context.ActorIndex, sequence) : ApplyNativeActiveHeaderContacts(graphics);
            graphics = StartNativeHeaderCamera(graphics, context);
            phase = 3;
        }
        NativeBattleGraphicsModel source = graphics.Models[context.ActorIndex];
        ushort flags = source.ResourceFlags44 ?? throw new NotSupportedException("Native50BB00 requires its source resource WORD44.");
        bool complete = ((flags & 0xC0) != 0 || (source.Flags0 & 2) == 0) && NativeCameraTaskMask == 0;
        if (complete)
        {
            graphics = graphics with { Models = graphics.Models.SetItem(context.ActorIndex, source with { ResourceFlags44 = (ushort)(flags & ~0x80) }) };
            graphics = CompleteNativeHeaderGraphics(graphics);
        }
        return (graphics, phase, complete);
    }
}
