namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void QueueNativeText(BattleCombatant actor, NativeBattleAiDefinition program, int text, int opcode, int delay = 0)
    {
        if ((uint)text >= program.Texts.Length)
        {
            throw new InvalidDataException("Native battle AI text index exceeds its installed bank.");
        }
        //487DF0 ->47E220;26 queues48AC60,34 queues48AC90 before the same30/mode3 message.
        int duration = opcode == 24 ? 8 * (definition.NativeMessageSpeed ?? throw new NotSupportedException("Native battle message24 requires its saved message-speed BYTE.")) + 8 : 30;
        events.Add(new BattleEvent(Frame, "NativeText", actor.Id, null, duration, program.Texts[text]) { NativeMessageMode = opcode == 24 ? 1 : 3, NativeMessageDelayFrames = delay });
    }
}
