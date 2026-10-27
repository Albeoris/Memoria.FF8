using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanViewerFrame(NativeBattleScanFrame Scan, ImmutableArray<NativeBattleScanByteOwner> Models, NativeBattleScanByteOwner CameraTarget8, NativeBattleScanTextFrame Text);
public sealed record NativeBattleScanViewerProfile(ushort Category, NativeBattleScanByteOwner Bytes16);
public sealed record NativeBattleScanViewerInputs(NativeBattleActionPacketsState Packets, NativeBattleScanInputBank Input, uint Options, IReadOnlyList<byte> ButtonMap, Func<int, NativeBattleScanByteOwner> ReadName24, Func<int, uint> ReadDescriptionPointer, Func<int, NativeBattleScanViewerProfile> ReadProfile, Func<NativeBattleScanViewerFrame, int, byte, int, NativeBattleVector> ReadBone502170)
{
    public bool UnrestrictedRotation { get; init; }
}
public sealed record NativeBattleScanViewerStep(NativeBattleScanViewerFrame Frame, int Result);

public static class NativeBattleScanViewer
{
    public static NativeBattleScanViewerStep Advance84F860(NativeBattleScanViewerFrame frame, uint address, NativeBattleScanViewerInputs inputs)
    {
        Validate(frame);
        NativeBattleEffectHierarchy hierarchy = new(frame.Scan.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != 0x84F860)
        {
            throw new InvalidDataException("Native Scan viewer requires its actual84F860 task.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                NativeBattleScanByteOwner name = inputs.ReadName24(task.ReadByte(0x2D));
                name.RequireLength(24);
                SetPresentation(Presentation() with { Name269AA78 = name });
                frame = frame with { Text = frame.Text with { Windows = frame.Text.Windows.SetText4A0410(2, 0x269AA78).SetFlags4A0700(2, 0).SetRect4A07A0(2, new(170, 30, 136, 28)).Open4A0640(2).SetText4A0410(0, inputs.ReadDescriptionPointer(task.ReadByte(0x2D))).SetFlags4A0700(0, 0).SetRect4A07A0(0, new(170, 52, 136, 128)).Open4A0640(0) } };
                _ = frame.Scan.Effect.Parameters ?? throw new InvalidDataException("Native Scan viewer requires its actual action parameters.");
                NativeBattleEffectHierarchyAppend result = hierarchy.Append8DC540(2, 0x84FAF0, 0x78, address, Select);
                hierarchy = result.Hierarchy;
                if (result.Address != 0)
                {
                    hierarchy = hierarchy.Write(result.Address, hierarchy.Read(result.Address).WriteWord(0x66, 0));
                }
                hierarchy = hierarchy.Append8DC540(4, 0x84F9E0, 0x6C, address, Select).Hierarchy;
                Publish();
                frame = Initialize84FCD0(frame, address, inputs);
                hierarchy = new(frame.Scan.Effect.Pools);
                task = hierarchy.Read(address);
                Next();
                break;
            case 1:
                Interactive();
                if (Process().Complete269D93A == 1)
                {
                    Next();
                }
                break;
            case 2:
                Interactive();
                ushort held = NativeInputConfiguration.Project(inputs.Input.Read49ED30(0, 0), inputs.Options, inputs.ButtonMap);
                if ((held & 0x40) != 0)
                {
                    task = task.WriteWord(0x66, 18);
                    SetPresentation(Presentation() with { Result269D938 = 1, Exit269CE2C = 1 });
                    Next();
                }
                break;
            case 3:
                Interactive();
                task = task.WriteWord(0x66, unchecked((ushort)(task.ReadWord(0x66) - 1)));
                if (unchecked((short)task.ReadWord(0x66)) <= 0)
                {
                    Next();
                }
                break;
            case 4:
                frame = Restore8506F0(frame);
                frame = frame with { Text = frame.Text with { Windows = frame.Text.Windows.Close4A0680(2).SetText4A0410(2, 0).Close4A0680(0).SetText4A0410(0, 0) } };
                task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                Next();
                break;
            case 5:
                break;
            default:
                throw new InvalidDataException("Native Scan viewer signed phase exceeds its six callbacks.");
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        Publish();
        return new(frame, complete ? 2 : 0);

        NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte actor) => NativeBattleCureController.SelectActors(frame.Scan.Effect.Parameters!, inputs.Packets, pointer, group, actor);
        NativeBattleScanProcess Process() => frame.Scan.Memory.Scan!;
        NativeBattleScanPresentationMemory Presentation() => Process().Presentation;
        void SetPresentation(NativeBattleScanPresentationMemory value) => frame = frame with { Scan = frame.Scan with { Memory = frame.Scan.Memory with { Scan = Process() with { Presentation = value } } } };
        void Next() => task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
        void Publish() => frame = frame with { Scan = frame.Scan with { Effect = frame.Scan.Effect with { Pools = hierarchy.Pools } } };
        void Interactive()
        {
            hierarchy = hierarchy.Write(address, task);
            Publish();
            frame = Advance84FD90(frame, address, inputs);
            hierarchy = new(frame.Scan.Effect.Pools);
            task = hierarchy.Read(address);
        }
    }

    public static NativeBattleScanViewerFrame Initialize84FCD0(NativeBattleScanViewerFrame frame, uint address, NativeBattleScanViewerInputs inputs)
    {
        Validate(frame);
        NativeBattleEffectHierarchy hierarchy = new(frame.Scan.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        int selected = Selected(frame);
        NativeBattleScanByteOwner model = frame.Models[selected];
        NativeBattleScanPresentationMemory presentation = frame.Scan.Memory.Scan!.Presentation;
        NativeBattleScanByteOwner camera = Copy(presentation.Camera269A168, 0x20, model, 0x0C, 8);
        camera = Copy(camera, 0x28, model, 0x1C, 8);
        model = model.WriteDword(0x30, checked(address + 0x30));
        task = task.WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096);
        hierarchy = hierarchy.Write(address, task);
        frame = frame with { Models = frame.Models.SetItem(selected, model), Scan = frame.Scan with { Effect = frame.Scan.Effect with { Pools = hierarchy.Pools }, Memory = frame.Scan.Memory with { Scan = frame.Scan.Memory.Scan! with { Presentation = presentation with { Camera269A168 = camera } } } } };
        NativeBattleVector bone = inputs.ReadBone502170(frame, selected, 0xF1, 0);
        NativeBattleScanViewerProfile profile = inputs.ReadProfile(task.ReadByte(0x2D));
        profile.Bytes16.RequireLength(16);
        camera = camera.WriteWord(0x4C, profile.Category);
        if (profile.Bytes16.ReadWord(8) == 0)
        {
            bone = Position(model) with { Y = 0 };
        }
        task = task.WriteWord(0x54, unchecked((ushort)(Word(model, 0x1C) - bone.X))).WriteWord(0x58, unchecked((ushort)(Word(model, 0x20) - bone.Z)));
        hierarchy = hierarchy.Write(address, task);
        return frame with { Scan = frame.Scan with { Effect = frame.Scan.Effect with { Pools = hierarchy.Pools }, Memory = frame.Scan.Memory with { Scan = frame.Scan.Memory.Scan! with { Presentation = frame.Scan.Memory.Scan!.Presentation with { Camera269A168 = camera } } } } };
    }

    public static NativeBattleScanViewerFrame Reset850600(NativeBattleScanViewerFrame frame, uint address)
    {
        Validate(frame);
        int selected = Selected(frame);
        NativeBattleScanByteOwner camera = frame.Scan.Memory.Scan!.Presentation.Camera269A168;
        NativeBattleScanByteOwner model = Copy(frame.Models[selected], 0x0C, camera, 0x20, 8);
        NativeBattleEffectHierarchy hierarchy = new(frame.Scan.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address).WriteWord(0x70, 0).WriteWord(0x40, 0).WriteWord(0x42, 0).WriteWord(0x44, 0).WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096);
        return frame with { Models = frame.Models.SetItem(selected, model), Scan = frame.Scan with { Effect = frame.Scan.Effect with { Pools = hierarchy.Write(address, task).Pools } } };
    }

