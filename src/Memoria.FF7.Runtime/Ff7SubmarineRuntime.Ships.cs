namespace Memoria.FF7.Runtime;

public sealed partial class Ff7SubmarineRuntime
{
    private void InitializeShips()
    {
        WriteGlobal(0xE981C0, 0);
        WriteGlobal(0xE981C4, 0);
        WriteGlobal(0xE981C8, 0);
        WriteGlobal(0xE981CC, 0);
        for (int slot = 0; slot < state.Ships.Length; slot++)
        {
            var source = resources.Fleet[slot];
            int[] ship = state.Ships[slot];
            Write32(ship, 0, unchecked(source.X << 18));
            Write32(ship, 4, unchecked(source.Y << 18));
            Write32(ship, 8, unchecked(source.Z << 18));
            Write32(ship, 0x10, source.HitPoints);
            Write16(ship, 0x1C, source.Pitch);
            Write16(ship, 0x1E, source.Yaw);
            Write16(ship, 0x20, source.Roll);
            Write32(ship, 0x2C, source.Speed);
            Write32(ship, 0x30, source.TargetSpeed);
            Write32(ship, 0x34, source.Flags);
            Write32(ship, 0x54, source.BehaviorIndex);
            Write32(ship, 0x58, source.RouteIndex);
            if ((source.Flags & 1) != 0)
            {
                WriteGlobal(0xE981C0, ReadGlobal(0xE981C0) + 1);
                WriteGlobal(0xE981CC, ReadGlobal(0xE981CC) + source.HitPoints);
            }
        }
    }

    private static int NativeAngle(int y, int x)
    {
        int absoluteY = Math.Abs(y);
        int absoluteX = Math.Abs(x);
        int angle;
        if (absoluteX == 0)
        {
            angle = absoluteY == 0 ? 0 : 1024;
        }
        else
        {
            bool swap = absoluteY >= absoluteX;
            float radians = (float)Math.Atan2(swap ? absoluteX : absoluteY, swap ? absoluteY : absoluteX);
            angle = unchecked((int)(radians * 57.29577951307855 * 11.377778053283691));
            if (swap)
            {
                angle = 1024 - angle;
            }
        }
        if (x < 0)
        {
            angle = 2048 - angle;
        }
        return y < 0 ? -angle : angle;
    }

    private static (int X, int Y, int Z) NativeCross((int X, int Y, int Z) a, (int X, int Y, int Z) b)
    {
        float ax = a.X / 4096f;
        float ay = a.Y / 4096f;
        float az = a.Z / 4096f;
        float bx = b.X / 4096f;
        float by = b.Y / 4096f;
        float bz = b.Z / 4096f;
        return (unchecked((int)((float)((double)ay * bz - (double)az * by) * 4096d)), unchecked((int)((float)((double)az * bx - (double)ax * bz) * 4096d)), unchecked((int)((float)((double)ax * by - (double)ay * bx) * 4096d)));
    }

    private void UpdateShipDirections()
    {
        foreach (int[] ship in state.Ships)
        {
            if ((Read32(ship, 0x34) & 3) == 0)
            {
                continue;
            }
            int routeIndex = Read32(ship, 0x58);
            if ((uint)routeIndex >= resources.RouteNodes.Count)
            {
                throw new NotSupportedException("A native submarine route points outside its admitted node allocation.");
            }
            var route = resources.RouteNodes[routeIndex];
            var delta = Normalize(unchecked(((route.X << 18) - Read32(ship, 0)) >> 12), unchecked(((route.Y << 18) - Read32(ship, 4)) >> 12), unchecked(((route.Z << 18) - Read32(ship, 8)) >> 12));
            var cross = NativeCross((delta.X, delta.Y, delta.Z), (ReadGlobal(0x987090), ReadGlobal(0x987094), ReadGlobal(0x987098)));
            var normalized = Normalize(cross.X, cross.Y, cross.Z);
            if (normalized.Length == 0)
            {
                cross = NativeCross((delta.X, delta.Y, delta.Z), (ReadGlobal(0x9870A0), ReadGlobal(0x9870A4), ReadGlobal(0x9870A8)));
                normalized = Normalize(cross.X, cross.Y, cross.Z);
                if (normalized.Length == 0)
                {
                    cross = NativeCross((delta.X, delta.Y, delta.Z), (ReadGlobal(0x987080), ReadGlobal(0x987084), ReadGlobal(0x987088)));
                    normalized = Normalize(cross.X, cross.Y, cross.Z);
                }
            }
            int horizontal = Normalize(delta.X, 0, delta.Z).Length;
            Write16(ship, 0x24, -NativeAngle(delta.Y, horizontal));
            Write16(ship, 0x26, -NativeAngle(normalized.Z, normalized.X));
            Write16(ship, 0x28, 0);
        }
    }

