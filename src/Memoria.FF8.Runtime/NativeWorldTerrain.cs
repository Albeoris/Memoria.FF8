namespace Memoria.FF8.Runtime;

public readonly record struct NativeWorldTerrainHeightResult(int Height, int FlightHeight);

public static class NativeWorldTerrain
{
    public static byte PermissionMask(int entityType)
    {
        // Native53E6B0 consumes an unsigned WORD entity and the packed flags' high WORD.
        ushort entity = unchecked((ushort)entityType);
        if (entity < 10 || entity == 128)
        {
            return 0x80;
        }
        if (entity is >= 32 and <= 40 or 132)
        {
            return 0x40;
        }
        return entity switch
        {
            48 => 0x20,
            49 => 0x10,
            _ => 0
        };
    }

    public static bool CanTraverse(uint packedFlags, int entityType, bool collisionEnabled = true)
    {
        // Native53E6B0: C75CF4=0 bypasses permissions; other native entities return1.
        byte permission = PermissionMask(entityType);
        return !collisionEnabled || permission == 0 || ((packedFlags >> 16) & permission) != 0;
    }

    public static bool CanDisembark(uint packedFlags, int entityType, bool collisionEnabled = true)
    {
        // Native53E730 uses a signed WORD entity. Packed flags are groundType | WORD14<<8.
        short entity = unchecked((short)entityType);
        uint flags = collisionEnabled ? packedFlags : packedFlags | 0xFFFFFF00;
        if (entity is >= 32 and <= 40 or 132)
        {
            return ((flags >> 16) & 4) != 0;
        }
        return entity switch
        {
            48 => ((flags >> 16) & 2) != 0,
            49 => ((flags >> 16) & 1) != 0,
            50 => ((flags >> 8) & 0x80) != 0,
            _ => true
        };
    }

    public static int ProbeRadius(int entityType)
    {
        // Native53BB90 chooses the five-point53D8A0 movement footprint.
        if (entityType is >= 16 and <= 22)
        {
            return 32;
        }
        RequireHeightEntity(entityType);
        if (entityType == 50)
        {
            return 256;
        }
        return entityType is >= 32 and <= 40 or 132 or 48 ? 64 : 32;
    }

    public static NativeWorldTerrainHeightResult ResolveHeight(int entityType, int groundHeight, byte groundType, int currentHeight, byte scriptMode = 0, int rawSpeed = 0, int flightHeight = 0)
    {
        // Native53ECE0 assembly: probe+36, body203EE88 and flight203FE4A are
        // narrowed at the original signed-WORD reads/comparisons, including wrap.
        RequireHeightEntity(entityType);
        short ground = unchecked((short)groundHeight);
        short current = unchecked((short)currentHeight);
        short flight = unchecked((short)flightHeight);
        if (entityType == 50)
        {
            if (scriptMode is not (0 or 13))
            {
                throw new NotSupportedException("Native flight height during scripted transitions returns the probe pointer's low WORD; it has no represented terrain height authority.");
            }
            // Ground WORD minus200 is compared as a DWORD, then stored as a WORD.
            int selected = flight < -3584 ? -3584 : Math.Min(flight, ground - 200);
            short nextFlight = unchecked((short)selected);
            return new NativeWorldTerrainHeightResult(nextFlight, nextFlight);
        }
        if (entityType == 48)
        {
            short upper = unchecked((short)(ground - 128));
            short lower = unchecked((short)(ground - 256));
            if (groundType <= 5)
            {
                upper = unchecked((short)(upper + 320));
                lower = unchecked((short)(lower + 320));
            }
            short selected = current;
            if (current < lower)
            {
                selected = lower;
            }
            else if (current > upper)
            {
                selected = upper;
            }
            if (groundType > 5 && selected > ground)
            {
                selected = ground;
            }
            return new NativeWorldTerrainHeightResult(selected, flight);
        }
        if (groundType <= 5)
        {
            return new NativeWorldTerrainHeightResult(unchecked((short)(ground + 320)), flight);
        }
        if (entityType != 49 || groundType is < 30 or > 34)
        {
            return new NativeWorldTerrainHeightResult(ground, flight);
        }
        short maximum = unchecked((short)(ground + 128));
        short height = current;
        if (rawSpeed != 0)
        {
            if (height > ground)
            {
                height = unchecked((short)(height - 4));
            }
        }
        else if (height < maximum)
        {
            height = unchecked((short)(height + 4));
        }
        if (height < ground)
        {
            height = ground;
        }
        else if (height > maximum)
        {
            height = maximum;
        }
        return new NativeWorldTerrainHeightResult(height, flight);
    }

    private static void RequireHeightEntity(int entityType)
    {
        if (entityType is >= 0 and < 10 or 128 or >= 32 and <= 40 or 132 or 48 or 49 or 50)
        {
            return;
        }
        throw new NotSupportedException($"Native terrain height and footprint for entity {entityType} have no represented movement contract.");
    }
}
