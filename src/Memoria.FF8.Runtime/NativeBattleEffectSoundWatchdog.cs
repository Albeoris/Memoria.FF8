namespace Memoria.FF8.Runtime;

/// <summary>50A9A0/50B0C0 increment a storedWORD and use signed JL900 before46B440.</summary>
public static class NativeBattleEffectSoundWatchdog
{
    public static (ushort Counter, bool StopSounds) Advance(ushort counter)
    {
        ushort next = unchecked((ushort)(counter + 1));
        return (next, unchecked((short)next) >= 900);
    }
}
