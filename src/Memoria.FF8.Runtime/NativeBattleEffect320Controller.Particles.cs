using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect320Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect320Controller
{
    public static NativeBattleEffect320Frame BirthEarlyParticles(NativeBattleEffect320Frame frame, uint main, int phase, NativeBattleEffect320Resources resources, NativeBattleEffect320Inputs inputs)
    {
        NativeBattleEffectArena template = NativeBattleEffectArena.Clear().Write(0, resources.ReadStatic(0xFF12A8, 8));
        for (int index = 0; index < 2; index++)
        {
            (frame, uint child) = Append(frame, ParticleDescriptor, ParticleCallback);
            if (child == 0)
            {
                break;
            }
            frame = frame with { Random = frame.Random.Draw(out ushort first) };
            int localX = (first & 31) + 31;
            short anchorX;
            if (index == 0)
            {
                anchorX = 100;
                localX += 100;
            }
            else
            {
                anchorX = -100;
                localX = -100 - localX;
            }
            frame = frame with { State = frame.State.WriteWord(MutableAnchorAddress, unchecked((ushort)anchorX)) };
            NativeBattleEffectArena source = template.Write(0, BitConverter.GetBytes(anchorX));
            NativeBattleEffect320PointStep origin = inputs.Origin65DA30(frame, source, TaskView(Read(frame, child), 0x10, 8));
            frame = WriteBytes(origin.Frame, child + 0x10, origin.Output8, 6);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, unchecked((ushort)((uint)phase > 20 ? phase - 20 : 0))));
            frame = frame with { Random = frame.Random.Draw(out ushort second) };
            frame = Write(frame, child, Read(frame, child).WriteWord(0x16, unchecked((ushort)((second & 1023) + 1024))));
            frame = frame with { Random = frame.Random.Draw(out ushort third) };
            int originalZ = BinaryPrimitives.ReadInt32LittleEndian(resources.ReadStatic(0xFF12AC, 4));
            NativeBattleEffectArena destination = template.Write(0, BitConverter.GetBytes(unchecked((short)localX))).Write(4, BitConverter.GetBytes(unchecked((short)(originalZ + (third & 15) + 16))));
            NativeBattleEffect320PointStep target = inputs.Origin65DA30(frame, destination, NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
            frame = target.Frame;
            NativeBattleVector endpoint = Vector(target.Output8);
            NativeBattleModulePoolTask task = Read(frame, child);
            frame = Write(frame, child, task.WriteWord(0x18, unchecked((ushort)(endpoint.X - Signed(task, 0x10)))).WriteWord(0x1A, unchecked((ushort)(endpoint.Y - Signed(task, 0x12)))).WriteWord(0x1C, unchecked((ushort)(endpoint.Z - Signed(task, 0x14)))));
        }
        return frame;
    }

    public static NativeBattleEffect320Frame BirthLateParticles(NativeBattleEffect320Frame frame, uint main, int factor, NativeBattleEffect320Inputs inputs)
    {
        frame = LoadMatrix(frame, PackedMatrix(TaskView(Read(frame, main), 0x10, 32)), true, false);
        int complement = 4096 - factor;
        for (int index = 0; index < 4; index++)
        {
            (frame, uint child) = Append(frame, ParticleDescriptor, ParticleCallback);
            if (child == 0)
            {
                break;
            }
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0));
            (frame, NativeBattleVector point) = Blend(frame, main, factor, complement, inputs);
            frame = WriteBytes(frame, child + 0x10, PointView(point), 6);
            frame = frame with { Random = frame.Random.Draw(out ushort first) };
            frame = Write(frame, child, Read(frame, child).WriteWord(0x16, unchecked((ushort)((first & 2047) + 4096))));
            frame = frame with { Random = frame.Random.Draw(out ushort angle) };
            NativeBattleVector direction = new(unchecked((short)NativeBattleCameraMath.Sine(angle)), unchecked((short)NativeBattleCameraMath.Cosine(angle)), 0);
            NativeBattleGeometryRegisterStep rotated = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers(frame), 0, direction, null), false);
            frame = Publish(frame, rotated);
            NativeBattleVector current = new(unchecked((short)rotated.Colors.Ir[0]!.Value), unchecked((short)rotated.Colors.Ir[1]!.Value), unchecked((short)rotated.Colors.Ir[2]!.Value));
            NativeBattleModulePoolTask task = Read(frame, child);
            frame = Write(frame, child, task.WriteWord(0x10, unchecked((ushort)(Signed(task, 0x10) + (unchecked((short)current.X) >> 6)))).WriteWord(0x12, unchecked((ushort)(Signed(task, 0x12) + (unchecked((short)current.Y) >> 6)))).WriteWord(0x14, unchecked((ushort)(Signed(task, 0x14) + (unchecked((short)current.Z) >> 6)))));
            frame = frame with { Random = frame.Random.Draw(out ushort third) };
            NativeBattleGeometryRegisterStep weighted = NativeBattleGeometry.LoadIrColumn(Registers(frame) with { Colors = Registers(frame).Colors with { Ir0 = unchecked((uint)((third & 15) + 16)) } }, unchecked((ushort)current.X), unchecked((ushort)current.Y), unchecked((ushort)current.Z));
            weighted = NativeBattleRaldo19BallRendering.Weighted(weighted, false, inputs.Colors);
            frame = Publish(frame, weighted);
            task = Read(frame, child);
            frame = Write(frame, child, task.WriteWord(0x18, unchecked((ushort)weighted.Colors.Ir[0]!.Value)).WriteWord(0x1A, unchecked((ushort)weighted.Colors.Ir[1]!.Value)).WriteWord(0x1C, unchecked((ushort)weighted.Colors.Ir[2]!.Value)));
            factor -= 102;
            complement += 102;
            if (factor < 0)
            {
                break;
            }
        }
        return frame;
    }
}
