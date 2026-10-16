namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>
/// The elemental affinities used by magic, attacks and abilities in Final Fantasy VIII.
/// </summary>
[Flags]
public enum Element : byte
{
    None = 0x00,
    Fire = 0x01,
    Ice = 0x02,
    Thunder = 0x04,
    Earth = 0x08,
    Poison = 0x10,
    Wind = 0x20,
    Water = 0x40,
    Holy = 0x80,
}
