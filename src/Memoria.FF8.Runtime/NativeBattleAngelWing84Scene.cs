namespace Memoria.FF8.Runtime;

public abstract record NativeBattleAngelWing84Operation;
public sealed record NativeBattleAngelWing84StaticTexture(uint Address) : NativeBattleAngelWing84Operation;
public sealed record NativeBattleAngelWing84CameraControl : NativeBattleAngelWing84Operation;
public sealed record NativeBattleAngelWing84ModelFlags(byte Actor, ushort Flags) : NativeBattleAngelWing84Operation;
public sealed record NativeBattleAngelWing84PhaseBirth(ushort Phase, uint Parent) : NativeBattleAngelWing84Operation;
public sealed record NativeBattleAngelWing84SceneInputs(NativeBattleAngelWing84Inputs Controller, Func<NativeBattleAngelWing84Frame, NativeBattleAngelWing84Operation, NativeBattleAngelWing84Frame> Execute);

/// <summary>78DCE0's scene phase handshake, native delays and temporary model visibility ownership.</summary>
public static class NativeBattleAngelWing84Scene
{
    public static NativeBattleAngelWing84Frame InitializeTarget(NativeBattleAngelWing84Frame frame, uint targetTask, NativeBattleAngelWing84Inputs inputs)
    {
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(targetTask);
        NativeBattleVector actor = (inputs.ReadModel(task.ReadByte(44)).Transform ?? throw new NotSupportedException("Native78DC50 requires its actual caster position.")).Position28;
        NativeBattleVector target = (inputs.ReadModel(task.ReadByte(45)).Transform ?? throw new NotSupportedException("Native78DC50 requires its actual target position.")).Position28;
        NativeBattleAngelWing84SceneMemory scene = NativeBattleAngelWing84SceneMemory.Initialize(actor, target, frame.State.Scene?.Camera);
        return frame with { State = frame.State with { Scene = scene } };
    }

    public static NativeBattleAngelWing84LeafStep Advance(NativeBattleAngelWing84Frame frame, uint address, NativeBattleAngelWing84SceneInputs inputs)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != 0x78DCE0)
        {
            throw new InvalidDataException("Native78DCE0 requires its actual72-byte scene task.");
        }
        NativeBattleAngelWing84SceneMemory scene = Read(frame);
        scene = scene.WriteWord(70, unchecked((ushort)(scene.Word(70) + 1)));
        if (scene.Word(64) != scene.Word(66))
        {
            scene = scene.WriteWord(64, scene.Word(66)).WriteWord(70, 0);
            frame = Store(frame, scene);
            if (scene.Word(64) is 1 or 2 or 3)
            {
                frame = inputs.Execute(frame, new NativeBattleAngelWing84PhaseBirth(scene.Word(64), address));
            }
            scene = Read(frame);
        }
        if (scene.Word(66) != scene.Word(68))
        {
            scene = scene.WriteWord(66, scene.Word(68));
        }
        frame = Store(frame, scene);
        hierarchy = new(frame.Effect.Pools);
        task = hierarchy.Read(address);
        switch (unchecked((sbyte)task.ReadByte(41)))
        {
            case 0:
                frame = inputs.Execute(frame, new NativeBattleAngelWing84StaticTexture(0x17243F8));
                frame = inputs.Execute(frame, new NativeBattleAngelWing84CameraControl());
                hierarchy = new(frame.Effect.Pools);
                NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native78DE00 requires its original action parameters.");
                NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(parameters, inputs.Controller.Packets, pointer, group, target);
                hierarchy = hierarchy.Append8DC540(5, 0x78DE40, 100, address, Select).Hierarchy;
                hierarchy = Next(hierarchy, address);
                break;
            case 1:
                Request(1);
                break;
            case 2:
                if (scene.Word(64) >= 1)
                {
                    SetOtherModelsHidden(true);
                    hierarchy = Next(new(frame.Effect.Pools), address);
                }
                break;
            case 3:
                Wait(45);
                break;
            case 4:
                Request(2);
                break;
            case 5:
                WaitForPhase(2);
                break;
            case 6:
                Wait(25);
                break;
            case 7:
                Request(3);
                break;
            case 8:
                WaitForPhase(3);
                break;
            case 9:
                Wait(65);
                break;
            case 10:
                Request(4);
                break;
            case 11:
                if (scene.Word(64) >= 4)
                {
                    SetOtherModelsHidden(false);
                    hierarchy = Next(new(frame.Effect.Pools), address);
                }
                break;
            case 12:
                Wait(60);
                break;
            case 13:
                if (scene.CanRequestPhase(5))
                {
                    frame = Store(frame, scene.WriteWord(68, 5).WriteWord(72, 1));
                    hierarchy = Next(new(frame.Effect.Pools), address);
                }
                break;
            case 14:
                hierarchy = hierarchy.Write(address, task.WriteByte(38, (byte)(task.ReadByte(38) | 1)));
                hierarchy = Next(hierarchy, address);
                break;
            case 15:
                break;
            default:
                throw new InvalidDataException("Native78DCE0's signed phase exceeds its sixteen callbacks.");
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

        void Request(short phase)
        {
            if (scene.CanRequestPhase(phase))
            {
                frame = Store(frame, scene.WriteWord(68, unchecked((ushort)phase)));
                hierarchy = Next(new(frame.Effect.Pools), address);
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

        void SetOtherModelsHidden(bool hidden)
        {
            int actor = task.ReadByte(44);
            for (byte index = 0; index < 7; index++)
            {
                NativeBattleGraphicsModel model = inputs.Controller.ReadModel(index);
                if (index == actor || (model.Flags0 & 2) == 0)
                {
                    continue;
                }
                ushort flags;
                if (hidden)
                {
                    frame = Store(frame, Read(frame).WriteDword(32 + index * 4, model.Flags0));
                    flags = (ushort)(model.Flags0 | 4);
                }
                else
                {
                    flags = (ushort)(model.Flags0 ^ ((model.Flags0 ^ Read(frame).Word(32 + index * 4)) & 4));
                }
                frame = inputs.Execute(frame, new NativeBattleAngelWing84ModelFlags(index, flags));
            }
        }
    }

    public static NativeBattleAngelWing84SceneMemory Read(NativeBattleAngelWing84Frame frame)
    {
        NativeBattleAngelWing84SceneMemory scene = frame.State.Scene ?? throw new NotSupportedException("Native Angel Wing requires its initialized scene workspace.");
        scene.Validate();
        return scene;
    }

    private static NativeBattleAngelWing84Frame Store(NativeBattleAngelWing84Frame frame, NativeBattleAngelWing84SceneMemory scene) => frame with { State = frame.State with { Scene = scene } };

    private static NativeBattleEffectHierarchy Next(NativeBattleEffectHierarchy hierarchy, uint address)
    {
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        return hierarchy.Write(address, task.WriteByte(41, unchecked((byte)(task.ReadByte(41) + 1))));
    }
}
