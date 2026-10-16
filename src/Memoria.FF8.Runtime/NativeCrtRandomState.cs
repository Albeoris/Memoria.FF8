namespace Memoria.FF8.Runtime;

/// <summary>FF8_EN CRT thread random DWORD:560565 initializes1;55CBD2 advances independently of48F020.</summary>
public sealed record NativeCrtRandomState(uint Seed)
{
    public static NativeCrtRandomState FromColdProcess()
    {
        return new(1);
    }

    public NativeCrtRandomState Draw(out ushort value)
    {
        uint seed = unchecked(214013 * Seed + 2531011);
        value = (ushort)((seed >> 16) & 0x7FFF);
        return new(seed);
    }
}