    private static short StepShipAngle(short current, short target, int step)
    {
        int start = (current + 4096) & 4095;
        int difference = ((target + 4096) & 4095) - start;
        if (difference > 2048)
        {
            difference -= 4096;
        }
        if (difference < -2048)
        {
            difference += 4096;
        }
        int result = start + Math.Sign(difference) * step;
        if (result > 2048)
        {
            result -= 4096;
        }
        if (result < -2048)
        {
            result += 4096;
        }
        return unchecked((short)result);
    }

    private void AdvanceShips()
    {
        for (int slot = 0; slot < state.Ships.Length; slot++)
        {
            int[] ship = state.Ships[slot];
            int flags = Read32(ship, 0x34);
            if ((flags & 3) != 0)
            {
                Write16(ship, 0x1C, StepShipAngle(Read16(ship, 0x1C), Read16(ship, 0x24), 4));
                Write16(ship, 0x1E, StepShipAngle(Read16(ship, 0x1E), Read16(ship, 0x26), 8));
                int pitch = Read16(ship, 0x1C) & 4095;
                int yaw = Read16(ship, 0x1E) & 4095;
                Write16(ship, 0x14, unchecked(NativeCos(pitch) * NativeSin(yaw)) >> 12);
                Write16(ship, 0x16, -NativeSin(pitch));
                Write16(ship, 0x18, unchecked(NativeCos(pitch) * NativeCos(yaw)) >> 12);
            }
            if ((flags & 1) != 0)
            {
                int speed = Read32(ship, 0x2C);
                int x = unchecked(Read32(ship, 0) + (unchecked(Read16(ship, 0x14) * speed) >> 12));
                int y = unchecked(Read32(ship, 4) + (unchecked(Read16(ship, 0x16) * speed) >> 12));
                int z = unchecked(Read32(ship, 8) + (unchecked(Read16(ship, 0x18) * speed) >> 12));
                if (x is < -0x800000 or > 0x800000 || y is < 0 or > 0x400000 || z is < -0x800000 or > 0x800000)
                {
                    Write32(ship, 0x34, flags & ~1 | 2);
                    continue;
                }
                Write32(ship, 0, x);
                Write32(ship, 4, y);
                Write32(ship, 8, z);
                int acceleration = unchecked(Read32(ship, 0x30) - speed);
                acceleration = acceleration is < -8 or > 8 ? acceleration / 8 : Math.Sign(acceleration);
                Write32(ship, 0x2C, unchecked(speed + acceleration));
            }
            else if ((flags & 0x20) != 0)
            {
                int cell = ShipCell(ship);
                int floor = state.Cells[cell * 8 + 7] << 12;
                if (Read32(ship, 4) < floor)
                {
                    Write32(ship, 4, unchecked(Read32(ship, 4) + 0x800));
                    if (Read16(ship, 0x1C) > -768)
                    {
                        Write16(ship, 0x1C, Read16(ship, 0x1C) - 4);
                    }
                    if (Read16(ship, 0x20) > -768)
                    {
                        Write16(ship, 0x20, Read16(ship, 0x20) - 4);
                        Write32(ship, 0, unchecked(Read32(ship, 0) + Read16(ship, 0x14) / 4));
                        Write32(ship, 8, unchecked(Read32(ship, 8) + Read16(ship, 0x18) / 4));
                    }
                }
                else
                {
                    Write32(ship, 0x34, 0);
                    WriteGlobal(0xE981C4, ReadGlobal(0xE981C4) + 1);
                    WriteGlobal(0xE981C8, ReadGlobal(0xE981C8) - 1);
                }
            }
            if ((Read32(ship, 0x34) & 1) != 0)
            {
                var route = resources.RouteNodes[Read32(ship, 0x58)];
                Write32(ship, 0x5C, Normalize(unchecked(((route.X << 18) - Read32(ship, 0)) >> 12), unchecked(((route.Y << 18) - Read32(ship, 4)) >> 12), unchecked(((route.Z << 18) - Read32(ship, 8)) >> 12)).Length);
            }
            flags = Read32(ship, 0x34);
            if ((flags & 3) == 0)
            {
                continue;
            }
            var relative = Normalize(unchecked((Read32(ship, 0) - Read32(state.Player, 0)) >> 12), unchecked((Read32(ship, 4) - Read32(state.Player, 4)) >> 12), unchecked((Read32(ship, 8) - Read32(state.Player, 8)) >> 12));
            Write32(ship, 0x40, relative.Length);
            Write16(ship, 0x4C, unchecked((Read32(ship, 0) - Read32(state.Player, 0)) >> 12));
            Write16(ship, 0x4E, unchecked((Read32(ship, 4) - Read32(state.Player, 4)) >> 12));
            Write16(ship, 0x50, unchecked((Read32(ship, 8) - Read32(state.Player, 8)) >> 12));
            int forward = Dot(relative, state.Player, 0x2C);
            int side = Dot(relative, state.Player, 0x1C);
            int facing = -Dot(relative, ship, 0x14);
            flags = forward >= ReadGlobal(0x987018) ? flags | 4 : flags & ~4;
            flags = facing >= ReadGlobal(0x98701C) ? flags | 8 : flags & ~8;
            Write32(ship, 0x34, flags);
            Write32(ship, 0x44, forward);
            Write32(ship, 0x48, side);
            if (Read32(ship, 0x10) <= 0)
            {
                Write32(ship, 0x34, 0x20);
                Write32(ship, 0x38, 0);
                Write32(ship, 0x3C, 0);
                Write32(ship, 0x10, 0);
                WriteGlobal(0xE981C8, ReadGlobal(0xE981C8) + 1);
                continue;
            }
            var currentRoute = resources.RouteNodes[Read32(ship, 0x58)];
            if (Read32(ship, 0x5C) < currentRoute.Radius)
            {
                int next = currentRoute.NextIndex;
                if ((uint)next >= resources.RouteNodes.Count)
                {
                    throw new NotSupportedException("A native submarine waypoint leaves the supported node allocation.");
                }
                Write32(ship, 0x58, next);
                Write32(ship, 0x30, resources.RouteNodes[next].TargetSpeed);
            }
            if ((flags & 8) != 0 && ReadGlobal(0x987020) != 0 && Read32(ship, 0x38) != 0)
            {
                for (int launcher = 0; launcher < 4; launcher++)
                {
                    if (Read32(ship, 0x60 + launcher * 4) == 0x20000)
                    {
                        FireEnemyTorpedo(slot, launcher);
                        break;
                    }
                }
            }
            UpdateShipSonar(ship);
            for (int launcher = 0; launcher < 4; launcher++)
            {
                int offset = 0x60 + launcher * 4;
                int reload = Read32(ship, offset);
                if ((reload & 0x10000) != 0)
                {
                    int left = (reload & 65535) - 1;
                    reload = left <= 0 ? 0x20000 : 0x10000 | left;
                }
                else if ((reload & 0x20000) == 0)
                {
                    reload = 0x10000 | ReadGlobal(0x987028);
                }
                Write32(ship, offset, reload);
            }
        }
    }

