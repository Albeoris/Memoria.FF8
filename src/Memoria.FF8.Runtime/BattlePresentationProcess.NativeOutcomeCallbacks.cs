using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private NativeBattleScriptTasks terminalScriptTasks = NativeBattleScriptTasks.Clear();

    private void AdvanceTerminalScriptTasks()
    {
        terminalScriptTasks = terminalScriptTasks.Advance((state, slot) => state with { Slots = state.Slots.SetItem(slot, PollNativeOutcome47E030(state.Slots[slot])) }, _ => ObserveNativeUnknownStackCallback(0x482C30, "script task release"));
    }

    private NativeBattleGraphicsState BuildNativeTerminalGraphics(NativeBattleGraphicsState graphics, BattleOutcome outcome, ushort escapeFlags)
    {
        bool skipVictory = outcome == BattleOutcome.Victory && (escapeFlags & 2) != 0;
        (byte Result, byte Style, ushort? Animation, int? TextId) terminal = outcome switch
        {
            // TODO(TD-009): Physical494D40 result banks remain unowned. Rewards consume the frozen logical facts at host return.
            BattleOutcome.Victory => ((byte)4, skipVictory ? (byte)1 : (byte)0, skipVictory ? (ushort)109 : (ushort)115, null),
            BattleOutcome.Defeat => ((byte)1, (byte)3, null, 0),
            BattleOutcome.Escaped => ((byte)2, (byte)2, (ushort)116, 1),
            BattleOutcome.ScriptedExit => ((byte)1, (byte)3, null, null),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome))
        };
        BattleEvent? message = null;
        if (terminal.TextId is int id)
        {
            if (this.resources.NativeGlobalMessages.Length <= id || this.resources.NativeGlobalMessages[id] is not { } text)
            {
                throw new NotSupportedException("Native terminal presentation requires its original miscellaneous text binding.");
            }
            int duration = 8 * (this.resources.NativeMessageSpeed ?? throw new NotSupportedException("Native terminal presentation requires its saved message-speed BYTE.")) + 8;
            message = new BattleEvent(Frame, "NativeText", null, null, duration, text) { NativeMessageMode = 3, NativeTextIdentity = BattleTextIdentity.Global(id) };
        }
        NativeBattleOutcomeState state = graphics.NativeOutcome ?? new();
        graphics = graphics with { NativeOutcome = state with { ResultByte6E7 = terminal.Result, VictoryQueuedDFE = 1, FadeStyleE01 = terminal.Style } };
        graphics = Queue(graphics, 112, 0);
        graphics = Queue(graphics, 10, 0x47DF60);
        if (message is not null)
        {
            graphics = QueueNativeDescriptorMessage(graphics, message, 86);
        }
        if (terminal.Animation is ushort command)
        {
            graphics = Queue(graphics, command, 0);
        }
        return Queue(graphics, 10, 0x47DFC0);

        NativeBattleGraphicsState Queue(NativeBattleGraphicsState current, ushort command, uint argument)
        {
            return current with { Descriptors = current.Descriptors.Enqueue(command, 128, argument).State };
        }
    }

    private NativeBattleGraphicsState BeginNativeOutcome47DF60(NativeBattleGraphicsState graphics)
    {
        NativeBattleOutcomeState outcome = graphics.NativeOutcome ?? throw new NotSupportedException("Native47DF60 requires its established outcome byte owners.");
        NativeBattleUiServices ui = graphics.UiServices ?? throw new NotSupportedException("Native47DF60 requires its original player UI queue.");
        NativeBattleUiCommands commands = ui.Commands;
        for (byte index = 0; index < 3; index++)
        {
            commands = commands.Enqueue(index, 18);

        }
        commands = commands.Enqueue(0, 65);
        nativeActionPackets = nativeActionPackets.RestartContactPublication();
        ReachHostedTerminalEntry();
        NativeEffectMemory = (NativeEffectMemory ?? throw new NotSupportedException("Native47DF60 requires its retained action-phase process byte.")) with { ActionPhaseDEA = 0 };
        return graphics with { NativeOutcome = outcome with { ActionOwnerB0D = 0, ExitModeDE8 = 2, AdmissionDFD = 1 }, UiServices = ui with { Commands = commands } };
    }

    private NativeBattleGraphicsState BeginNativeOutcomeWait47DFC0(NativeBattleGraphicsState graphics)
    {
        NativeBattleOutcomeState outcome = graphics.NativeOutcome ?? throw new NotSupportedException("Native47DFC0 requires its established fade style BYTE.");
        byte duration = NativeBattleOutcomeCallbacks.Duration47DFC0(outcome.FadeStyleE01);
        (NativeBattleScriptTasks tasks, int slot) = terminalScriptTasks.Allocate(0x47E030);
        terminalScriptTasks = tasks with { Slots = tasks.Slots.SetItem(slot, tasks.Slots[slot].WriteWord(8, (ushort)(duration - NativeBattleOutcomeCallbacks.FadeLeadFrames47E030))) };
        return graphics with { NativeOutcome = outcome with { FadeDurationB0C = duration } };
    }

    private NativeBattleScriptTask PollNativeOutcome47E030(NativeBattleScriptTask task)
    {
        return NativeBattleOutcomeCallbacks.Advance47E030(task, () =>
        {
            NativeBattleGraphicsState graphics = RequireNativeGraphics();
            NativeBattleOutcomeState outcome = graphics.NativeOutcome ?? throw new NotSupportedException("Native47E030 requires its retained fade style BYTE.");
            byte style = outcome.FadeStyleE01 ?? throw new NotSupportedException("Native47E030 requires its established fade style BYTE.");
            NativeGraphics = QueueNativeDescriptor(graphics, 4, 0, priority: 240);
            if (style == 3)
            {
                NativeBattleMusicProcessState music = NativeMusicProcess ?? throw new NotSupportedException("Native47E030 style3 requires its retained music return channel.");
                ushort channel = music.ReturnChannel ?? throw new NotSupportedException("Native47E030 requires its unsigned music return WORD1CD8FC4.");
                NativeGlobalSoundProcess sound = NativeGlobalSoundProcess ?? throw new NotSupportedException("Native47E030 requires its retained sound playback owner.");
                NativeMusicPlaybackState playback = sound.MusicPlayback ?? NativeMusicPlaybackState.Unknown();
                NativeGlobalSoundProcess = sound with { MusicPlayback = playback.FadeGain46BBC0(channel, 60, 0) };
            }
        });
    }

    private void AdvanceNativeOutcomeCountdown47CCB0()
    {
        if (NativeGraphics is { NativeOutcome: { FadeDurationB0C: byte remaining } outcome } graphics && remaining is not (0 or 255))
        {
            NativeGraphics = graphics with { NativeOutcome = outcome with { FadeDurationB0C = NativeBattleOutcomeCallbacks.AdvanceCountdown47CCB0(remaining) } };
        }
    }

    private NativeBattleGraphicsState PublishNativeScreenFade501D10(NativeBattleGraphicsState graphics, byte fade, bool complete)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native501D10 requires its retained primitive process.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native501D10 requires its retained packet and publication bytes.");
        NativeEffectMemory = memory with { PrimitiveProcess = process with { ScreenFadeEB8 = fade, PublicationState03A = complete ? (byte)2 : process.PublicationState03A } };
        graphics = AppendNativePrimitivePackets(graphics, NativeBattleOutcomeCallbacks.FadePackets501D10(fade));
        if (hostedTerminal is not null && complete)
        {
            hostedTerminalFadePublished = true;
        }
        return graphics;
    }

    private NativeBattleGraphicsState BeginNativeOutcomeFade501E10(NativeBattleGraphicsState graphics)
    {
        return graphics with { Auxiliary = graphics.Auxiliary.AppendOutcomeFade501E10(NativeBattleOutcomeCallbacks.OutcomeFadeDuration501E10) };
    }
}
