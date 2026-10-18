using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanDecorationsFrame(NativeBattleScanPairMorphFrame Visual, ushort Complete269CE2C, ushort Ready269D938);
public sealed record NativeBattleScanDecorationsInputs(NativeBattleActionPacketsState Packets, NativeBattleScanPairMorphInputs Visual, Func<byte, NativeBattleScanByteOwner> ReadActorDescriptor);
public sealed record NativeBattleScanDecorationsStep(NativeBattleScanDecorationsFrame Frame, int Result);

/// <summary>The normal Scan spinner, board, stat bars and backdrop, sharing84D8B0's direct mesh drawing.</summary>
public static class NativeBattleScanDecorations
{
    public static NativeBattleScanDecorationsStep Advance(NativeBattleScanDecorationsFrame frame, uint address, NativeBattleScanDecorationsInputs inputs)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Visual.Scan.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        int phase = unchecked((sbyte)task.ReadByte(0x29));
        if ((uint)phase > 3)
        {
            throw new InvalidDataException("Native Scan decoration signed phase exceeds its four callbacks.");
        }
        switch (task.Callback)
        {
            case 0x84F300:
                Spinner();
                Angle(0x46, 192);
                Angle(0x44, 96);
                break;
            case 0x84F430:
                Board();
                break;
            case 0x84F520:
                StatBar();
                Angle(0x68, 0x100);
                short pulse = unchecked((short)(NativeBattleCameraMath.Sine(task.ReadWord(0x68)) + 0x1000));
                task = task.WriteWord(0x50, unchecked((ushort)(pulse / 6)));
                break;
            case 0x84F9E0:
                Backdrop();
                break;
            default:
                throw new NotSupportedException("Native Scan decoration requires its original callback.");
        }
        frame = frame with { Visual = NativeBattleScanPairMorph.Draw84D8B0(frame.Visual, task, inputs.Visual.Draw) };
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        frame = frame with { Visual = frame.Visual with { Scan = frame.Visual.Scan with { Effect = frame.Visual.Scan.Effect with { Pools = hierarchy.Pools } } } };
        return new(frame, complete ? 2 : 0);

        void Next()
        {
            task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
        }

        void FinishIf(bool condition)
        {
            if (condition)
            {
                task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                Next();
            }
        }

        void Scale(uint value)
        {
            task = task.WriteDword(0x30, value).WriteDword(0x34, value).WriteDword(0x38, value);
        }

        void Angle(int offset, int amount)
        {
            task = task.WriteWord(offset, (ushort)((task.ReadWord(offset) + amount) & 0xFFF));
        }

