namespace Memoria.FF8.Runtime;

/// <summary>Bio's placed and decelerating particles share the exact two-axis placement and frame timing.</summary>
public static class NativeBattleBio22LaterParticles
{
    public static NativeBattleBio22ParticleStep Advance89CE60(NativeBattleBio22ParticleFrame frame, Func<NativeBattleBio22ParticleFrame, short, NativeBattleBio22ParticleFrame> drawBillboard)
    {
        Require(frame.Task, 0x89CE60);
        switch (unchecked((sbyte)frame.Task.ReadByte(0x29)))
        {
            case 0:
                frame = CopyPrimaryPosition(frame);
                short radius = unchecked((short)(unchecked(S(frame.Primary, 0x86) * 3 * 256) / 4096));
                frame = Place89CF30(frame, radius);
                frame = frame with { Task = frame.Task.WriteDword(0x4C, 0x1615858).WriteWord(0x52, 15).WriteByte(0x29, 1) };
                break;
            case 1:
                frame = frame with { Task = AdvanceAnimation(frame.Task) };
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException("Native Bio placed particle phase leaves its callback table.");
        }
        return Finish(frame, drawBillboard);
    }

    public static NativeBattleBio22ParticleStep Advance89D120(NativeBattleBio22ParticleFrame frame, Func<NativeBattleBio22ParticleFrame, short, NativeBattleBio22ParticleFrame> drawBillboard)
    {
        Require(frame.Task, 0x89D120);
        switch (unchecked((sbyte)frame.Task.ReadByte(0x29)))
        {
            case 0:
                frame = CopyPrimaryPosition(frame);
                short radius = unchecked((short)(unchecked(S(frame.Primary, 0x86) * 5 * 256) / 4096));
                frame = Place89D280(frame, radius);
                NativeCrtRandomState random = frame.Random.Draw(out ushort value);
                int denominator = (value & 7) + 12;
                NativeBattleModulePoolTask task = frame.Task.WriteDword(0x4C, 0x16155B4).WriteWord(0x52, 11);
                for (int axis = 0; axis < 3; axis++)
                {
                    short velocity = (short)((S(task, 0x1C + axis * 2) - S(frame.Primary, 0x78 + axis * 2)) / denominator);
                    task = task.WriteWord(0x58 + axis * 2, W(velocity)).WriteWord(0x60 + axis * 2, W(-(velocity / 12)));
                }
                frame = frame with { Task = task.WriteByte(0x29, 1), Random = random };
                break;
            case 1:
                task = frame.Task;
                for (int axis = 0; axis < 3; axis++)
                {
                    int velocityOffset = 0x58 + axis * 2;
                    int positionOffset = 0x1C + axis * 2;
                    task = task.WriteWord(velocityOffset, W(task.ReadWord(velocityOffset) + task.ReadWord(0x60 + axis * 2)));
                    task = task.WriteWord(positionOffset, W(task.ReadWord(positionOffset) + task.ReadWord(velocityOffset)));
                }
                frame = frame with { Task = AdvanceAnimation(task) };
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException("Native Bio decelerating particle phase leaves its callback table.");
        }
        return Finish(frame, drawBillboard);
    }

    public static NativeBattleBio22ParticleFrame Place89CF30(NativeBattleBio22ParticleFrame frame, short radius) => Place(frame, radius, true);
    public static NativeBattleBio22ParticleFrame Place89D280(NativeBattleBio22ParticleFrame frame, short radius) => Place(frame, radius, false);

    private static NativeBattleBio22ParticleFrame Place(NativeBattleBio22ParticleFrame frame, short radius, bool randomRadius)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort yaw);
        random = random.Draw(out ushort pitch);
        if (radius == 0)
        {
            radius = 1;
        }
        short z = radius;
        if (randomRadius)
        {
            random = random.Draw(out ushort distance);
            z = (short)(distance % radius);
        }
        NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), (short)(yaw & 0xFFF));
        rotation = NativeBattleCuragaMatrices.RotateX8DD7E0(rotation, (short)(pitch & 0xFFF));
        NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(rotation, new(0, 0, z));
        NativeBattleModulePoolTask task = frame.Task.WriteWord(0x1C, W(frame.Task.ReadWord(0x1C) + offset.X)).WriteWord(0x1E, W(frame.Task.ReadWord(0x1E) + offset.Y)).WriteWord(0x20, W(frame.Task.ReadWord(0x20) + offset.Z));
        return frame with { Task = task, Random = random };
    }

    private static NativeBattleBio22ParticleFrame CopyPrimaryPosition(NativeBattleBio22ParticleFrame frame) => frame with { Task = frame.Task.WriteDword(0x1C, frame.Primary.ReadDword(0x78)).WriteDword(0x20, frame.Primary.ReadDword(0x7C)) };

    private static NativeBattleModulePoolTask AdvanceAnimation(NativeBattleModulePoolTask task)
    {
        NativeBattleBio22FrameAdvance advanced = NativeBattleBio22Particles.AdvanceFrame89BEE0(task);
        return advanced.Result == 1 ? advanced.Task.WriteByte(0x26, (byte)(advanced.Task.ReadByte(0x26) | 1)).WriteByte(0x29, (byte)(advanced.Task.ReadByte(0x29) + 1)) : advanced.Task;
    }

    private static NativeBattleBio22ParticleStep Finish(NativeBattleBio22ParticleFrame frame, Func<NativeBattleBio22ParticleFrame, short, NativeBattleBio22ParticleFrame> drawBillboard)
    {
        if (S(frame.Task, 0x1E) > 0)
        {
            frame = frame with { Task = frame.Task.WriteByte(0x26, (byte)(frame.Task.ReadByte(0x26) | 5)) };
        }
        frame = drawBillboard(frame, S(frame.Task, 0x30));
        byte flags = frame.Task.ReadByte(0x26);
        NativeBattleModulePoolTask task = frame.Task.WriteWord(0x24, W(frame.Task.ReadWord(0x24) + 1));
        return new(frame with { Task = task }, (flags & 1) != 0 && task.ReadByte(0x28) == 0 ? 2u : 0u);
    }

    private static void Require(NativeBattleModulePoolTask task, uint callback)
    {
        if (task.Callback != callback || task.Payload.IsDefault || task.Payload.Length != 0x70 - 12)
        {
            throw new InvalidDataException("Native Bio later particle requires its actual callback and hierarchy extent.");
        }
    }

    private static short S(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static ushort W(int value) => unchecked((ushort)value);
}
