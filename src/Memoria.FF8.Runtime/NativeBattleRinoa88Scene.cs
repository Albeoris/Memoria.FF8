namespace Memoria.FF8.Runtime;

public abstract record NativeBattleRinoa88Operation;
public sealed record NativeBattleRinoa88StaticTexture(uint Address) : NativeBattleRinoa88Operation;
public sealed record NativeBattleRinoa88CameraControl : NativeBattleRinoa88Operation;
public sealed record NativeBattleRinoa88PhaseBirth(uint SceneAddress, ushort Phase) : NativeBattleRinoa88Operation;
public sealed record NativeBattleRinoa88Visibility(uint Callback) : NativeBattleRinoa88Operation;
public sealed record NativeBattleRinoa88SceneInputs(NativeBattleRinoa88Inputs Controller, Func<NativeBattleRinoa88Frame, NativeBattleRinoa88Operation, NativeBattleRinoa88Frame> Execute);

/// <summary>768ED0/768F90's fifteen-state scene and original two-stage requests.</summary>
public static class NativeBattleRinoa88Scene
{
    public static NativeBattleRinoa88Frame InitializeTarget(NativeBattleRinoa88Frame frame, uint address, NativeBattleRinoa88Inputs inputs, NativeBattleRinoa88IoResources resources)
    {
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        NativeBattleRinoa88SceneMemory scene = NativeBattleRinoa88SceneMemory.Initialize(task.ReadByte(44), task.ReadByte(45), Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray(), frame.State.Scene?.Camera, frame.State.Scene?.CameraAnchorPaddingKnown);
        NativeBattleRinoa88IoState io = NativeBattleRinoa88Io.Initialize7689C0(frame.State.Io?.Script ?? resources.Script.Data, checked(0x20DFAB8u + (uint)frame.State.AuxiliaryCursor));
        return frame with { State = frame.State with { Scene = scene, Io = io, AuxiliaryCursor = checked(frame.State.AuxiliaryCursor + 40960) } };
    }

    public static NativeBattleRinoa88LeafStep Advance(NativeBattleRinoa88Frame frame, uint address, NativeBattleRinoa88SceneInputs inputs)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != 0x768ED0)
        {
            throw new InvalidDataException("Native768ED0 requires its actual72-byte scene task.");
        }
        NativeBattleRinoa88SceneMemory scene = Read(frame);
        scene = scene.WriteWord(70, unchecked((ushort)(scene.Word(70) + 1)));
        if (scene.Word(64) != scene.Word(66))
        {
            scene = scene.WriteWord(64, scene.Word(66)).WriteWord(70, 0);
            frame = Store(frame, scene);
            frame = inputs.Execute(frame, new NativeBattleRinoa88PhaseBirth(address, scene.Word(64)));
            hierarchy = new(frame.Effect.Pools);
        }
        if (scene.Word(66) != scene.Word(68))
        {
            scene = scene.WriteWord(66, scene.Word(68));
        }
        frame = Store(frame, scene);
        switch (unchecked((sbyte)task.ReadByte(41)))
        {
            case 0:
                Execute(new NativeBattleRinoa88StaticTexture(0x16F3000));
                Execute(new NativeBattleRinoa88StaticTexture(0x16FB420));
                Execute(new NativeBattleRinoa88CameraControl());
                hierarchy = hierarchy.Append8DC540(5, 0x769060, 144, address, Select).Hierarchy;
                Request(1);
                break;
            case 1:
                if (scene.Word(64) >= 1)
                {
                    Execute(new NativeBattleRinoa88Visibility(0x769CB0));
                    hierarchy = Next(hierarchy, address);
                }
                break;
            case 2:
                NativeBattleRinoa88IoRelease release = NativeBattleRinoa88Io.ReleaseWait769D10(frame.State.Io ?? throw new NotSupportedException("Native769D10 requires its initialized IO context."));
                frame = frame with { State = frame.State with { Io = release.State } };
                if (release.Released)
                {
                    hierarchy = Next(hierarchy, address);
                }
                break;
            case 3:
                if ((frame.State.Io ?? throw new NotSupportedException("Native769D50 requires its actual IO handshake.")).WaitReleased == 0)
                {
                    hierarchy = Next(hierarchy, address);
                }
                break;
            case 4:
                Request(2);
                break;
            case 5:
                if (scene.Word(64) >= 3)
                {
                    Execute(new NativeBattleRinoa88Visibility(0x769DB0));
                    Execute(new NativeBattleRinoa88Visibility(0x769DD0));
                    hierarchy = Next(hierarchy, address);
                }
                break;
            case 6:
                if (scene.SignedWord(70) >= 21)
                {
                    Request(4);
                }
                break;
            case 7:
                if (scene.Word(64) >= 4)
                {
                    Execute(new NativeBattleRinoa88Visibility(0x769E60));
                    Execute(new NativeBattleRinoa88Visibility(0x769E90));
                    hierarchy = Next(hierarchy, address);
                }
                break;
            case 8:
                if (scene.SignedWord(70) >= 35)
                {
                    frame = Store(frame, scene.WriteWord(72, 1));
                    hierarchy = Next(hierarchy, address);
                }
                break;
            case 9:
                if (scene.SignedWord(70) >= 52)
                {
                    Request(5);
                }
                break;
            case 10:
                if (scene.Word(64) >= 5)
                {
                    Execute(new NativeBattleRinoa88Visibility(0x769F40));
                    hierarchy = Next(hierarchy, address);
                }
                break;
            case 11:
                if (scene.SignedWord(70) >= 20)
                {
                    hierarchy = Next(hierarchy, address);
                }
                break;
            case 12:
                Request(6);
                break;
            case 13:
                if (scene.Word(64) >= 6)
                {
                    task = hierarchy.Read(address);
                    hierarchy = hierarchy.Write(address, task.WriteByte(38, (byte)(task.ReadByte(38) | 1)));
                    hierarchy = Next(hierarchy, address);
                }
                break;
            case 14:
                break;
            default:
                throw new InvalidDataException("Native768ED0 scene signed phase exceeds its fifteen callbacks.");
        }
        task = hierarchy.Read(address);
        task = task.WriteWord(36, unchecked((ushort)(task.ReadWord(36) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(38) & 1) != 0 && task.ReadByte(40) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        return new(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, complete ? 2 : 0);

        NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(frame.Effect.Parameters!, inputs.Controller.Packets, pointer, group, target);
        void Execute(NativeBattleRinoa88Operation operation)
        {
            frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
            frame = inputs.Execute(frame, operation);
            scene = Read(frame);
            hierarchy = new(frame.Effect.Pools);
        }
        void Request(short phase)
        {
            if (scene.CanRequestPhase(phase))
            {
                scene = scene.WriteWord(68, unchecked((ushort)phase));
                frame = Store(frame, scene);
                hierarchy = Next(hierarchy, address);
            }
        }
    }

    public static NativeBattleRinoa88SceneMemory Read(NativeBattleRinoa88Frame frame)
    {
        NativeBattleRinoa88SceneMemory scene = frame.State.Scene ?? throw new NotSupportedException("Native Rinoa88 requires its initialized scene workspace.");
        scene.Validate();
        return scene;
    }

    private static NativeBattleRinoa88Frame Store(NativeBattleRinoa88Frame frame, NativeBattleRinoa88SceneMemory scene) => frame with { State = frame.State with { Scene = scene } };

    private static NativeBattleEffectHierarchy Next(NativeBattleEffectHierarchy hierarchy, uint address)
    {
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        return hierarchy.Write(address, task.WriteByte(41, unchecked((byte)(task.ReadByte(41) + 1))));
    }
}
