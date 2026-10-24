using static Memoria.FF8.Runtime.NativeBattleEffect210Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect210Controller
{
    private static NativeBattleEffect210Frame InitializeTargetMotion(NativeBattleEffect210Frame frame, uint main, NativeBattleEffect210Inputs inputs)
    {
        int count = Read(frame, main).ReadWord(14);
        for (int index = 0; index < count; index++)
        {
            uint record = main + 16u + (uint)(index * 24);
            uint model = Dword(ReadBytes(frame, record, 4), 0);
            frame = inputs.Execute(frame, new NativeBattleEffect210Bounds9800(model, record + 20));
            frame = ModelFlags(frame, Actor(model), 0xFFFF, 12, inputs, true);
        }
        return frame;
    }

    private static NativeBattleEffect210Frame MoveTargets(NativeBattleEffect210Frame frame, uint main, int age, NativeBattleEffect210Inputs inputs)
    {
        int count = Read(frame, main).ReadWord(14);
        int phase = age >= 195 ? age - 195 : age - 115;
        int q = age >= 195 ? phase * 4096 / 40 + 51 : phase * 4096 / 80 + 25;
        for (int index = 0; index < count; index++)
        {
            uint record = main + 16u + (uint)(index * 24);
            uint model = Dword(ReadBytes(frame, record, 4), 0);
            int actor = Actor(model);
            int originalY = Word(ReadBytes(frame, record + 6, 2), 0);
            if (age >= 195 || !Frozen(frame))
            {
                int y;
                if (age >= 195)
                {
                    y = originalY + q * 4 - 8192;
                }
                else if (q < 2048)
                {
                    y = originalY - NativeBattleCameraMath.Sine(q >> 1) * 2;
                }
                else
                {
                    int value = 4096 - NativeBattleCameraMath.Sine(q >> 1);
                    value = unchecked(value * value) >> 12;
                    value = unchecked(value * value) >> 12;
                    y = originalY + value * 2 - 4096;
                }
                frame = WriteModel(frame, actor, 30, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(unchecked((short)y))), 2, inputs);
            }
            short currentY = Word(inputs.ReadModelArena9C(frame, actor), 30);
            frame = WriteModel(frame, actor, 0x58, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((int)currentY)), 4, inputs);
            ushort angle = unchecked((ushort)(Word(ReadBytes(frame, record + 12, 2), 0) + ((index & 1) != 0 ? 256 : -256)));
            frame = WriteBytes(frame, record + 12, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(angle)), 2);
            short midpoint = unchecked((short)((Word(ReadBytes(frame, record + 20, 2), 0) + Word(ReadBytes(frame, record + 22, 2), 0)) >> 1));
            NativeBattleEffectArena pivot = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, new byte[6]).Write(2, BitConverter.GetBytes(midpoint));
            frame = inputs.Execute(frame, new NativeBattleEffect210DrawModel9960(model, ReadBytes(frame, record + 12, 8), pivot));
        }
        return frame;
    }

    private static NativeBattleEffect210Frame FinishTargets(NativeBattleEffect210Frame frame, uint main, bool birth, NativeBattleEffect210Resources resources, NativeBattleEffect210Inputs inputs)
    {
        int count = Read(frame, main).ReadWord(14);
        for (int index = 0; index < count; index++)
        {
            uint record = main + 16u + (uint)(index * 24);
            int actor = Actor(Dword(ReadBytes(frame, record, 4), 0));
            short y = unchecked((short)(Word(inputs.ReadModelArena9C(frame, actor), 30) + 512));
            frame = WriteModel(frame, actor, 30, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(y)), 2, inputs);
            if (birth)
            {
                (frame, uint child) = Append(frame, ActDescriptor, 0x6F9AA0);
                if (child == 0)
                {
                    throw new InvalidDataException("Effect210 requires its final native ACT allocation.");
                }
                NativeBattleEffect210PointStep point = inputs.Anchor571400(frame, actor, ReadBytes(frame, child + 12, 8));
                frame = point.Frame;
                frame = WriteBytes(frame, child + 12, point.Output8, 8);
                frame = Write(frame, child, Read(frame, child).WriteWord(14, 0));
                frame = NativeBattleEffect210Programme.Initialize(frame, child + 0x14, 0x146ED2C, 0x1C0, resources);
            }
        }
        return frame;
    }

    public static NativeBattleEffect210Frame BirthRingParticles(NativeBattleEffect210Frame frame, uint main, NativeBattleEffect210Resources resources, NativeBattleEffect210Inputs inputs)
    {
        NativeBattleVector center = Vector(ReadBytes(frame, main + 0x58, 6));
        for (int index = 0; index < 8; index++)
        {
            (frame, uint child) = Append(frame, SpriteDescriptor, 0x6F9C60);
            if (child == 0)
            {
                break;
            }
            frame = Write(frame, child, Read(frame, child).WriteWord(0x12, 0));
            int radius = (Roll(ref frame) & 2047) + 2048;
            int angle = Roll(ref frame);
            NativeBattleVector circle = Circle67BB70(angle, radius);
            ushort velocity = unchecked((ushort)(-128 - (Roll(ref frame) & 127)));
            frame = Write(frame, child, Read(frame, child).WriteWord(12, unchecked((ushort)(circle.X + center.X))).WriteWord(14, unchecked((ushort)center.Y)).WriteWord(16, unchecked((ushort)(circle.Z + center.Z))).WriteWord(20, velocity));
        }
        return BirthMeshParticles(frame, main, 8, 0, false);
    }

    private static NativeBattleEffect210Frame BirthMeshParticles(NativeBattleEffect210Frame frame, uint main, int count, ushort lifetime, bool above)
    {
        NativeBattleVector center = Vector(ReadBytes(frame, main + 0x58, 6));
        for (int index = 0; index < count; index++)
        {
            (frame, uint child) = Append(frame, MeshDescriptor, 0x6F9B60);
            if (child == 0)
            {
                break;
            }
            frame = Write(frame, child, Read(frame, child).WriteWord(20, lifetime));
            int radius = (Roll(ref frame) & 2047) + 2048;
            int angle = Roll(ref frame);
            NativeBattleVector circle = Circle67BB70(angle, radius);
            NativeBattleModulePoolTask task = Read(frame, child).WriteWord(12, unchecked((ushort)(circle.X + center.X))).WriteWord(14, unchecked((ushort)(center.Y - (above ? 2048 : 0)))).WriteWord(16, unchecked((ushort)(circle.Z + center.Z)));
            int speed = Roll(ref frame) & 127;
            task = task.WriteWord(18, unchecked((ushort)(above ? speed + 256 : -128 - speed)));
            task = task.WriteWord(26, unchecked((ushort)(((Roll(ref frame) << 11) >> 15) + 2048)));
            task = task.WriteByte(22, unchecked((byte)Roll(ref frame))).WriteByte(23, unchecked((byte)Roll(ref frame))).WriteByte(24, unchecked((byte)Roll(ref frame))).WriteByte(25, unchecked((byte)Roll(ref frame)));
            frame = Write(frame, child, task);
        }
        return frame;
    }

    private static NativeBattleVector Circle67BB70(int angle, int radius) => new(unchecked((short)(unchecked(NativeBattleCameraMath.Sine(angle) * radius) >> 12)), 0, unchecked((short)(unchecked(-unchecked(NativeBattleCameraMath.Cosine(angle) * radius)) >> 12)));

    private static int Roll(ref NativeBattleEffect210Frame frame)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort value);
        frame = frame with { Random = random };
        return value;
    }
}
