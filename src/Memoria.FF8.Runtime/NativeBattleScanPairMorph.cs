using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanPairMorphFrame(NativeBattleScanFrame Scan, ushort Highlight269AA90, ImmutableDictionary<uint, NativeBattleScanByteOwner> MutableMeshes);
public sealed record NativeBattleScanMeshDraw(uint ResourceAddress, NativeBattleActionMatrix Matrix, uint Color, short Fade)
{
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleScanPairMorphInputs(NativeBattleScanVisualResources Resources, Func<NativeBattleScanPairMorphFrame, NativeBattleScanMeshDraw, NativeBattleScanPairMorphFrame> Draw);
public sealed record NativeBattleScanPairMorphStep(NativeBattleScanPairMorphFrame Frame, int Result);

/// <summary>84D810/84DAA0's two opening mesh timelines and XYZ-only morph stores.</summary>
public static class NativeBattleScanPairMorph
{
    public static NativeBattleScanPairMorphStep Advance84D810(NativeBattleScanPairMorphFrame frame, uint address, NativeBattleScanPairMorphInputs inputs)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Scan.Effect.Pools);
        NativeBattleModulePoolTask task = Require(hierarchy, address, 0x84D810);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4)).WriteWord(0x20, 0x480).WriteDword(0x38, 0x1000).WriteWord(0x48, 0xA00);
                Next(ref task);
                break;
            case 1:
                if (Word(task, 0x24) >= 3)
                {
                    Next(ref task);
                }
                break;
            case 2:
                int index = Word(task, 0x24);
                task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) & ~4));
                task = task.WriteWord(0x1C, unchecked((ushort)(Table(0x15D15E8, index * 4) - 60))).WriteWord(0x1E, unchecked((ushort)(Table(0x15D15E8, index * 4 + 2) + 60)));
                task = task.WriteDword(0x30, unchecked((uint)(int)Table(0x15D1630, index * 4))).WriteDword(0x34, unchecked((uint)(int)Table(0x15D1630, index * 4 + 2)));
                task = task.WriteWord(0x5C, unchecked((ushort)Table(0x15D15A0, index * 2))).WriteWord(0x5E, unchecked((ushort)Table(0x15D15C4, index * 2)));
                if (index >= 17)
                {
                    Next(ref task);
                }
                break;
            case 3:
                task = task.WriteWord(0x68, (ushort)((task.ReadWord(0x68) + 0x200) & 0xFFF));
                short sum = unchecked((short)(NativeBattleCameraMath.Sine(task.ReadWord(0x68)) + 0x1000));
                task = task.WriteWord(0x5E, unchecked((ushort)(sum / 4)));
                if (frame.Highlight269AA90 == 0)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                    Next(ref task);
                }
                break;
            case 4:
                break;
            default:
                throw new InvalidDataException("Native Scan pair signed phase exceeds its five callbacks.");
        }
        task = task.WriteWord(0x50, task.ReadWord(0x5C)).WriteDword(0x4C, 0x15CE93C);
        frame = Draw84D8B0(frame, task, inputs.Draw);
        task = task.WriteWord(0x50, task.ReadWord(0x5E)).WriteDword(0x4C, 0x15CE9B8);
        frame = Draw84D8B0(frame, task, inputs.Draw);
        return Finish(frame, hierarchy, address, task);

        short Table(uint source, int offset)
        {
            if (!inputs.Resources.TablesByAddress.TryGetValue(source, out ImmutableArray<byte> bytes) || offset < 0 || offset > bytes.Length - 2)
            {
                throw new InvalidDataException("Native Scan pair requires its actual indexed table WORD.");
            }
            return BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(offset, 2));
        }
    }

    public static NativeBattleScanPairMorphStep Advance84DAA0(NativeBattleScanPairMorphFrame frame, uint address, NativeBattleScanPairMorphInputs inputs)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Scan.Effect.Pools);
        NativeBattleModulePoolTask task = Require(hierarchy, address, 0x84DAA0);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = task.WriteWord(0x1C, 0).WriteWord(0x1E, 0).WriteWord(0x20, 0x480).WriteDword(0x30, 0x3FF8).WriteDword(0x34, 0x3FF8).WriteDword(0x38, 0x3FF8);
                task = task.WriteWord(0x44, 0x5DC).WriteWord(0x46, 0x2A0).WriteWord(0x48, 0xF808).WriteDword(0x4C, 0x15CDDEC).WriteDword(0x54, 0x15CFAB4).WriteDword(0x58, 0x15CFC3C).WriteWord(0x50, 0x1000);
                Next(ref task);
                break;
            case 1:
                task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) - 0x155)));
                if (Word(task, 0x50) < 0)
                {
                    task = task.WriteWord(0x50, 0);
                }
                Angle(ref task, 0x44, -125);
                Angle(ref task, 0x46, -56);
                Angle(ref task, 0x48, 170);
                int scale = unchecked((int)task.ReadDword(0x30) - 1194);
                if (scale <= 2048)
                {
                    scale = 2048;
                    task = task.WriteWord(0x66, 12);
                    Next(ref task);
                }
                task = task.WriteDword(0x30, unchecked((uint)scale)).WriteDword(0x34, unchecked((uint)scale)).WriteDword(0x38, unchecked((uint)scale));
                break;
            case 2:
                Angle(ref task, 0x48, 170);
                task = task.WriteWord(0x66, unchecked((ushort)(task.ReadWord(0x66) - 1)));
                if (Word(task, 0x66) <= 0)
                {
                    Next(ref task);
                }
                break;
            case 3:
                task = task.WriteWord(0x6A, unchecked((ushort)(task.ReadWord(0x6A) + 0x200)));
                Angle(ref task, 0x48, 170);
                if (Word(task, 0x6A) >= 0x1000)
                {
                    task = task.WriteWord(0x6A, 0).WriteDword(0x4C, 0x15CE1D4).WriteDword(0x54, 0x15CFDC4).WriteDword(0x58, 0x15D02CC);
                    Next(ref task);
                }
                break;
            case 4:
                task = task.WriteWord(0x6A, unchecked((ushort)(task.ReadWord(0x6A) + 0x400)));
                if (Word(task, 0x6A) >= 0x1000)
                {
                    task = task.WriteWord(0x6A, 0x1000);
                    Next(ref task);
                }
                break;
            case 5:
                if ((task.ReadByte(0x24) & 7) < 4)
                {
                    Angle(ref task, 0x48, 42);
                }
                if (frame.Highlight269AA90 == 0)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                    Next(ref task);
                }
                break;
            case 6:
                break;
            default:
                throw new InvalidDataException("Native Scan morph signed phase exceeds its seven callbacks.");
        }
        uint destination = task.ReadDword(0x4C);
        if (!frame.MutableMeshes.TryGetValue(destination, out NativeBattleScanByteOwner? mesh))
        {
            throw new NotSupportedException("Native Scan morph requires its retained process mesh bytes.");
        }
        ImmutableArray<byte> first = inputs.Resources.VertexBanksByAddress[task.ReadDword(0x54)];
        ImmutableArray<byte> second = inputs.Resources.VertexBanksByAddress[task.ReadDword(0x58)];
        int count = BinaryPrimitives.ReadInt32LittleEndian(first.AsSpan().Slice(4, 4));
        ImmutableArray<byte> morphed = NativeBattleGfMorphGeometry.Morph45E9D0(first, 8, second, 8, count, Word(task, 0x6A), mesh.Bytes, 8);
        for (int vertex = 0; vertex < count; vertex++)
        {
            int offset = 8 + vertex * 8;
            mesh = mesh.Write(offset, morphed.AsSpan().Slice(offset, 6));
        }
        frame = frame with { MutableMeshes = frame.MutableMeshes.SetItem(destination, mesh) };
        frame = Draw84D8B0(frame, task, inputs.Draw);
        return Finish(frame, hierarchy, address, task);
    }

    public static NativeBattleScanPairMorphFrame Draw84D8B0(NativeBattleScanPairMorphFrame frame, NativeBattleModulePoolTask task, Func<NativeBattleScanPairMorphFrame, NativeBattleScanMeshDraw, NativeBattleScanPairMorphFrame> draw)
    {
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return frame;
        }
        ArgumentNullException.ThrowIfNull(draw);
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770();
        matrix = NativeBattleCuragaMatrices.RotateY8DD8A0(matrix, Word(task, 0x46));
        matrix = NativeBattleCuragaMatrices.RotateX8DD7E0(matrix, Word(task, 0x44));
        matrix = NativeBattleCuragaMatrices.RotateZ8DD960(matrix, Word(task, 0x48));
        matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, unchecked((int)task.ReadDword(0x30)), unchecked((int)task.ReadDword(0x34)), unchecked((int)task.ReadDword(0x38))) with { X = Word(task, 0x1C), Y = Word(task, 0x1E), Z = Word(task, 0x20) };
        return draw(frame, new(task.ReadDword(0x4C), matrix, task.ReadDword(0x40), Word(task, 0x50)) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(40, task, unchecked((int)task.ReadDword(0x4C))) });
    }

    private static NativeBattleScanPairMorphStep Finish(NativeBattleScanPairMorphFrame frame, NativeBattleEffectHierarchy hierarchy, uint address, NativeBattleModulePoolTask task)
    {
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        return new(frame with { Scan = frame.Scan with { Effect = frame.Scan.Effect with { Pools = hierarchy.Pools } } }, complete ? 2 : 0);
    }

    private static NativeBattleModulePoolTask Require(NativeBattleEffectHierarchy hierarchy, uint address, uint callback)
    {
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != callback)
        {
            throw new InvalidDataException("Native Scan pair/morph task has a different callback.");
        }
        return task;
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));

    private static void Next(ref NativeBattleModulePoolTask task)
    {
        task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
    }

    private static void Angle(ref NativeBattleModulePoolTask task, int offset, int delta)
    {
        task = task.WriteWord(offset, (ushort)((task.ReadWord(offset) + delta) & 0xFFF));
    }
}
