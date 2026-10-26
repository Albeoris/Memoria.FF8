namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void QueueNativeText(BattleCombatant actor, NativeBattleAiDefinition program, int text, int opcode, int delay = 0)
    {
        if ((uint)text >= program.Texts.Length)
        {
            throw new InvalidDataException("Native battle AI text index exceeds its installed bank.");
        }
        StopTurnAdvance();
        //487DF0 ->47E220;26 schedules48AC60,34 schedules48AC90 in the shared sixteen-slot callback bank.
        int duration = opcode == 24 ? 8 * (definition.NativeMessageSpeed ?? throw new NotSupportedException("Native battle message24 requires its saved message-speed BYTE.")) + 8 : 30;
        BattleEvent message = new(Frame, "NativeText", actor.Id, null, duration, program.Texts[text]) { NativeMessageMode = opcode == 24 ? 1 : 3, NativeTextIdentity = actor.IsPlayer ? null : BattleTextIdentity.Enemy(actor.ModelId, text) };
        if (opcode is 26 or 34)
        {
            nativeScriptState = nativeScriptState with { Tasks = nativeScriptState.Tasks.AppendMessage(message, opcode == 34, checked((ushort)delay)) };
        }
        else
        {
            QueueNativeDescriptorMessage(message, 0);
        }
    }
}
