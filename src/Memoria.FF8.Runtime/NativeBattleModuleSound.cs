namespace Memoria.FF8.Runtime;

public sealed record NativeBattleModuleSound(uint Sound, ushort Flags, NativeBattleModuleAnchor? Anchor)
{
    public byte? Pan { get; init; }
}
