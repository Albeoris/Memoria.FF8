using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRayBomb307Controller
{
    public static NativeBattleRayBomb307TaskStep AdvanceBeam(NativeBattleRayBomb307Frame frame, uint address, NativeBattleRayBomb307Resources resources, NativeBattleRayBomb307Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        NativeBattleEffectArena origin = frame.State.ReadView(0x2242580, 8);
        NativeBattleModuleAnchor muzzle = new(Short(origin, 0), Short(origin, 2), Short(origin, 4), 0);
        NativeBattleVector target = new(Signed(task, 16), Signed(task, 18), Signed(task, 20));
        NativeBattleVector eye = inputs.ReadCameraEye?.Invoke(frame) ?? throw new NotSupportedException("Ray-Bomb beam requires its current camera eye.");
        NativeBattleRayBomb10BeamGeometry geometry = NativeBattleRayBomb10Geometry.Beam628B00(target, muzzle, eye, Signed(task, 24), Signed(task, 32), unchecked((short)age));
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(ReadView(frame, inputs).ReadMatrix(), geometry.World);
        frame = DrawStatic(frame, matrix, 0xCE3BB4, age >= 3 ? 0xF3u : 0x33u, age >= 3 ? unchecked((age - 3) * 682) : null, age >= 3, inputs);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, 0);
        }
        short velocity = Signed(task, 26);
        task = task.WriteWord(24, unchecked((ushort)(Signed(task, 24) + velocity))).WriteWord(26, unchecked((ushort)(velocity + (velocity >> 4))));
        frame = Write(frame, address, task);
        if (age >= 2)
        {
            frame = DrawRandom(frame, out ushort countRoll);
            int count = countRoll % 5 + 3;
            NativeBattleEffectArena endpoint = origin.Write(0, BitConverter.GetBytes(geometry.End.X)).Write(2, BitConverter.GetBytes(geometry.End.Y)).Write(4, BitConverter.GetBytes(geometry.End.Z));
            for (int index = 0; index < count; index++)
            {
                (frame, uint childAddress) = Append(frame, 1, 0x5A2340);
                NativeBattleModulePoolTask child = Read(frame, childAddress).WriteWord(12, 0);
                frame = DrawRandom(frame, out ushort delay);
                child = child.WriteWord(14, (ushort)((delay & 7) + 8));
                child = CopyTask(child, 16, endpoint, 8);
                frame = DrawRandom(frame, out ushort x);
                child = child.WriteWord(18, 0).WriteWord(16, unchecked((ushort)(Signed(child, 16) + x % 1000 - 500)));
                frame = DrawRandom(frame, out ushort z);
                child = child.WriteWord(20, unchecked((ushort)(Signed(child, 20) + z % 900 - 400)));
                frame = DrawRandom(frame, out ushort speed);
                child = child.WriteWord(22, unchecked((ushort)(-10 - speed % 20)));
                frame = DrawRandom(frame, out ushort scale);
                child = child.WriteWord(28, (ushort)(scale % 0x700 + 0xC00));
                frame = Write(frame, childAddress, child);
            }
        }
        short next = unchecked((short)(age + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next)));
        return new(frame, next > 9 ? 2 : 0);

        static short Short(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    }
}
