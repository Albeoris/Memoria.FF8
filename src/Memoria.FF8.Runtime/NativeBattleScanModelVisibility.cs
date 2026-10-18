using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanVisibilityFrame(NativeBattleScanFrame Scan, ImmutableArray<NativeBattleGraphicsModel> Models, NativeBattleStageState Stage, uint BackgroundB8B7D8);
public sealed record NativeBattleScanVisibilityStep(NativeBattleScanVisibilityFrame Frame, int Result);

/// <summary>84ED90/8507F0's shared model and stage fade, including retained physical backups.</summary>
public static class NativeBattleScanModelVisibility
{
    public static NativeBattleScanVisibilityStep Advance(NativeBattleScanVisibilityFrame frame, uint address)
    {
        NativeBattleScanProcess process = frame.Scan.Memory.Scan ?? throw new NotSupportedException("Native Scan fade requires its retained presentation owners.");
        NativeBattleScanPresentationMemory memory = process.Presentation;
        memory.Validate();
        if (frame.Models.IsDefault || frame.Models.Length != 7 || frame.Stage.Groups.IsDefault || frame.Stage.Groups.Length != 4)
        {
            throw new InvalidDataException("Native Scan fade requires its seven physical models and four stage rows.");
        }
        NativeBattleEffectHierarchy hierarchy = new(frame.Scan.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        bool restore = task.Callback == 0x8507F0;
        if (!restore && task.Callback != 0x84ED90)
        {
            throw new InvalidDataException("Native Scan fade requires84ED90 or8507F0.");
        }
        int phase = unchecked((sbyte)task.ReadByte(0x29));
        switch (phase)
        {
            case 0 when !restore:
                task = task.WriteWord(0x1C, 0);
                SetStage(group => group with { ColorDepth = 0, ColorFlags = group.ColorFlags & 0xFF000000u });
                for (int index = 0; index < 7; index++)
                {
                    NativeBattleGraphicsModel model = Model(index);
                    uint color = Color(model);
                    memory = memory with { SavedFlags269A1D8 = memory.SavedFlags269A1D8.WriteDword(index * 4, model.Flags0), SavedColors269CE34 = memory.SavedColors269CE34.WriteDword(index * 4, color) };
                    if (!Selected(index))
                    {
                        SetModel(index, model with { Flags0 = (ushort)(model.Flags0 | 0x0800) });
                    }
                }
                Next();
                break;
            case 0:
                task = task.WriteWord(0x1C, 4096);
                SetStage(group => group with { Flags = (byte)((group.Flags & 0xFD) | ((group.Flags >> 1) & 2)) });
                for (int index = 0; index < 7; index++)
                {
                    NativeBattleGraphicsModel model = Model(index);
                    ushort oldFlags = unchecked((ushort)memory.SavedFlags269A1D8.ReadDword(index * 4));
                    bool selected = Selected(index);
                    ushort mask = selected ? (ushort)0x20 : (ushort)0x24;
                    model = model with { Flags0 = (ushort)((model.Flags0 & ~mask) | (oldFlags & mask)) };
                    if (selected)
                    {
                        model = model with { Color40 = memory.SavedColors269CE34.ReadDword(index * 4) };
                    }
                    SetModel(index, model);
                }
                Next();
                break;
            case 1:
                short fade = unchecked((short)(task.ReadWord(0x1C) + (restore ? -256 : 256)));
                if (restore ? fade <= 0 : fade >= 4096)
                {
                    fade = restore ? (short)0 : (short)4096;
                    Next();
                }
                task = task.WriteWord(0x1C, unchecked((ushort)fade));
                SetStage(group => group with { ColorDepth = fade });
                for (int index = 0; index < 7; index++)
                {
                    if (Selected(index))
                    {
                        continue;
                    }
                    NativeBattleGraphicsModel model = Model(index);
                    uint color = Color(model) & 0xFF000000u;
                    for (int shift = 0; shift < 24; shift += 8)
                    {
                        int background = (byte)(frame.BackgroundB8B7D8 >> shift);
                        byte channel = unchecked((byte)(background - background * fade / 4096));
                        color |= (uint)channel << shift;
                    }
                    SetModel(index, model with { Color40 = color });
                }
                break;
            case 2 when !restore:
                SetStage(group => group with { Flags = (byte)(group.Flags & 0xFD) });
                for (int index = 0; index < 7; index++)
                {
                    NativeBattleGraphicsModel model = Model(index);
                    model = memory.Selection269A1BC.ReadDword(index * 4) == 1 ? model with { Color40 = 0x00808080, Flags0 = (ushort)(model.Flags0 | 0x20) } : model with { Flags0 = (ushort)(model.Flags0 | 0x24) };
                    SetModel(index, model);
                }
                Finish();
                break;
            case 2:
                for (int index = 0; index < 7; index++)
                {
                    NativeBattleGraphicsModel model = Model(index);
                    ushort backup = unchecked((ushort)memory.SavedFlags269A1D8.ReadDword(index * 4));
                    if (Selected(index))
                    {
                        model = model with { Flags0 = (ushort)((model.Flags0 & ~0x20) | (backup & 0x20)) };
                    }
                    else
                    {
                        model = model with { Flags0 = (ushort)((model.Flags0 & ~0x0800) | (backup & 0x0800)), Color40 = (Color(model) & 0xFF000000u) | (frame.BackgroundB8B7D8 & 0xFFFFFFu) };
                    }
                    SetModel(index, model);
                }
                Finish();
                break;
            case 3:
                break;
            default:
                throw new InvalidDataException("Native Scan fade signed phase exceeds its four callbacks.");
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        frame = frame with { Scan = frame.Scan with { Effect = frame.Scan.Effect with { Pools = hierarchy.Pools }, Memory = frame.Scan.Memory with { Scan = process with { Presentation = memory } } } };
        return new(frame, complete ? 2 : 0);

        bool Selected(int index) => memory.Selection269A1BC.ReadDword(index * 4) != 0;

        NativeBattleGraphicsModel Model(int index)
        {
            NativeBattleGraphicsModel model = frame.Models[index];
            if (model.Index != index)
            {
                throw new InvalidDataException("Native Scan model order differs from its physical bank.");
            }
            return model;
        }

        static uint Color(NativeBattleGraphicsModel model) => model.Color40 ?? throw new NotSupportedException("Native Scan fade requires the model's actual DWORD28 color.");

        void SetModel(int index, NativeBattleGraphicsModel model) => frame = frame with { Models = frame.Models.SetItem(index, model) };

        void SetStage(Func<NativeBattleStageGroup, NativeBattleStageGroup> change) => frame = frame with { Stage = frame.Stage with { Groups = frame.Stage.Groups.Select(change).ToImmutableArray() } };

        void Next() => task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));

        void Finish()
        {
            task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
            Next();
        }
    }
}