    public static NativeBattleScanViewerFrame Advance84FD90(NativeBattleScanViewerFrame frame, uint address, NativeBattleScanViewerInputs inputs)
    {
        Validate(frame);
        NativeBattleEffectHierarchy hierarchy = new(frame.Scan.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        NativeBattleScanViewerProfile profile = inputs.ReadProfile(task.ReadByte(0x2D));
        profile.Bytes16.RequireLength(16);
        NativeBattleScanProcess process = frame.Scan.Memory.Scan!;
        process = process with { Presentation = process.Presentation with { Camera269A168 = process.Presentation.Camera269A168.WriteWord(0x4C, profile.Category) }, InteractiveInput269AAA8 = NativeBattleScanInput.Sample8501F0(process.InteractiveInput269AAA8, inputs.Input, inputs.Options, inputs.ButtonMap) };
        frame = frame with { Scan = frame.Scan with { Memory = frame.Scan.Memory with { Scan = process } } };
        uint held = process.InteractiveInput269AAA8.ReadDword(0);
        if ((held & 5) == 5)
        {
            Reset();
        }
        int selected = Selected(frame);
        NativeBattleScanByteOwner model = frame.Models[selected];
        if (unchecked((short)task.ReadWord(0x70)) == 0)
        {
            task = task.WriteWord(0x72, 75);
            if (inputs.UnrestrictedRotation || profile.Bytes16.ReadByte(0x0D) == 1)
            {
                model = model.WriteWord(0x0E, unchecked((ushort)(model.ReadWord(0x0E) + 64)));
            }
        }
        else
        {
            task = task.WriteWord(0x72, unchecked((ushort)(task.ReadWord(0x72) - 1)));
            if (unchecked((short)task.ReadWord(0x72)) <= 0)
            {
                Reset();
                model = frame.Models[selected];
            }
        }
        Rotate(0x20, 0x0E, 0x0D, 16);
        Rotate(0x22, 0x0E, 0x0D, -16);
        Rotate(0x24, 0x0C, 0x0C, -16);
        Rotate(0x26, 0x0C, 0x0C, 16);
        int axis = Axis(0x1C);
        if (axis > 0)
        {
            Scale(Math.Min(unchecked((int)task.ReadDword(0x30) + axis * 16), 8192));
        }
        else if ((axis = Axis(0x1E)) > 0)
        {
            Scale(Math.Max(unchecked((int)task.ReadDword(0x30) - axis * 16), 2048));
        }
        Translate(0x28, 0x40, -8);
        Translate(0x2A, 0x40, 8);
        Translate(0x2C, 0x42, -8);
        Translate(0x2E, 0x42, 8);
        model = model.WriteWord(0x0C, (ushort)(model.ReadWord(0x0C) & 0xFFF)).WriteWord(0x0E, (ushort)(model.ReadWord(0x0E) & 0xFFF)).WriteWord(0x10, (ushort)(model.ReadWord(0x10) & 0xFFF));
        NativeBattleScanByteOwner camera = process.Presentation.Camera269A168;
        model = Copy(model, 0x1C, camera, 0x28, 8);
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Scale56BEF0(Euler(model, 0x0C), unchecked((int)task.ReadDword(0x30)), unchecked((int)task.ReadDword(0x34)), unchecked((int)task.ReadDword(0x38)));
        short pivot = Word(profile.Bytes16, 6);
        Add(NativeBattleGfMatrices.RotateShort56C4F0(matrix, new(0, pivot, 0)));
        model = model.WriteWord(0x1E, unchecked((ushort)(model.ReadWord(0x1E) - pivot)));
        NativeBattleActionMatrix basis = Euler(camera, 0x20);
        if (task.ReadWord(0x70) == 1 && profile.Bytes16.ReadWord(8) == 0)
        {
            PublishModel();
            NativeBattleVector bone = inputs.ReadBone502170(frame, selected, 0xF1, 0);
            Add(new(unchecked((short)(Word(frame.CameraTarget8, 0) - bone.X)), unchecked((short)(Word(frame.CameraTarget8, 2) - bone.Y)), unchecked((short)(Word(frame.CameraTarget8, 4) - bone.Z))));
            PublishTranslation();
            Add(NativeBattleGfMatrices.RotateShort56C4F0(basis, new(unchecked((short)-Word(profile.Bytes16, 2)), unchecked((short)-Word(profile.Bytes16, 0x0A)), 0)));
            PublishTranslation();
        }
        Add(NativeBattleGfMatrices.RotateShort56C4F0(basis, new(unchecked((short)task.ReadWord(0x40)), unchecked((short)task.ReadWord(0x42)), unchecked((short)task.ReadWord(0x44)))));
        PublishTranslation();
        NativeBattleScanByteOwner source = model;
        for (int index = 0; index < 7; index++)
        {
            if (process.Presentation.Selection269A1BC.ReadDword(index * 4) == 1)
            {
                frame = frame with { Models = frame.Models.SetItem(index, Copy(Copy(frame.Models[index], 0x1C, source, 0x1C, 8), 0x40, source, 0x40, 32)) };
            }
        }
        PublishModel();
        return frame;

        int Axis(int offset) => Word(process.InteractiveInput269AAA8, offset);
        void Manual() => task = task.WriteWord(0x70, 1).WriteWord(0x72, 75);
        void Rotate(int input, int angle, int permission, int factor)
        {
            int value = Axis(input);
            if (value > 0)
            {
                Manual();
                if (inputs.UnrestrictedRotation || profile.Bytes16.ReadByte(permission) == 1)
                {
                    model = model.WriteWord(angle, unchecked((ushort)(model.ReadWord(angle) + value * factor)));
                }
            }
        }
        void Scale(int value)
        {
            Manual();
            task = task.WriteDword(0x30, unchecked((uint)value)).WriteDword(0x34, unchecked((uint)value)).WriteDword(0x38, unchecked((uint)value));
        }
        void Translate(int input, int field, int factor)
        {
            int value = Axis(input);
            if (value > 0)
            {
                Manual();
                task = task.WriteWord(field, unchecked((ushort)(task.ReadWord(field) + value * factor)));
            }
        }
        void Add(NativeBattleVector value) => model = model.WriteWord(0x1C, unchecked((ushort)(model.ReadWord(0x1C) + value.X))).WriteWord(0x1E, unchecked((ushort)(model.ReadWord(0x1E) + value.Y))).WriteWord(0x20, unchecked((ushort)(model.ReadWord(0x20) + value.Z)));
        void PublishTranslation() => model = model.WriteDword(0x54, unchecked((uint)(int)Word(model, 0x1C))).WriteDword(0x58, unchecked((uint)(int)Word(model, 0x1E))).WriteDword(0x5C, unchecked((uint)(int)Word(model, 0x20)));
        void PublishModel()
        {
            PublishTranslation();
            hierarchy = hierarchy.Write(address, task);
            frame = frame with { Models = frame.Models.SetItem(selected, model), Scan = frame.Scan with { Effect = frame.Scan.Effect with { Pools = hierarchy.Pools } } };
        }
        void Reset()
        {
            hierarchy = hierarchy.Write(address, task);
            frame = frame with { Scan = frame.Scan with { Effect = frame.Scan.Effect with { Pools = hierarchy.Pools } } };
            frame = Reset850600(frame, address);
            hierarchy = new(frame.Scan.Effect.Pools);
            task = hierarchy.Read(address);
        }
    }

    public static NativeBattleScanViewerFrame Restore8506F0(NativeBattleScanViewerFrame frame)
    {
        Validate(frame);
        NativeBattleScanPresentationMemory presentation = frame.Scan.Memory.Scan!.Presentation;
        for (int index = 0; index < 7; index++)
        {
            if (presentation.Selection269A1BC.ReadDword(index * 4) == 1)
            {
                NativeBattleScanByteOwner model = Copy(Copy(frame.Models[index], 0x0C, presentation.Camera269A168, 0x20, 8), 0x1C, presentation.Camera269A168, 0x28, 8).WriteDword(0x30, 0);
                frame = frame with { Models = frame.Models.SetItem(index, model) };
            }
        }
        return frame;
    }

    public static NativeBattleScanByteOwner Copy(NativeBattleScanByteOwner destination, int destinationOffset, NativeBattleScanByteOwner source, int sourceOffset, int length)
    {
        if (source.Bytes.IsDefault || source.Known.IsDefault || source.Bytes.Length != source.Known.Length || destination.Bytes.IsDefault || destination.Known.IsDefault || destination.Bytes.Length != destination.Known.Length || sourceOffset < 0 || sourceOffset > source.Bytes.Length - length || destinationOffset < 0 || destinationOffset > destination.Bytes.Length - length || length < 0)
        {
            throw new InvalidDataException("Native Scan byte copy leaves its actual source or destination extent.");
        }
        ImmutableArray<byte>.Builder bytes = destination.Bytes.ToBuilder();
        ImmutableArray<bool>.Builder known = destination.Known.ToBuilder();
        for (int index = 0; index < length; index++)
        {
            bytes[destinationOffset + index] = source.Bytes[sourceOffset + index];
            known[destinationOffset + index] = source.Known[sourceOffset + index];
        }
        return new(bytes.ToImmutable(), known.ToImmutable());
    }

    private static short Word(NativeBattleScanByteOwner owner, int offset) => unchecked((short)owner.ReadWord(offset));
    private static NativeBattleVector Position(NativeBattleScanByteOwner owner) => new(Word(owner, 0x1C), Word(owner, 0x1E), Word(owner, 0x20));
    private static NativeBattleActionMatrix Euler(NativeBattleScanByteOwner owner, int offset) => NativeBattleGfMatrices.Euler56CE30(new(Word(owner, offset), Word(owner, offset + 2), Word(owner, offset + 4)));
    private static int Selected(NativeBattleScanViewerFrame frame)
    {
        uint offset = unchecked(frame.Scan.Memory.Scan!.Presentation.PrimaryModel269CE30 - NativeBattleScanModelSelection.ModelBase);
        if (offset >= 7 * 156 || offset % 156 != 0)
        {
            throw new NotSupportedException("Native Scan viewer selected model leaves its actual physical bank.");
        }
        return (int)(offset / 156);
    }
    private static void Validate(NativeBattleScanViewerFrame frame)
    {
        if (frame.Scan.Memory.Scan is null || frame.Models.IsDefault || frame.Models.Length != 7)
        {
            throw new NotSupportedException("Native Scan viewer requires its actual process and seven current physical model views.");
        }
        foreach (NativeBattleScanByteOwner model in frame.Models)
        {
            model.RequireLength(156);
        }
        frame.CameraTarget8.RequireLength(8);
    }
}
