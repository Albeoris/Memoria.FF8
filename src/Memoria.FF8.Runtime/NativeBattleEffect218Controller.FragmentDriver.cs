using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect218Controller
{
    public static (NativeBattleEffect218Frame Frame, int Result) AdvanceFragmentDriver634C00(NativeBattleEffect218Frame frame, uint address, uint graphicsFlags, NativeBattleEffect218Resources resources, Func<NativeBattleEffect218Frame, uint, byte, short, NativeBattleEffect218PointStep> readBone502170, Func<NativeBattleEffect218Frame, NativeBattleVector, NativeBattleEffect218Frame> capture634E40)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.ReadWord(12) == 0)
        {
            NativeBattleActionMatrix basis = NativeBattleArenaPose.ReadMatrix(frame.State.ReadView(CameraBasis, 32), 0);
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(basis, new(0, 0, -700));
            NativeBattleVector origin = new(unchecked((short)(rotated.X + Word(frame, 0x24F235C))), unchecked((short)(rotated.Y + Word(frame, 0x24F2360))), unchecked((short)(rotated.Z + Word(frame, 0x24F2364))));
            frame = Write(frame, address, task.WriteWord(16, unchecked((ushort)origin.X)).WriteWord(18, unchecked((ushort)origin.Y)).WriteWord(20, unchecked((ushort)origin.Z)));
            frame = capture634E40(frame, origin);
        }
        if (Read(frame, address).ReadWord(12) == 15)
        {
            uint model = ModelAddress(checked((int)frame.State.ReadDword(TargetGlobal)));
            NativeBattleEffect218PointStep sampled = readBone502170(frame, model, 241, 0);
            frame = sampled.Frame;
            NativeBattleVector target = sampled.Point with { Y = unchecked((short)(sampled.Point.Y + 100)) };
            frame = SpawnFragments634CE0(frame, address, resources, target);
        }
        if (Read(frame, address).ReadWord(12) == 16)
        {
            (frame, uint first) = AppendRequired(frame, MainDescriptor, 0x636CA0);
            frame = Write(frame, first, Read(frame, first).WriteWord(12, 0));
            (frame, uint second) = AppendRequired(frame, MainDescriptor, 0x636EB0);
            frame = Write(frame, second, Read(frame, second).WriteWord(12, 0));
        }
        if ((graphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 21 ? 2 : 0);
    }

    public static NativeBattleEffect218Frame SpawnFragments634CE0(NativeBattleEffect218Frame frame, uint address, NativeBattleEffect218Resources resources, NativeBattleVector target)
    {
        NativeBattleActionMatrix basis = NativeBattleArenaPose.ReadMatrix(frame.State.ReadView(CameraBasis, 32), 0);
        NativeBattleVector zero = NativeBattleGfMatrices.RotateShort56C4F0(basis, new(0, 0, 0));
        short originZ = unchecked((short)(zero.Z + Word(frame, 0x24F2364)));
        NativeBattleEffectArena origin = TaskView(Read(frame, address), 16, 8);
        for (int index = 0; index < 24; index++)
        {
            (frame, uint child) = AppendRequired(frame, MainDescriptor, 0x635110);
            NativeBattleModulePoolTask task = Read(frame, child).WriteWord(12, 0).WriteWord(22, (ushort)(index / 4)).WriteWord(14, (ushort)index).WriteWord(28, 4096);
            frame = Write(frame, child, task);
            uint location = checked(0x24F3378u + (uint)(index * 16));
            NativeBattleEffectArena row = ReadBytes(frame, location, 16);
            row = NativeBattleEffectNodeContextMemory.Copy(origin, 0, row, 0, 8);
            ReadOnlySpan<byte> offsets = resources.FragmentOrigins.AsSpan().Slice(index * 8, 8);
            for (int axis = 0; axis < 3; axis++)
            {
                short coordinate = ParticleWord218(row, axis * 2);
                short offset = BinaryPrimitives.ReadInt16LittleEndian(offsets[(axis * 2)..]);
                row = ParticleWord218(row, axis * 2, coordinate + offset);
            }
            int x = ParticleWord218(row, 0) / 3 * 2;
            frame = Roll(frame, out ushort vertical);
            int y = unchecked((vertical % 800 + target.Y) * 2 - 800);
            int z = target.Z - originZ;
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
            frame = Roll(frame, out ushort velocity);
            int speed = velocity % 350 + 250;
            row = ParticleWord218(row, 8, unchecked(direction.X * speed) >> 12);
            row = ParticleWord218(row, 10, unchecked(direction.Y * speed) >> 12);
            row = ParticleWord218(row, 12, unchecked(direction.Z * speed) >> 12);
            frame = WriteBytes(frame, location, row, 16);
        }
        return frame;
    }
}
