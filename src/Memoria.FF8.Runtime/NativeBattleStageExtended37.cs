using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStageExtendedState([property: System.Text.Json.Serialization.JsonRequired] NativeBattleStage37State Stage37, [property: System.Text.Json.Serialization.JsonRequired] NativeBattleStage137State Stage137, [property: System.Text.Json.Serialization.JsonRequired] NativeBattleStage147State Stage147)
{
    public static NativeBattleStageExtendedState FromColdProcess() => new(new(), new(), new());

    public void Validate()
    {
        if (Stage37 is null || Stage137 is null || Stage147 is null || Stage37.FadeRemaining is < 0 or > 5 || Stage37.Pulse is < 0 or > 5 || Stage37.Strips is < 0 or > 5 || Stage147.Subphase is < -1 or > 6)
        {
            throw new InvalidDataException("Native specialized stage process requires its original counters and retained globals.");
        }
    }
}

public sealed record NativeBattleStage37State
{
    public NativeBattleEffectHeapAllocation? ClutSource { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public int Hp { get; init; } = 4096;
    [System.Text.Json.Serialization.JsonRequired]
    public int PreviousHp { get; init; } = 4096;
    [System.Text.Json.Serialization.JsonRequired]
    public int TargetHp { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public int FadeRemaining { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public int Pulse { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public bool PulseDirection { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public int Strips { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public bool StripsDirection { get; init; }
}

public sealed record NativeBattleStage37Draw(bool Strips, int Intensity, short DrawMode, short Height, uint? Rgb);
public sealed record NativeBattleStage37Step(NativeBattleStage37State State, short Phase, short RotationSpeed, short CameraShake, ImmutableArray<NativeBattleStage37Draw> Draws);

public static class NativeBattleStage37
{
    public const uint CallbackAddress = 0x518F50;

    public static NativeBattleStage37State Initialize518A80(NativeBattleStage37State state) => state with { Hp = 4096 };

    public static NativeBattleStage37State UpdateHp518A80(NativeBattleStage37State state, int hp, int maximumHp) => state with { Hp = ((unchecked((ushort)((hp << 12) / maximumHp)) + 511) & 0xFE00) };

    public static NativeBattleStage37State Dispatch518A80(NativeBattleStage37State state, int command)
    {
        return command switch
        {
            100 => state with { Pulse = 1 },
            101 => state with { Pulse = 0 },
            102 => state with { Strips = 1 },
            103 => state with { Strips = 0 },
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };
    }

    public static NativeBattleStage37Step Advance518F50(NativeBattleStage37State state, short phase, short rotationSpeed, short cameraShake)
    {
        if (phase != 1)
        {
            return new(state, phase == 0 ? (short)1 : phase, rotationSpeed, cameraShake, []);
        }
        ImmutableArray<NativeBattleStage37Draw>.Builder draws = ImmutableArray.CreateBuilder<NativeBattleStage37Draw>();
        if (state.FadeRemaining != 0)
        {
            int remaining = state.FadeRemaining - 1;
            int fraction = (remaining << 12) / 5;
            draws.Add(new(false, 4096 - ((fraction * (state.PreviousHp - state.TargetHp)) >> 12) - state.TargetHp, 64, 2, 0x808080));
            state = state with { FadeRemaining = remaining, PreviousHp = remaining == 0 ? state.TargetHp : state.PreviousHp };
        }
        else if (state.Hp < state.PreviousHp)
        {
            state = state with { TargetHp = state.Hp, FadeRemaining = 5 };
        }
        if (state.Pulse != 0)
        {
            int fraction = (state.Pulse << 12) / 5;
            rotationSpeed = unchecked((short)(((48 * fraction) >> 12) + 8));
            if (cameraShake == 0)
            {
                cameraShake = state.PulseDirection ? (short)1 : (short)-1;
                state = state with { PulseDirection = !state.PulseDirection };
            }
            draws.Add(new(false, fraction, 64, 16, 0x101000));
            state = state with { Pulse = state.Pulse < 5 ? state.Pulse + 1 : state.Pulse };
        }
        if (state.Strips != 0)
        {
            rotationSpeed = unchecked((short)(((392 * ((state.Strips << 12) / 5)) >> 12) + 8));
            if (cameraShake == 0)
            {
                cameraShake = state.StripsDirection ? (short)1 : (short)-2;
                state = state with { StripsDirection = !state.StripsDirection };
            }
            draws.Add(new(true, 0, 0, 216, null));
            state = state with { Strips = state.Strips < 5 ? state.Strips + 1 : state.Strips };
        }
        return new(state, phase, rotationSpeed, cameraShake, draws.ToImmutable());
    }
}
