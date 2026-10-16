namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static NativeBattleSequenceTrack? NativeControlTrack(BattleCombatant actor, byte? sequence)
    {
        return sequence is byte selected ? actor.NativeAi?.SequenceTracks.SingleOrDefault(track => track.Sequence == selected) : null;
    }

    private int NativeActionWindupFrames(BattleCombatant actor, byte? sequence)
    {
        if (NativeGraphics is not null)
        {
            return 1;
        }
        NativeBattleSequenceTrack? track = NativeControlTrack(actor, sequence);
        return track is null ? AttackWindupFrames : Math.Max(1, track.CompletionFrame);
    }

    private BattleActionPresentation AdvanceNativeControlSequence(BattleActionPresentation action)
    {
        BattleCombatant actor = byId[action.ActorId];
        NativeBattleSequenceTrack track = NativeControlTrack(actor, action.NativeSequence) ?? throw new InvalidDataException("Native sequence progress lacks its original local control track.");
        int current = action.NativeSequenceFrame ?? throw new InvalidDataException("Active native control sequence lacks its saved callback progress.");
        if (current >= track.CompletionFrame)
        {
            return action;
        }
        int next = current + 1;
        PublishNativeSequenceMessages(actor, action.NativeSequence, next);
        return action with { NativeSequenceFrame = next, RemainingFrames = Math.Max(1, track.CompletionFrame - next) };
    }

    private void AdvanceNativeSequenceDuringWait()
    {
        if (ActiveAction is { Phase: BattleActionPhase.Windup, NativeSequenceFrame: not null } action)
        {
            //502AB0/504290 continue graphics while4842B0 gates gameplay; AA's receipt waits for gameplay to consume it.
            ActiveAction = AdvanceNativeControlSequence(action);
        }
    }

    private void ValidateNativeSequenceProgress(BattleCombatant actor, BattleActionPresentation action)
    {
        if (NativeGraphics is { } graphics)
        {
            if (action.NativeSequenceFrame is not null || graphics.ActionActorId != actor.Id || action.Phase == BattleActionPhase.Recovery && !graphics.ActionCallbackReady)
            {
                throw new InvalidDataException("Native active action contradicts its raw graphical callback owner.");
            }
            return;
        }
        NativeBattleSequenceTrack? track = NativeControlTrack(actor, action.NativeSequence);
        if (track is null)
        {
            if (action.NativeSequenceFrame is not null)
            {
                throw new InvalidDataException("Saved sequence callback progress has no original control program.");
            }
            return;
        }
        if (action.NativeSequenceFrame is not int frame || frame < 0 || frame > track.CompletionFrame || action.Phase == BattleActionPhase.Windup && action.RemainingFrames != Math.Max(1, track.CompletionFrame - frame) || action.Phase == BattleActionPhase.Recovery && frame != track.CompletionFrame)
        {
            throw new InvalidDataException("Saved sequence callback progress contradicts its AA completion receipt.");
        }
    }

    private void PublishNativeSequenceMessages(BattleCombatant actor, byte? sequence, int frame)
    {
        if (NativeGraphics is not null)
        {
            return;
        }
        NativeBattleSequenceTrack? track = NativeControlTrack(actor, sequence);
        if (track is null)
        {
            return;
        }
        NativeBattleAiDefinition program = actor.NativeAi!;
        foreach (NativeBattleSequenceMessage message in track.Messages.Where(message => message.Frame == frame))
        {
            if (message.Text >= program.Texts.Length)
            {
                throw new InvalidDataException("Native sequence91 text exceeds the actor's original text bank.");
            }
            //504BB0/91 ->48AE60 ->506EE0(durationBYTE, mode3, delay0).
            events.Add(new BattleEvent(Frame, "NativeText", actor.Id, null, message.Duration, program.Texts[message.Text]) { NativeMessageMode = 3 });
        }
    }
}
