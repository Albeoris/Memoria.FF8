namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private BattleAttackCommand PrepareNativeGraphicsAction(BattleCombatant actor, BattleAttackCommand command)
    {
        if (NativeGraphics is not { } graphics)
        {
            return command;
        }
        byte sequence = command.NativeSequence ?? 0;
        byte nativeCommand = command.NativeQueuedCommand ?? command.NativeAction?.Command ?? throw new NotSupportedException("Native graphics dispatch requires the original queued command BYTE.");
        if (actor.NativeIndex < 3)
        {
            if (nativeCommand < 39)
            {
                if (definition.NativeCommandSequences.Length != 39)
                {
                    throw new InvalidDataException("Native party graphics dispatch requires the original39-byte command sequence table.");
                }
                sequence = definition.NativeCommandSequences[nativeCommand];
            }
            else
            {
                sequence = nativeCommand switch { 236 or 240 or 245 => 0, 239 => 28, 244 or 254 => 14, 247 => 11, _ => sequence };
            }
        }
        if (nativeCommand is 244 or 254 && command.ActionId is 15 or 70)
        {
            sequence = 11;
        }
        if (sequence == 0)
        {
            throw new NotSupportedException("Native effect-only command requires its original effect-module completion producer.");
        }
        int index = graphics.ActorIds.IndexOf(actor.Id);
        NativeBattleGraphicsBinding binding = FindNativeGraphicsBinding(actor.Id) ?? throw new InvalidDataException("Native graphical command has no immutable actor resource.");
        if (index < 0 || !binding.Resource.Contains(sequence) || graphics.ActionActorId is not null)
        {
            throw new InvalidDataException("Native graphical command lacks its loaded sequence or serialized callback owner.");
        }
        graphics = PrepareNativeActionGeometry(graphics, actor, command);
        NativeBattleGraphicsModel model = NativeBattleGraphicsModels.SelectSequence(graphics.Models[index], sequence);
        NativeGraphics = graphics with { Models = graphics.Models.SetItem(index, model), ActionActorId = actor.Id, ActionCallbackReady = false };
        return command with { NativeSequence = sequence };
    }

    private void CompleteNativeGraphicsAction()
    {
        if (NativeGraphics is { } graphics)
        {
            NativeGraphics = graphics with { ActionActorId = null, ActionCallbackReady = false };
        }
    }
}
