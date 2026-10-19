using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleDiablos325Trail
{
    public static NativeBattleDiablos325Frame SpawnAttached6559D3(NativeBattleDiablos325Frame frame, NativeBattleModulePoolTask task)
    {
        NativeBattleVector point = AttachedPoint(frame, task);
        NativeCrtRandomState random = frame.Random ?? throw new NotSupportedException("Native Diablos trail birth requires its current CRT random owner.");
        byte[] context = Build6559D3(point, ref random);
        NativeBattleDiablos325TrailBirth born = Spawn658890(frame with { Random = random }, context);
        return born.Frame;
    }

    public static NativeBattleDiablos325Frame SpawnPaired655B7F(NativeBattleDiablos325Frame frame, NativeBattleModulePoolTask task)
    {
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        if (task.ReadWord(0xC) == 298)
        {
            NativeBattleDiablos325Trail trail = state.Trail ?? throw new NotSupportedException("Native Diablos paired trails require their current initialized cursors.");
            frame = frame with { Memory = frame.Memory with { Diablos325 = state with { Trail = trail with { Shift5200 = 9, Step5202 = 32, Parameter5204 = 2048, Parameter5206 = unchecked((ushort)-4) } } } };
        }
        NativeBattleVector point = AttachedPoint(frame, task);
        NativeCrtRandomState random = frame.Random ?? throw new NotSupportedException("Native Diablos paired trails require their current CRT owner.");
        random = random.Draw(out ushort radiusRandom);
        int radius = (radiusRandom & 1023) + 3000;
        for (int index = 0; index < 2; index++)
        {
            random = random.Draw(out ushort angle);
            int x = NativeBattleCameraMath.Cosine(angle) * radius >> 12;
            int z = NativeBattleCameraMath.Sine(angle) * radius >> 12;
            byte[] context = new byte[48];
            Word(8, point.X + (x >> 8));
            Word(10, point.Y);
            Word(12, point.Z + (z >> 8));
            Word(14, 3000);
            Word(16, point.X + x + (x >> 4));
            Word(18, point.Y - radius / 2);
            Word(20, point.Z + z + (z >> 4));
            Word(22, 0);
            Word(24, x >> 6);
            Word(26, 200);
            Word(28, z >> 6);
            Word(30, 512);
            for (int axis = 0; axis < 3; axis++)
            {
                random = random.Draw(out ushort velocity);
                Word(32 + axis * 2, (velocity * 300 >> 15) - 150);
            }
            Word(38, 2);
            Word(40, 12288);
            Word(42, -512);
            Word(46, 4096);
            frame = Spawn658890(frame with { Random = random }, context).Frame;

            void Word(int offset, int value)
            {
                BinaryPrimitives.WriteInt16LittleEndian(context.AsSpan(offset), unchecked((short)value));
            }
        }
        return frame;
    }

    private static NativeBattleVector AttachedPoint(NativeBattleDiablos325Frame frame, NativeBattleModulePoolTask task)
    {
        NativeBattleVector point;
        if ((task.ReadByte(0x10C) & 2) == 0)
        {
            point = new(unchecked((short)task.ReadWord(0x128)), unchecked((short)task.ReadWord(0x12A)), unchecked((short)task.ReadWord(0x12C)));
        }
        else
        {
            Span<byte> record = stackalloc byte[8];
            record.Clear();
            BinaryPrimitives.WriteInt16LittleEndian(record[6..], 33);
            point = NativeBattleDiablos325Point.Sample6F0CC0(frame.Memory, task, record);
        }
        return point;
    }

    public static byte[] Build6559D3(NativeBattleVector point, ref NativeCrtRandomState random)
    {
        random = random.Draw(out ushort radiusRandom);
        int radius = (radiusRandom & 4095) + 1024;
        random = random.Draw(out ushort angle);
        int x = NativeBattleCameraMath.Cosine(angle) * radius >> 12;
        int z = NativeBattleCameraMath.Sine(angle) * radius >> 12;
        random = random.Draw(out ushort vertical);
        byte[] context = new byte[48];
        Word(8, point.X + (x >> 7));
        Word(10, point.Y + (vertical & 255) - 128);
        Word(12, point.Z + (z >> 7));
        Word(14, 1500);
        Word(16, point.X + (x >> 3));
        Word(18, point.Y - radius / 8);
        Word(20, point.Z + (z >> 3));
        Word(22, 0);
        Word(24, -(x >> 7));
        Word(26, -100);
        Word(28, -(z >> 7));
        Word(30, 256);
        for (int axis = 0; axis < 3; axis++)
        {
            random = random.Draw(out ushort velocity);
            Word(32 + axis * 2, (velocity * 100 >> 15) - 50);
        }
        Word(38, 0);
        Word(40, 12288);
        Word(42, -512);
        Word(46, 4096);
        return context;

        void Word(int offset, int value)
        {
            BinaryPrimitives.WriteInt16LittleEndian(context.AsSpan(offset), unchecked((short)value));
        }
    }
}
