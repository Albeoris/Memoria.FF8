using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public abstract record NativeBattleAngelWing84BodyOperation;
public sealed record NativeBattleAngelWing84BindCaster(byte Actor) : NativeBattleAngelWing84BodyOperation;
public sealed record NativeBattleAngelWing84InitializeBodyShader(uint Address) : NativeBattleAngelWing84BodyOperation;
public sealed record NativeBattleAngelWing84BodySound(uint Sound) : NativeBattleAngelWing84BodyOperation;
public sealed record NativeBattleAngelWing84DrawBody(uint Address) : NativeBattleAngelWing84BodyOperation;
public sealed record NativeBattleAngelWing84BodyInputs(NativeBattleAngelWing84BodyResources Resources, NativeBattleAngelWing84Inputs Controller, Func<NativeBattleAngelWing84Frame, NativeBattleAngelWing84BodyOperation, NativeBattleAngelWing84Frame> Execute);

public static partial class NativeBattleAngelWing84Body
{
    public static NativeBattleAngelWing84LeafStep Advance(NativeBattleAngelWing84Frame frame, uint address, NativeBattleAngelWing84BodyInputs inputs)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        NativeBattleEffectOwnedModelState model = frame.State.BodyModel ?? throw new NotSupportedException("Native Angel Wing body requires its actual initialized model owner.");
        if (task.Callback != 0x7961E0)
        {
            throw new InvalidDataException("Native7961E0 requires its original body task.");
        }
        switch (unchecked((sbyte)task.ReadByte(41)))
        {
            case 0:
                NativeBattleModelTransform actor = inputs.Controller.ReadModel(task.ReadByte(44)).Transform ?? throw new NotSupportedException("Native796BF0 requires its actual caster origin and vertical WORD34.");
                task = task.WriteDword(96, checked(address + 276)).WriteWord(76, unchecked((ushort)actor.Position28.X)).WriteWord(78, unchecked((ushort)actor.Position28.Y)).WriteWord(80, unchecked((ushort)actor.Position28.Z)).WriteWord(82, unchecked((ushort)actor.Vertical34)).WriteDword(276, 4096).WriteDword(280, 4096).WriteDword(284, 4096);
                Pose(0, 0);
                Store();
                frame = frame with { State = frame.State with { BodyShaderAngle = 0 } };
                frame = inputs.Execute(frame, new NativeBattleAngelWing84InitializeBodyShader(address));
                Read();
                task = task.WriteByte(38, (byte)(task.ReadByte(38) | 8));
                Store();
                frame = inputs.Execute(frame, new NativeBattleAngelWing84BindCaster(task.ReadByte(44)));
                Read();
                Next();
                break;
            case 1:
                if (Signed(310) >= 21)
                {
                    Store();
                    frame = inputs.Execute(frame, new NativeBattleAngelWing84BodySound(inputs.Resources.Sound15720AC));
                    Read();
                    if (Signed(310) >= 21)
                    {
                        ushort angle = unchecked((ushort)((frame.State.BodyShaderAngle ?? throw new NotSupportedException("Native Angel Wing shader angle has no initialized owner.")) + 32));
                        frame = frame with { State = frame.State with { BodyShaderAngle = angle >= 1280 ? (ushort)1280 : angle } };
                    }
                }
                if (NativeBattleAngelWing84Scene.Read(frame).Word(64) >= 2)
                {
                    task = task.WriteByte(38, (byte)(task.ReadByte(38) & ~8));
                    Next();
                }
                Pose(1, 0);
                break;
            case 2:
                if (Pose(1, 0))
                {
                    Pose(0, 1);
                    Next();
                }
                break;
            case 3:
                if (NativeBattleAngelWing84Scene.Read(frame).Word(64) >= 4)
                {
                    Next();
                }
                Pose(1, 0);
                break;
            case 4:
                Pose(1, 0);
                short fade = unchecked((short)(task.ReadWord(314) + 256));
                task = task.WriteWord(314, unchecked((ushort)fade));
                if (fade >= 4096)
                {
                    task = task.WriteByte(38, (byte)(task.ReadByte(38) | 5)).WriteWord(314, 4096);
                    Next();
                }
                task = task.WriteWord(316, task.ReadWord(314));
                ApplyFade();
                break;
            case 5:
                break;
            default:
                throw new InvalidDataException("Native Angel Wing body phase exceeds its six original states.");
        }
        Store();
        if ((task.ReadByte(38) & 4) == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleAngelWing84DrawBody(address));
            Read();
        }
        if (task.ReadWord(36) is 50 or 100)
        {
            Store();
            frame = NativeBattleAngelWing84SceneBirth.AppendProgram(frame, address, 0x796290, 0x1720570, 344, 1, 0, inputs.Controller);
            Read();
        }
        task = task.WriteWord(36, unchecked((ushort)(task.ReadWord(36) + 1)));
        Store();
        hierarchy = new(frame.Effect.Pools);
        bool complete = (task.ReadByte(38) & 1) != 0 && task.ReadByte(40) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        return new(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, complete ? 2 : 0);

        short Signed(int offset) => unchecked((short)task.ReadWord(offset));
        void Next() => task = task.WriteByte(41, unchecked((byte)(task.ReadByte(41) + 1)));
        void Store()
        {
            NativeBattleEffectHierarchy current = new(frame.Effect.Pools);
            frame = frame with { Effect = frame.Effect with { Pools = current.Write(address, task).Pools }, State = frame.State with { BodyModel = model } };
        }
        void Read()
        {
            task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
            model = frame.State.BodyModel ?? throw new NotSupportedException("Native Angel Wing body operation lost its model owner.");
        }
        bool Pose(int mode, short animation)
        {
            NativeBattleEffectOwnedModelStep step = mode == 0 ? NativeBattleEffectOwnedModel.Select8DD190(task, model, inputs.Resources.Model, animation) : NativeBattleEffectOwnedModel.AdvanceOnce8DD220(task, model, inputs.Resources.Model);
            task = step.Task;
            model = step.State;
            return step.Completed;
        }
        void ApplyFade()
        {
            short factor = Signed(314);
            uint source = inputs.Resources.Transition40 ?? throw new NotSupportedException("Native796E50 requires its actual transition-color DWORD.");
            if (factor == 0)
            {
                task = task.WriteDword(88, source).WriteByte(91, 0).WriteByte(55, 0);
            }
            else
            {
                task = task.WriteByte(91, 2).WriteByte(55, (byte)(unchecked((byte)(-1 - 255 * factor / 4096)) >> 2));
                for (int index = 0; index < 3; index++)
                {
                    byte initial = (byte)(source >> (index * 8));
                    task = task.WriteByte(88 + index, unchecked((byte)(initial - factor * initial / 4096)));
                }
            }
            for (int index = 0; index < 3; index++)
            {
                byte initial = (byte)(inputs.Resources.DefaultColor44 >> (index * 8));
                task = task.WriteByte(92 + index, unchecked((byte)(initial - Signed(316) * initial / 4096)));
            }
        }
    }
}
