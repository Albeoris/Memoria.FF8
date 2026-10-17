using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Triple's private primary/morph phase kernels, not Double's different linear envelope.</summary>
public static class NativeBattleTripleScalars
{
    public static ushort Curve89E5E0(short index, NativeBattleTripleResources resources) => ReadCurve(resources.Curve89E5E0, index);
    public static ushort Curve89E620(short index, NativeBattleTripleResources resources) => ReadCurve(resources.Curve89E620, index);

    public static NativeBattleModulePoolTask AdvancePrimaryPhase89E560(NativeBattleModulePoolTask task, NativeBattleTripleResources resources)
    {
        Require(task, 0x89E250, 124);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = task.WriteWord(0x64, unchecked((ushort)(task.ReadWord(0x64) - 512))).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 2));
                if (unchecked((short)task.ReadWord(0x64)) <= 0)
                {
                    task = task.WriteWord(0x64, 0).WriteByte(0x5C, 0).WriteByte(0x5D, 0).WriteByte(0x5E, 0).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
                }
                return task;
            case 1:
                task = task.WriteWord(0x78, unchecked((ushort)(task.ReadWord(0x78) + 1)));
                short index = unchecked((short)task.ReadWord(0x78));
                ushort first = Curve89E620(index, resources);
                task = task.WriteWord(0x50, first).WriteWord(0x64, first);
                ushort second = Curve89E5E0(index, resources);
                task = task.WriteWord(0x50, second).WriteWord(0x66, second);
                if (index >= 11)
                {
                    task = task.WriteWord(0x26, (ushort)((task.ReadWord(0x26) & ~2) | 5)).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
                }
                return task;
            case 2:
                return task;
            default:
                throw new InvalidDataException("Native89E250 signed phase exceeds its three original callbacks.");
        }
    }

    public static NativeBattleModulePoolTask AdvanceMorphPhase89E780(NativeBattleModulePoolTask task, NativeBattleTripleResources resources)
    {
        Require(task, 0x89E670, 124);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = task.WriteWord(0x78, unchecked((ushort)(task.ReadWord(0x78) + 1)));
                short index = unchecked((short)task.ReadWord(0x78));
                ushort ratio = Curve89E620(index, resources);
                task = task.WriteWord(0x50, ratio).WriteWord(0x74, ratio);
                if (index >= 11)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
                }
                return task;
            case 1:
                return task;
            default:
                throw new InvalidDataException("Native89E670 signed phase exceeds its two original callbacks.");
        }
    }

    private static ushort ReadCurve(ImmutableArray<ushort> curve, short index)
    {
        if (curve.IsDefault || curve.Length != 12)
        {
            throw new NotSupportedException("Native Triple curve requires its authenticated twelve WORDs.");
        }
        return curve[Math.Clamp((int)index, 0, 11)];
    }

    private static void Require(NativeBattleModulePoolTask task, uint callback, int stride)
    {
        if (task.Callback != callback || task.Payload.IsDefault || task.Payload.Length != stride - 12)
        {
            throw new InvalidDataException("Native Triple scalar phase requires its original124-byte private task.");
        }
    }
}
