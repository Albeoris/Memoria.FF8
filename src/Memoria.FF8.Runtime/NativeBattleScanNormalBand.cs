using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanNormalBandFrame(NativeBattleScanFrame Scan, ushort Ready269D938);
public sealed record NativeBattleScanNormalBandStep(NativeBattleScanNormalBandFrame Frame, int Result);

/// <summary>84EFF0's fading textured grid, retaining the native private Scan packet ownership.</summary>
public static class NativeBattleScanNormalBand
{
    public static NativeBattleScanNormalBandStep Advance84EFF0(NativeBattleScanNormalBandFrame frame, uint address, Func<NativeBattleScanNormalBandFrame, NativeBattleModulePacketBatch, NativeBattleScanNormalBandFrame> draw)
    {
        ArgumentNullException.ThrowIfNull(draw);
        NativeBattleEffectHierarchy hierarchy = new(frame.Scan.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != 0x84EFF0)
        {
            throw new InvalidDataException("Native Scan normal band requires its84EFF0 callback.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                Next();
                break;
            case 1:
                task = task.WriteWord(0x6E, unchecked((ushort)(task.ReadWord(0x6E) + 8)));
                if (unchecked((short)task.ReadWord(0x6E)) >= 128)
                {
                    task = task.WriteWord(0x6E, 128);
                    Next();
                }
                Draw();
                break;
            case 2:
                Draw();
                if (frame.Ready269D938 == 1)
                {
                    Next();
                }
                break;
            case 3:
                task = task.WriteWord(0x6E, unchecked((ushort)(task.ReadWord(0x6E) - 8)));
                if (unchecked((short)task.ReadWord(0x6E)) <= 0)
                {
                    task = task.WriteWord(0x6E, 0).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                    Next();
                }
                Draw();
                break;
            case 4:
                break;
            default:
                throw new InvalidDataException("Native Scan normal band signed phase exceeds its five callbacks.");
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

        void Draw()
        {
            frame = draw(frame, Draw84F0B0(task.ReadWord(0x24), task.ReadByte(0x6E), NativeBattlePilotVisualLifetimes.Identity(40, task)));
        }
    }

    public static NativeBattleModulePacketBatch Draw84F0B0(ushort counter, byte intensity, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        ImmutableArray<NativeBattleAllocatedPrimitive>.Builder packets = ImmutableArray.CreateBuilder<NativeBattleAllocatedPrimitive>(15);
        int y = (counter & 63) - 64;
        uint color = 0x2C000000u | (uint)intensity * 0x010101u;
        for (int row = 0; row < 5; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                int x = column * 128;
                NativeBattleGeometryPrimitive primitive = new(4381, [0x09000000, color, XY(x, y), 0x3C940080, XY(x + 128, y), 0x00BA0000, XY(x, y + 64), 0x00004080, XY(x + 128, y + 64), 0x00004000], [0, 0, 0, 0]);
                if (pilotIdentity is { } identity)
                {
                    primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 397 + row * 3 + column), ResourceGeneration = counter >> 6 }, new(x, y, 0), new(x + 128, y, 0), new(x, y + 64, 0), new(x + 128, y + 64, 0), 4) { ScreenSpace = true } };
                }
                packets.Add(new(primitive, packets.Count * 40, 1));
            }
            y += 64;
        }
        return new(packets.MoveToImmutable(), 600);
    }

    private static uint XY(int x, int y)
    {
        return (uint)(ushort)x | (uint)unchecked((ushort)y) << 16;
    }
}
