using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBio22GeometryTaskStep(NativeBattleModulePoolTask Task, NativeBattleEffectMemory Memory, uint Result)
{
    [JsonIgnore]
    public bool CompleteRequested => Result == 2;
}

/// <summary>The geometry callback updates its selected shared mesh before its mandatory post-phase draw.</summary>
public static class NativeBattleBio22GeometryTask
{
    public static NativeBattleBio22GeometryTaskStep Advance89AAE0(NativeBattleModulePoolTask task, NativeBattleModulePoolTask parent, NativeBattleEffectMemory memory, NativeBattleBio22Resources resources, Func<NativeBattleEffectMemory, NativeBattleModulePoolTask, NativeBattleBio22Geometry, bool, NativeBattleEffectMemory> draw)
    {
        if (task.PayloadLength != 0x98 - 12 || task.Callback != 0x89AAE0)
        {
            throw new InvalidDataException("Native Bio geometry requires its actual hierarchy task.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = Initialize(task, parent, resources);
                break;
            case 1:
                task = task.WriteWord(0x84, W(task.ReadWord(0x84) + 0x800));
                if (S(task, 0x84) > 0x1000)
                {
                    task = task.WriteWord(0x84, W(task.ReadWord(0x84) - 0x1000)).WriteWord(0x74, W(task.ReadWord(0x74) + 1)).WriteWord(0x76, W(task.ReadWord(0x76) + 1));
                    if (S(task, 0x74) >= 4)
                    {
                        int difference = (task.ReadWord(0x78) - parent.ReadWord(0x82)) & 0xFFF;
                        if (difference >= 0x800)
                        {
                            difference -= 0x1000;
                        }
                        task = task.WriteWord(0x90, W(30 - parent.ReadWord(0x24))).WriteWord(0x96, W(task.ReadWord(0x30) - parent.ReadWord(0x86))).WriteDword(0x4C, 0x1617E3C).WriteWord(0x74, 5).WriteWord(0x76, 6).WriteWord(0x82, W(difference)).WriteWord(0x94, 1).WriteByte(0x29, 2);
                    }
                    task = task.WriteDword(0x6C, Pointer(resources, S(task, 0x74))).WriteDword(0x70, Pointer(resources, S(task, 0x76)));
                }
                break;
            case 2:
                task = Interpolate(task.WriteWord(0x84, W(task.ReadWord(0x84) + 0x400)), parent);
                if (S(task, 0x84) >= 0x1000)
                {
                    task = task.WriteWord(0x84, 0x1000).WriteByte(0x29, 3);
                }
                break;
            case 3:
                task = Interpolate(task, parent);
                if (S(task, 0x8E) >= S(task, 0x90))
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, 4);
                }
                break;
            case 4:
                if (S(parent, 0x24) >= 30)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, 5);
                }
                break;
            case 5:
                break;
            default:
                throw new NotSupportedException("Native Bio geometry phase leaves its selected callback table.");
        }
        task = Position89B170(task, parent);
        uint address = task.ReadDword(0x4C);
        NativeBattleBio22Geometry current = address switch
        {
            0x1615CE4 => memory.Bio22Geometry ?? throw new NotSupportedException("Native Bio requires its retained first geometry owner."),
            0x1617E3C => memory.Bio22SecondaryGeometry ?? throw new NotSupportedException("Native Bio requires its retained second geometry owner."),
            _ => throw new NotSupportedException("Native Bio geometry pointer leaves its two physical owners.")
        };
        NativeBattleBio22Geometry next = current.Morph89B0D0(Source(resources, task.ReadDword(0x6C)), Source(resources, task.ReadDword(0x70)), S(task, 0x84));
        if (!next.Bytes.Equals(current.Bytes))
        {
            next.ValidateForAddress(address);
        }
        memory = address == 0x1615CE4 ? memory with { Bio22Geometry = next } : memory with { Bio22SecondaryGeometry = next };
        memory = draw(memory, task, next, task.ReadWord(0x94) != 0);
        byte flags = task.ReadByte(0x26);
        task = task.WriteWord(0x24, W(task.ReadWord(0x24) + 1));
        return new(task, memory, (flags & 1) != 0 && task.ReadByte(0x28) == 0 ? 2u : 0u);
    }

    public static NativeBattleModulePoolTask Position89B170(NativeBattleModulePoolTask task, NativeBattleModulePoolTask parent)
    {
        int t = S(task, 0x92);
        short[] xyz = new short[3];
        for (int axis = 0; axis < 3; axis++)
        {
            int start = S(task, 0x5C + axis * 2);
            int target = S(task, 0x54 + axis * 2);
            xyz[axis] = unchecked((short)(start + unchecked((target - start) * t) / 4096));
        }
        NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), S(task, 0x78));
        NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(rotation, new(xyz[0], xyz[1], xyz[2]));
        task = task.WriteDword(0x1C, parent.ReadDword(0x78)).WriteDword(0x20, parent.ReadDword(0x7C));
        return task.WriteWord(0x1C, W(task.ReadWord(0x1C) + rotated.X)).WriteWord(0x1E, W(task.ReadWord(0x1E) + rotated.Y)).WriteWord(0x20, W(task.ReadWord(0x20) + rotated.Z));
    }

    private static NativeBattleModulePoolTask Initialize(NativeBattleModulePoolTask task, NativeBattleModulePoolTask parent, NativeBattleBio22Resources resources)
    {
        int q = S(parent, 0x86);
        uint half = unchecked((uint)(q / 2));
        task = task.WriteDword(0x30, half).WriteDword(0x34, half).WriteDword(0x38, half);
        int variant = task.ReadWord(0x8C);
        if (variant <= 5)
        {
            (int x, int y, int z, int yaw) = variant switch
            {
                0 => (-4240, 2720, -1200, 0x800),
                1 => (-240, 4400, -80, 0x800),
                2 => (1760, -5200, 80, 0xA00),
                3 => (-400, 80, -5200, 0x100),
                4 => (-1760, -160, 4400, 0xC00),
                _ => (5200, 1440, 640, 0xC00)
            };
            task = task.WriteWord(0x54, W(unchecked(q * x) / 4096 / 10)).WriteWord(0x56, W(unchecked(q * y) / 4096 / 10)).WriteWord(0x58, W(unchecked(q * z) / 4096 / 10)).WriteWord(0x46, W(yaw));
        }
        return task.WriteWord(0x5C, W(2 * task.ReadWord(0x54))).WriteWord(0x76, 1).WriteDword(0x4C, 0x1615CE4).WriteWord(0x5E, W(-S(parent, 0x7A) - q * 320 / 4096)).WriteWord(0x78, parent.ReadWord(0x82)).WriteWord(0x74, 0).WriteWord(0x50, 0).WriteWord(0x60, W(2 * task.ReadWord(0x58))).WriteDword(0x6C, Pointer(resources, 0)).WriteDword(0x70, Pointer(resources, 1)).WriteByte(0x29, 1);
    }

    private static NativeBattleModulePoolTask Interpolate(NativeBattleModulePoolTask task, NativeBattleModulePoolTask parent)
    {
        int n = S(task, 0x90);
        int old = S(task, 0x8E);
        int remaining = n - old;
        int scale = unchecked(unchecked(S(task, 0x96) * remaining) / n + S(parent, 0x86));
        task = task.WriteWord(0x78, W(unchecked(S(task, 0x82) * remaining) / n + parent.ReadWord(0x82))).WriteDword(0x30, unchecked((uint)scale)).WriteDword(0x34, unchecked((uint)scale)).WriteDword(0x38, unchecked((uint)scale)).WriteWord(0x8E, W(old + 1));
        return task.WriteWord(0x92, W((S(task, 0x8E) << 12) / n));
    }

    private static uint Pointer(NativeBattleBio22Resources resources, int index)
    {
        if (index < 0 || index >= 7 || resources.MorphPointers.Data.IsDefault || resources.MorphPointers.Data.Length != 0x1C)
        {
            throw new InvalidDataException("Native Bio source index leaves its seven-entry pointer directory.");
        }
        return BinaryPrimitives.ReadUInt32LittleEndian(resources.MorphPointers.Data.AsSpan().Slice(index * 4));
    }

    private static ImmutableArray<byte> Source(NativeBattleBio22Resources resources, uint pointer)
    {
        if (!resources.MorphSources.IsDefault)
        {
            foreach (NativeBattleEffectHeapAllocation source in resources.MorphSources)
            {
                if (source.Identity == $"FF8_EN:{pointer:X}")
                {
                    return source.Data;
                }
            }
        }
        throw new NotSupportedException("Native Bio morph source pointer has no authenticated resource owner.");
    }

    private static short S(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static ushort W(int value) => unchecked((ushort)value);
}
