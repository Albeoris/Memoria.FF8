namespace Memoria.FF7.Runtime;

/// <summary>Source saved statuses with an independent owner instead of a host status-bit alias.</summary>
public static class Ff7OriginSavedStatusRules
{
    public const ushort Zombie = 0x40;
    public const ushort Supported = Zombie;

    public static bool IsZombie(ushort statuses) => (statuses & Zombie) != 0;
}
