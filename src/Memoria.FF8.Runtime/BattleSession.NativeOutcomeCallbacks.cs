using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleOutcomeState
{
    public byte? ResultByte6E7 { get; init; }
    public byte? FadeStyleE01 { get; init; }
    public byte? VictoryQueuedDFE { get; init; }
    public byte? ExitModeDE8 { get; init; }
    public byte? AdmissionDFD { get; init; }
    public byte? ActionOwnerB0D { get; init; }
    public byte? FadeDurationB0C { get; init; }
}

public sealed partial record NativeBattleGraphicsState
{
    public NativeBattleOutcomeState? NativeOutcome { get; init; }
}

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState BeginNativeOutcome47DF60(NativeBattleGraphicsState graphics)
    {
        NativeBattleOutcomeState outcome = graphics.NativeOutcome ?? throw new NotSupportedException("Native47DF60 requires its established outcome byte owners.");
        NativeBattleUiServices ui = graphics.UiServices ?? throw new NotSupportedException("Native47DF60 requires its original player UI queue.");
        NativeBattleUiCommands commands = ui.Commands;
        for (byte index = 0; index < 3; index++)
        {
            commands = commands.Enqueue(index, 18);
            BattleCombatant? actor = combatants.SingleOrDefault(actor => actor.NativeIndex == index);
            if (actor is not null)
            {
                actor.NativeCombatantFlags &= 0x7FFFFFFFu;
            }
        }
        commands = commands.Enqueue(0, 65);
        nativeActionPackets = nativeActionPackets with { ContactCount = 0 };
        SetNativeBattleClockActive(false);
        NativeEffectMemory = (NativeEffectMemory ?? throw new NotSupportedException("Native47DF60 requires its retained action-phase process byte.")) with { ActionPhaseDEA = 0 };
        return graphics with { NativeOutcome = outcome with { ActionOwnerB0D = 0, ExitModeDE8 = 2, AdmissionDFD = 1 }, UiServices = ui with { Commands = commands } };
    }

    private NativeBattleGraphicsState BeginNativeOutcomeWait47DFC0(NativeBattleGraphicsState graphics)
    {
        NativeBattleOutcomeState outcome = graphics.NativeOutcome ?? throw new NotSupportedException("Native47DFC0 requires its established fade style BYTE.");
        byte duration = NativeBattleOutcomeCallbacks.Duration47DFC0(outcome.FadeStyleE01);
        (NativeBattleScriptTasks tasks, int slot) = nativeScriptState.Tasks.Allocate(0x47E030);
        nativeScriptState = nativeScriptState with { Tasks = tasks with { Slots = tasks.Slots.SetItem(slot, tasks.Slots[slot].WriteWord(8, (ushort)(duration - 15))) } };
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
        return AppendNativePrimitivePackets(graphics, NativeBattleOutcomeCallbacks.FadePackets501D10(fade));
    }

    private NativeBattleGraphicsState BeginNativeOutcomeFade501E10(NativeBattleGraphicsState graphics)
    {
        return graphics with { Auxiliary = graphics.Auxiliary.AppendOutcomeFade501E10(13) };
    }
}

public static class NativeBattleOutcomeCallbacks
{
    public static ImmutableArray<NativeBattleGeometryPrimitive> FadePackets501D10(byte fade)
    {
        uint shade = unchecked((byte)(128 - fade));
        uint color = 0x2A000000u | shade | shade << 8 | shade << 16;
        NativeBattleGeometryPrimitive quad = new(1, [0x05000000, color, 0, 320, 216u << 16, (216u << 16) | 320], [0, 0, 0, 0]);
        NativeBattleGeometryPrimitive mode = new(1, [0x01000000, 0xE1000040], [0, 0, 0, 0]);
        return [quad, mode];
    }

    public static byte AdvanceCountdown47CCB0(byte remaining)
    {
        return remaining is 0 or 255 ? remaining : (byte)(remaining - 1);
    }

    public static NativeBattleScriptTask Advance47E030(NativeBattleScriptTask task, Action queueFade)
    {
        ushort counter = task.ReadWord(8);
        if (counter == 0)
        {
            queueFade();
            task = task.WriteByte(15, 1);
        }
        return task.WriteWord(8, unchecked((ushort)(counter - 1)));
    }

    public static byte Duration47DFC0(byte? style)
    {
        return style switch
        {
            0 or 3 => 60,
            1 => 30,
            2 => 40,
            _ => throw new NotSupportedException("Native47DFC0 requires one of its four established fade style BYTE values.")
        };
    }
}
