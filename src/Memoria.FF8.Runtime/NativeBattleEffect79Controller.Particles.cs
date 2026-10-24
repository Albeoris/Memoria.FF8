using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect79Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect79Controller
{
    public static NativeBattleEffect79Step AdvanceParticle7BCD50(NativeBattleEffect79Frame frame, uint address, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs)
    {
        switch (Phase(frame, address))
        {
            case 0:
                frame = Write(frame, address, Read(frame, address).WriteWord(0x1C, 0));
                frame = inputs.Execute(frame, new NativeBattleEffect79StageTransition(0, true));
                frame = NextPhase(frame, address);
                break;
            case 1:
                short value = unchecked((short)(Signed(Read(frame, address), 0x1C) + 0xA0));
                if (value >= 0x800)
                {
                    value = 0x800;
                    frame = NextPhase(MarkComplete(frame, address), address);
                }
                frame = Write(frame, address, Read(frame, address).WriteWord(0x1C, unchecked((ushort)value)));
                frame = inputs.Execute(frame, new NativeBattleEffect79StageTransition(value, false));
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Effect79 stage rise leaves its three native phases.");
        }
        return Finish(frame, address);
    }

    public static NativeBattleEffect79Step AdvanceParticle7BCE40(NativeBattleEffect79Frame frame, uint address, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs)
    {
        switch (Phase(frame, address))
        {
            case 0:
                if (Signed(Read(frame, address), 0x24) > 0x5A)
                {
                    frame = Write(frame, address, Read(frame, address).WriteWord(0x1C, 0x800));
                    frame = NextPhase(frame, address);
                }
                break;
            case 1:
                short value = unchecked((short)(Signed(Read(frame, address), 0x1C) - 0x100));
                if (value <= 0)
                {
                    value = 0;
                    frame = NextPhase(MarkComplete(frame, address), address);
                }
                frame = Write(frame, address, Read(frame, address).WriteWord(0x1C, unchecked((ushort)value)));
                frame = inputs.Execute(frame, new NativeBattleEffect79StageTransition(value, false));
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Effect79 stage fall leaves its three native phases.");
        }
        return Finish(frame, address);
    }

    public static NativeBattleEffect79Step AdvanceParticle7BBC20(NativeBattleEffect79Frame frame, uint address, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs) => AdvanceParticleProducer(frame, address, 0x32, false, inputs);

    public static NativeBattleEffect79Step AdvanceParticle7BCB40(NativeBattleEffect79Frame frame, uint address, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs) => AdvanceParticleProducer(frame, address, 0x26, true, inputs);

    private static NativeBattleEffect79Step AdvanceParticleProducer(NativeBattleEffect79Frame frame, uint address, ushort duration, bool stationary, NativeBattleEffect79Inputs inputs)
    {
        switch (Phase(frame, address))
        {
            case 0:
                frame = NextPhase(Write(frame, address, Read(frame, address).WriteWord(0xB0, duration)), address);
                break;
            case 1:
                frame = stationary ? Birth7BCBF0(frame, address, inputs) : Birth7BBCD0(frame, address, inputs);
                frame = DecrementParticleProducer(frame, address);
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Effect79 particle producer leaves its three native phases.");
        }
        return Finish(frame, address);
    }

    public static NativeBattleEffect79Step AdvanceParticle7BC610(NativeBattleEffect79Frame frame, uint address, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs)
    {
        switch (Phase(frame, address))
        {
            case 0:
                if (Signed(Read(frame, address), 0x24) >= 0x5A)
                {
                    frame = inputs.Execute(frame, new NativeBattleEffect79Sound501330(0x1580B28, 0, 128));
                    frame = NextPhase(Write(frame, address, Read(frame, address).WriteWord(0xB0, 8)), address);
                }
                break;
            case 1:
                for (int index = 0; index < 2; index++)
                {
                    ushort angle = (ushort)((Read(frame, address).ReadWord(0xB2) - 0x40) & 0xFFF);
                    frame = Write(frame, address, Read(frame, address).WriteWord(0xB2, angle));
                    frame = Birth7BC720(frame, address, true, unchecked((short)angle), inputs);
                }
                short remaining = unchecked((short)(Signed(Read(frame, address), 0xB0) - 1));
                frame = Write(frame, address, Read(frame, address).WriteWord(0xB0, unchecked((ushort)remaining)));
                if (remaining <= 0)
                {
                    frame = NextPhase(Write(frame, address, Read(frame, address).WriteWord(0xB0, 0x34)), address);
                }
                break;
            case 2:
                frame = Birth7BC720(frame, address, false, 0, inputs);
                frame = DecrementParticleProducer(frame, address);
                break;
            case 3:
                break;
            default:
                throw new InvalidDataException("Effect79 directional producer leaves its four native phases.");
        }
        return Finish(frame, address);
    }

    public static NativeBattleEffect79Step AdvanceParticle7BBDF0(NativeBattleEffect79Frame frame, uint address, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs)
    {
        frame = AdvanceParticleSpritePhase(frame, address, 0x1580E54, -128, 15);
        for (int index = 0; index < 2; index++)
        {
            NativeBattleModulePoolTask task = Read(frame, address);
            for (int component = 0; component < 3; component++)
            {
                int position = 0x6C + index * 8 + component * 2;
                int velocity = 0x8C + index * 8 + component * 2;
                task = task.WriteWord(position, unchecked((ushort)(Signed(task, position) + Signed(task, velocity) / 16)));
            }
            frame = Write(frame, address, task);
        }
        for (int index = 0; index < 2; index++)
        {
            frame = CopyParticlePosition(frame, address, 0x6C + index * 8);
            frame = NativeBattleEffect79ParticleDrawing.Draw7BBEB0(frame, address, resources, inputs);
        }
        return Finish(frame, address);
    }

    public static NativeBattleEffect79Step AdvanceParticle7BC840(NativeBattleEffect79Frame frame, uint address, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs)
    {
        frame = AdvanceParticleSpritePhase(frame, address, 0x1580B30, -64, 21);
        NativeBattleModulePoolTask task = Read(frame, address);
        for (int index = 0; index < 2; index++)
        {
            int velocity = 0x8C + index * 8;
            task = task.WriteWord(velocity + 2, unchecked((ushort)(Signed(task, velocity + 2) + 0x100)));
            for (int component = 0; component < 3; component++)
            {
                int field = velocity + component * 2;
                short damped = unchecked((short)(Signed(task, field) - Signed(task, field) / 8));
                task = task.WriteWord(field, unchecked((ushort)damped));
                int position = 0x6C + index * 8 + component * 2;
                task = task.WriteWord(position, unchecked((ushort)(Signed(task, position) + damped / 16)));
            }
        }
        frame = Write(frame, address, task);
        for (int index = 0; index < 2; index++)
        {
            frame = CopyParticlePosition(frame, address, 0x6C + index * 8);
            frame = NativeBattleEffect79ParticleDrawing.Draw7BC950(frame, address, resources, inputs, 8192);
        }
        return Finish(frame, address);
    }

    public static NativeBattleEffect79Step AdvanceParticle7BCC80(NativeBattleEffect79Frame frame, uint address, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs)
    {
        frame = AdvanceParticleSpritePhase(frame, address, 0x1580D78, -128, 7);
        frame = CopyParticlePosition(frame, address, 0x6C);
        frame = NativeBattleEffect79ParticleDrawing.Draw7BBEB0(frame, address, resources, inputs);
        return Finish(frame, address);
    }

    public static NativeBattleEffect79Step AdvanceParticle7BDAD0(NativeBattleEffect79Frame frame, uint address, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs)
    {
        switch (Phase(frame, address))
        {
            case 0:
                NativeBattleModulePoolTask task = Read(frame, address);
                frame = NativeBattleEffect79Programme.Initialize7016B0(frame, address, task.ReadDword(0x74), unchecked((int)task.ReadDword(0x78)), resources);
                frame = Write(frame, address, Read(frame, address).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096).WriteWord(0x88, 0xFE00));
                frame = CopyBodyParticleAnchor(frame, address);
                frame = NativeBattleEffect79Programme.Draw7B5C50(frame, address, resources, inputs).Frame;
                frame = NextPhase(frame, address);
                break;
            case 1:
                frame = CopyBodyParticleAnchor(frame, address);
                NativeBattleEffect79ProgrammeStep step = NativeBattleEffect79Programme.Draw7B5C50(frame, address, resources, inputs);
                frame = step.Frame;
                if (step.Remaining == 0)
                {
                    frame = NextPhase(MarkComplete(frame, address), address);
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Effect79 body particle programme leaves its three native phases.");
        }
        return Finish(frame, address);
    }

    public static NativeBattleEffect79Frame Birth7BCBF0(NativeBattleEffect79Frame frame, uint parent, NativeBattleEffect79Inputs inputs)
    {
        frame = inputs.Execute(frame, new NativeBattleEffect79Point502170(parent, 0xF1));
        (frame, uint child) = AppendHierarchy(frame, 5, 0x7BCC80, 0xB4, parent, inputs);
        frame = Write(frame, child, CopyTask(Read(frame, child), 0x6C, TaskView(Read(frame, parent), 0x1C, 8), 8));
        frame = ParticleRandom(frame, out int x);
        frame = ParticleRandom(frame, out int y);
        frame = ParticleRandom(frame, out int z);
        NativeBattleModulePoolTask task = Read(frame, child);
        task = task.WriteWord(0x6C, unchecked((ushort)(Signed(task, 0x6C) + (x & 0x7FF) - 0x400))).WriteWord(0x6E, unchecked((ushort)(Signed(task, 0x6E) + (y & 0x7FF) - 0x400))).WriteWord(0x70, unchecked((ushort)(Signed(task, 0x70) + (z & 0x1FF) - 0x100)));
        return Write(frame, child, task);
    }

    public static NativeBattleEffect79Frame Birth7BC720(NativeBattleEffect79Frame frame, uint parent, bool initializeBasis, short angle, NativeBattleEffect79Inputs inputs)
    {
        NativeBattleEffect79ParticleBasisStep basis = (inputs.ParticleBasis ?? throw new NotSupportedException("Effect79 requires its current caller particle basis."))(frame, parent, 49, !initializeBasis, angle);
        frame = basis.Frame;
        NativeBattleActionMatrix rotation = ParticleRotation(basis.Matrix);
        (frame, uint child) = AppendHierarchy(frame, 5, 0x7BC840, 0xB4, parent, inputs);
        for (int index = 0; index < 2; index++)
        {
            NativeBattleModulePoolTask task = CopyTask(Read(frame, child), 0x6C + index * 8, TaskView(Read(frame, parent), 0x1C, 8), 8);
            frame = ParticleRandom(frame, out int x);
            frame = ParticleRandom(frame, out int y);
            frame = ParticleRandom(frame, out int z);
            NativeBattleVector velocity = NativeBattleGfMatrices.RotateShort56C4F0(rotation, new(unchecked((short)((x & 0x7FF) - 0x400)), unchecked((short)((y & 0x7FF) - 0x400)), unchecked((short)(-4096 - (z & 0x7FF)))));
            frame = Write(frame, child, ParticleVector(task, 0x8C + index * 8, velocity));
        }
        return frame;
    }

    public static NativeBattleEffect79Frame Birth7BBCD0(NativeBattleEffect79Frame frame, uint parent, NativeBattleEffect79Inputs inputs)
    {
        frame = inputs.Execute(frame, new NativeBattleEffect79Point502170(parent, 49));
        (frame, uint child) = AppendHierarchy(frame, 5, 0x7BBDF0, 0xB4, parent, inputs);
        for (int index = 0; index < 2; index++)
        {
            frame = ParticleRandom(frame, out int yaw);
            frame = ParticleRandom(frame, out int pitch);
            NativeBattlePackedMatrix32 matrix = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
            NativeBattleStatefulMatrixRotationStep rotated = NativeBattleStatefulMatrixRotation.Rotate(frame.Memory, RequireStaging(frame), matrix, 'Y', (short)(yaw & 0xFFF));
            rotated = NativeBattleStatefulMatrixRotation.Rotate(rotated.Memory, rotated.Staging, rotated.Matrix, 'X', (short)(pitch & 0xFFF));
            frame = frame with { Memory = rotated.Memory, Staging = rotated.Staging };
            frame = ParticleRandom(frame, out int displacement);
            NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(ParticleRotation(rotated.Matrix), new(0, 0, (short)((displacement & 0x1FF) + 0x200)));
            NativeBattleModulePoolTask task = CopyTask(Read(frame, child), 0x6C + index * 8, TaskView(Read(frame, parent), 0x1C, 8), 8);
            int position = 0x6C + index * 8;
            task = ParticleVector(task, position, new(unchecked((short)(Signed(task, position) + offset.X)), unchecked((short)(Signed(task, position + 2) + offset.Y)), unchecked((short)(Signed(task, position + 4) + offset.Z))));
            frame = ParticleRandom(frame, out int speed);
            NativeBattleVector velocity = NativeBattleGfMatrices.RotateShort56C4F0(ParticleRotation(rotated.Matrix), new(0, 0, (short)((speed & 0x1FF) + 0x200)));
            frame = Write(frame, child, ParticleVector(task, 0x8C + index * 8, velocity));
        }
        return frame;
    }

    private static NativeBattleEffect79Frame CopyBodyParticleAnchor(NativeBattleEffect79Frame frame, uint address) => Write(frame, address, Read(frame, address).WriteDword(0x1C, frame.State.ReadDword(BodyContextAddress + 0x78)).WriteDword(0x20, frame.State.ReadDword(BodyContextAddress + 0x7C)));

    private static NativeBattleEffect79Frame CopyParticlePosition(NativeBattleEffect79Frame frame, uint address, int offset) => Write(frame, address, CopyTask(Read(frame, address), 0x1C, TaskView(Read(frame, address), offset, 8), 8));

    private static NativeBattleEffect79Frame DecrementParticleProducer(NativeBattleEffect79Frame frame, uint address)
    {
        short remaining = unchecked((short)(Signed(Read(frame, address), 0xB0) - 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(0xB0, unchecked((ushort)remaining)));
        return remaining <= 0 ? NextPhase(MarkComplete(frame, address), address) : frame;
    }

    private static NativeBattleEffect79Frame AdvanceParticleSpritePhase(NativeBattleEffect79Frame frame, uint address, uint source, short depth, ushort end)
    {
        switch (Phase(frame, address))
        {
            case 0:
                return NextPhase(Write(frame, address, Read(frame, address).WriteDword(0x4C, source).WriteWord(0x56, unchecked((ushort)depth)).WriteWord(0x52, end)), address);
            case 1:
                short next = unchecked((short)(Signed(Read(frame, address), 0x50) + 1));
                bool complete = next > Signed(Read(frame, address), 0x52);
                NativeBattleModulePoolTask task = Read(frame, address).WriteWord(0x50, complete ? Read(frame, address).ReadWord(0x52) : unchecked((ushort)next));
                if (complete)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5));
                }
                frame = Write(frame, address, task);
                return complete ? NextPhase(frame, address) : frame;
            case 2:
                return frame;
            default:
                throw new InvalidDataException("Effect79 sprite particle leaves its three native phases.");
        }
    }

    private static NativeBattleEffect79Frame ParticleRandom(NativeBattleEffect79Frame frame, out int value)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort result);
        value = result;
        return frame with { Random = random };
    }

    private static NativeBattleModulePoolTask ParticleVector(NativeBattleModulePoolTask task, int offset, NativeBattleVector vector) => task.WriteWord(offset, unchecked((ushort)vector.X)).WriteWord(offset + 2, unchecked((ushort)vector.Y)).WriteWord(offset + 4, unchecked((ushort)vector.Z));

    private static NativeBattleActionMatrix ParticleRotation(NativeBattlePackedMatrix32 packed)
    {
        packed.Validate();
        if (packed.Known.Take(18).Any(known => !known))
        {
            throw new NotSupportedException("Effect79 CPU particle rotation requires its retained caller rotation bytes.");
        }
        ImmutableArray<short> rotation = Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(packed.Bytes.AsSpan().Slice(index * 2, 2))).ToImmutableArray();
        return new(rotation, 0, 0, 0, 4096);
    }
}

