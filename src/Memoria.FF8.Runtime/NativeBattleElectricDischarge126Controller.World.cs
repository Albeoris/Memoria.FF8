using static Memoria.FF8.Runtime.NativeBattleElectricDischarge126Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleElectricDischarge126Controller
{
    public static (NativeBattleElectricDischarge126Frame Frame, int Result) AdvanceWorldTrail(NativeBattleElectricDischarge126Frame frame, uint address, NativeBattleElectricDischarge126Resources resources, NativeBattleElectricDischarge126Inputs inputs)
    {
        for (int pass = 0; pass < 8; pass++)
        {
            NativeBattleModulePoolTask task = Read(frame, address);
            if (Signed(task, 0x1E) >= 4096)
            {
                continue;
            }
            task = Vector(task, 0x18, AddWorld(Vector(task, 0x18), Vector(task, 0x28))).WriteWord(0x1E, unchecked((ushort)(task.ReadWord(0x1E) + task.ReadWord(0x26))));
            for (int axis = 0; axis < 3; axis++)
            {
                frame = DrawRandom(frame, out ushort random);
                task = task.WriteWord(0x28 + axis * 2, unchecked((ushort)(task.ReadWord(0x28 + axis * 2) + (random & 127) - 64)));
            }
            frame = DrawRandom(frame, out ushort jitter);
            if (jitter < 4096)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    frame = DrawRandom(frame, out ushort random);
                    task = task.WriteWord(0x18 + axis * 2, unchecked((ushort)(task.ReadWord(0x18 + axis * 2) + (random & 511) - 256)));
                }
            }
            frame = Write(frame, address, task);
            int progress = Signed(task, 0x1E);
            frame = WeightedBlend(frame, Vector(task, 0x18), Vector(task, 0x20), 4096 - progress, progress, inputs, out NativeBattleVector position);
            if (progress >= 4096)
            {
                short actor = Signed(task, 0x36);
                if (actor >= 0)
                {
                    frame = DrawRandom(frame, out ushort random);
                    uint threshold = frame.State.ReadFlashThreshold();
                    if (random < unchecked((int)threshold))
                    {
                        frame = inputs.Execute(frame, new NativeBattleElectricDischarge126Flash(0, 1, 0, 64));
                        threshold = 4096;
                    }
                    else
                    {
                        threshold = unchecked(threshold + 4096);
                    }
                    frame = frame with { State = frame.State with { FlashThreshold = threshold } };
                }
                if (actor >= 0 || position.Y >= 0)
                {
                    (frame, uint spriteAddress) = Append(frame, 4, SpriteCallback);
                    if (spriteAddress != 0)
                    {
                        NativeBattleModulePoolTask sprite = Vector(Read(frame, spriteAddress).WriteWord(12, 0), 0x10, position).WriteByte(14, actor >= 0 ? (byte)0 : (byte)1).WriteWord(0x16, unchecked((ushort)(Signed(task, 0x38) << (actor >= 0 ? 4 : 5)))).WriteByte(15, unchecked((byte)actor));
                        frame = Write(frame, spriteAddress, sprite);
                    }
                }
            }
            (frame, uint point) = AllocateWorldPoint(frame, position, Signed(task, 0x38));
            task = Read(frame, address);
            if (point == 0)
            {
                frame = Write(frame, address, task.WriteWord(0x1E, 4096));
                continue;
            }
            frame = WriteArena(frame, task.ReadDword(0x14) + 0x24, BitConverter.GetBytes(point));
            task = task.WriteWord(14, unchecked((ushort)(task.ReadWord(14) + 1))).WriteDword(0x14, point);
            frame = Write(frame, address, task);
            int wave = resources.WaveTable[(Signed(task, 0x1E) >> 1) & 4095];
            NativeBattleVector retained = new(unchecked((short)frame.State.ReadWord(TargetPointGlobal)), unchecked((short)frame.State.ReadWord(TargetPointGlobal + 2)), unchecked((short)frame.State.ReadWord(TargetPointGlobal + 4)));
            frame = WeightedBlend(frame, Vector(task, 0x30), retained, wave, 4096 - wave, inputs, out NativeBattleVector velocity);
            frame = PointVector(frame, point + 8, velocity);
            if (Signed(task, 0x1E) < 3072)
            {
                frame = DrawRandom(frame, out ushort branch);
                if (branch < (Signed(task, 0x2E) << 13))
                {
                    frame = BranchWorld(frame, address, point, position);
                }
            }
        }
        NativeBattleModulePoolTask current = Read(frame, address);
        uint head = current.ReadDword(0x10);
        for (uint point = head, visited = 0; point != 0; point = PointDword(frame, point + 0x24), visited++)
        {
            RequireWorldPoint(frame, point, checked((int)visited));
            short color = PointSigned(frame, point + 0x20);
            if (color <= 0)
            {
                continue;
            }
            short fade = PointSigned(frame, point + 0x22);
            color = unchecked((short)(color + fade));
            frame = PointWord(frame, point + 0x20, color <= 0 ? (short)0 : color);
            if (color > 0)
            {
                short life = PointSigned(frame, point + 14);
                if (life != 0 && fade <= 0)
                {
                    frame = PointWord(frame, point + 14, unchecked((short)(life - 1)));
                }
                else if (fade > -384)
                {
                    frame = PointWord(frame, point + 0x22, unchecked((short)(fade - 320)));
                }
            }
        }
        frame = inputs.Execute(frame, new NativeBattleElectricDischarge126ProjectWorld(head, Signed(current, 14)));
        current = Read(frame, address);
        if (Signed(current, 14) >= 2)
        {
            frame = inputs.Execute(frame, new NativeBattleElectricDischarge126DrawWorld(head, Signed(current, 14), Signed(current, 0x3A)));
        }
        for (uint point = head, visited = 0; point != 0; point = PointDword(frame, point + 0x24), visited++)
        {
            RequireWorldPoint(frame, point, checked((int)visited));
            frame = PointVector(frame, point, AddWorld(new(PointSigned(frame, point), PointSigned(frame, point + 2), PointSigned(frame, point + 4)), new(PointSigned(frame, point + 8), PointSigned(frame, point + 10), PointSigned(frame, point + 12))));
        }
        for (int visited = 0; PointSigned(frame, head + 0x20) == 0; visited++)
        {
            RequireWorldPoint(frame, head, visited);
            uint next = PointDword(frame, head + 0x24);
            RequireWorldPoint(frame, next, visited);
            if (PointSigned(frame, next + 0x20) != 0)
            {
                break;
            }
            frame = WriteArena(frame, Working(frame) + WorldFreePointerOffset, BitConverter.GetBytes(head));
            frame = PointWord(frame, head + 6, -1);
            current = Read(frame, address).WriteWord(14, unchecked((ushort)(Read(frame, address).ReadWord(14) - 1))).WriteDword(0x10, next);
            frame = Write(frame, address, current);
            head = next;
            if (Signed(current, 14) < 2)
            {
                return (frame, 2);
            }
        }
        return (frame, 0);
    }

    private static NativeBattleElectricDischarge126Frame BranchWorld(NativeBattleElectricDischarge126Frame frame, uint address, uint point, NativeBattleVector position)
    {
        short[] target = new short[3];
        for (int axis = 0; axis < 2; axis++)
        {
            frame = DrawRandom(frame, out ushort sign);
            frame = DrawRandom(frame, out ushort magnitude);
            int delta = sign > 16384 ? -512 - (magnitude & 511) : 512 + (magnitude & 511);
            target[axis] = unchecked((short)((axis == 0 ? position.X : position.Y) + delta));
        }
        frame = DrawRandom(frame, out ushort z);
        target[2] = unchecked((short)(position.Z + (z & 1023) + 1024));
        NativeBattleModulePoolTask task = Read(frame, address);
        short[] velocity = new short[3];
        for (int axis = 0; axis < 3; axis++)
        {
            frame = DrawRandom(frame, out ushort random);
            velocity[axis] = unchecked((short)(Signed(task, 0x28 + axis * 2) + (random & 511) - 256));
        }
        (frame, uint child) = StartWorld(frame, new(velocity[0], velocity[1], velocity[2]), position, new(target[0], target[1], target[2]), Vector(task, 0x30), -1, Signed(task, 0x1E), 512, unchecked((short)(Signed(task, 0x38) >> 1)), Signed(task, 0x3A), 0);
        if (child != 0)
        {
            frame = WriteArena(frame, child + 8, Arena(frame).Read(Offset(point + 8, 8), 8));
        }
        task = Read(frame, address);
        return Write(frame, address, task.WriteWord(0x2E, unchecked((ushort)(task.ReadWord(0x2E) - 1))));
    }

    private static NativeBattleVector AddWorld(NativeBattleVector first, NativeBattleVector second) => new(unchecked((short)(first.X + second.X)), unchecked((short)(first.Y + second.Y)), unchecked((short)(first.Z + second.Z)));
}
