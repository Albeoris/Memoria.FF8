using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleFinalStage142State([property: JsonRequired] int CurrentRatio, [property: JsonRequired] int TargetRatio, [property: JsonRequired] int CapturedTarget, [property: JsonRequired] int FlashCountdown, [property: JsonRequired] int TransitionA, [property: JsonRequired] int AlternatorA, [property: JsonRequired] int TransitionB, [property: JsonRequired] int AlternatorB, [property: JsonRequired] ushort SavedRotation, [property: JsonRequired] uint OverlayCursor, [property: JsonRequired] NativeBattleEffectArena OverlayBank0, [property: JsonRequired] NativeBattleEffectArena OverlayBank1)
{
    [JsonRequired]
    public NativeBattleEffectHeapAllocation? ClutSource { get; init; }
    [JsonRequired]
    public uint GenericCursor { get; init; }
    public static NativeBattleFinalStage142State FromColdProcess() => new(0x1000, 0x1000, 0, 0, 0, 0, 0, 0, 0, 0, NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear());
}

public enum NativeBattleFinalStage142CommandKind
{
    FarColor,
    Overlay,
    PcRenderRequest,
    Texture0,
    StandardStage
}

public sealed record NativeBattleFinalStage142Command(NativeBattleFinalStage142CommandKind Kind, int X = 0, int Y = 0, int Z = 0, uint PacketAddress = 0);
public sealed record NativeBattleFinalStage142Step(NativeBattleFinalStage142State State, NativeBattleStageState Stage, short Phase, short CameraShake, ImmutableArray<NativeBattleFinalStage142Command> Commands);
public sealed record NativeBattleFinalStage142Actor(int ActorIndex, byte Flags, byte Identity, int CurrentHp, int MaximumHp);

public static class NativeBattleFinalStage142
{
    public const uint CallbackAddress = 0x512980;
    public const uint OverlayBankAddress = 0x1D9A020;
    public const int OverlayBankBytes = 1024;
    public const int OverlayBytes = 72;
    public static NativeBattleStageColors InitializerColors { get; } = new(0x00707070, 0x00808080);
    public static NativeBattleStageColors LoadedColors { get; } = new(0x00707070, 0x32808080);

    public static NativeBattleFinalStage142State Initialize0(NativeBattleFinalStage142State state) => state with { TargetRatio = 0x1000 };

    public static NativeBattleFinalStage142State UpdateTarget3(NativeBattleFinalStage142State state, ImmutableArray<NativeBattleFinalStage142Actor> actors)
    {
        state.Validate();
        for (int actor = 3; actor <= 6; actor++)
        {
            NativeBattleFinalStage142Actor selected = actors.SingleOrDefault(row => row.ActorIndex == actor) ?? throw new NotSupportedException("Native final stage HP sampling requires each actual actor descriptor3 through6.");
            if ((selected.Flags & 2) == 0 || selected.Identity != 0x8C)
            {
                continue;
            }
            int numerator = unchecked(selected.CurrentHp << 12);
            if (selected.MaximumHp == 0 || numerator == int.MinValue && selected.MaximumHp == -1)
            {
                throw new NotSupportedException("Native final stage HP ratio faults on its actual signed division operands.");
            }
            int ratio = numerator / selected.MaximumHp;
            return state with { TargetRatio = unchecked(ratio + 0x1FF) & 0xFE00 };
        }
        return state;
    }

    public static NativeBattleFinalStage142Step Control(NativeBattleFinalStage142State state, NativeBattleStageState stage, short phase, short cameraShake, uint mode)
    {
        state.Validate();
        ImmutableArray<NativeBattleFinalStage142Command>.Builder commands = ImmutableArray.CreateBuilder<NativeBattleFinalStage142Command>();
        switch (mode)
        {
            case 100:
                state = state with { TransitionA = 1 };
                break;
            case 101:
                state = state with { TransitionA = 0 };
                stage = LowRotation(stage, state.SavedRotation);
                Overlay(ref state, commands, 0, 32, 16);
                break;
            case 102:
                state = state with { TransitionB = 1 };
                break;
            case 103:
                state = state with { TransitionB = 0 };
                stage = LowRotation(stage, state.SavedRotation);
                break;
            default:
                throw new NotSupportedException("Native final stage control requires its original transition command.");
        }
        return new(state, stage, phase, cameraShake, commands.ToImmutable());
    }

