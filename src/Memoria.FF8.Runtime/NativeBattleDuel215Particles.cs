namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel215ParticleBirth(NativeBattleDuel215Frame Frame, NativeBattleModulePoolTask Task, uint? Address);

public static partial class NativeBattleDuel215Controller
{
    public static NativeBattleModulePoolTask ClearParticles69B040(NativeBattleModulePoolTask task, uint taskAddress)
    {
        for (int index = 0; index < 128; index++)
        {
            task = task.WriteWord(2306 + index * 22, 0);
        }
        return task.WriteDword(5116, checked(taskAddress + 2300));
    }

    public static NativeBattleDuel215ParticleBirth AllocateParticle69CC10(NativeBattleDuel215Frame frame, NativeBattleModulePoolTask task, uint taskAddress)
    {
        uint address = task.ReadDword(5116);
        int offset = Offset(address);
        if (task.ReadWord(offset + 6) != 0)
        {
            address = checked(taskAddress + 2300);
            offset = 2300;
            if (task.ReadWord(offset + 6) != 0)
            {
                int remaining = 128;
                do
                {
                    address = checked(address + 22);
                    offset += 22;
                    if (--remaining == 0)
                    {
                        return new(frame, task, null);
                    }
                }
                while (task.ReadWord(offset + 6) != 0);
            }
        }
        NativeBattleDuel28Presentation.Allocated(task, offset);
        task = task.WriteDword(5116, offset < 5094 ? checked(address + 22) : checked(taskAddress + 2300));
        task = task.WriteWord(offset + 16, Rand()).WriteWord(offset + 18, Rand()).WriteByte(offset + 20, unchecked((byte)Rand())).WriteByte(offset + 21, unchecked((byte)Rand()));
        return new(frame, task, address);

        int Offset(uint pointer)
        {
            long relative = (long)pointer - taskAddress;
            if (relative < 2300 || relative > 5094 || (relative - 2300) % 22 != 0)
            {
                throw new InvalidDataException("Native Duel215 particle hint leaves its actual128-row owner.");
            }
            return (int)relative;
        }
        ushort Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }

    public static NativeBattleModulePoolTask AdvanceParticles69D080(NativeBattleModulePoolTask task, uint taskAddress)
    {
        for (int index = 0; index < 128; index++)
        {
            int offset = 2300 + index * 22;
            ushort life = task.ReadWord(offset + 6);
            if (life == 0)
            {
                continue;
            }
            life = unchecked((ushort)(life - 1));
            task = task.WriteWord(offset + 6, life);
            if (life == 0)
            {
                task = task.WriteDword(5116, checked(taskAddress + (uint)offset));
                continue;
            }
            int x = unchecked((short)task.ReadWord(offset)) + unchecked((short)task.ReadWord(offset + 8));
            int y = unchecked((short)task.ReadWord(offset + 2)) + unchecked((short)task.ReadWord(offset + 10));
            int z = unchecked((short)task.ReadWord(offset + 4)) + unchecked((short)task.ReadWord(offset + 12));
            short velocity = unchecked((short)(unchecked((short)task.ReadWord(offset + 10)) - 10));
            if (unchecked((short)y) >= 0)
            {
                velocity = unchecked((short)(velocity / -2));
            }
            task = task.WriteWord(offset, unchecked((ushort)x)).WriteWord(offset + 2, unchecked((ushort)y)).WriteWord(offset + 4, unchecked((ushort)z)).WriteWord(offset + 10, unchecked((ushort)velocity));
            task = task.WriteWord(offset + 16, unchecked((ushort)(task.ReadWord(offset + 16) + 4 * unchecked((sbyte)task.ReadByte(offset + 20))))).WriteWord(offset + 18, unchecked((ushort)(task.ReadWord(offset + 18) + 4 * unchecked((sbyte)task.ReadByte(offset + 21)))));
        }
        return task;
    }

    public static NativeBattleActionMatrix ParticleMatrix69CC90(NativeBattleDuel215Resources resources, NativeBattleModulePoolTask task, int offset, NativeBattleActionMatrix view)
    {
        if (offset < 2300 || offset > 5094 || (offset - 2300) % 22 != 0)
        {
            throw new InvalidDataException("Native Duel215 particle matrix requires its actual22-byte row.");
        }
        short angle = unchecked((short)task.ReadWord(offset + 16));
        short scale = unchecked((short)task.ReadWord(offset + 14));
        short sine = unchecked((short)((scale * resources.Sine(angle)) >> 12));
        short cosine = unchecked((short)((scale * resources.Cosine(angle)) >> 12));
        NativeBattleActionMatrix first = new([scale, 0, 0, 0, cosine, sine, 0, unchecked((short)-sine), cosine], 0, 0, 0, 4096);
        short secondAngle = unchecked((short)task.ReadWord(offset + 18));
        short secondSine = resources.Sine(secondAngle);
        short secondCosine = resources.Cosine(secondAngle);
        NativeBattleActionMatrix second = new([secondCosine, 0, unchecked((short)-secondSine), 0, 4096, 0, secondSine, 0, secondCosine], 0, 0, 0, 4096);
        NativeBattleActionMatrix rotation = NativeBattlePoseMatrices.MultiplyGteRotation45E470(view, NativeBattlePoseMatrices.MultiplyGteRotation45E470(first, second));
        NativeBattleVector point = new(unchecked((short)task.ReadWord(offset)), unchecked((short)task.ReadWord(offset + 2)), unchecked((short)task.ReadWord(offset + 4)));
        NativeBattleFullMacVertex transformed = NativeBattleGfMatrices.TransformMac460840(view, point);
        return rotation with { X = transformed.X, Y = transformed.Y, Z = transformed.Z };
    }
}
