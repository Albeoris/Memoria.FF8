using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public abstract record NativeBattleChoco97ModelOperation;
public sealed record NativeBattleChoco97ModelAppendAuxiliary(uint Callback, int TaskBytes, uint PoolAddress) : NativeBattleChoco97ModelOperation;
public sealed record NativeBattleChoco97ModelSound(uint DescriptorAddress, uint Flags, byte Pan) : NativeBattleChoco97ModelOperation;
public sealed record NativeBattleChoco97ModelInputs(ImmutableArray<byte> ActorRaw8, Func<int, bool> AtLeast, Func<int, bool> Request, Func<NativeBattleEffectOwnedModelStep, NativeBattleChoco97ModelOperation, NativeBattleEffectOwnedModelStep> Execute, Func<NativeBattleEffectOwnedModelStep, NativeBattleEffectOwnedModelDraw> Draw);
public sealed record NativeBattleChoco97ModelAdvance(NativeBattleModulePoolTask Task, NativeBattleEffectOwnedModelState State, NativeBattleEffectOwnedModelDraw? Drawing, ImmutableArray<NativeBattleTextureCopy> TextureCopies, bool ReadyToComplete);

public static class NativeBattleChoco97Model
{
    public static NativeBattleEffectOwnedModelStep Initialize(NativeBattleModulePoolTask task, uint taskAddress, NativeBattleChoco97ModelResources resources)
    {
        return NativeBattleEffectOwnedModel.Initialize8DD0F0(task, taskAddress, resources.Model, 1, resources.DefaultColor44);
    }

    public static NativeBattleChoco97ModelAdvance Advance7306A0(NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState state, NativeBattleChoco97ModelResources resources, NativeBattleChoco97ModelInputs inputs)
    {
        NativeBattleEffectOwnedModel.Validate(task, state, resources.Model);
        NativeBattleEffectOwnedModelStep step = new(task, state, false);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                if (inputs.ActorRaw8.IsDefault || inputs.ActorRaw8.Length != 8)
                {
                    throw new InvalidDataException("Native Choco model initialization requires its actual raw actor origin.");
                }
                step = step with { Task = step.Task.WriteDword(0x4C, BinaryPrimitives.ReadUInt32LittleEndian(inputs.ActorRaw8.AsSpan())).WriteDword(0x50, BinaryPrimitives.ReadUInt32LittleEndian(inputs.ActorRaw8.AsSpan().Slice(4))).WriteDword(0x114, 0x1800).WriteDword(0x118, 0x1800).WriteDword(0x11C, 0x1800).WriteDword(0x60, checked(state.TaskAddress + 0x114)) };
                Select(0);
                step = step with { Task = step.Task.WriteDword(0x110, resources.TextureProgram.Address).WriteWord(0x12E, 0).WriteWord(0x12C, 0).WriteWord(0x130, 0).WriteWord(0x132, 0) };
                Next();
                break;
            case 1:
                Once();
                if (step.Completed)
                {
                    Select(1);
                    Next();
                }
                break;
            case 2:
                Loop();
                if (Counter() >= 0)
                {
                    Select(2);
                    step = inputs.Execute(step, new NativeBattleChoco97ModelAppendAuxiliary(0x730C10, 0x70, 0x2575458));
                    Next();
                }
                break;
            case 3:
                Once();
                if (step.Completed && inputs.AtLeast(1))
                {
                    Select(3);
                    step = inputs.Execute(step, new NativeBattleChoco97ModelSound(0x152B5C0, 0, 128));
                    Next();
                }
                break;
            case 4:
                Loop();
                if (inputs.Request(2))
                {
                    Next();
                }
                break;
            case 5:
                Loop();
                if (Counter() >= 50)
                {
                    Next();
                }
                break;
            case 6:
                Loop();
                if (inputs.Request(4))
                {
                    Select(4);
                    step = inputs.Execute(step, new NativeBattleChoco97ModelSound(0x152B5C4, 0, 128));
                    Next();
                }
                break;
            case 7:
                Once();
                if (step.Completed)
                {
                    Select(5);
                    Next();
                }
                break;
            case 8:
                Loop();
                if (inputs.AtLeast(6))
                {
                    step = step with { Task = step.Task.WriteByte(0x26, (byte)(step.Task.ReadByte(0x26) | 5)) };
                    Next();
                }
                break;
            case 9:
                break;
            default:
                throw new NotSupportedException("Native Choco model phase leaves its ten original callbacks.");
        }
        NativeBattleEffectOwnedModelDraw? drawing = null;
        ImmutableArray<NativeBattleTextureCopy> copies = [];
        if ((step.Task.ReadByte(0x26) & 4) == 0)
        {
            drawing = inputs.Draw(step);
            NativeBattleMoogle96TextureStep texture = NativeBattleMoogle96TextureMotion.Advance739890(drawing.Task, resources.TextureProgram, resources.TextureTable);
            step = step with { Task = texture.Task };
            copies = texture.Copies;
        }
        step = step with { Task = step.Task.WriteWord(0x24, unchecked((ushort)(step.Task.ReadWord(0x24) + 1))) };
        return new(step.Task, step.State, drawing, copies, (step.Task.ReadByte(0x26) & 1) != 0 && step.Task.ReadByte(0x28) == 0);

        short Counter() => unchecked((short)step.Task.ReadWord(0x136));
        void Next() => step = step with { Task = step.Task.WriteByte(0x29, unchecked((byte)(step.Task.ReadByte(0x29) + 1))) };
        void Select(short pose) => step = NativeBattleEffectOwnedModel.Select8DD190(step.Task, step.State, resources.Model, pose);
        void Loop() => step = NativeBattleEffectOwnedModel.AdvanceLoop8DD1C0(step.Task, step.State, resources.Model);
        void Once() => step = NativeBattleEffectOwnedModel.AdvanceOnce8DD220(step.Task, step.State, resources.Model);
    }
}
