namespace Memoria.FF7.Runtime;

public sealed partial class Ff7SubmarineRuntime
{
    private void FirePlayerTorpedo()
    {
        int launcher = -1;
        for (int index = 0; index < 4; index++)
        {
            if (PlayerInt(0xAC + index * 4) == 0x20000)
            {
                launcher = index;
                break;
            }
        }
        int slot = Array.FindIndex(state.Torpedoes, 0, 16, torpedo => Read32(torpedo, 0x7C) == 1);
        int target = Array.FindIndex(state.Ships, ship => (Read32(ship, 0x38) & 0x800) != 0);
        if (launcher < 0 || slot < 0 || target < 0)
        {
            return;
        }
        int[] torpedo = state.Torpedoes[slot];
        for (int axis = 0; axis < 3; axis++)
        {
            int lateral = unchecked(PlayerWord(0x1C + axis * 2) * PlayerInt(0x1A8));
            if ((launcher & 1) != 0)
            {
                lateral = unchecked(-lateral);
            }
            int position = unchecked(PlayerInt(axis * 4) + lateral + PlayerWord(0x24 + axis * 2) * PlayerInt(0x1AC));
            Write32(torpedo, axis * 4, position);
            Write32(torpedo, 0x10 + axis * 4, Read32(state.Ships[target], axis * 4));
            Write16(torpedo, 0x20 + axis * 2, PlayerWord(0x14 + axis * 2));
        }
        Write32(torpedo, 0x70, Math.Max(PlayerInt(0xA4), ReadGlobal(0x988510) / 2));
        Write32(torpedo, 0x74, 48);
        Write32(torpedo, 0x7C, 2);
        Write32(torpedo, 0x84, target);
        Write32(torpedo, 0x88, PlayerInt(0x1B0));
        PlayerInt(0xAC + launcher * 4, 0);
        InitializeTorpedoTrail(torpedo);
        EmitSound(0x1D2);
    }

    private void FireEnemyTorpedo(int shipIndex, int launcher)
    {
        if ((uint)shipIndex >= state.Ships.Length || (uint)launcher >= 4)
        {
            throw new ArgumentOutOfRangeException(nameof(shipIndex));
        }
        int[] ship = state.Ships[shipIndex];
        if (Read32(ship, 0x60 + launcher * 4) != 0x20000)
        {
            return;
        }
        int slot = Array.FindIndex(state.Torpedoes, 16, 16, torpedo => Read32(torpedo, 0x7C) == 1);
        if (slot < 0 || Read32(ship, 0x40) >= 512)
        {
            return;
        }
        int[] torpedo = state.Torpedoes[slot];
        for (int axis = 0; axis < 3; axis++)
        {
            Write32(torpedo, axis * 4, Read32(ship, axis * 4));
            Write32(torpedo, 0x10 + axis * 4, PlayerInt(axis * 4));
            Write16(torpedo, 0x20 + axis * 2, Read16(ship, 0x1C + axis * 2));
        }
        Write32(torpedo, 0x70, ReadGlobal(0x98851C) / 2);
        Write32(torpedo, 0x74, 48);
        Write32(torpedo, 0x7C, 2);
        Write32(torpedo, 0x84, -1);
        Write32(torpedo, 0x88, PlayerInt(0x1B0));
        Write32(ship, 0x60 + launcher * 4, 0);
        InitializeTorpedoTrail(torpedo);
    }

