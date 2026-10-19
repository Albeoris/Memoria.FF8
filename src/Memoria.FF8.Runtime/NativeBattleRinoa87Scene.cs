using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public abstract record NativeBattleRinoa87Operation;
public sealed record NativeBattleRinoa87StaticTexture(uint Address) : NativeBattleRinoa87Operation;
public sealed record NativeBattleRinoa87CameraControl : NativeBattleRinoa87Operation;
public sealed record NativeBattleRinoa87BirthBody(uint Address) : NativeBattleRinoa87Operation;
public sealed record NativeBattleRinoa87SceneInputs(NativeBattleRinoa87Inputs Controller, Func<NativeBattleRinoa87Frame, NativeBattleRinoa87Operation, NativeBattleRinoa87Frame> Execute);

/// <summary>774190/774240's original ten-state scene and its two-stage requests.</summary>
public static class NativeBattleRinoa87Scene
{
    public static NativeBattleRinoa87Frame InitializeTarget(NativeBattleRinoa87Frame frame, uint address, NativeBattleRinoa87Inputs inputs, NativeBattleRinoa87IoResources resources, Func<int, int> minimum501FF0)
    {
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        NativeBattleRinoa87SceneMemory scene = NativeBattleRinoa87SceneMemory.Initialize(task.ReadByte(44), task.ReadByte(45), Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray(), minimum501FF0, frame.State.Scene?.Camera);
        NativeBattleRinoa87IoState io = NativeBattleRinoa87Io.Initialize773D40(frame.State.Io?.Script ?? resources.Script.Data, checked(0x20DFAB8u + (uint)frame.State.AuxiliaryCursor));
        return frame with { State = frame.State with { Scene = scene, Io = io, AuxiliaryCursor = checked(frame.State.AuxiliaryCursor + 40960) } };
    }

    public static NativeBattleRinoa87LeafStep Advance(NativeBattleRinoa87Frame frame, uint address, NativeBattleRinoa87SceneInputs inputs)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != 0x774240)
        {
            throw new InvalidDataException("Native774240 requires its actual72-byte scene task.");
        }
        NativeBattleRinoa87SceneMemory scene = Read(frame);
        scene = scene.WriteWord(70, unchecked((ushort)(scene.Word(70) + 1)));
        if (scene.Word(64) != scene.Word(66))
        {
            scene = scene.WriteWord(64, scene.Word(66)).WriteWord(70, 0);
            if (scene.Word(64) == 1)
            {
                NativeBattleEffectHierarchyAppend body = hierarchy.Append8DC540(8, 0x7751A0, 376, address, Select);
                frame = frame with { Effect = frame.Effect with { Pools = body.Hierarchy.Pools } };
                frame = inputs.Execute(frame, new NativeBattleRinoa87BirthBody(body.Address));
                hierarchy = new(frame.Effect.Pools);
            }
        }
        if (scene.Word(66) != scene.Word(68))
        {
            scene = scene.WriteWord(66, scene.Word(68));
        }
        frame = Store(frame, scene) with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        switch (unchecked((sbyte)task.ReadByte(41)))
        {
            case 0:
                frame = inputs.Execute(frame, new NativeBattleRinoa87StaticTexture(0x16FDF78));
                frame = inputs.Execute(frame, new NativeBattleRinoa87CameraControl());
                hierarchy = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(5, 0x7743A0, 144, address, Select).Hierarchy;
                Request(1);
                break;
            case 1:
                WaitForPhase(1);
                break;
            case 2:
                NativeBattleRinoa87IoRelease release = NativeBattleRinoa87Io.ReleaseWait774FD0(frame.State.Io ?? throw new NotSupportedException("Native774FD0 requires its actual initialized IO context."));
                frame = frame with { State = frame.State with { Io = release.State } };
                if (release.Released)
                {
                    hierarchy = Next(hierarchy, address);
                }
                break;
            case 3:
                if (scene.SignedWord(70) >= 28)
                {
                    bool waiting = (frame.State.Io ?? throw new NotSupportedException("Native775010 requires its actual IO wait handshake.")).WaitReleased != 0;
                    int pause = waiting ? 1 : 0;
                    if (pause != 0 && frame.State.Counter25B4B4C == 0 || pause == 0 && frame.State.Counter25B4B4C == 1)
                    {
                        frame = frame with { State = frame.State with { Counter25B4B4C = pause, Counter25AECD4 = pause } };
                    }
                    if (!waiting)
                    {
                        hierarchy = Next(hierarchy, address);
                    }
                }
                break;
            case 4:
                Request(2);
                break;
            case 5:
                WaitForPhase(2);
                break;
            case 6:
                if (scene.SignedWord(70) >= 72)
                {
                    frame = Store(frame, scene.WriteWord(72, 1));
                    hierarchy = Next(hierarchy, address);
                }
                break;
            case 7:
                Request(3);
                break;
            case 8:
                if (scene.Word(64) >= 3)
                {
                    hierarchy = hierarchy.Write(address, hierarchy.Read(address).WriteByte(38, (byte)(task.ReadByte(38) | 1)));
                    hierarchy = Next(hierarchy, address);
                }
                break;
            case 9:
                break;
            default:
                throw new InvalidDataException("Native774240 scene signed phase exceeds its ten callbacks.");
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
        void Request(short phase)
        {
            if (scene.CanRequestPhase(phase))
            {
                frame = Store(frame, scene.WriteWord(68, unchecked((ushort)phase)));
                hierarchy = Next(hierarchy, address);
            }
        }
        void WaitForPhase(ushort phase)
        {
            if (scene.Word(64) >= phase)
            {
                hierarchy = Next(hierarchy, address);
            }
        }

    }

    public static NativeBattleRinoa87SceneMemory Read(NativeBattleRinoa87Frame frame)
    {
        NativeBattleRinoa87SceneMemory scene = frame.State.Scene ?? throw new NotSupportedException("Native Rinoa87 requires its initialized scene workspace.");
        scene.Validate();
        return scene;
    }
    private static NativeBattleRinoa87Frame Store(NativeBattleRinoa87Frame frame, NativeBattleRinoa87SceneMemory scene) => frame with { State = frame.State with { Scene = scene } };
    private static NativeBattleEffectHierarchy Next(NativeBattleEffectHierarchy hierarchy, uint address)
    {
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        return hierarchy.Write(address, task.WriteByte(41, unchecked((byte)(task.ReadByte(41) + 1))));
    }
}
