namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>
/// The global durations of timed status effects and battle timers. This section has no
/// associated display text in the original format and always contains a single record.
/// </summary>
public sealed class TimerSettings
{
    /// <summary>The duration, in frames, of each of the 14 timed status effects.</summary>
    public Byte[] StatusTimers { get; set; } = new Byte[14];

    public Byte AtbSpeedMultiplier { get; set; }
    public Byte DeadTimer { get; set; }

    /// <summary>The duration, in frames, of each of the 32 status effects inflicted by a limit break.</summary>
    public Byte[] StatusLimitEffects { get; set; } = new Byte[32];

    /// <summary>Timers and starting move identifiers used by Zell's Duel limit break.</summary>
    public Byte[] DuelTimersAndStartMoves { get; set; } = new Byte[8];

    /// <summary>Timers used by Irvine's Shot limit break.</summary>
    public Byte[] ShotTimers { get; set; } = new Byte[4];
}
