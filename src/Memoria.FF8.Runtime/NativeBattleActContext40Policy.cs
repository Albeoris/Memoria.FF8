namespace Memoria.FF8.Runtime;

public enum NativeBattleActContext40Euler
{
    Default701310,
    Header40000
}

public enum NativeBattleActContext40Scale
{
    Rows56BEF0,
    Header100
}

public sealed record NativeBattleActContext40Policy(NativeBattleActContext40Euler Euler, NativeBattleActContext40Scale Scale, bool InterpolationAllowed, int MorphScratchLength)
{
    public static NativeBattleActContext40Policy EerieSoundWave130 { get; } = new(NativeBattleActContext40Euler.Header40000, NativeBattleActContext40Scale.Header100, true, 768);
    public static NativeBattleActContext40Policy Hypnotize133 { get; } = new(NativeBattleActContext40Euler.Default701310, NativeBattleActContext40Scale.Rows56BEF0, false, 0);
    public static NativeBattleActContext40Policy BeamLaser134 { get; } = new(NativeBattleActContext40Euler.Default701310, NativeBattleActContext40Scale.Header100, true, 392);

    public static NativeBattleActContext40Policy Sonic139 { get; } = new(NativeBattleActContext40Euler.Header40000, NativeBattleActContext40Scale.Rows56BEF0, true, 0x648);

    public static NativeBattleActContext40Policy Saliva138 { get; } = new(NativeBattleActContext40Euler.Header40000, NativeBattleActContext40Scale.Header100, true, 864);

    public void Validate()
    {
        if (!Enum.IsDefined(Euler) || !Enum.IsDefined(Scale) || MorphScratchLength < 0 || (MorphScratchLength & 7) != 0 || InterpolationAllowed && MorphScratchLength == 0)
        {
            throw new InvalidDataException("ACT context40 requires its actual Euler, scaling and interpolation policy.");
        }
    }
}
