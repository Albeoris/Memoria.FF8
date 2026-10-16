namespace Memoria.FF8.Runtime;

public sealed record NativeBattleModuleSound(uint Sound, uint Flags, NativeBattleModuleAnchor? Anchor)
{
    public byte? Pan { get; init; }
}
