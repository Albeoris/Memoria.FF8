using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBlaster186Controller
{
    public static (NativeBattleBlaster186Frame Frame, int Result) AdvanceBeam(NativeBattleBlaster186Frame frame, uint address, NativeBattleBlaster186Resources resources, NativeBattleBlaster186Inputs inputs)
    {
        int caster = checked((int)frame.State.ReadDword(0x22BC0FC));
        NativeBattleBlaster186PointStep first = inputs.ReadBone502170(frame, caster, 0x31, 4096);
        NativeBattleBlaster186PointStep second = inputs.ReadBone502170(first.Frame, caster, 0x32, 4096);
        frame = second.Frame;
        int x = BinaryPrimitives.ReadInt16LittleEndian(first.Point8.Read(0, 2)) - BinaryPrimitives.ReadInt16LittleEndian(second.Point8.Read(0, 2));
        int y = BinaryPrimitives.ReadInt16LittleEndian(first.Point8.Read(2, 2)) - BinaryPrimitives.ReadInt16LittleEndian(second.Point8.Read(2, 2));
        int z = BinaryPrimitives.ReadInt16LittleEndian(first.Point8.Read(4, 2)) - BinaryPrimitives.ReadInt16LittleEndian(second.Point8.Read(4, 2));
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
        int length = NativeBattleRayBomb10Geometry.Sqrt56BEC0(NativeBattleSleepingGas168.NormalizeSquare(x, y, z));
        frame = Dword(Dword(Dword(frame, 0x22B7FA0, unchecked((uint)direction.X)), 0x22B7FA4, unchecked((uint)direction.Y)), 0x22B7FA8, unchecked((uint)direction.Z));
        frame = Word(frame, 0x22B7F9E, length);
        for (int axis = 0; axis < 3; axis++)
        {
            int a = BinaryPrimitives.ReadInt16LittleEndian(first.Point8.Read(axis * 2, 2));
            int b = BinaryPrimitives.ReadInt16LittleEndian(second.Point8.Read(axis * 2, 2));
            frame = Word(frame, 0x22B7F98u + (uint)(axis * 2), (a + b) / 2);
        }
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        int interval = Signed(task, 14);
        if (interval < 1 || interval >= resources.Timings.Length)
        {
            throw new NotSupportedException("Blaster beam leaves its actual addressed interval table.");
        }
        int age = Signed(task, 12);
        int start = resources.Timings[interval - 1];
        int end = resources.Timings[interval];
        if (age >= start && age < end && age == start)
        {
            if ((interval & 1) != 0)
            {
                short duration = unchecked((short)(end - start));
                (frame, uint child) = RequireAppend(frame, 0x5BFEC0);
                frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(14, unchecked((ushort)duration)).WriteWord(28, 0x680).WriteWord(32, 0x400));
                (frame, child) = RequireAppend(frame, 0x5C0040);
                frame = DrawRandom(frame, out ushort scale);
                frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(14, unchecked((ushort)duration)).WriteWord(28, (ushort)(scale % 0x300 + 0x900)).WriteWord(32, unchecked((ushort)(duration / 2))).WriteWord(34, unchecked((ushort)(duration / 2))));
            }
            task = Read(frame, address).WriteWord(14, unchecked((ushort)(interval + 1)));
            frame = Write(frame, address, task);
        }
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 40 ? 2 : 0);
    }

    public static (NativeBattleBlaster186Frame Frame, int Result) AdvanceEnvelope(NativeBattleBlaster186Frame frame, uint address, NativeBattleBlaster186Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        int descriptor = Signed(task, 14);
        int attack = descriptor & 15;
        int sustain = (descriptor >> 4) & 15;
        int release = (descriptor >> 8) & 15;
        int amplitude = Signed(task, 28);
        int value;
        if (age < attack)
        {
            value = unchecked(amplitude / attack * age);
        }
        else if (age - attack < sustain)
        {
            value = amplitude;
        }
        else if (age - attack - sustain < release)
        {
            value = unchecked(amplitude - amplitude / release * (age - attack - sustain));
        }
        else
        {
            value = -1;
        }
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1))));
        return value < 0 ? (frame, 2) : (inputs.Execute(frame, new NativeBattleBlaster186DrawCommonRectangle571250(value, value >> 2, value >> 2) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(186, task) }), 0);
    }
}
