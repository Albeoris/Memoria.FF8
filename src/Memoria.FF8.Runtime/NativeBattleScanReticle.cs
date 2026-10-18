using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanReticlePoint(short X, short Y);
public sealed record NativeBattleScanReticleFrame(NativeBattleScanFrame Scan, ushort Highlight269AA90);
public sealed record NativeBattleScanReticleStep(NativeBattleScanReticleFrame Frame, int Result);
public sealed record NativeBattleScanReticleInputs(NativeBattleActionPacketsState Packets, ImmutableArray<NativeBattleScanReticlePoint> Trajectory15D1898, Func<NativeBattleScanReticleFrame, NativeBattleModulePoolTask, short, short, short, NativeBattleScanReticleFrame> DrawRing, Func<NativeBattleScanReticleFrame, NativeBattleModulePoolTask, NativeBattleScanReticleFrame> DrawFourLines);

/// <summary>84D5B0's reticle timeline with actual child ownership and explicit drawing services.</summary>
public static class NativeBattleScanReticle
{
    public static NativeBattleScanReticleStep Advance84D5B0(NativeBattleScanReticleFrame frame, uint address, NativeBattleScanReticleInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs.DrawRing);
        ArgumentNullException.ThrowIfNull(inputs.DrawFourLines);
        NativeBattleEffectHierarchy hierarchy = new(frame.Scan.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != 0x84D5B0)
        {
            throw new InvalidDataException("Native Scan reticle requires its84D5B0 callback.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                Position();
                for (ushort corner = 0; corner < 4; corner++)
                {
                    Append(2, 0x84E010, 0x78, corner);
                }
                for (ushort corner = 0; corner < 4; corner++)
                {
                    Append(2, 0x84DDD0, 0x78, corner);
                }
                Append(4, 0x84DAA0, 0x6C);
                Append(4, 0x84D810, 0x6C);
                Draw(8);
                Next();
                break;
            case 1:
                NativeBattleScanReticlePoint point = ReadPoint();
                if (point.X == short.MaxValue)
                {
                    task = task.WriteWord(0x66, 0);
                    Next();
                }
                else
                {
                    task = task.WriteWord(0x1C, unchecked((ushort)point.X)).WriteWord(0x1E, unchecked((ushort)point.Y));
                }
                Draw(8);
                task = task.WriteWord(0x66, 8);
                break;
            case 2:
                task = task.WriteWord(0x66, unchecked((ushort)(task.ReadWord(0x66) - 2)));
                short radius = unchecked((short)task.ReadWord(0x66));
                if (radius > 0)
                {
                    Draw(radius);
                }
                else
                {
                    Next();
                }
                break;
            case 3:
                Append(2, 0x84E3D0, 0x78);
                Next();
                break;
            case 4:
                if (frame.Highlight269AA90 == 0)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                    Next();
                }
                break;
            case 5:
                break;
            default:
                throw new InvalidDataException("Native Scan reticle signed phase exceeds its six callbacks.");
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        frame = frame with { Scan = frame.Scan with { Effect = frame.Scan.Effect with { Pools = hierarchy.Pools } } };
        return new(frame, complete ? 2 : 0);

        void Next()
        {
            task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
        }

        NativeBattleScanReticlePoint ReadPoint()
        {
            int index = unchecked((short)task.ReadWord(0x24));
            if (inputs.Trajectory15D1898.IsDefault || inputs.Trajectory15D1898.Length != 21 || (uint)index >= 21)
            {
                throw new InvalidDataException("Native Scan reticle requires its actual indexed trajectory record.");
            }
            return inputs.Trajectory15D1898[index];
        }

        void Position()
        {
            NativeBattleScanReticlePoint point = ReadPoint();
            task = task.WriteWord(0x1C, unchecked((ushort)point.X)).WriteWord(0x1E, unchecked((ushort)point.Y));
        }

        void Draw(short radius)
        {
            frame = inputs.DrawRing(frame, task, radius, 0, 32);
            frame = inputs.DrawFourLines(frame, task);
        }

        void Append(int pool, uint callback, ushort stride, ushort? corner = null)
        {
            NativeBattleEffectParameters parameters = frame.Scan.Effect.Parameters ?? throw new InvalidDataException("Native Scan reticle requires its current action parameters.");
            hierarchy = hierarchy.Write(address, task);
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(pool, callback, stride, address, (pointer, group, target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target));
            hierarchy = appended.Hierarchy;
            task = hierarchy.Read(address);
            if (corner is ushort value)
            {
                hierarchy = hierarchy.Write(appended.Address, hierarchy.Read(appended.Address).WriteWord(0x74, value));
            }
        }
    }
}