    public static NativeBattleFinalStage142Step Advance(NativeBattleFinalStage142State state, NativeBattleStageState stage, short phase, byte framebuffer, short cameraShake)
    {
        state.Validate();
        if (framebuffer > 1)
        {
            throw new NotSupportedException("Native final stage requires its actual double-buffer selector.");
        }
        state = state with { OverlayCursor = OverlayBankAddress + ((uint)framebuffer << 10) };
        ImmutableArray<NativeBattleFinalStage142Command>.Builder commands = ImmutableArray.CreateBuilder<NativeBattleFinalStage142Command>();
        if (phase == 0)
        {
            return new(state, stage, 1, cameraShake, []);
        }
        if (phase != 1)
        {
            return new(state, stage, phase, cameraShake, []);
        }
        if (state.FlashCountdown != 0)
        {
            int count = unchecked(state.FlashCountdown - 1);
            int t = unchecked(count << 12) / 5;
            int difference = unchecked(state.CurrentRatio - state.CapturedTarget);
            int cue = unchecked(4096 - (unchecked(difference * t) >> 12) - state.CapturedTarget);
            state = state with { FlashCountdown = count };
            commands.Add(new(NativeBattleFinalStage142CommandKind.FarColor, 128, 128, 128));
            Overlay(ref state, commands, cue, 64, 3);
            if (count == 0)
            {
                state = state with { CurrentRatio = state.CapturedTarget };
            }
        }
        else if (state.TargetRatio < state.CurrentRatio)
        {
            state = state with { CapturedTarget = state.TargetRatio, FlashCountdown = 5 };
        }
        if ((stage.Flags & 8) == 0)
        {
            if (state.TransitionA != 0)
            {
                int t = unchecked(state.TransitionA << 12) / 5;
                stage = LowRotation(stage, unchecked(((t * 48) >> 12) + 8));
                if (cameraShake == 0)
                {
                    cameraShake = state.AlternatorA != 0 ? (short)1 : (short)-1;
                    state = state with { AlternatorA = state.AlternatorA == 0 ? 1 : 0 };
                }
                commands.Add(new(NativeBattleFinalStage142CommandKind.FarColor, 0, 16, 16));
                Overlay(ref state, commands, t, 64, 16);
                if (state.TransitionA < 5)
                {
                    state = state with { TransitionA = unchecked(state.TransitionA + 1) };
                }
            }
            if (state.TransitionB != 0)
            {
                int t = unchecked(state.TransitionB << 12) / 5;
                stage = LowRotation(stage, unchecked(((t * 392) >> 12) + 8));
                if (cameraShake == 0)
                {
                    cameraShake = state.AlternatorB != 0 ? (short)1 : (short)-2;
                    state = state with { AlternatorB = state.AlternatorB == 0 ? 1 : 0 };
                }
                commands.Add(new(NativeBattleFinalStage142CommandKind.PcRenderRequest, 255, 255, 255));
                if (state.TransitionB < 5)
                {
                    state = state with { TransitionB = unchecked(state.TransitionB + 1) };
                }
            }
        }
        if ((stage.Flags & 2) == 0 && stage.TextureAnimations is not null)
        {
            commands.Add(new(NativeBattleFinalStage142CommandKind.Texture0));
        }
        commands.Add(new(NativeBattleFinalStage142CommandKind.StandardStage));
        return new(state, stage, phase, cameraShake, commands.ToImmutable());
    }

    private static NativeBattleStageState LowRotation(NativeBattleStageState stage, int value) => stage with { Rotation = unchecked((stage.Rotation & (int)0xFFFF0000) | (ushort)value) };

    private static void Overlay(ref NativeBattleFinalStage142State state, ImmutableArray<NativeBattleFinalStage142Command>.Builder commands, int cue, int drawMode, int height)
    {
        commands.Add(new(NativeBattleFinalStage142CommandKind.Overlay, cue, drawMode, height, state.OverlayCursor));
        state = state with { OverlayCursor = unchecked(state.OverlayCursor + OverlayBytes) };
    }
}
