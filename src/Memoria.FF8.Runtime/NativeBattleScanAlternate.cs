using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanAlternateFrame(NativeBattleScanCameraFrame Camera, ImmutableArray<NativeBattleGraphicsModel> Models, ushort Highlight269AA90, ushort Complete269D93A, ushort Exit269CE2C);
public sealed record NativeBattleScanAlternateInputs(NativeBattleActionPacketsState Packets, Func<int, NativeBattleScanViewerProfile> ReadProfile);
public sealed record NativeBattleScanAlternateStep(NativeBattleScanAlternateFrame Frame, int Result);
public sealed record NativeBattleScanSavedCamera(NativeBattleScanByteOwner Eye8, NativeBattleScanByteOwner Target8, ushort Zoom);

public static class NativeBattleScanAlternate
{
    public static NativeBattleScanAlternateStep Advance850B40(NativeBattleScanAlternateFrame frame, uint address, NativeBattleScanAlternateInputs inputs)
    {
        NativeBattleEffectParameters parameters = frame.Camera.Scan.Effect.Parameters ?? throw new InvalidDataException("Native Scan alternate primary requires its current action parameters.");
        NativeBattleEffectHierarchy hierarchy = new(frame.Camera.Scan.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != 0x850B40)
        {
            throw new InvalidDataException("Native Scan alternate primary requires its850B40 task.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                Flags(value => (ushort)(value | 0x7800));
                Append(2, 0x84E810, 0x78, 2);
                frame = frame with { Highlight269AA90 = 1 };
                Append(2, 0x84D5B0, 0x78);
                task = task.WriteWord(0x66, 45);
                Next();
                break;
            case 1:
                if (CountDown())
                {
                    Append(4, 0x850C70, 0x6C);
                    task = task.WriteWord(0x66, 8);
                    Next();
                }
                break;
            case 2:
                if (CountDown())
                {
                    Append(2, 0x850FD0, 0x78);
                    Append(4, 0x850EB0, 0x6C);
                    Next();
                }
                break;
            case 3:
                NativeBattleScanViewerProfile profile = inputs.ReadProfile(task.ReadByte(0x2D));
                profile.Bytes16.RequireLength(16);
                NativeBattleScanByteOwner camera = frame.Camera.Scan.Memory.Scan!.Presentation.Camera269A168.WriteWord(0x4C, profile.Category);
                if (profile.Bytes16.ReadByte(0x0E) == 1)
                {
                    camera = camera.WriteWord(0x34, (ushort)((camera.ReadWord(0x34) + 64) & 0xFFF));
                }
                if (unchecked((short)profile.Bytes16.ReadWord(4)) < -384)
                {
                    short height = unchecked((short)(camera.ReadWord(0x40) - unchecked((short)camera.ReadWord(0x44)) / 32));
                    camera = camera.WriteWord(0x40, height > 0 ? (ushort)0 : unchecked((ushort)height));
                }
                frame = frame with { Camera = NativeBattleScanCameraTransition.Publish84EC80(frame.Camera, camera) };
                if (frame.Complete269D93A == 1)
                {
                    frame = frame with { Highlight269AA90 = 0, Exit269CE2C = 1 };
                    Next();
                }
                break;
            case 4:
                if (task.ReadByte(0x28) == 0)
                {
                    NativeBattleModulePoolTask owner = hierarchy.Read(task.ReadDword(0x10));
                    if (unchecked((sbyte)task.ReadByte(0x2A)) == unchecked((short)owner.ReadWord(0x58)))
                    {
                        Append(2, 0x850A30, 0x78);
                    }
                    else if (unchecked((short)frame.Camera.Scan.Memory.Scan!.Presentation.Camera269A168.ReadWord(0x3A)) <= -8192)
                    {
                        Append(2, 0x84E810, 0x78, 3);
                    }
                    Flags(value => (ushort)(value & 0x87FF));
                    Next();
                }
                break;
            case 5:
                if (task.ReadByte(0x28) == 0)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                    Next();
                }
                break;
            case 6:
                break;
            default:
                throw new InvalidDataException("Native Scan alternate primary signed phase exceeds its seven callbacks.");
        }
        NativeBattleScanCameraStep finished = Finish(frame.Camera, hierarchy, task, address);
        return new(frame with { Camera = finished.Frame }, finished.Result);

        void Next()
        {
            task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
        }

        bool CountDown()
        {
            task = task.WriteWord(0x66, unchecked((ushort)(task.ReadWord(0x66) - 1)));
            return unchecked((short)task.ReadWord(0x66)) < 0;
        }

        void Append(int pool, uint callback, ushort stride, ushort? mode = null)
        {
            hierarchy = hierarchy.Write(address, task);
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(pool, callback, stride, address, (pointer, group, target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target));
            hierarchy = appended.Hierarchy;
            task = hierarchy.Read(address);
            if (mode is ushort value && appended.Address != 0)
            {
                hierarchy = hierarchy.Write(appended.Address, hierarchy.Read(appended.Address).WriteWord(0x68, value));
            }
        }

        void Flags(Func<ushort, ushort> update)
        {
            int actor = task.ReadByte(0x2D);
            if (frame.Models.IsDefault || frame.Models.Length != 7 || actor >= 7 || frame.Models[actor].Index != actor)
            {
                throw new InvalidDataException("Native Scan alternate primary requires its seven current physical models.");
            }
            frame = frame with { Models = frame.Models.SetItem(actor, frame.Models[actor] with { Flags0 = update(frame.Models[actor].Flags0) }) };
        }
    }

    public static NativeBattleScanCameraStep Advance850A30(NativeBattleScanCameraFrame frame, uint address, NativeBattleScanSavedCamera saved)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Scan.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != 0x850A30)
        {
            throw new InvalidDataException("Native Scan camera restoration requires its850A30 task.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                saved.Eye8.RequireLength(8);
                saved.Target8.RequireLength(8);
                frame = frame with { Eye = NativeBattleScanViewer.Copy(frame.Eye, 0, saved.Eye8, 0, 8), Target = NativeBattleScanViewer.Copy(frame.Target, 0, saved.Target8, 0, 8), Zoom = saved.Zoom };
                task = task.WriteWord(0x66, 1).WriteByte(0x29, 1);
                break;
            case 1:
                task = task.WriteWord(0x66, unchecked((ushort)(task.ReadWord(0x66) - 1)));
                if (unchecked((short)task.ReadWord(0x66)) <= 0)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, 2);
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Native Scan camera restoration signed phase exceeds its three callbacks.");
        }
        return Finish(frame, hierarchy, task, address);
    }

    private static NativeBattleScanCameraStep Finish(NativeBattleScanCameraFrame frame, NativeBattleEffectHierarchy hierarchy, NativeBattleModulePoolTask task, uint address)
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
}
