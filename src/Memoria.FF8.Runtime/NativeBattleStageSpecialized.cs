using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStageSpecializedState([property: JsonRequired] int Counter19, [property: JsonRequired] int Angle44, [property: JsonRequired] int Angle45, [property: JsonRequired] int Angle46, [property: JsonRequired] int Countdown92, [property: JsonRequired] int Rise92, [property: JsonRequired] int Magnitude92, [property: JsonRequired] int Delay92, [property: JsonRequired] ImmutableArray<byte> Packets44, [property: JsonRequired] ImmutableArray<byte> Packets45, [property: JsonRequired] ImmutableArray<byte> Packets46)
{
    [JsonRequired]
    public uint PublishedColor { get; init; }

    public static NativeBattleStageSpecializedState FromColdProcess() => new(0, 0, 0, 0, 0, 0, 0, 0, new byte[512].ToImmutableArray(), new byte[512].ToImmutableArray(), new byte[512].ToImmutableArray());

    public void Validate()
    {
        if (Packets44.IsDefault || Packets44.Length != 512 || Packets45.IsDefault || Packets45.Length != 512 || Packets46.IsDefault || Packets46.Length != 512 || Countdown92 < 0 || Rise92 < 0 || Delay92 < 0 || Countdown92 > 60 && Rise92 == 0)
        {
            throw new InvalidDataException("Native specialized stage requires its retained counters and three physical double-buffered packet banks.");
        }
    }
}

public sealed record NativeBattleStageSpecializedOverlay(uint Callback, int Cue);
public sealed record NativeBattleStageSpecializedStep(NativeBattleStageSpecializedState State, short Phase, short RotationSpeed, short CameraShake, bool Texture0, bool StandardStage, NativeBattleStageSpecializedOverlay? Overlay);

public static class NativeBattleStageSpecialized
{
    public static bool Supports(uint callback) => callback is 0x519FC0 or 0x518440 or 0x518290 or 0x5180C0 or 0x5156D0;

    public static NativeBattleStageSpecializedStep Advance(NativeBattleStageSpecializedState state, uint callback, short phase, uint flags, short rotationSpeed, short cameraShake, Func<uint> nextGlobalRandom, Func<int> nextCrtRandom)
    {
        state.Validate();
        if (!Supports(callback))
        {
            throw new NotSupportedException("Native specialized stage callback has no matching service.");
        }
        if (phase == 0)
        {
            if (callback == 0x5156D0)
            {
                rotationSpeed = 0;
                state = Initialize92(state, nextGlobalRandom, nextCrtRandom);
            }
            return new(state, 1, rotationSpeed, cameraShake, false, false, null);
        }
        if (phase != 1)
        {
            return new(state, phase, rotationSpeed, cameraShake, false, false, null);
        }
        NativeBattleStageSpecializedOverlay? overlay = null;
        bool texture0 = false;
        if (callback == 0x519FC0 && (flags & 3) == 0)
        {
            state = state with { Counter19 = unchecked(state.Counter19 + 1) };
            if ((state.Counter19 & 1) == 0 && cameraShake == 0)
            {
                cameraShake = -1;
            }
        }
        if (callback is 0x518440 or 0x518290 or 0x5180C0 && (flags & 8) == 0)
        {
            int angle = callback == 0x518440 ? state.Angle44 : callback == 0x518290 ? state.Angle45 : state.Angle46;
            overlay = new(callback, (NativeBattleCameraMath.Sine(angle) + 4096) / 2);
            angle = unchecked(angle + 128);
            state = callback == 0x518440 ? state with { Angle44 = angle } : callback == 0x518290 ? state with { Angle45 = angle } : state with { Angle46 = angle };
        }
        if (callback == 0x5156D0)
        {
            texture0 = (flags & 2) == 0;
            if ((flags & 8) == 0)
            {
                rotationSpeed = 0;
                if (state.Countdown92 == 0)
                {
                    state = Initialize92(state, nextGlobalRandom, nextCrtRandom);
                }
                else if (state.Delay92 != 0)
                {
                    state = state with { Delay92 = state.Delay92 - 1 };
                }
                else
                {
                    int envelope;
                    if (state.Countdown92 > 60)
                    {
                        int inner = NativeBattleCameraMath.Sine(unchecked((state.Countdown92 - 60) << 12) / state.Rise92 / 4);
                        envelope = 4096 - NativeBattleCameraMath.Sine(inner / 4);
                    }
                    else if (state.Countdown92 > 30)
                    {
                        envelope = 4096;
                    }
                    else
                    {
                        envelope = 4096 - NativeBattleCameraMath.Sine((state.Countdown92 << 12) / 30 / 4 + 1024);
                    }
                    rotationSpeed = unchecked((short)((envelope * state.Magnitude92) >> 12));
                    state = state with { Countdown92 = state.Countdown92 - 1 };
                }
            }
        }
        return new(state, phase, rotationSpeed, cameraShake, texture0, true, overlay);
    }

    private static NativeBattleStageSpecializedState Initialize92(NativeBattleStageSpecializedState state, Func<uint> nextGlobalRandom, Func<int> nextCrtRandom)
    {
        int delay = (int)(nextGlobalRandom() % 30) + 30;
        int rise = (int)(nextGlobalRandom() % 30) + 30;
        int magnitude = (int)(nextGlobalRandom() % 768) + 256;
        if (nextCrtRandom() < 0x4000)
        {
            magnitude = -magnitude;
        }
        return state with { Delay92 = delay, Rise92 = rise, Countdown92 = rise + 60, Magnitude92 = magnitude };
    }
}
