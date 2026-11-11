namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleAuthoredSound(ushort Sound, byte Pan);

public sealed record Ff7BattleDelayedSound(ushort Sound, int Delay);

public sealed record Ff7BattleImpactPresentation(int Source, int Target, int Sound, int ReactionEffect, int Hitmark)
{
    public short? ModelAction { get; init; }
}

public sealed record Ff7BattleSoundState(Ff7BattleDelayedSound[] Pending, long Sequence = 0, bool EscapePlayed = false)
{
    public static Ff7BattleSoundState Empty { get; } = new([]);
    public Ff7BattleAuthoredSound[] DueEffects { get; init; } = [];
    public Ff7BattleImpactPresentation[] PreparedImpacts { get; init; } = [];
    public Ff7BattleImpactPresentation[] DueImpacts { get; init; } = [];
    public Ff7BattleImpactPresentation[] PendingHitmarks { get; init; } = [];
}

public sealed record Ff7BattleSoundReceipt(long Sequence, ushort Sound, byte Pan, int Channel = 1);

public sealed record Ff7BattleSoundUpdate(Ff7BattleSoundState State, IReadOnlyList<Ff7BattleSoundReceipt> Sounds);

public static class Ff7NativeBattleSound
{
    public static Ff7BattleSoundUpdate Impact(Ff7BattleSoundState state, int sound, int impactEffect, int sourceSlot, short targetScreenX)
    {
        if (sound == -1)
        {
            return new(state, []);
        }

        int selected = impactEffect == -1 && sourceSlot >= 4 ? 5 : sound;
        return Emit(state, unchecked((ushort)selected), ActorPan(targetScreenX));
    }

    public static Ff7BattleSoundUpdate Escape(Ff7BattleSoundState state)
    {
        return state.EscapePlayed ? new(state, []) : Emit(state with { EscapePlayed = true }, 0x185, 64);
    }

    public static Ff7BattleSoundUpdate Emit(Ff7BattleSoundState state, ushort sound, byte pan, int channel = 1)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pan, (byte)127);
        ArgumentOutOfRangeException.ThrowIfLessThan(channel, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(channel, 5);
        long sequence = checked(state.Sequence + 1);
        return new(state with { Sequence = sequence }, [new(sequence, sound, pan, channel)]);
    }

    public static int WeaponImpactSound(ReadOnlySpan<byte> weapon, bool critical, bool missed, bool berserk)
    {
        if (weapon.Length != 44)
        {
            throw new InvalidDataException("FFVII weapon impact sound requires its complete original record.");
        }

        int index = missed ? 2 : critical ? 1 : 0;
        if (berserk)
        {
            return index == 2 ? 5 : 363 + index;
        }

        return weapon[36 + index] | ((weapon[11] & (1 << index)) != 0 ? 0x100 : 0);
    }

    public static byte PointPan(short projectedScreenX)
    {
        return (byte)(Math.Clamp((int)projectedScreenX, 0, 319) * 128 / 320);
    }

    public static byte ActorPan(short projectedScreenX)
    {
        return (byte)((projectedScreenX / 5 * 2) & 0x7F);
    }

    public static int SelectChannel(uint compressedByteLength, bool loop)
    {
        return compressedByteLength > 0x20000 && !loop ? 0 : 1;
    }
}
