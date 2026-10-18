using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBio22ParticleFrame(NativeBattleModulePoolTask Task, NativeBattleModulePoolTask Primary, NativeBattleEffectMemory Memory, NativeCrtRandomState Random);
public sealed record NativeBattleBio22BillboardAppend(uint ParentAddress, ushort Variant, ImmutableArray<byte> ScaleBlock16)
{
    public const uint Descriptor = 0x270E540;
    public const uint Callback = 0x89BD20;
    public const int Size = 0x70;
}
public sealed record NativeBattleBio22ParticleInputs(Func<NativeBattleBio22ParticleFrame, short, NativeBattleBio22ParticleFrame> DrawBillboard, Func<NativeBattleBio22ParticleFrame, NativeBattleBio22ParticleFrame> DrawPlanar, Func<NativeBattleBio22ParticleFrame, NativeBattleBio22BillboardAppend, NativeBattleBio22ParticleFrame> AppendBillboard);
public sealed record NativeBattleBio22ParticleStep(NativeBattleBio22ParticleFrame Frame, uint Result)
{
    [JsonIgnore]
    public bool CompleteRequested => Result == 2;
}
public sealed record NativeBattleBio22FrameAdvance(NativeBattleModulePoolTask Task, uint Result);

/// <summary>Bio's three particle callbacks share frame timing while retaining their distinct motion and hierarchy rules.</summary>
public static class NativeBattleBio22Particles
{
    public static NativeBattleBio22FrameAdvance AdvanceFrame89BEE0(NativeBattleModulePoolTask task)
    {
        task = task.WriteWord(0x50, W(task.ReadWord(0x50) + 1));
        if (S(task, 0x50) > S(task, 0x52))
        {
            return new(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4)).WriteWord(0x50, task.ReadWord(0x52)), 1);
        }
        return new(task, 0);
    }

    public static NativeBattleBio22ParticleStep Advance89C930(NativeBattleBio22ParticleFrame frame, NativeBattleBio22ParticleInputs inputs)
    {
        Require(frame.Task, 0x89C930);
        NativeBattleModulePoolTask task = frame.Task;
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                NativeCrtRandomState random = frame.Random.Draw(out ushort value);
                uint scale = unchecked((uint)(unchecked(((value & 7) + 1) * S(frame.Primary, 0x86)) / 8));
                task = task.WriteDword(0x4C, 0x16156F8).WriteWord(0x52, 12).WriteDword(0x30, scale).WriteDword(0x34, scale).WriteDword(0x38, scale).WriteByte(0x29, 1);
                frame = frame with { Random = random };
                break;
            case 1:
                task = task.WriteWord(0x58, W(task.ReadWord(0x58) + task.ReadWord(0x60))).WriteWord(0x5C, W(task.ReadWord(0x5C) + task.ReadWord(0x64)));
                task = task.WriteWord(0x1C, W(task.ReadWord(0x1C) + task.ReadWord(0x58))).WriteWord(0x20, W(task.ReadWord(0x20) + task.ReadWord(0x5C)));
                ushort dy = W(task.ReadWord(0x62) - 6);
                task = task.WriteWord(0x5A, W(task.ReadWord(0x5A) + dy));
                task = task.WriteWord(0x1E, W(task.ReadWord(0x1E) + task.ReadWord(0x5A)));
                task = CompleteAnimation(task);
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException("Native Bio emitted particle phase leaves its callback table.");
        }
        if (S(task, 0x1E) > 0)
        {
            task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5));
        }
        return Finish(frame with { Task = task }, inputs, false);
    }

    public static NativeBattleBio22ParticleStep Advance89BD20(NativeBattleBio22ParticleFrame frame, NativeBattleBio22ParticleInputs inputs)
    {
        Require(frame.Task, 0x89BD20);
        NativeBattleModulePoolTask task = frame.Task;
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = task.WriteDword(0x4C, task.ReadWord(0x68) == 0 ? 0x1614F74u : 0x1615120u).WriteWord(0x52, 15).WriteWord(0x5A, 0xFFC0);
                NativeCrtRandomState random = frame.Random.Draw(out ushort angle);
                random = random.Draw(out ushort step);
                task = task.WriteWord(0x6A, (ushort)(angle & 0xFFF)).WriteWord(0x6C, (ushort)((step & 0x80) + 0x1C0)).WriteByte(0x29, 1);
                frame = frame with { Random = random };
                break;
            case 1:
                task = task.WriteWord(0x6A, W((task.ReadWord(0x6A) + task.ReadWord(0x6C)) & 0xFFF));
                short x = (short)(NativeBattleCameraMath.Sine(S(task, 0x6A)) / 128);
                short z = (short)(NativeBattleCameraMath.Cosine(S(task, 0x6A)) / 128);
                task = task.WriteWord(0x58, W(x)).WriteWord(0x5C, W(z));
                task = task.WriteWord(0x1E, W(task.ReadWord(0x1E) + task.ReadWord(0x5A))).WriteWord(0x1C, W(task.ReadWord(0x1C) + x)).WriteWord(0x20, W(task.ReadWord(0x20) + z));
                task = CompleteAnimation(task);
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException("Native Bio billboard particle phase leaves its callback table.");
        }
        return Finish(frame with { Task = task }, inputs, false);
    }

    public static NativeBattleBio22ParticleStep Advance89BF20(uint selfAddress, NativeBattleBio22ParticleFrame frame, NativeBattleBio22ParticleInputs inputs)
    {
        Require(frame.Task, 0x89BF20);
        NativeBattleModulePoolTask task = frame.Task;
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = task.WriteWord(0x40, 0x400).WriteDword(0x4C, task.ReadWord(0x68) == 0 ? 0x16152CCu : 0x1615440u).WriteWord(0x52, 12).WriteByte(0x29, 1);
                frame = frame with { Task = task };
                break;
            case 1:
                NativeBattleBio22FrameAdvance advanced = AdvanceFrame89BEE0(task);
                frame = frame with { Task = advanced.Task };
                if (advanced.Result == 1)
                {
                    NativeBattleBio22BillboardAppend request = new(selfAddress, frame.Task.ReadWord(0x68), frame.Task.Payload.AsSpan().Slice(0x30 - 12, 16).ToArray().ToImmutableArray());
                    frame = inputs.AppendBillboard(frame, request);
                    frame = frame with { Task = frame.Task.WriteByte(0x26, (byte)(frame.Task.ReadByte(0x26) | 1)).WriteByte(0x29, 2) };
                }
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException("Native Bio planar particle phase leaves its callback table.");
        }
        return Finish(frame, inputs, true);
    }

    private static NativeBattleModulePoolTask CompleteAnimation(NativeBattleModulePoolTask task)
    {
        NativeBattleBio22FrameAdvance advanced = AdvanceFrame89BEE0(task);
        return advanced.Result == 1 ? advanced.Task.WriteByte(0x26, (byte)(advanced.Task.ReadByte(0x26) | 1)).WriteByte(0x29, (byte)(advanced.Task.ReadByte(0x29) + 1)) : advanced.Task;
    }

    private static NativeBattleBio22ParticleStep Finish(NativeBattleBio22ParticleFrame frame, NativeBattleBio22ParticleInputs inputs, bool planar)
    {
        frame = planar ? inputs.DrawPlanar(frame) : inputs.DrawBillboard(frame, S(frame.Task, 0x30));
        byte flags = frame.Task.ReadByte(0x26);
        NativeBattleModulePoolTask task = frame.Task.WriteWord(0x24, W(frame.Task.ReadWord(0x24) + 1));
        return new(frame with { Task = task }, (flags & 1) != 0 && task.ReadByte(0x28) == 0 ? 2u : 0u);
    }

    private static void Require(NativeBattleModulePoolTask task, uint callback)
    {
        if (task.Callback != callback || task.Payload.IsDefault || task.Payload.Length != 0x70 - 12)
        {
            throw new InvalidDataException("Native Bio particle requires its actual hierarchy callback and task extent.");
        }
    }

    private static short S(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static ushort W(int value) => unchecked((ushort)value);
}
