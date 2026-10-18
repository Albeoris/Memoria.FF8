namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanAlternateDecorationInputs(NativeBattleActionPacketsState Packets, Func<NativeBattleScanPairMorphFrame, NativeBattleScanMeshDraw, NativeBattleScanPairMorphFrame> DrawBoard, Func<NativeBattleScanPairMorphFrame, NativeBattleScanMeshDraw, NativeBattleScanPairMorphFrame> DrawSpinner);

public static class NativeBattleScanAlternateDecorations
{
    public static NativeBattleScanPairMorphStep Advance(NativeBattleScanPairMorphFrame frame, uint address, ushort exit269CE2C, NativeBattleScanAlternateDecorationInputs inputs)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Scan.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        bool spinner = task.Callback == 0x850C70;
        if (!spinner && task.Callback != 0x850EB0)
        {
            throw new InvalidDataException("Native Scan alternate decoration requires its board or specialized spinner callback.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = task.WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096).WriteWord(0x1C, unchecked((ushort)(spinner ? -512 : -136))).WriteWord(0x1E, unchecked((ushort)(spinner ? 0 : -60))).WriteWord(0x20, spinner ? (ushort)0x6C0 : (ushort)0x240).WriteDword(0x4C, spinner ? 0x15CF674u : 0x15CF614u);
                if (spinner)
                {
                    task = task.WriteWord(0x46, 0x400);
                }
                Next();
                break;
            case 1:
                if (spinner)
                {
                    short angle = unchecked((short)(task.ReadWord(0x46) - 128));
                    task = task.WriteWord(0x46, angle <= 0 ? (ushort)0 : unchecked((ushort)angle));
                    if (angle <= 0)
                    {
                        Next();
                    }
                }
                else
                {
                    NativeBattleEffectParameters parameters = frame.Scan.Effect.Parameters ?? throw new InvalidDataException("Native Scan alternate board requires its actual action parameters.");
                    hierarchy = hierarchy.Write(address, task);
                    for (ushort index = 1; index < 8; index++)
                    {
                        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(4, 0x84F520, 0x6C, address, (pointer, group, target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target));
                        hierarchy = appended.Hierarchy;
                        if (appended.Address != 0)
                        {
                            hierarchy = hierarchy.Write(appended.Address, hierarchy.Read(appended.Address).WriteWord(0x62, index).WriteWord(0x60, 1));
                        }
                    }
                    task = hierarchy.Read(address);
                    Next();
                }
                break;
            case 2:
                if (exit269CE2C == 1)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                    Next();
                }
                break;
            case 3:
                break;
            default:
                throw new InvalidDataException("Native Scan alternate decoration signed phase exceeds its four callbacks.");
        }
        frame = NativeBattleScanPairMorph.Draw84D8B0(frame, task, spinner ? inputs.DrawSpinner : inputs.DrawBoard);
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        return new(frame with { Scan = frame.Scan with { Effect = frame.Scan.Effect with { Pools = hierarchy.Pools } } }, complete ? 2 : 0);

        void Next()
        {
            task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
        }
    }
}
