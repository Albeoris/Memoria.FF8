namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static bool NativeHeaderTargetsBusy(NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context)
    {
        BattleModelTargets targets = context.ResolveTargets(graphics);
        BattleModelTargets cover = context.ResolveCover(graphics);
        return graphics.ActorModels.Entries.Any(entry =>
            (targets.Contains(entry.Owner) || cover.Contains(entry.Owner)) &&
            NativeBattleActionGraphics.IsModelBusy(entry.State, 0x140));
    }

    private (NativeBattleGraphicsState Graphics, byte Phase, bool Complete) PollNativeControlGraphics(NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context, byte phase)
    {
        if (context.Callback == 0x50BD80)
        {
            //50BD80 waits only for50AE20's target/cover set, then publishes the physical contacts once.
            if (phase == 0)
            {
                if (NativeHeaderTargetsBusy(graphics, context))
                {
                    return (graphics, phase, false);
                }
                phase = 1;
            }
            if (phase != 1)
            {
                throw new InvalidDataException("Native50BD80 reached an invalid descriptor phase.");
            }
            return (ApplyNativeActiveHeaderContacts(graphics), phase, true);
        }
        //50BD00 has no contacts: only sequence10 waits for the actor after selection.
        if (phase == 0)
        {
            if (NativeBattleActionGraphics.IsModelBusy(graphics.ActorModels.Get(context.ResolveActor(graphics)), 0x40))
            {
                return (graphics, phase, false);
            }
            phase = 1;
        }
        if (phase == 1)
        {
            NativeBattleGraphicsModel actor = graphics.ActorModels.Get(context.ResolveActor(graphics));
            actor = actor with { Flags8 = (actor.Flags8 ?? throw new InvalidDataException("Native50BD00 requires its actor DWORD8.")) & ~0x800000u };
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(context.ResolveActor(graphics), actor) };
            byte sequence = nativeActionPackets.ReadHeader(context.Header).Sequence;
            graphics = SelectNativeHeaderSequence(graphics, context.ResolveActor(graphics), sequence);
            if (sequence != 10)
            {
                return (graphics, phase, true);
            }
            return (graphics, 2, false);
        }
        if (phase == 2)
        {
            return (graphics, phase, !NativeBattleActionGraphics.IsModelBusy(graphics.ActorModels.Get(context.ResolveActor(graphics)), 0x40));
        }
        throw new InvalidDataException("Native50BD00 reached an invalid descriptor phase.");
    }
}
