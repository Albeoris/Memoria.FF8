using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanVisibilityFrame(NativeBattleScanFrame Scan, BattleActorModels Models, NativeBattleStageState Stage, uint BackgroundB8B7D8);
public sealed record NativeBattleScanVisibilityStep(NativeBattleScanVisibilityFrame Frame, int Result);

/// <summary>84ED90/8507F0's shared model and stage fade, including retained physical backups.</summary>
public static class NativeBattleScanModelVisibility
{
    public static NativeBattleScanVisibilityStep Advance(NativeBattleScanVisibilityFrame frame, uint address, NativeBattleModelAliases aliases)
    {
        NativeBattleScanProcess process = frame.Scan.Memory.Scan ?? throw new NotSupportedException("Native Scan fade requires its retained presentation owners.");
        NativeBattleScanPresentationMemory memory = process.Presentation;
        memory.Validate();
        if (frame.Stage.Groups.IsDefault || frame.Stage.Groups.Length != 4)
        {
            throw new InvalidDataException("Native Scan fade requires its four stage rows.");
        }
        NativeBattleEffectHierarchy hierarchy = new(frame.Scan.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        bool restore = task.Callback == 0x8507F0;
        if (!restore && task.Callback != 0x84ED90)
        {
            throw new InvalidDataException("Native Scan fade requires84ED90 or8507F0.");
        }
        var savedModels = frame.Scan.Effect.ScanSavedModels;
        int phase = unchecked((sbyte)task.ReadByte(0x29));
        switch (phase)
        {
            case 0 when !restore:
                task = task.WriteWord(0x1C, 0);
                savedModels = savedModels.Clear();
                SetStage(group => group with { ColorDepth = 0, ColorFlags = group.ColorFlags & 0xFF000000u });
                foreach (BattleModelOwner owner in frame.Models.Owners)
                {
                    NativeBattleGraphicsModel model = frame.Models.Get(owner);
                    uint color = Color(model);
                    savedModels = savedModels.SetItem(owner, new(model.Flags0, color));
                    if (aliases.TryEncodeExact(owner, out byte alias))
                    {
                        memory = memory with { SavedFlags269A1D8 = memory.SavedFlags269A1D8.WriteDword(alias * 4, model.Flags0), SavedColors269CE34 = memory.SavedColors269CE34.WriteDword(alias * 4, color) };
                    }
                    if (!Selected(owner))
                    {
                        SetModel(owner, model with { Flags0 = (ushort)(model.Flags0 | 0x0800) });
                    }
                }
                Next();
                break;
            case 0:
                task = task.WriteWord(0x1C, 4096);
                SetStage(group => group with { Flags = (byte)((group.Flags & 0xFD) | ((group.Flags >> 1) & 2)) });
                foreach (BattleModelOwner owner in frame.Models.Owners)
                {
                    NativeBattleGraphicsModel model = frame.Models.Get(owner);
                    NativeBattleScanSavedModel saved = ReadSaved(owner);
                    ushort oldFlags = saved.Flags;
                    bool selected = Selected(owner);
                    ushort mask = selected ? (ushort)0x20 : (ushort)0x24;
                    model = model with { Flags0 = (ushort)((model.Flags0 & ~mask) | (oldFlags & mask)) };
                    if (selected)
                    {
                        model = model with { Color40 = saved.Color };
                    }
                    SetModel(owner, model);
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
                foreach (BattleModelOwner owner in frame.Models.Owners)
                {
                    if (Selected(owner))
                    {
                        continue;
                    }
                    NativeBattleGraphicsModel model = frame.Models.Get(owner);
                    uint color = Color(model) & 0xFF000000u;
                    for (int shift = 0; shift < 24; shift += 8)
                    {
                        int background = (byte)(frame.BackgroundB8B7D8 >> shift);
                        byte channel = unchecked((byte)(background - background * fade / 4096));
                        color |= (uint)channel << shift;
                    }
                    SetModel(owner, model with { Color40 = color });
                }
                break;
            case 2 when !restore:
                SetStage(group => group with { Flags = (byte)(group.Flags & 0xFD) });
                foreach (BattleModelOwner owner in frame.Models.Owners)
                {
                    NativeBattleGraphicsModel model = frame.Models.Get(owner);
                    model = Selected(owner) ? model with { Color40 = 0x00808080, Flags0 = (ushort)(model.Flags0 | 0x20) } : model with { Flags0 = (ushort)(model.Flags0 | 0x24) };
                    SetModel(owner, model);
                }
                Finish();
                break;
            case 2:
                foreach (BattleModelOwner owner in frame.Models.Owners)
                {
                    NativeBattleGraphicsModel model = frame.Models.Get(owner);
                    ushort backup = ReadSaved(owner).Flags;
                    if (Selected(owner))
                    {
                        model = model with { Flags0 = (ushort)((model.Flags0 & ~0x20) | (backup & 0x20)) };
                    }
                    else
                    {
                        model = model with { Flags0 = (ushort)((model.Flags0 & ~0x0800) | (backup & 0x0800)), Color40 = (Color(model) & 0xFF000000u) | (frame.BackgroundB8B7D8 & 0xFFFFFFu) };
                    }
                    SetModel(owner, model);
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
        frame = frame with { Scan = frame.Scan with { Effect = frame.Scan.Effect with { Pools = hierarchy.Pools, ScanSavedModels = savedModels }, Memory = frame.Scan.Memory with { Scan = process with { Presentation = memory } } } };
        return new(frame, complete ? 2 : 0);

        bool Selected(BattleModelOwner owner) => frame.Scan.Effect.ScanSelectedTargets.Contains(owner);

        NativeBattleScanSavedModel ReadSaved(BattleModelOwner owner) => savedModels.TryGetValue(owner, out NativeBattleScanSavedModel? saved) ? saved : throw new InvalidDataException("Native Scan cannot restore flags and color without their captured model owner.");

        static uint Color(NativeBattleGraphicsModel model) => model.Color40 ?? throw new NotSupportedException("Native Scan fade requires the model's actual DWORD28 color.");

        void SetModel(BattleModelOwner owner, NativeBattleGraphicsModel model) => frame = frame with { Models = frame.Models.Set(owner, model) };

        void SetStage(Func<NativeBattleStageGroup, NativeBattleStageGroup> change) => frame = frame with { Stage = frame.Stage with { Groups = frame.Stage.Groups.Select(change).ToImmutableArray() } };

        void Next() => task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));

        void Finish()
        {
            task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
            Next();
        }
    }
}