        void Fade(int decrement)
        {
            task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) - decrement)));
            if (Word(0x50) <= 0)
            {
                task = task.WriteWord(0x50, 0);
                Next();
            }
        }

        short Word(int offset) => unchecked((short)task.ReadWord(offset));

        void Spinner()
        {
            switch (phase)
            {
                case 0:
                    Scale(4096);
                    task = task.WriteWord(0x50, 0x1000).WriteWord(0x1C, unchecked((ushort)-128)).WriteWord(0x1E, unchecked((ushort)-160)).WriteWord(0x20, 0x480).WriteDword(0x4C, 0x15CF37C);
                    Next();
                    break;
                case 1:
                    Fade(0x100);
                    break;
                case 2:
                    FinishIf(frame.Complete269CE2C == 1);
                    break;
            }
        }

        void Board()
        {
            switch (phase)
            {
                case 0:
                    Scale(4096);
                    task = task.WriteWord(0x1C, unchecked((ushort)-64)).WriteWord(0x1E, unchecked((ushort)-88)).WriteWord(0x20, 0x240).WriteDword(0x4C, 0x15CF5B4);
                    Next();
                    break;
                case 1:
                    NativeBattleEffectParameters parameters = frame.Visual.Scan.Effect.Parameters ?? throw new InvalidDataException("Native Scan board requires its current action parameters.");
                    for (ushort index = 0; index < 8; index++)
                    {
                        hierarchy = hierarchy.Write(address, task);
                        NativeBattleEffectHierarchyAppend child = hierarchy.Append8DC540(4, 0x84F520, 0x6C, address, (pointer, group, target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target));
                        hierarchy = child.Hierarchy;
                        task = hierarchy.Read(address);
                        hierarchy = hierarchy.Write(child.Address, hierarchy.Read(child.Address).WriteWord(0x62, index).WriteWord(0x60, 0));
                    }
                    Next();
                    break;
                case 2:
                    FinishIf(frame.Complete269CE2C == 1);
                    break;
            }
        }

        void StatBar()
        {
            switch (phase)
            {
                case 0:
                    int index = Word(0x62);
                    uint table = task.ReadWord(0x60) == 0 ? 0x15D1678u : 0x15D16C8u;
                    var bytes = inputs.Visual.Resources.TablesByAddress[table];
                    int offset = index * 10;
                    if (offset < 0 || offset > bytes.Length - 10 || index > 7)
                    {
                        throw new InvalidDataException("Native Scan stat bar requires its actual indexed row.");
                    }
                    NativeBattleScanByteOwner descriptor = inputs.ReadActorDescriptor(task.ReadByte(0x2D));
                    descriptor.RequireLength(208);
                    uint[] meshes = [0x15CF6FC, 0x15CF6FC, 0x15CF784, 0x15CF80C, 0x15CF894, 0x15CF91C, 0x15CF9A4, 0x15CFA2C];
                    int target;
                    if (index <= 1)
                    {
                        int hp = Math.Min(unchecked((int)descriptor.ReadDword(0x18)), 999);
                        target = unchecked(hp << 14) / 999;
                    }
                    else
                    {
                        int maximum = index == 7 ? 25 : 127;
                        int value = Math.Min(descriptor.ReadByte(index == 7 ? 0xC3 : 0xBD + index - 2), maximum);
                        target = (value << 14) / maximum;
                    }
                    NativeBattleModulePoolTask parent = hierarchy.Read(task.ReadDword(0x18));
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4)).WriteDword(0x4C, meshes[index]).WriteWord(0x64, unchecked((ushort)target));
                    task = task.WriteDword(0x30, 0).WriteDword(0x34, 4096).WriteDword(0x38, 4096);
                    task = task.WriteWord(0x1C, unchecked((ushort)(Row(0) / 2 + unchecked((short)parent.ReadWord(0x1C))))).WriteWord(0x1E, unchecked((ushort)(Row(2) / 2 + unchecked((short)parent.ReadWord(0x1E))))).WriteWord(0x20, parent.ReadWord(0x20));
                    task = task.WriteWord(0x48, unchecked((ushort)Row(4))).WriteWord(0x66, unchecked((ushort)Row(6))).WriteWord(0x68, unchecked((ushort)Row(8)));
                    Next();
                    short Row(int relative) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(offset + relative, 2));
                    break;
                case 1:
                    task = task.WriteWord(0x66, unchecked((ushort)(task.ReadWord(0x66) - 1)));
                    if (Word(0x66) < 0)
                    {
                        task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) & ~4));
                        Next();
                    }
                    break;
                case 2:
                    int scale = unchecked((int)task.ReadDword(0x30) + 0x300);
                    int limit = Word(0x64);
                    task = task.WriteDword(0x30, unchecked((uint)(scale > limit ? limit : scale)));
                    FinishIf(frame.Complete269CE2C == 1);
                    break;
            }
        }

        void Backdrop()
        {
            switch (phase)
            {
                case 0:
                    Scale(0x1200);
                    task = task.WriteWord(0x1C, 0xF080).WriteWord(0x1E, 0xF00).WriteWord(0x20, 0x7FFF).WriteDword(0x4C, 0x15CEA34).WriteWord(0x50, 0x1000);
                    Next();
                    break;
                case 1:
                    Fade(0x200);
                    break;
                case 2:
                    FinishIf(frame.Ready269D938 == 1);
                    break;
            }
        }
    }
}
