using System.Buffers.Binary;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7SubmarineRuntime
{
    private int PlayerInt(int offset) => Read32(state.Player, offset);
    private void PlayerInt(int offset, int value) => Write32(state.Player, offset, value);
    private short PlayerWord(int offset) => Read16(state.Player, offset);
    private void PlayerWord(int offset, int value) => Write16(state.Player, offset, value);
    private short GlobalWord(int address) => unchecked((short)(ReadGlobal(address & ~3) >> ((address & 2) * 8)));

    private void GlobalWord(int address, int value)
    {
        int shift = (address & 2) * 8;
        int aligned = address & ~3;
        WriteGlobal(aligned, ReadGlobal(aligned) & ~(65535 << shift) | (value & 65535) << shift);
    }

    private static int DataInt(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
    private static short DataWord(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadInt16LittleEndian(data[offset..]);

    private void InitializeTerrain()
    {
        if (resources.TerrainAscii.Count != 4096 || state.Cells.Length != 4096 * 8 || !state.Globals.ContainsKey(0x9884FC))
        {
            throw new InvalidDataException("FFVII submarine terrain initialization requires the native cells and initial height base.");
        }
        for (int index = 0; index < 4096; index++)
        {
            int height = Math.Clamp(unchecked((sbyte)resources.TerrainAscii[index]) + ReadGlobal(0x9884FC) - 48, 0, 16) << 6;
            state.Cells[index * 8] = (ushort)height;
            state.Cells[index * 8 + 7] = (ushort)height;
        }
        for (int index = 0; index < 4096; index++)
        {
            int column = index % 64;
            int row = index / 64;
            state.Cells[index * 8 + 3] = column == 63 ? (ushort)960 : state.Cells[(index + 1) * 8];
            state.Cells[index * 8 + 2] = column == 0 ? (ushort)960 : state.Cells[(index - 1) * 8];
            state.Cells[index * 8 + 1] = row == 63 ? (ushort)960 : state.Cells[(index + 64) * 8];
            state.Cells[index * 8 + 4] = row == 0 ? (ushort)960 : state.Cells[(index - 64) * 8];
        }
    }

    private void InitializePlayer()
    {
        if (resources.PlayerTypes.Count != 5 || resources.SpeedTable.Count != 19 || resources.MotionTables.Count != 4 || resources.MotionTables.Any(table => table.Count != 33) || resources.Player.Data.Length != 40 || PlayerInt(0xA0) is < 0 or > 18)
        {
            throw new InvalidDataException("FFVII submarine player initialization requires its complete native tables and initial speed state.");
        }
        var player = resources.Player;
        PlayerInt(0, unchecked(player.X << 18));
        PlayerInt(4, unchecked(player.Y << 18));
        PlayerInt(8, unchecked(player.Z << 18));
        PlayerInt(0x10, player.HitPoints);
        PlayerWord(0x14, player.Pitch);
        PlayerWord(0x16, player.Yaw);
        PlayerWord(0x18, player.Roll);
        PlayerWord(0x4C, player.Pitch);
        PlayerWord(0x4E, player.Yaw);
        PlayerWord(0x50, player.Roll);
        PlayerWord(0x5E, player.Yaw);
        PlayerInt(0x6C, player.ActionType);
        PlayerInt(0x70, 0);
        ReadOnlySpan<byte> type = ReadPlayerType(player.ActionType);
        PlayerWord(0x44, DataWord(type, 0));
        PlayerWord(0x46, DataWord(type, 4));
        PlayerWord(0x48, DataWord(type, 8));
        CopyPlayerTypeProperties(type);
        PlayerInt(0x90, PlayerCellIndex(PlayerInt(0), PlayerInt(8)));
        PlayerInt(0xA4, resources.SpeedTable[PlayerInt(0xA0)]);
        PlayerInt(0xA8, unchecked(0x989CF8 + player.BehaviorIndex * 16));
        for (int index = 0; index < 3; index++)
        {
            int angle = PlayerWord(0x44 + index * 2);
            GlobalWord(0xE99738 + index * 2, index == 0 ? angle - 512 : angle);
            GlobalWord(0xE99740 + index * 2, angle);
            GlobalWord(0xE99748 + index * 2, index == 0 ? 512 : index == 1 ? 4096 : 0);
        }
        WriteGlobal(0xE99750, 2048);
        WriteGlobal(0xE99754, unchecked((short)PlayerInt(0x74)));
        WriteGlobal(0xE99758, unchecked((short)(PlayerInt(0x74) - 2048)));
        WriteGlobal(0xE9975C, 2048);
        WriteGlobal(0xE99760, unchecked((short)ReadGlobal(0x987078)));
        WriteGlobal(0xE99764, unchecked((short)(ReadGlobal(0x987078) - 2048)));
        WriteGlobal(0xE99768, 0);
        WriteGlobal(0xE9976C, 32);
        WriteGlobal(0xE99770, 1);
        int scale = DataInt(player.Data.Span, 0x1C);
        for (int index = 0; index < resources.SpeedTable.Count; index++)
        {
            WriteGlobal(0x9870B0 + index * 4, unchecked(scale * resources.SpeedTable[index]) / 16);
        }
    }

    private ReadOnlySpan<byte> ReadPlayerType(int type)
    {
        if ((uint)type >= resources.PlayerTypes.Count || resources.PlayerTypes[type].Length != 0x38)
        {
            throw new InvalidDataException("FFVII submarine player type is outside its native table.");
        }
        return resources.PlayerTypes[type].Span;
    }

    private void CopyPlayerTypeProperties(ReadOnlySpan<byte> type)
    {
        PlayerInt(0x74, DataInt(type, 0x10));
        PlayerInt(0x94, DataInt(type, 0x18));
        WriteGlobal(0x987078, DataInt(type, 0x14));
        PlayerInt(0x1A8, DataInt(type, 0x20));
        PlayerInt(0x1AC, DataInt(type, 0x24));
        PlayerInt(0xC0, DataInt(type, 0x28));
        PlayerInt(0xC4, DataInt(type, 0x2C));
        PlayerInt(0xC8, DataInt(type, 0x30));
    }

    private static int WrapPlayerAngle(int value) => value > 2048 ? value - 4096 : value < -2048 ? value + 4096 : value;

    private void AdvancePlayerInput(ushort held, ushort pressed)
    {
        if (ReadGlobal(0xE74768) == 0)
        {
            bool Held(int mask) => (held & mask) != 0;
            bool Pressed(int mask) => (pressed & mask) != 0;
            int speed = PlayerInt(0xA0);
            if (Held(0x10))
            {
                speed++;
            }
            if (Held(0x40))
            {
                speed--;
            }
            speed = Math.Clamp(speed, 1, 17);
            PlayerInt(0xA0, speed);
            PlayerInt(0xA4, ReadGlobal(0x9870B0 + speed * 4));
            if (Pressed(0x80))
            {
                FirePlayerTorpedo();
            }
            if (Held(0x10) && speed == 17)
            {
                speed++;
            }
            if (Held(0x40) && speed == 1)
            {
                speed--;
            }
            PlayerInt(0xA0, speed);
            PlayerInt(0xA4, ReadGlobal(0x9870B0 + speed * 4));
            int height = PlayerInt(4);
            if (Held(4))
            {
                height = unchecked(height - 4096);
            }
            if (Held(1))
            {
                height = unchecked(height + 4096);
            }
            height = Math.Clamp(height, ReadGlobal(0x988500), 0x400000);
            int cell = PlayerInt(0x90);
            if ((uint)cell >= 4096)
            {
                throw new InvalidDataException("FFVII submarine player has an invalid native terrain cell.");
            }
            if (height < state.Cells[cell * 8 + 7] << 12)
            {
                PlayerInt(4, height);
            }
            int pitch = PlayerWord(0x14);
            int yaw = PlayerWord(0x16);
            int roll = PlayerWord(0x18);
            if (Held(0x1000))
            {
                pitch -= 4;
            }
            if (Held(0x4000))
            {
                pitch += 4;
            }
            if (Held(0x2000))
            {
                yaw += 8;
            }
            if (Held(0x8000))
            {
                yaw -= 8;
            }
            PlayerWord(0x14, Math.Clamp(pitch, -1008, 1008));
            PlayerWord(0x16, WrapPlayerAngle(yaw));
            PlayerWord(0x18, roll);
            int targetPitch = PlayerWord(0x4C);
            int targetYaw = PlayerWord(0x4E);
            int targetRoll = PlayerWord(0x50);
            bool pitchHeld = Held(0x1000) || Held(0x4000);
            bool yawHeld = Held(0x2000) || Held(0x8000);
            if (Held(0x1000))
            {
                targetPitch -= 4;
            }
            if (Held(0x4000))
            {
                targetPitch += 4;
            }
            if (Held(0x2000))
            {
                targetYaw += 8;
            }
            if (Held(0x8000))
            {
                targetYaw -= 8;
            }
            targetPitch = Math.Clamp(targetPitch, -1008, 1008);
            targetYaw = WrapPlayerAngle(targetYaw);
            if (PlayerInt(0x6C) is not (2 or 3) && PlayerInt(0x70) == 0)
            {
                int difference = PlayerWord(0x4C) - PlayerWord(0x14);
                if (pitchHeld)
                {
                    if (difference <= -128 || difference >= 128)
                    {
                        PlayerWord(0x4C, targetPitch);
                    }
                }
                else if (difference < 0)
                {
                    PlayerWord(0x4C, PlayerWord(0x4C) + 4);
                }
                else if (difference > 0)
                {
                    PlayerWord(0x4C, PlayerWord(0x4C) - 4);
                }
                difference = PlayerWord(0x4E) - PlayerWord(0x16);
                if (difference >= 512)
                {
                    difference -= 4096;
                }
                if (difference <= -512)
                {
                    difference += 4096;
                }
                if (yawHeld)
                {
                    if (difference <= -256 || difference >= 256)
                    {
                        PlayerWord(0x4E, targetYaw);
                    }
                }
                else if (difference < 0)
                {
                    PlayerWord(0x4E, PlayerWord(0x4E) + 8);
                }
                else if (difference > 0)
                {
                    PlayerWord(0x4E, PlayerWord(0x4E) - 8);
                }
            }
            else
            {
                PlayerWord(0x4C, targetPitch);
                PlayerWord(0x4E, targetYaw);
            }
            PlayerWord(0x50, targetRoll);
            int weaponYaw = PlayerWord(0x5E);
            if (Held(0x2000))
            {
                weaponYaw += 8;
            }
            if (Held(0x8000))
            {
                weaponYaw -= 8;
            }
            PlayerWord(0x5E, WrapPlayerAngle(weaponYaw));
            int pitchMotion = PlayerWord(0x34);
            int yawMotion = PlayerWord(0x36);
            int rollMotion = PlayerWord(0x38);
            int motionPressed = pressed;
            if (PlayerInt(0x94) == 0 || speed == 9)
            {
                motionPressed &= ~0xF000;
            }
            if ((motionPressed & 0x1000) != 0)
            {
                pitchMotion++;
            }
            else if ((motionPressed & 0x4000) != 0)
            {
                pitchMotion--;
            }
            else
            {
                pitchMotion -= Math.Sign(pitchMotion);
            }
            if ((motionPressed & 0x2000) != 0)
            {
                yawMotion--;
                rollMotion++;
            }
            else if ((motionPressed & 0x8000) != 0)
            {
                yawMotion++;
                rollMotion--;
            }
            else
            {
                yawMotion -= Math.Sign(yawMotion);
                rollMotion -= Math.Sign(rollMotion);
            }
            pitchMotion = Math.Clamp(pitchMotion, -16, 16);
            yawMotion = Math.Clamp(yawMotion, -16, 16);
            rollMotion = Math.Clamp(rollMotion, -16, 16);
            PlayerWord(0x34, pitchMotion);
            PlayerWord(0x36, yawMotion);
            PlayerWord(0x38, rollMotion);
            PlayerWord(0x3C, resources.MotionTables[0][pitchMotion + 16]);
            PlayerWord(0x3E, resources.MotionTables[1][yawMotion + 16]);
            PlayerWord(0x40, resources.MotionTables[2][rollMotion + 16]);
            PlayerWord(0x18, resources.MotionTables[3][rollMotion + 16]);
            int type = PlayerInt(0x6C);
            if (Pressed(2))
            {
                type++;
                if (type > 3)
                {
                    type = 0;
                }
                BeginPlayerTypeTransition(type);
                WriteGlobal(0xE99770, 1);
                PlayerInt(0x70, 0);
                PlayerInt(0x6C, type);
            }
            else if (Pressed(8))
            {
                PlayerInt(0x70, PlayerInt(0x70) ^ 1);
                if (PlayerInt(0x70) == 1)
                {
                    type = 4;
                }
                BeginPlayerTypeTransition(type);
                WriteGlobal(0xE99770, 1);
            }
        }
        if (ReadGlobal(0xE99770) != 0)
        {
            AdvancePlayerViewMetadata();
        }
    }

    private void BeginPlayerTypeTransition(int typeIndex)
    {
        ReadOnlySpan<byte> type = ReadPlayerType(typeIndex);
        int pitch = PlayerWord(0x44);
        if (PlayerInt(0x98) == 0 && DataInt(type, 0x1C) != 0)
        {
            pitch += PlayerWord(0x4C);
        }
        else if (PlayerInt(0x98) != 0 && DataInt(type, 0x1C) == 0)
        {
            pitch -= PlayerWord(0x4C);
        }
        for (int index = 0; index < 3; index++)
        {
            int start = index == 0 ? pitch : PlayerWord(0x44 + index * 2);
            int target = DataInt(type, index * 4);
            GlobalWord(0xE99738 + index * 2, start);
            GlobalWord(0xE99740 + index * 2, target);
            GlobalWord(0xE99748 + index * 2, WrapPlayerAngle(((target + 4096) & 4095) - ((start + 4096) & 4095)));
        }
        WriteGlobal(0xE99750, unchecked((short)PlayerInt(0x74)));
        WriteGlobal(0xE99754, unchecked((short)DataInt(type, 0x10)));
        WriteGlobal(0xE99758, unchecked((short)(DataInt(type, 0x10) - PlayerInt(0x74))));
        WriteGlobal(0xE9975C, unchecked((short)ReadGlobal(0x987078)));
        WriteGlobal(0xE99760, unchecked((short)DataInt(type, 0x14)));
        WriteGlobal(0xE99764, unchecked((short)(DataInt(type, 0x14) - ReadGlobal(0x987078))));
        WriteGlobal(0xE99768, 0);
        WriteGlobal(0xE9976C, 256);
        PlayerInt(0x94, DataInt(type, 0x18));
        PlayerInt(0x98, DataInt(type, 0x1C));
        PlayerInt(0x1A8, DataInt(type, 0x20));
        PlayerInt(0x1AC, DataInt(type, 0x24));
        PlayerInt(0xC0, DataInt(type, 0x28));
        PlayerInt(0xC4, DataInt(type, 0x2C));
        PlayerInt(0xC8, DataInt(type, 0x30));
    }

    private void AdvancePlayerViewMetadata()
    {
        int fraction = ReadGlobal(0xE99768);
        if (fraction < 4096)
        {
            for (int index = 0; index < 3; index++)
            {
                PlayerWord(0x44 + index * 2, unchecked(GlobalWord(0xE99738 + index * 2) + (GlobalWord(0xE99748 + index * 2) * fraction >> 12)));
            }
            PlayerInt(0x74, unchecked(ReadGlobal(0xE99750) + (ReadGlobal(0xE99758) * fraction >> 12)));
            WriteGlobal(0x987078, unchecked(ReadGlobal(0xE9975C) + (ReadGlobal(0xE99764) * fraction >> 12)));
            WriteGlobal(0xE99768, unchecked(fraction + ReadGlobal(0xE9976C)));
        }
        else
        {
            for (int index = 0; index < 3; index++)
            {
                PlayerWord(0x44 + index * 2, GlobalWord(0xE99740 + index * 2));
            }
            PlayerInt(0x74, ReadGlobal(0xE99754));
            WriteGlobal(0x987078, ReadGlobal(0xE99760));
            WriteGlobal(0xE99768, 0);
            WriteGlobal(0xE99770, 0);
        }
    }

    private readonly record struct PlayerVector(int X, int Y, int Z);

    private void WritePlayerVector(int offset, PlayerVector vector)
    {
        PlayerWord(offset, vector.X);
        PlayerWord(offset + 2, vector.Y);
        PlayerWord(offset + 4, vector.Z);
    }

    private PlayerVector ReadPlayerVector(int offset) => new(PlayerWord(offset), PlayerWord(offset + 2), PlayerWord(offset + 4));

    private PlayerVector PlayerDirection(int pitch, int yaw)
    {
        int sinePitch = unchecked((short)NativeSin(pitch));
        int cosinePitch = unchecked((short)NativeCos(pitch));
        int sineYaw = unchecked((short)NativeSin(yaw));
        int cosineYaw = unchecked((short)NativeCos(yaw));
        return new(unchecked((short)(cosinePitch * sineYaw >> 12)), unchecked((short)-sinePitch), unchecked((short)(cosinePitch * cosineYaw >> 12)));
    }

    private static int PlayerMatrixComponent(int first, int firstVector, int second, int secondVector) => unchecked((int)(float)(((double)first * firstVector + (double)second * secondVector) / 4096.0));

    private PlayerVector RotatePlayerBasisVector(PlayerVector vector, int pitch, int yaw)
    {
        int sinePitch = unchecked((short)NativeSin((pitch + 4096) & 4095));
        int cosinePitch = unchecked((short)NativeCos((pitch + 4096) & 4095));
        int sineYaw = unchecked((short)NativeSin((yaw + 4096) & 4095));
        int cosineYaw = unchecked((short)NativeCos((yaw + 4096) & 4095));
        int x = vector.X;
        int y = PlayerMatrixComponent(cosinePitch, vector.Y, -sinePitch, vector.Z);
        int z = PlayerMatrixComponent(sinePitch, vector.Y, cosinePitch, vector.Z);
        return new(unchecked((short)PlayerMatrixComponent(cosineYaw, x, sineYaw, z)), unchecked((short)y), unchecked((short)PlayerMatrixComponent(-sineYaw, x, cosineYaw, z)));
    }

    private void UpdatePlayerBasis()
    {
        int pitch = PlayerWord(0x14);
        int yaw = PlayerWord(0x16);
        int roll = (PlayerWord(0x18) + 4096) & 4095;
        int sineRoll = unchecked((short)NativeSin(roll));
        int cosineRoll = unchecked((short)NativeCos(roll));
        WritePlayerVector(0x1C, RotatePlayerBasisVector(new(cosineRoll, sineRoll, 0), pitch, yaw));
        WritePlayerVector(0x24, RotatePlayerBasisVector(new(-sineRoll, cosineRoll, 0), pitch, yaw));
        WritePlayerVector(0x2C, RotatePlayerBasisVector(new(0, 0, 4096), pitch, yaw));
    }

    private static int PlayerCellIndex(int x4096, int z4096)
    {
        int column = Math.Clamp(((x4096 >> 12) + 2048) / 64, 0, 63);
        int row = Math.Clamp((2048 - (z4096 >> 12)) / 64, 0, 63);
        return row * 64 + column;
    }

    private int ProbePlayerTerrain(PlayerVector vector)
    {
        int x = unchecked(PlayerInt(0) + (vector.X << 6)) >> 12;
        int y = unchecked(PlayerInt(4) + (vector.Y << 6)) >> 12;
        int z = unchecked(PlayerInt(8) + (vector.Z << 6)) >> 12;
        int column = Math.Clamp((x + 2048) / 64, 0, 63);
        int row = Math.Clamp((2048 - z) / 64, 0, 63);
        int index = row * 64 + column;
        int flags = state.Cells[index * 8 + 5];
        int result = 0;
        int heightDifference = y - state.Cells[index * 8 + 6];
        if ((flags & 0x2000) != 0 && (flags & 0x8000) == 0 && heightDifference > -64 && heightDifference < 0)
        {
            result |= 4;
        }
        if (state.Cells[index * 8 + 7] < y)
        {
            result |= 1;
        }
        return result;
    }

    private static PlayerVector AddPlayerVectors(PlayerVector first, PlayerVector second) => new(unchecked(first.X + second.X), unchecked(first.Y + second.Y), unchecked(first.Z + second.Z));
    private static PlayerVector NegatePlayerVector(PlayerVector vector) => new(unchecked(-vector.X), unchecked(-vector.Y), unchecked(-vector.Z));
    private static PlayerVector ScalePlayerVector(PlayerVector vector, int scale) => new(unchecked(vector.X * scale), unchecked(vector.Y * scale), unchecked(vector.Z * scale));
    private static PlayerVector HalfPlayerVector(PlayerVector vector) => new(vector.X / 2, vector.Y / 2, vector.Z / 2);

    private void AdvancePlayerMotion()
    {
        WritePlayerVector(0x2C, PlayerDirection(PlayerWord(0x14), PlayerWord(0x16)));
        UpdatePlayerBasis();
        WritePlayerVector(0x64, PlayerDirection(PlayerWord(0x5C), PlayerWord(0x5E)));
        PlayerInt(0x9C, 0);
        PlayerVector direction = ReadPlayerVector(0x2C);
        int speed = PlayerInt(0xA4);
        int x = unchecked(PlayerInt(0) + (direction.X * speed >> 12));
        int y = unchecked(PlayerInt(4) + (direction.Y * speed >> 12));
        int z = unchecked(PlayerInt(8) + (direction.Z * speed >> 12));
        bool withinBounds = x >= -0x800000 && x <= 0x800000 && z >= -0x800000 && z <= 0x800000;
        if (!withinBounds)
        {
            PlayerInt(0x9C, PlayerInt(0x9C) | 2);
        }
        else
        {
            int candidateCell = ((2048 - (z >> 12)) / 64) * 64 + ((x >> 12) + 2048) / 64;
            if ((uint)candidateCell >= 4096)
            {
                throw new NotSupportedException("FFVII submarine boundary movement addresses terrain beyond its initialized native cells.");
            }
            int floor = state.Cells[candidateCell * 8 + 7] << 12;
            int surface = ReadGlobal(0x988500);
            if (y >= floor)
            {
                int pitch = PlayerWord(0x14);
                if (pitch < 0)
                {
                    if (pitch <= -16)
                    {
                        PlayerWord(0x14, pitch + 16);
                        PlayerWord(0x4C, PlayerWord(0x4C) + 16);
                    }
                    else
                    {
                        PlayerWord(0x14, 0);
                        PlayerWord(0x4C, 0);
                    }
                }
                PlayerInt(0x9C, PlayerInt(0x9C) | 2);
            }
            else if (y < surface)
            {
                int pitch = PlayerWord(0x14);
                if (pitch > 0)
                {
                    if (pitch >= 16)
                    {
                        PlayerWord(0x14, pitch - 16);
                        PlayerWord(0x4C, PlayerWord(0x4C) - 16);
                    }
                    else
                    {
                        PlayerWord(0x14, 0);
                        PlayerWord(0x4C, 0);
                    }
                }
                PlayerInt(0, x);
                PlayerInt(4, surface);
                PlayerInt(8, z);
            }
            else
            {
                PlayerInt(0, x);
                PlayerInt(4, y);
                PlayerInt(8, z);
            }
        }
        int planeX = 0;
        int planeY = (0x410000 - PlayerInt(4)) >> 12;
        int planeZ = 0;
        int planeAngle = -PlayerWord(0x5C) % 4096;
        if (planeAngle > 0 && planeAngle < 1024)
        {
            int sine = NativeSin(planeAngle);
            if (sine != 0)
            {
                int distance = (0x410000 - PlayerInt(4)) / sine;
                if (distance < 32767)
                {
                    planeY = unchecked(planeY * distance);
                }
            }
        }
        PlayerWord(0x88, unchecked((PlayerInt(0) >> 12) + planeX));
        PlayerWord(0x8A, unchecked((PlayerInt(4) >> 12) + planeY));
        PlayerWord(0x8C, unchecked((PlayerInt(8) >> 12) + planeZ));
        int cell = PlayerCellIndex(PlayerInt(0), PlayerInt(8));
        PlayerInt(0x90, cell);
        int cellFlags = state.Cells[cell * 8 + 5];
        int mineDifference = (PlayerInt(4) >> 12) - state.Cells[cell * 8 + 6];
        if ((cellFlags & 0x2000) != 0 && (cellFlags & 0x8000) == 0 && mineDifference > -64 && mineDifference < 0)
        {
            state.Cells[cell * 8 + 5] = unchecked((ushort)(cellFlags | 0x8080));
            PlayerInt(0x10, unchecked(PlayerInt(0x10) - 2048));
            WriteGlobal(0x987050, unchecked(ReadGlobal(0x987050) + ReadGlobal(0x987040) * 6));
            EmitSound(0x229);
        }
        direction = ReadPlayerVector(0x2C);
        PlayerVector side = ReadPlayerVector(0x24);
        int probeFlags = ProbePlayerTerrain(AddPlayerVectors(direction, side));
        probeFlags |= ProbePlayerTerrain(AddPlayerVectors(direction, NegatePlayerVector(side)));
        probeFlags |= ProbePlayerTerrain(AddPlayerVectors(NegatePlayerVector(direction), side));
        probeFlags |= ProbePlayerTerrain(AddPlayerVectors(NegatePlayerVector(direction), NegatePlayerVector(side)));
        probeFlags &= ~1;
        probeFlags |= ProbePlayerTerrain(direction);
        probeFlags |= ProbePlayerTerrain(ScalePlayerVector(direction, 2));
        probeFlags |= ProbePlayerTerrain(NegatePlayerVector(direction));
        probeFlags |= ProbePlayerTerrain(ScalePlayerVector(direction, -2));
        PlayerVector otherSide = ReadPlayerVector(0x1C);
        probeFlags |= ProbePlayerTerrain(HalfPlayerVector(AddPlayerVectors(direction, otherSide)));
        probeFlags |= ProbePlayerTerrain(HalfPlayerVector(AddPlayerVectors(direction, NegatePlayerVector(otherSide))));
        probeFlags |= ProbePlayerTerrain(HalfPlayerVector(AddPlayerVectors(NegatePlayerVector(direction), otherSide)));
        probeFlags |= ProbePlayerTerrain(HalfPlayerVector(AddPlayerVectors(NegatePlayerVector(direction), NegatePlayerVector(otherSide))));
        PlayerInt(0x9C, PlayerInt(0x9C) | probeFlags);
        for (int index = 0; index < 4; index++)
        {
            int offset = 0xAC + index * 4;
            int reload = PlayerInt(offset);
            uint phase = unchecked((uint)reload) & 0xFFFF0000u;
            if (phase == 0)
            {
                reload = ReadGlobal(0x987028) | 0x10000;
            }
            else if (phase == 0x10000)
            {
                int remaining = (reload & 65535) - 1;
                reload = remaining <= 0 ? 0x20000 : 0x10000 | remaining & 65535;
            }
            PlayerInt(offset, reload);
        }
    }
}