    private static void InitializeTorpedoTrail(int[] torpedo)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            Write16(torpedo, 0xA0 + axis * 2, Read32(torpedo, axis * 4) >> 12);
        }
        for (int index = 0; index < 5; index++)
        {
            Write32(torpedo, 0x8C + index * 4, 0xA0 + index * 8);
            if (index != 0)
            {
                Write32(torpedo, 0xA0 + index * 8, Read32(torpedo, 0xA0));
                Write32(torpedo, 0xA4 + index * 8, Read32(torpedo, 0xA4));
            }
        }
    }

    private void ReadTorpedoTargetMetrics(int[] torpedo)
    {
        int x = unchecked(Read32(torpedo, 0x10) - Read32(torpedo, 0)) >> 12;
        int y = unchecked(Read32(torpedo, 0x14) - Read32(torpedo, 4)) >> 12;
        int z = unchecked(Read32(torpedo, 0x18) - Read32(torpedo, 8)) >> 12;
        var direction = Normalize(x, y, z);
        Write32(torpedo, 0x64, direction.Length);
        var cross = TorpedoCross(direction.X, direction.Y, direction.Z, ReadGlobal(0xE996E8), ReadGlobal(0xE996EC), ReadGlobal(0xE996F0));
        var normal = Normalize(cross.X, cross.Y, cross.Z);
        if (normal.Length == 0)
        {
            cross = TorpedoCross(direction.X, direction.Y, direction.Z, ReadGlobal(0x9870A0), ReadGlobal(0x9870A4), ReadGlobal(0x9870A8));
            normal = Normalize(cross.X, cross.Y, cross.Z);
            if (normal.Length == 0)
            {
                cross = TorpedoCross(direction.X, direction.Y, direction.Z, ReadGlobal(0x987080), ReadGlobal(0x987084), ReadGlobal(0x987088));
                normal = Normalize(cross.X, cross.Y, cross.Z);
            }
        }
        int horizontal = Normalize(direction.X, 0, direction.Z).Length;
        Write16(torpedo, 0x40, -NativeAngle(direction.Y, horizontal));
        Write16(torpedo, 0x42, -NativeAngle(normal.Z, normal.X));
        Write16(torpedo, 0x44, 0);
    }

    private static (int X, int Y, int Z) TorpedoCross(int ax, int ay, int az, int bx, int by, int bz)
    {
        return (unchecked((int)(float)(((double)ay * bz - (double)az * by) / 4096.0)), unchecked((int)(float)(((double)az * bx - (double)ax * bz) / 4096.0)), unchecked((int)(float)(((double)ax * by - (double)ay * bx) / 4096.0)));
    }

    private static int AdvanceTorpedoAngle(int target, int current)
    {
        target = (target + 4096) & 4095;
        current = (current + 4096) & 4095;
        int delta = target - current;
        if (delta > 2048)
        {
            delta -= 4096;
        }
        if (delta < -2048)
        {
            delta += 4096;
        }
        int step = delta / 16;
        for (int divisor = 8; step == 0 && divisor >= 2; divisor /= 2)
        {
            step = delta / divisor;
        }
        if (step == 0)
        {
            step = delta;
        }
        current += step;
        if (current > 2048)
        {
            current -= 4096;
        }
        if (current < -2048)
        {
            current += 4096;
        }
        return current;
    }

    private void AdvanceTorpedoGuidance(int[] torpedo)
    {
        Write16(torpedo, 0x20, AdvanceTorpedoAngle(Read16(torpedo, 0x40), Read16(torpedo, 0x20)));
        Write16(torpedo, 0x22, AdvanceTorpedoAngle(Read16(torpedo, 0x42), Read16(torpedo, 0x22)));
        Write16(torpedo, 0x24, (Read16(torpedo, 0x24) + 256) & 4095);
        int pitch = Read16(torpedo, 0x20);
        int yaw = Read16(torpedo, 0x22);
        int roll = Read16(torpedo, 0x24);
        int sine = unchecked((short)NativeSin((roll + 4096) & 4095));
        int cosine = unchecked((short)NativeCos((roll + 4096) & 4095));
        WriteTorpedoVector(torpedo, 0x28, RotatePlayerBasisVector(new(cosine, sine, 0), pitch, yaw));
        WriteTorpedoVector(torpedo, 0x30, RotatePlayerBasisVector(new(-sine, cosine, 0), pitch, yaw));
        WriteTorpedoVector(torpedo, 0x38, RotatePlayerBasisVector(new(0, 0, 4096), pitch, yaw));
        WriteTorpedoVector(torpedo, 0x48, PlayerDirection(Read16(torpedo, 0x40), Read16(torpedo, 0x42)));
        Write32(torpedo, 0x68, TorpedoDot(torpedo, 0x38, torpedo, 0x48));
        int column = ((Read32(torpedo, 0) >> 12) + 2048) / 64;
        int row = (2048 - (Read32(torpedo, 8) >> 12)) / 64;
        Write32(torpedo, 0x80, unchecked(row * 64 + column));
        int speed = Read32(torpedo, 0x70);
        int delta = unchecked(Read32(torpedo, 0x6C) - speed);
        speed = unchecked(speed + (delta > 4 ? delta / 4 : delta > 0 ? 1 : delta < 0 ? -1 : 0));
        Write32(torpedo, 0x70, speed);
    }

    private static void WriteTorpedoVector(int[] torpedo, int offset, PlayerVector vector)
    {
        Write16(torpedo, offset, vector.X);
        Write16(torpedo, offset + 2, vector.Y);
        Write16(torpedo, offset + 4, vector.Z);
    }

    private static int TorpedoDot(int[] first, int firstOffset, int[] second, int secondOffset)
    {
        return unchecked(Read16(first, firstOffset) * Read16(second, secondOffset) + Read16(first, firstOffset + 2) * Read16(second, secondOffset + 2) + Read16(first, firstOffset + 4) * Read16(second, secondOffset + 4)) >> 12;
    }

    private void ReadTorpedoPlayerMetrics(int[] torpedo)
    {
        int x = unchecked(Read32(torpedo, 0) - PlayerInt(0)) >> 12;
        int y = unchecked(Read32(torpedo, 4) - PlayerInt(4)) >> 12;
        int z = unchecked(Read32(torpedo, 8) - PlayerInt(8)) >> 12;
        Write16(torpedo, 0x50, x);
        Write16(torpedo, 0x52, y);
        Write16(torpedo, 0x54, z);
        var normal = Normalize(x, y, z);
        Write32(torpedo, 0x58, normal.Length);
        int nx = unchecked((short)normal.X);
        int ny = unchecked((short)normal.Y);
        // The native metric repeats its Y product for the third component.
        int nz = ny;
        Write32(torpedo, 0x60, unchecked(nx * PlayerWord(0x2C) + ny * PlayerWord(0x2E) + nz * PlayerWord(0x30)) >> 12);
        Write32(torpedo, 0x5C, unchecked((short)-nx * PlayerWord(0x1C) + (short)-ny * PlayerWord(0x1E) + (short)-nz * PlayerWord(0x20)) >> 12);
    }

    private static void AdvanceTorpedoTrail(int[] torpedo)
    {
        int oldest = Read32(torpedo, 0x9C);
        if (oldest < 0xA0 || oldest > 0xC0 || (oldest & 7) != 0)
        {
            throw new InvalidDataException("FFVII submarine torpedo trail has an invalid local record offset.");
        }
        for (int axis = 0; axis < 3; axis++)
        {
            Write16(torpedo, oldest + axis * 2, Read32(torpedo, axis * 4) >> 12);
        }
        for (int offset = 0x9C; offset > 0x8C; offset -= 4)
        {
            Write32(torpedo, offset, Read32(torpedo, offset - 4));
        }
        Write32(torpedo, 0x8C, oldest);
    }

    private int[]? ReadTorpedoTarget(int[] torpedo)
    {
        int target = Read32(torpedo, 0x84);
        return target switch
        {
            -2 => null,
            -1 => state.Player,
            >= 0 and < 12 => state.Ships[target],
            _ => throw new InvalidDataException("FFVII submarine torpedo has an invalid target slot.")
        };
    }

    private static void SetTorpedoState(int[] torpedo, int flags, int lifetime)
    {
        Write32(torpedo, 0x7C, flags);
        Write32(torpedo, 0x74, lifetime);
        Write32(torpedo, 0x84, -2);
    }

    private void AdvanceTorpedoes()
    {
        foreach (int[] torpedo in state.Torpedoes)
        {
            ReadTorpedoTargetMetrics(torpedo);
        }
        for (int slot = 0; slot < state.Torpedoes.Length; slot++)
        {
            int[] torpedo = state.Torpedoes[slot];
            if ((Read32(torpedo, 0x7C) & 2) != 0)
            {
                AdvanceTorpedoGuidance(torpedo);
            }
            int x = Read32(torpedo, 0);
            int y = Read32(torpedo, 4);
            int z = Read32(torpedo, 8);
            if ((Read32(torpedo, 0x7C) & 6) != 0)
            {
                int speed = Read32(torpedo, 0x70);
                x = unchecked(x + (Read16(torpedo, 0x38) * speed >> 12));
                y = unchecked(y + (Read16(torpedo, 0x3A) * speed >> 12));
                z = unchecked(z + (Read16(torpedo, 0x3C) * speed >> 12));
            }
            if (x < -0x800000 || x > 0x800000 || z < -0x800000 || z > 0x800000 || y < 0 || y > 0x400000)
            {
                SetTorpedoState(torpedo, 1, 0);
            }
            else
            {
                Write32(torpedo, 0, x);
                Write32(torpedo, 4, y);
                Write32(torpedo, 8, z);
            }
            if ((Read32(torpedo, 0x7C) & 6) != 0)
            {
                Write32(torpedo, 0x88, unchecked(Read32(torpedo, 0x88) - 1));
                if (Read32(torpedo, 0x88) <= 0)
                {
                    AdvanceTorpedoTrail(torpedo);
                    Write32(torpedo, 0x88, PlayerInt(0x1B0));
                }
                ReadTorpedoPlayerMetrics(torpedo);
                int cell = Read32(torpedo, 0x80);
                if ((uint)cell >= 4096)
                {
                    throw new NotSupportedException("FFVII submarine torpedo reached an unsupported native terrain cell.");
                }
                int flags = state.Cells[cell * 8 + 5];
                int aboveMine = (Read32(torpedo, 4) >> 12) - state.Cells[cell * 8 + 6];
                if ((flags & 0x2000) != 0 && (flags & 0x8000) == 0 && aboveMine > -64 && aboveMine < 0)
                {
                    SetTorpedoState(torpedo, 8, 12);
                    state.Cells[cell * 8 + 5] = (ushort)(flags | 0x8080);
                    EmitSound(0x229);
                }
                if (state.Cells[cell * 8 + 7] - (Read32(torpedo, 4) >> 12) < 0)
                {
                    SetTorpedoState(torpedo, 8, 12);
                }
            }
            int phase = Read32(torpedo, 0x7C);
            int lifetime = Read32(torpedo, 0x74);
            switch (phase)
            {
                case 2:
                    if (lifetime <= 0)
                    {
                        SetTorpedoState(torpedo, 16, 12);
                        CountMissedPlayerTorpedo(slot);
                        break;
                    }
                    if (Read32(torpedo, 0x64) < Read32(torpedo, 0x78))
                    {
                        int[]? target = ReadTorpedoTarget(torpedo);
                        if (target is not null)
                        {
                            EmitSound(0x22A);
                            if (ReferenceEquals(target, state.Player))
                            {
                                WriteGlobal(0x987050, unchecked(ReadGlobal(0x987050) + ReadGlobal(0x987040)));
                            }
                            Write32(target, 0x10, unchecked(Read32(target, 0x10) - 512));
                        }
                        SetTorpedoState(torpedo, 32, 12);
                    }
                    else
                    {
                        int[]? target = ReadTorpedoTarget(torpedo);
                        if (Read32(torpedo, 0x68) < 2048 || target is not null && Read32(target, 0x10) <= 0)
                        {
                            Write32(torpedo, 0x7C, 4);
                            Write32(torpedo, 0x84, -2);
                            if (Read32(torpedo, 0x74) >= 32)
                            {
                                Write32(torpedo, 0x74, 32);
                            }
                            CountMissedPlayerTorpedo(slot);
                        }
                        else if (target is not null)
                        {
                            for (int axis = 0; axis < 3; axis++)
                            {
                                Write32(torpedo, 0x10 + axis * 4, Read32(target, axis * 4));
                            }
                        }
                    }
                    Write32(torpedo, 0x74, unchecked(Read32(torpedo, 0x74) - 1));
                    break;
                case 4:
                    if (lifetime <= 0 || Read32(torpedo, 0x64) < Read32(torpedo, 0x78))
                    {
                        Write32(torpedo, 0x7C, 16);
                        Write32(torpedo, 0x74, 12);
                    }
                    if (lifetime > 0)
                    {
                        Write32(torpedo, 0x74, unchecked(Read32(torpedo, 0x74) - 1));
                    }
                    break;
                case 8:
                case 16:
                case 32:
                    Write32(torpedo, 0x74, unchecked(lifetime - 1));
                    if (Read32(torpedo, 0x74) <= 0)
                    {
                        SetTorpedoState(torpedo, 1, 0);
                    }
                    break;
            }
        }
    }

    private void CountMissedPlayerTorpedo(int slot)
    {
        if (slot < 16)
        {
            WriteGlobal(0xEC7AA0, unchecked(ReadGlobal(0xEC7AA0) + 1));
        }
    }
}