    private static int Dot((int Length, int X, int Y, int Z) vector, int[] words, int offset)
    {
        return unchecked(unchecked(vector.X * Read16(words, offset)) + unchecked(vector.Y * Read16(words, offset + 2)) + unchecked(vector.Z * Read16(words, offset + 4))) >> 12;
    }

    private int ShipCell(int[] ship)
    {
        int cell = ((2048 - (Read32(ship, 8) >> 12)) / 64) * 64 + ((Read32(ship, 0) >> 12) + 2048) / 64;
        if ((uint)cell >= 4096)
        {
            throw new NotSupportedException("A native submarine body references terrain outside its initialized allocation.");
        }
        return cell;
    }

    private void UpdateShipSonar(int[] ship)
    {
        int flags = Read32(ship, 0x34);
        int mode = Read32(ship, 0x38);
        if (Read32(ship, 0x40) <= ReadGlobal(0x9873F8))
        {
            Write32(ship, 0x34, flags | 0x10);
            if (ReadGlobal(0xEC1824) != 0)
            {
                int range = ReadGlobal(0xEC1828) - Read32(ship, 0x40);
                if (range >= 0 && range <= ReadGlobal(0x9873FC))
                {
                    if (mode == 0)
                    {
                        Write32(ship, 0x38, 0x100);
                        Write32(ship, 0x3C, 60);
                    }
                    else if (mode is 0x200 or 0x400)
                    {
                        Write32(ship, 0x38, 0x400);
                        Write32(ship, 0x3C, 45);
                    }
                    else if (mode == 0x800)
                    {
                        Write32(ship, 0x3C, 45);
                    }
                }
                else if (mode == 0x100)
                {
                    Write32(ship, 0x38, 0x200);
                    Write32(ship, 0x3C, 45);
                }
            }
        }
        else
        {
            Write32(ship, 0x34, flags & ~0x10);
            Write32(ship, 0x38, 0);
            Write32(ship, 0x3C, 0);
        }
        int clock = Read32(ship, 0x3C) - 1;
        Write32(ship, 0x3C, clock);
        if (clock < 0)
        {
            switch (Read32(ship, 0x38))
            {
                case 0x800:
                    Write32(ship, 0x38, 0x400);
                    Write32(ship, 0x3C, 45);
                    break;
                case 0x400:
                    Write32(ship, 0x38, 0x200);
                    Write32(ship, 0x3C, 45);
                    break;
                default:
                    Write32(ship, 0x38, 0);
                    Write32(ship, 0x3C, 0);
                    break;
            }
        }
    }

    private void UpdateTargetLocks()
    {
        foreach (int[] ship in state.Ships)
        {
            if ((Read32(ship, 0x38) & 0x800) == 0)
            {
                continue;
            }
            if ((Read32(ship, 0x34) & 0x10) == 0)
            {
                Write32(ship, 0x38, 0);
                Write32(ship, 0x3C, 0);
            }
            else if ((Read32(ship, 0x34) & 4) == 0)
            {
                Write32(ship, 0x38, 0x200);
                Write32(ship, 0x3C, 45);
            }
            else
            {
                return;
            }
        }
        int[]? nearest = null;
        int distance = int.MaxValue;
        foreach (int[] ship in state.Ships)
        {
            if ((Read32(ship, 0x34) & 0x14) == 0x14 && (Read32(ship, 0x38) & 0x400) != 0 && Read32(ship, 0x40) < distance)
            {
                nearest = ship;
                distance = Read32(ship, 0x40);
            }
        }
        if (nearest is not null)
        {
            Write32(nearest, 0x38, 0x800);
            Write32(nearest, 0x3C, 45);
        }
    }
}
