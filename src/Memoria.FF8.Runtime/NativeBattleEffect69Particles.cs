using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect69Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect69Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect69Particles
{
    public static NativeBattleEffect69Frame InitializeDebris6FE250(NativeBattleEffect69Frame frame)
    {
        uint arena = frame.State.ReadDword(ArenaGlobal);
        for (int index = 0; index < 364; index++)
        {
            frame = StoreWord(frame, arena + (uint)(index * 60 + 6), 0);
        }
        for (int index = 0; index < 64; index++)
        {
            short[] angles = new short[3];
            for (int axis = 0; axis < 3; axis++)
            {
                frame = DrawRandom(frame, out ushort value);
                angles[axis] = (short)((value & 511) - 256);
            }
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CD50(new(angles[0], angles[1], angles[2]));
            frame = DrawRandom(frame, out ushort scaleDraw);
            int scale = 3840 - (scaleDraw & 511);
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale, scale, scale);
            uint address = arena + 0x5550 + (uint)index * 20;
            for (int element = 0; element < 9; element++)
            {
                frame = StoreWord(frame, address + (uint)element * 2, matrix.Rotation[element]);
            }
        }
        return frame;
    }

    public static NativeBattleEffect69Frame InitializeSparks6FE5A0(NativeBattleEffect69Frame frame)
    {
        uint arena = frame.State.ReadDword(ArenaGlobal);
        for (int index = 0; index < 128; index++)
        {
            frame = StoreByte(frame, arena + 0x802F + (uint)index * 16, 0);
        }
        return frame with { State = frame.State.WriteDword(SparkCursorGlobal, arena + 0x8020) };
    }

    public static NativeBattleEffect69Frame BirthSparks(NativeBattleEffect69Frame frame, NativeBattleEffect69Resources resources, NativeBattleEffect69Inputs inputs)
    {
        frame = DrawRandom(frame, out ushort initialAngle);
        int angle = initialAngle;
        for (int index = 0; index < 4; index++)
        {
            (frame, uint address) = AllocateSpark700310(frame);
            if (address == 0)
            {
                break;
            }
            frame = DrawRandom(frame, out ushort radiusDraw);
            int radius = radiusDraw & 2047;
            NativeBattleGraphicsModel caster = inputs.ReadModel(frame, checked((int)((frame.State.ReadDword(CasterGlobal) - 0x1D972C0) / 156)));
            NativeBattleVector position = caster.Transform?.Position28 ?? throw new NotSupportedException("Effect69 sparks require the current caster position.");
            frame = StoreWord(frame, address + 4, unchecked((short)(position.X + NativeBattleCameraMath.Sine(angle) * radius / 4096)));
            frame = StoreWord(frame, address + 6, 0);
            frame = StoreWord(frame, address + 8, unchecked((short)(position.Z + NativeBattleCameraMath.Cosine(angle) * radius / 4096)));
            frame = StoreDword(frame, address, resources.Sprite.Address);
            frame = DrawRandom(frame, out ushort x);
            frame = StoreByte(frame, address + 12, unchecked((byte)((x & 3) - 2)));
            frame = DrawRandom(frame, out ushort z);
            frame = StoreByte(frame, address + 14, unchecked((byte)((z & 3) - 2)));
            frame = DrawRandom(frame, out ushort y);
            frame = StoreByte(frame, address + 13, unchecked((byte)(240 - (y & 31))));
            frame = StoreWord(frame, address + 10, 2048);
            frame = StoreByte(frame, address + 15, 18);
            angle += 1024;
        }
        return frame;
    }

    public static NativeBattleEffect69Frame AdvanceSparks700370(NativeBattleEffect69Frame frame)
    {
        uint arena = frame.State.ReadDword(ArenaGlobal);
        for (int index = 0; index < 128; index++)
        {
            uint address = arena + 0x8020 + (uint)index * 16;
            byte life = ReadBytes(frame, address + 15, 1).Read(0, 1)[0];
            if (life == 0)
            {
                continue;
            }
            frame = StoreByte(frame, address + 15, unchecked((byte)(life - 1)));
            for (int axis = 0; axis < 3; axis++)
            {
                int velocity = unchecked((sbyte)ReadBytes(frame, address + 12 + (uint)axis, 1).Read(0, 1)[0]);
                frame = StoreWord(frame, address + 4 + (uint)axis * 2, unchecked((short)(ReadWord(frame, address + 4 + (uint)axis * 2) + (velocity << 2))));
            }
        }
        return frame;
    }

    public static NativeBattleEffect69Frame AdvanceDebris6FF5F0(NativeBattleEffect69Frame frame)
    {
        uint arena = frame.State.ReadDword(ArenaGlobal);
        for (int index = 0; index < 364; index++)
        {
            uint address = arena + (uint)index * 60;
            if (ReadWord(frame, address + 6) == 0)
            {
                continue;
            }
            short life = unchecked((short)(ReadWord(frame, address + 14) - 1));
            frame = StoreWord(frame, address + 14, life);
            if (life == 0)
            {
                frame = RetireDebris(frame, address);
                continue;
            }
            uint rotationAddress = ReadDword(frame, address + 16);
            NativeBattleEffectArena rotation = ReadBytes(frame, rotationAddress, 20);
            (byte[] raw, bool[] known) = rotation.ReadSnapshot(0, 20);
            byte?[] bytes = raw.Select((value, offset) => known[offset] ? (byte?)value : null).ToArray();
            NativeBattleGeometryRegisterStep registers = Registers(frame);
            registers = registers with { Geometry = registers.Geometry with { Rotation20 = bytes.ToImmutableArray() } };
            for (int corner = 0; corner < 4; corner++)
            {
                uint pointAddress = address + 20 + (uint)corner * 6;
                NativeBattleEffectArena point = ReadBytes(frame, pointAddress, corner % 2 == 0 ? 8 : 6);
                registers = NativeBattleGeometry.Transform(corner % 2 == 0 ? LoadParticleVector(registers, 0, point) : NativeBattleGeometry.LoadVector(registers, 0, Vector(point), 0), false);
                if (corner == 1 && ReadDword(frame, address + 20) == 0 && ReadWord(frame, address + 24) == 0)
                {
                    frame = RetireDebris(Publish(frame, registers), address);
                    break;
                }
                for (int axis = 0; axis < 3; axis++)
                {
                    frame = StoreWord(frame, pointAddress + (uint)axis * 2, unchecked((short)registers.Colors.Ir[axis]!.Value));
                }
            }
            if (ReadWord(frame, address + 6) == 0)
            {
                continue;
            }
            frame = Publish(frame, registers);
            for (int axis = 0; axis < 3; axis++)
            {
                frame = StoreWord(frame, address + (uint)axis * 2, unchecked((short)(ReadWord(frame, address + (uint)axis * 2) + ReadWord(frame, address + 8 + (uint)axis * 2))));
            }
            short vertical = unchecked((short)(ReadWord(frame, address + 10) + 30));
            frame = StoreWord(frame, address + 10, Math.Min(vertical, (short)-100));
        }
        return frame;
    }

    private static (NativeBattleEffect69Frame Frame, uint Address) AllocateSpark700310(NativeBattleEffect69Frame frame)
    {
        uint arena = frame.State.ReadDword(ArenaGlobal);
        uint address = frame.State.ReadDword(SparkCursorGlobal);
        if (ReadBytes(frame, address + 15, 1).Read(0, 1)[0] != 0)
        {
            address = arena + 0x8020;
            int index = 0;
            while (ReadBytes(frame, address + 15, 1).Read(0, 1)[0] != 0)
            {
                if (++index == 128)
                {
                    return (frame, 0);
                }
                address += 16;
            }
        }
        uint next = address >= arena + 0x8810 ? arena + 0x8020 : address + 16;
        return (frame with { State = frame.State.WriteDword(SparkCursorGlobal, next) }, address);
    }

    private static NativeBattleEffect69Frame RetireDebris(NativeBattleEffect69Frame frame, uint address) => StoreWord(frame with { State = frame.State.WriteDword(DebrisCursorGlobal, address) }, address + 6, 0);
    private static NativeBattleGeometryRegisterStep LoadParticleVector(NativeBattleGeometryRegisterStep registers, int slot, NativeBattleEffectArena point)
    {
        (byte[] bytes, bool[] known) = point.ReadSnapshot(0, 8);
        ImmutableArray<byte?> packed = bytes.Select((value, index) => known[index] ? (byte?)value : null).ToImmutableArray();
        return slot switch
        {
            0 => registers with { Colors = registers.Colors with { NormalInput = bytes.ToImmutableArray(), NormalInputKnown = known.ToImmutableArray() } },
            1 => registers with { Geometry = registers.Geometry with { V1 = packed } },
            2 => registers with { Geometry = registers.Geometry with { V2 = packed } },
            _ => throw new ArgumentOutOfRangeException(nameof(slot))
        };
    }
    private static NativeBattleEffect69Frame DrawRandom(NativeBattleEffect69Frame frame, out ushort value) => frame with { Random = frame.Random.Draw(out value) };
    private static short ReadWord(NativeBattleEffect69Frame frame, uint address) => BinaryPrimitives.ReadInt16LittleEndian(ReadBytes(frame, address, 2).Read(0, 2));
    private static uint ReadDword(NativeBattleEffect69Frame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(frame, address, 4).Read(0, 4));
    private static NativeBattleEffect69Frame StoreByte(NativeBattleEffect69Frame frame, uint address, byte value) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, [value]), 1);
    private static NativeBattleEffect69Frame StoreWord(NativeBattleEffect69Frame frame, uint address, int value) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(unchecked((short)value))), 2);
    private static NativeBattleEffect69Frame StoreDword(NativeBattleEffect69Frame frame, uint address, uint value) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 4);
}
