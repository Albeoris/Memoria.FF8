using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public abstract record NativeBattleRinoa86Operation;
public sealed record NativeBattleRinoa86StaticTexture(uint Address) : NativeBattleRinoa86Operation;
public sealed record NativeBattleRinoa86SceneInputs(NativeBattleRinoa86Inputs Controller, NativeBattleRinoa86Resources Resources, Func<NativeBattleRinoa86Frame, NativeBattleRinoa86Operation, NativeBattleRinoa86Frame> Execute);

/// <summary>77FAE0/77FBF0's original scene and its two-stage phase requests.</summary>
public static class NativeBattleRinoa86Scene
{
    public static NativeBattleRinoa86Frame InitializeTarget(NativeBattleRinoa86Frame frame, uint address, NativeBattleRinoa86Inputs inputs, NativeBattleRinoa86Resources resources)
    {
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        NativeBattleRinoa86SceneMemory scene = NativeBattleRinoa86SceneMemory.Initialize(task.ReadByte(44), Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray());
        NativeBattleRinoa86IoState io = NativeBattleRinoa86Io.Initialize77F680(frame.State.Io?.Script ?? frame.State.RetainedScript ?? resources.Io.Script.Data, checked(0x20DFAB8u + (uint)frame.State.AuxiliaryCursor));
        return frame with { State = frame.State with { Scene = scene, Io = io, AuxiliaryCursor = checked(frame.State.AuxiliaryCursor + 40960) } };
    }

    public static NativeBattleRinoa86LeafStep Advance(NativeBattleRinoa86Frame frame, uint address, NativeBattleRinoa86SceneInputs inputs)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != 0x77FBF0)
        {
            throw new InvalidDataException("Native77FBF0 requires its actual72-byte scene task.");
        }
        NativeBattleRinoa86SceneMemory scene = Read(frame);
        scene = scene.WriteWord(70, unchecked((ushort)(scene.Word(70) + 1)));
        if (scene.Word(64) != scene.Word(66))
        {
            scene = scene.WriteWord(64, scene.Word(66)).WriteWord(70, 0);
            if (scene.Word(64) == 1)
            {
                NativeBattleEffectHierarchyAppend body = hierarchy.Append8DC540(7, 0x77FF90, 352, address, Select);
                NativeBattleEffectOwnedModelStep initialized = NativeBattleEffectOwnedModel.Initialize8DD0F0(body.Hierarchy.Read(body.Address), body.Address, inputs.Resources.Model, 3, inputs.Resources.DefaultColor44);
                hierarchy = body.Hierarchy.Write(body.Address, initialized.Task);
                frame = frame with { State = frame.State with { BodyModel = initialized.State, BodyBones = frame.State.BodyBones ?? NativeBattleBodyBoneMatrices.ReadLoadedSkeleton(inputs.Resources.Model.PoseSource).Matrices } };
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
                frame = inputs.Execute(frame, new NativeBattleRinoa86StaticTexture(0x170CDCC));
                hierarchy = new(frame.Effect.Pools);
                Request(1);
                break;
            case 1:
                WaitForPhase(1);
                break;
            case 2:
                NativeBattleRinoa86IoRelease release = NativeBattleRinoa86Io.ReleaseWait77FDB0(frame.State.Io ?? throw new NotSupportedException("Native77FDB0 requires its actual initialized IO context."));
                frame = frame with { State = frame.State with { Io = release.State } };
                if (release.Released)
                {
                    hierarchy = Next(hierarchy, address);
                }
                break;
            case 3:
                Wait(15);
                break;
            case 4:
                Request(2);
                break;
            case 5:
                WaitForPhase(2);
                break;
            case 6:
                if ((frame.State.Io ?? throw new NotSupportedException("Native77FE50 requires its actual IO handshake.")).WaitReleased == 0)
                {
                    hierarchy = Next(hierarchy, address);
                }
                break;
            case 7:
                Request(3);
                break;
            case 8:
                WaitForPhase(3);
                break;
            case 9:
                if (scene.SignedWord(70) >= 50)
                {
                    frame = Store(frame, scene.WriteWord(72, 1));
                    hierarchy = Next(hierarchy, address);
                }
                break;
            case 10:
                Wait(60);
                break;
            case 11:
                Request(4);
                break;
            case 12:
                if (scene.Word(64) >= 4)
                {
                    hierarchy = hierarchy.Write(address, hierarchy.Read(address).WriteByte(38, (byte)(task.ReadByte(38) | 1)));
                    hierarchy = Next(hierarchy, address);
                }
                break;
            case 13:
                break;
            default:
                throw new InvalidDataException("Native77FBF0 scene phase exceeds its fourteen callbacks.");
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
        void Wait(short ticks)
        {
            if (scene.SignedWord(70) >= ticks)
            {
                hierarchy = Next(hierarchy, address);
            }
        }
    }

    public static NativeBattleRinoa86SceneMemory Read(NativeBattleRinoa86Frame frame)
    {
        NativeBattleRinoa86SceneMemory scene = frame.State.Scene ?? throw new NotSupportedException("Native Rinoa86 requires its initialized scene workspace.");
        scene.Validate();
        return scene;
    }
    private static NativeBattleRinoa86Frame Store(NativeBattleRinoa86Frame frame, NativeBattleRinoa86SceneMemory scene) => frame with { State = frame.State with { Scene = scene } };
    private static NativeBattleEffectHierarchy Next(NativeBattleEffectHierarchy hierarchy, uint address)
    {
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        return hierarchy.Write(address, task.WriteByte(41, unchecked((byte)(task.ReadByte(41) + 1))));
    }
}
