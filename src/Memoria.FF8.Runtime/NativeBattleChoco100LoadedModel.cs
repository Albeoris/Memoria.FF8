using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco100LoadedModelInputs(ImmutableArray<byte> TargetRaw8, Func<int, bool> AtLeast, Func<int, bool> Request, Func<NativeBattleEffectOwnedModelStep, NativeBattleEffectOwnedModelDraw> Draw);

public static class NativeBattleChoco100LoadedModel
{
    public const uint Callback = 0x7175F0;

    public static NativeBattleEffectOwnedModelStep Initialize(NativeBattleModulePoolTask task, uint taskAddress, NativeBattleChoco100LoadedModelResources resources)
    {
        resources.Validate();
        RequireTask(task);
        return NativeBattleEffectOwnedModel.Initialize8DD0F0(task, taskAddress, resources.Model, 0x3C, resources.DefaultColor44);
    }

    public static NativeBattleChoco97ModelAdvance Advance7175F0(NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState state, NativeBattleChoco100LoadedModelResources resources, NativeBattleChoco100LoadedModelInputs inputs)
    {
        resources.Validate();
        RequireTask(task);
        NativeBattleEffectOwnedModel.Validate(task, state, resources.Model);
        NativeBattleEffectOwnedModelStep step = new(task, state, false);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                if (inputs.TargetRaw8.IsDefault || inputs.TargetRaw8.Length != 8)
                {
                    throw new InvalidDataException("Native Choco100 loaded model requires its actual raw target position.");
                }
                Store(step.Task.WriteDword(0x4C, BinaryPrimitives.ReadUInt32LittleEndian(inputs.TargetRaw8.AsSpan())).WriteDword(0x50, BinaryPrimitives.ReadUInt32LittleEndian(inputs.TargetRaw8.AsSpan().Slice(4))).WriteWord(0x4E, 0xB000).WriteDword(0x60, checked(state.TaskAddress + 0x114)).WriteDword(0x114, 0x2800).WriteDword(0x118, 0x2800).WriteDword(0x11C, 0x2800).WriteWord(0x3E, 0x800));
                Select(0);
                Store(step.Task.WriteByte(0x26, (byte)(step.Task.ReadByte(0x26) | 4)).WriteWord(0x134, 8));
                Next();
                break;
            case 1:
                Countdown();
                if (Word(0x134) <= 0)
                {
                    Store(Reveal717750(step.Task.WriteByte(0x26, (byte)(step.Task.ReadByte(0x26) & ~4)), resources.DefaultColor44));
                    Next();
                    Store(step.Task.WriteWord(0x134, 30));
                }
                break;
            case 2:
                Countdown();
                if (Word(0x134) <= 0)
                {
                    Next();
                }
                Store(Ease717800(step.Task, resources.DefaultColor44));
                break;
            case 3:
                if (inputs.Request(8))
                {
                    Store(step.Task.WriteWord(0x134, 0).WriteWord(0x126, 0x200));
                    Next();
                }
                Store(Ease717800(step.Task, resources.DefaultColor44));
                break;
            case 4:
                Store(step.Task.WriteWord(0x134, unchecked((ushort)(step.Task.ReadWord(0x134) + 1))));
                if (Word(0x134) == 4)
                {
                    Store(Normal717910(step.Task, resources.DefaultColor44));
                }
                Store(step.Task.WriteWord(0x126, unchecked((ushort)(step.Task.ReadWord(0x126) + 0x60))));
                Store(step.Task.WriteWord(0x4E, unchecked((ushort)(step.Task.ReadWord(0x4E) + step.Task.ReadWord(0x126)))));
                if (inputs.AtLeast(9))
                {
                    Store(step.Task.WriteWord(0x134, 0).WriteWord(0x4E, unchecked((ushort)resources.YProgram.Values[0])));
                    Next();
                }
                Store(Ease717800(step.Task, resources.DefaultColor44));
                break;
            case 5:
                Store(step.Task.WriteWord(0x134, unchecked((ushort)(step.Task.ReadWord(0x134) + 1))));
                int index = Word(0x134);
                if ((uint)index >= resources.YProgram.Values.Length)
                {
                    throw new InvalidDataException("Native Choco100 loaded model Y index leaves its actual signed WORD program.");
                }
                short y = resources.YProgram.Values[index];
                Store(step.Task.WriteWord(0x4E, unchecked((ushort)y)));
                if (y == 0)
                {
                    Select(1);
                    Store(step.Task.WriteDword(0x110, resources.TextureProgram.Address).WriteWord(0x12E, 0).WriteWord(0x12C, 0).WriteWord(0x130, 0).WriteWord(0x132, 0));
                    Next();
                }
                break;
            case 6:
                step = NativeBattleEffectOwnedModel.AdvanceOnce8DD220(step.Task, step.State, resources.Model);
                if (step.Completed)
                {
                    Select(2);
                    Next();
                    Store(step.Task.WriteWord(0x134, 10));
                }
                break;
            case 7:
                step = NativeBattleEffectOwnedModel.AdvanceLoop8DD1C0(step.Task, step.State, resources.Model);
                Countdown();
                if (Word(0x134) <= 0)
                {
                    Next();
                }
                break;
            case 8:
                Store(step.Task.WriteWord(0x13A, unchecked((ushort)(step.Task.ReadWord(0x13A) + 0x100))));
                if (Word(0x13A) >= 0x1000)
                {
                    Store(step.Task.WriteWord(0x13A, 0x1000).WriteByte(0x26, (byte)(step.Task.ReadByte(0x26) | 5)));
                    Next();
                }
                Store(step.Task.WriteWord(0x13C, step.Task.ReadWord(0x13A)));
                Store(FadeBodyType1_717A70(step.Task, resources.DefaultFadeColor58));
                Store(Recolor717770(step.Task, resources.DefaultColor44));
                break;
            case 9:
                break;
            default:
                throw new NotSupportedException("Native Choco100 loaded model leaves its ten original phases.");
        }
        NativeBattleEffectOwnedModelDraw? drawing = null;
        ImmutableArray<NativeBattleTextureCopy> copies = [];
        if ((step.Task.ReadByte(0x26) & 4) == 0)
        {
            drawing = inputs.Draw(step);
            NativeBattleMoogle96TextureStep texture = NativeBattleMoogle96TextureMotion.Advance739890(drawing.Task, resources.TextureProgram, resources.TextureTable);
            Store(texture.Task);
            copies = texture.Copies;
        }
        Store(step.Task.WriteWord(0x24, unchecked((ushort)(step.Task.ReadWord(0x24) + 1))));
        return new(step.Task, step.State, drawing, copies, (step.Task.ReadByte(0x26) & 1) != 0 && step.Task.ReadByte(0x28) == 0);

