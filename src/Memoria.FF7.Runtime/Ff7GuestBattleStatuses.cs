namespace Memoria.FF7.Runtime;

/// <summary>The first mixed profile maps semantic persistent statuses and keeps transient time in the host.</summary>
public static class Ff7GuestBattleStatuses
{
    public const uint SupportedHost = 1u | 4u | 8u | 0x80u | 0x100u | 0x200u | 0x04000000u;
    public const ushort SupportedOrigin = 1 | 2 | 8 | 16;

    public static uint FromOrigin(ushort saved)
    {
        if ((saved & ~SupportedOrigin) != 0)
        {
            throw new NotSupportedException("The mixed profile cannot project this FFVIII persistent status.");
        }
        uint host = 0;
        if ((saved & 1) != 0)
        {
            host |= Ff7NativeBattleActionResolver.Death;
        }
        if ((saved & 2) != 0)
        {
            host |= Ff7NativeBattleActionResolver.Poison;
        }
        if ((saved & 8) != 0)
        {
            host |= Ff7NativeBattleActionResolver.Darkness;
        }
        if ((saved & 16) != 0)
        {
            host |= 0x80;
        }
        return host;
    }

    public static ushort ToOrigin(uint host)
    {
        if ((host & ~SupportedHost) != 0)
        {
            throw new NotSupportedException("The mixed profile cannot settle this host status into FFVIII.");
        }
        ushort saved = 0;
        if ((host & Ff7NativeBattleActionResolver.Death) != 0)
        {
            saved |= 1;
        }
        if ((host & Ff7NativeBattleActionResolver.Poison) != 0)
        {
            saved |= 2;
        }
        if ((host & Ff7NativeBattleActionResolver.Darkness) != 0)
        {
            saved |= 8;
        }
        if ((host & 0x80) != 0)
        {
            saved |= 16;
        }
        return saved;
    }
}
