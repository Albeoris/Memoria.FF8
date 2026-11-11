using System.Text.Json.Serialization;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattleActorModelModifiers(bool ScaleOverride = false, byte Red = 0, byte Green = 0, byte Blue = 0, ushort Transparency = 0, bool Visible = true, bool Deferred = false, bool ExtraEnabled = false, float ExtraAngleX = 0, float ExtraAngleY = 0, float ExtraAngleZ = 0, float ExtraX = 0, float ExtraY = 0, float ExtraZ = 0)
{
    [JsonIgnore]
    public BattleActorPoseTransform? PoseTransform => ExtraEnabled ? new(ExtraAngleX, ExtraAngleY, ExtraAngleZ, ExtraX, ExtraY, ExtraZ) : null;

    [JsonIgnore]
    public byte Alpha => Transparency >> 4 >= 256 ? (byte)0 : (byte)(255 - (Transparency >> 4));
}

public sealed record Ff7BattleActorModelWrite(int Target, Ff7BattleActorModelModifiers? Modifiers = null, short? WorldZ = null, bool RestoreHome = false, bool Ready = false);
public sealed record Ff7BattleActorModelSound(int Target, ushort Sound, int Channel = 3);
public sealed record Ff7BattleBossDeathShakeRequest(int Target, short BaseZ);
public sealed record Ff7BattleActorDeathResources(float AngleStep, IReadOnlyList<short> LiftSteps);
public sealed record Ff7BattleActorDeathState(int Target, byte Kind, bool PlaySound = true, short Remaining = -1);
public sealed record Ff7BattleActorDeathUpdate(Ff7BattleActorDeathState? State, Ff7BattleActorModelWrite Write, Ff7BattleActorModelSound? Sound = null, bool StartShake = false);
public sealed record Ff7BattleBossDeathShakeState(int Target, short BaseZ, short Remaining = 64);
public sealed record Ff7BattleBossDeathShakeUpdate(Ff7BattleBossDeathShakeState? State, Ff7BattleActorModelWrite? Write = null, bool Flash = false);

public static class Ff7BattleActorDeath
{
    public static Ff7BattleActorDeathUpdate Advance(Ff7BattleActorDeathState state, Ff7BattleActorModelModifiers current, Ff7BattleActorDeathResources? resources = null)
    {
        if (state.Target is < 0 or > 9 || state.Kind is not (0 or 1 or 17 or 18) || state.Remaining < -1 || state.Remaining > (state.Kind >= 17 ? 64 : 14))
        {
            throw new InvalidDataException("FFVII actor death callback lost its physical actor, native kind or countdown.");
        }
        bool first = state.Remaining == -1;
        int remaining = first ? state.Kind >= 17 ? 64 : 14 : state.Remaining;
        bool done = state.Kind >= 17 ? remaining == 1 : remaining == 0;
        if (done)
        {
            Ff7BattleActorModelModifiers hidden = current with { ScaleOverride = false, Visible = false };
            if (state.Kind >= 17)
            {
                hidden = hidden with { Red = 0, Transparency = 4096 };
            }
            return new(null, new(state.Target, hidden, RestoreHome: true, Ready: true));
        }
        Ff7BattleActorModelModifiers next = first ? current with { ScaleOverride = true, Red = state.Kind >= 17 ? (byte)255 : (byte)248, Green = 0, Blue = 0, Transparency = 2048, Deferred = true } : current;
        int redStep = state.Kind >= 17 ? 4 : 16;
        int transparencyStep = state.Kind >= 17 ? 32 : 128;
        next = next with { Red = unchecked((byte)(next.Red - redStep)), Transparency = unchecked((ushort)(next.Transparency + transparencyStep)) };
        if (state.Kind == 1)
        {
            if (resources is not { LiftSteps.Count: 15 } || !float.IsFinite(resources.AngleStep))
            {
                throw new InvalidDataException("FFVII spinning actor death requires its original angle and lift resources.");
            }
            next = next with { ExtraEnabled = true, ExtraAngleX = next.ExtraAngleX + resources.AngleStep, ExtraAngleY = next.ExtraAngleY + resources.AngleStep, ExtraAngleZ = next.ExtraAngleZ + resources.AngleStep, ExtraY = next.ExtraY + resources.LiftSteps[remaining] };
        }
        Ff7BattleActorModelSound? sound = first && state.PlaySound ? new(state.Target, state.Kind >= 17 ? (ushort)0x1C0 : (ushort)0x16) : null;
        return new(state with { Remaining = (short)(remaining - 1) }, new(state.Target, next), sound, first && state.Kind >= 17);
    }

    public static Ff7BattleBossDeathShakeUpdate AdvanceShake(Ff7BattleBossDeathShakeState state)
    {
        if (state.Target is < 0 or > 9 || state.Remaining is < 0 or > 64)
        {
            throw new InvalidDataException("FFVII boss death shake lost its actor or countdown.");
        }
        if (state.Remaining == 0)
        {
            return new(null);
        }
        short z = unchecked((short)(state.BaseZ + ((state.Remaining & 1) != 0 ? 64 : -64)));
        return new(state with { Remaining = (short)(state.Remaining - 1) }, new(state.Target, WorldZ: z), state.Remaining is 64 or 58);
    }
}