        short Word(int offset) => unchecked((short)step.Task.ReadWord(offset));
        void Store(NativeBattleModulePoolTask current) => step = step with { Task = current };
        void Next() => Store(step.Task.WriteByte(0x29, unchecked((byte)(step.Task.ReadByte(0x29) + 1))));
        void Countdown() => Store(step.Task.WriteWord(0x134, unchecked((ushort)(step.Task.ReadWord(0x134) - 1))));
        void Select(short pose) => step = NativeBattleEffectOwnedModel.Select8DD190(step.Task, step.State, resources.Model, pose);
    }

    public static NativeBattleModulePoolTask Reveal717750(NativeBattleModulePoolTask task, uint actualDefaultColor44) => Recolor717770(task.WriteWord(0x13E, 0x2400).WriteWord(0x13C, 0x1000), actualDefaultColor44);
    public static NativeBattleModulePoolTask Normal717910(NativeBattleModulePoolTask task, uint actualDefaultColor44) => Recolor717770(task.WriteWord(0x13E, 0x1000).WriteWord(0x13C, 0), actualDefaultColor44);

    public static NativeBattleModulePoolTask Ease717800(NativeBattleModulePoolTask task, uint actualDefaultColor44)
    {
        if (Signed(task, 0x13E) != 0x1000)
        {
            task = task.WriteWord(0x13E, unchecked((ushort)(task.ReadWord(0x13E) - 0x78)));
            if (Signed(task, 0x13E) <= 0x1400)
            {
                task = task.WriteWord(0x13E, 0x1400);
            }
        }
        if (Signed(task, 0x13C) != 0)
        {
            task = task.WriteWord(0x13C, unchecked((ushort)(task.ReadWord(0x13C) - 0x69)));
            if (Signed(task, 0x13C) <= 0x200)
            {
                task = task.WriteWord(0x13C, 0x200);
            }
        }
        return Recolor717770(task, actualDefaultColor44);
    }

    public static NativeBattleModulePoolTask Recolor717770(NativeBattleModulePoolTask task, uint actualDefaultColor44)
    {
        short ratio = Signed(task, 0x13C);
        for (int channel = 0; channel < 3; channel++)
        {
            int value = (byte)(actualDefaultColor44 >> (channel * 8));
            task = task.WriteByte(0x5C + channel, unchecked((byte)(value - unchecked(value * ratio) / 4096)));
        }
        return task;
    }

    public static NativeBattleModulePoolTask FadeBodyType1_717A70(NativeBattleModulePoolTask task, uint actualDefaultFadeColor58)
    {
        short ratio = Signed(task, 0x13A);
        if (ratio <= 0 || ratio > 4096)
        {
            throw new NotSupportedException("Native Choco100 loaded model admits only its reached positive type1 body fade.");
        }
        task = task.WriteByte(0x5B, 2);
        for (int channel = 0; channel < 3; channel++)
        {
            int half = (byte)(actualDefaultFadeColor58 >> (channel * 8)) >> 1;
            task = task.WriteByte(0x58 + channel, unchecked((byte)(half - unchecked(half * ratio) / 4096)));
        }
        return task;
    }

    private static void RequireTask(NativeBattleModulePoolTask task)
    {
        if (task.Callback != Callback || task.Payload.IsDefault || task.PayloadLength != 0x140 - 12)
        {
            throw new InvalidDataException("Native Choco100 loaded model requires its complete actual model task.");
        }
    }
    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
