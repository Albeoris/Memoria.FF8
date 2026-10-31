using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect308Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect308Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect308Lower
{
    public static NativeBattleEffect308Step Advance(NativeBattleEffect308Frame frame, uint address, NativeBattleEffect308Resources resources, NativeBattleEffect308Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback == 0x6685E0)
        {
            NativeBattleActionMatrix local = NativeBattlePandemona291Restoration.Rotation701270(Signed(task, 0x14)) with { X = Signed(task, 12), Y = Signed(task, 14), Z = Signed(task, 16) };
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
            NativeBattleEffectArena caller = PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null));
            for (int axis = 0; axis < 3; axis++)
            {
                caller = Put308(caller, 0x20 + axis * 4, unchecked((uint)(int)Signed(task, 0x12)));
            }
            caller = Put308(Put308(Put308(caller, 0x2C, 1), 0x30, unchecked((uint)(int)Signed(task, 0x16))), 0x34, checked(frame.State.ReadDword(ArenaGlobal) + MorphOffset));
            uint source = Dword308(ReadBytes(frame, address + 0x18, 4), 0);
            int selector = resources.Programmes.Single(pair => pair.Value.Address == source).Key;
            int length = resources.WorkspaceLengths[selector];
            NativeBattleEffect308Step result = NativeBattleEffect308Programme.Advance(frame, address + 0x18, length, caller, resources, inputs);
            return new(result.Frame, result.Result == 0 ? 2 : 0);
        }
        if (task.Callback != 0x668A50)
        {
            throw new NotSupportedException("Effect308 requires its selected ACT or particle callback.");
        }
        NativeBattleEffectArena point = TaskView(task, 12, 8);
        (byte[] packed, bool[] known) = point.ReadSnapshot(0, 8);
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), Vector(point), known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(packed.AsSpan(6)) : null, Signed(task, 0x12), -1024) };
        frame = Publish(frame, Registers(frame) with { Colors = Registers(frame).Colors with { NormalInput = packed.ToImmutableArray(), NormalInputKnown = known.ToImmutableArray() } });
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleModuleResource sprite = resources.ReadSprite(task.ReadBytes(0x15, 1)[0]);
        int phases = BinaryPrimitives.ReadInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8));
        NativeBattleEffectArena context = PutWord308(PutWord308(Put308(Context(frame, temporary, 180), 0, sprite.Address), 4, task.ReadBytes(0x14, 1)[0]), 0x24, 0);
        NativeBattleModuleSpriteDraw draw = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, context, resources.Sprite, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, 2, pilotIdentity: inputs.CapturePresentation ? NativeBattleOffsetBodyVisuals.At(308, frame.Effect.Pools, address, sprite.Address) : null);
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary, draw.Temporary180, 180);
        frame = inputs.Execute(frame, new NativeBattleEffect308DrawBatch(draw.Batch, CursorGlobal));
        frame = Release(frame, 180);
        task = Read(frame, address);
        byte[] motion = task.ReadBytes(0x16, 6);
        for (int axis = 0; axis < 3; axis++)
        {
            task = task.WriteWord(12 + axis * 2, unchecked((ushort)(Signed(task, 12 + axis * 2) + (unchecked((sbyte)motion[axis]) << 2))));
        }
        task = task.WriteBytes(0x16, [unchecked((byte)(motion[0] + motion[3])), unchecked((byte)(motion[1] + motion[4] * 2))]);
        byte phase = unchecked((byte)(task.ReadBytes(0x14, 1)[0] + 1));
        task = task.WriteBytes(0x14, [phase]);
        return new(Write(frame, address, task), phase < phases ? 0 : 2);
    }

    public static (NativeBattleEffect308Frame Frame, uint Next) BirthAct6684C0(NativeBattleEffect308Frame frame, uint pc, uint model, uint rows, bool forceYzero, NativeBattleEffect308Resources resources, NativeBattleEffect308Inputs inputs)
    {
        short[] operands = ReadOperands(frame, pc, 6);
        (frame, uint address) = Append(frame, ActDescriptor, 0x6685E0);
        if (address == 0)
        {
            return (frame, checked(pc + 12));
        }
        frame = inputs.Execute(frame, new NativeBattleEffect308ApplyOffsetPose662C00(model, rows));
        NativeBattleEffect308PointStep anchor = inputs.Point502170(frame, model, operands[0], operands[1], TaskView(Read(frame, address), 12, 8));
        frame = anchor.Frame;
        NativeBattleEffectArena point = AddOffset(frame, anchor.Output8, rows, operands[0], forceYzero);
        NativeBattleModulePoolTask task = CopyTask(Read(frame, address), 12, point, 8).WriteWord(0x12, unchecked((ushort)operands[3])).WriteWord(0x14, unchecked((ushort)operands[4])).WriteWord(0x16, unchecked((ushort)operands[5]));
        frame = Write(frame, address, task);
        frame = inputs.Execute(frame, new NativeBattleEffect308RestorePose508C90(model));
        byte[] descriptor = resources.ReadStatic(unchecked(0x102A400u + (uint)(operands[2] * 8)), 8);
        uint source = BinaryPrimitives.ReadUInt32LittleEndian(descriptor);
        int clear = BinaryPrimitives.ReadInt32LittleEndian(descriptor.AsSpan(4));
        frame = NativeBattleEffect308Programme.Initialize(frame, address + 0x18, source, clear, resources, inputs.CapturePresentation);
        return (frame, checked(pc + 12));
    }

    public static (NativeBattleEffect308Frame Frame, uint Next) BirthParticle668970(NativeBattleEffect308Frame frame, uint pc, uint model, uint rows, NativeBattleEffect308Resources resources, NativeBattleEffect308Inputs inputs)
    {
        short[] operands = ReadOperands(frame, pc, 11);
        if (operands[0] != 0)
        {
            throw new NotSupportedException("Effect308 requires its selected allocated particle mode.");
        }
        (frame, uint address) = Append(frame, ParticleDescriptor, 0x668A50);
        if (address == 0)
        {
            return (frame, checked(pc + 22));
        }
        frame = inputs.Execute(frame, new NativeBattleEffect308ApplyOffsetPose662C00(model, rows));
        NativeBattleEffect308PointStep anchor = inputs.Point502170(frame, model, operands[1], operands[2], TaskView(Read(frame, address), 12, 8));
        frame = anchor.Frame;
        NativeBattleEffectArena point = AddOffset(frame, anchor.Output8, rows, operands[1], false);
        NativeBattleModulePoolTask task = CopyTask(Read(frame, address), 12, point, 8).WriteWord(0x12, unchecked((ushort)operands[4])).WriteBytes(0x14, [0, unchecked((byte)operands[3])]);
        for (int index = 0; index < 6; index++)
        {
            task = task.WriteBytes(0x16 + index, [unchecked((byte)operands[5 + index])]);
        }
        return (Write(frame, address, task), checked(pc + 22));
    }

    public static (NativeBattleEffect308Frame Frame, uint Next) BirthBurst668B20(NativeBattleEffect308Frame frame, uint pc, uint model, uint rows, NativeBattleEffect308Resources resources, NativeBattleEffect308Inputs inputs)
    {
        short[] operands = ReadOperands(frame, pc, 19);
        if (operands[0] != 0)
        {
            throw new NotSupportedException("Effect308 requires its selected allocated burst mode.");
        }
        frame = inputs.Execute(frame, new NativeBattleEffect308ApplyOffsetPose662C00(model, rows));
        NativeBattleEffect308PointStep anchor = inputs.Point502170(frame, model, operands[1], operands[2], NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
        frame = anchor.Frame;
        NativeBattleEffectArena point = AddOffset(frame, anchor.Output8, rows, operands[1], false);
        int remaining = operands[5];
        while (remaining-- != 0)
        {
            (frame, uint address) = Append(frame, ParticleDescriptor, 0x668A50);
            if (address == 0)
            {
                break;
            }
            NativeBattleModulePoolTask task = Read(frame, address);
            for (int axis = 0; axis < 3; axis++)
            {
                NativeBattleEffect308RandomStep random = Range668D30(frame, operands[6 + axis * 2], operands[7 + axis * 2]);
                frame = random.Frame;
                task = task.WriteWord(12 + axis * 2, unchecked((ushort)(Word308(point, axis * 2) + random.Value)));
            }
            task = task.WriteWord(0x12, unchecked((ushort)operands[4])).WriteBytes(0x14, [0, unchecked((byte)operands[3])]);
            for (int axis = 0; axis < 3; axis++)
            {
                NativeBattleEffect308RandomStep random = Range668D30(frame, operands[12 + axis * 2], operands[13 + axis * 2]);
                frame = random.Frame;
                byte velocity = unchecked((byte)random.Value);
                task = task.WriteBytes(0x16 + axis, [velocity]).WriteBytes(0x19 + axis, [unchecked((byte)(unchecked((sbyte)velocity) >> (operands[18] & 31)))]);
            }
            frame = Write(frame, address, task);
        }
        return (frame, checked(pc + 38));
    }

    public static NativeBattleEffect308RandomStep Range668D30(NativeBattleEffect308Frame frame, int span, int basis)
    {
        if (span == 0)
        {
            return new(frame, basis);
        }
        NativeBattleEffect308RandomStep random = NativeBattleEffect308Fragments.InternalRandom668450(frame);
        return new(random.Frame, unchecked((unchecked(random.Value * span) >> 15) + basis));
    }

    public static (NativeBattleEffect308Frame Frame, uint Next) SetBackground668D60(NativeBattleEffect308Frame frame, uint pc, NativeBattleEffect308Inputs inputs)
    {
        return SetBackground668D60(frame, pc, inputs, null);
    }

    public static (NativeBattleEffect308Frame Frame, uint Next) SetBackground668D60(NativeBattleEffect308Frame frame, uint pc, NativeBattleEffect308Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity)
    {
        short[] rgb = ReadOperands(frame, pc, 3);
        uint color = 0x2A000000u | unchecked((byte)rgb[0]) | (uint)unchecked((byte)rgb[1]) << 8 | (uint)unchecked((byte)rgb[2]) << 16;
        uint[] words = [0x05000000, color, 0, 320, 216u << 16, 320 | 216u << 16];
        byte[] data = new byte[24];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(index * 4), words[index]);
        }
        NativeBattleGfGeometryAttempt attempt = new(0, data.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 24).ToImmutableArray(), new(8, words.ToImmutableArray(), [0, 0, 0, 0]));
        frame = inputs.Execute(frame, new NativeBattleEffect308DrawBatch(NativeBattleOffsetBodyVisuals.FixedQuad(new([attempt], 24), pilotIdentity), CursorGlobal));
        return (frame, checked(pc + 6));
    }

    private static short[] ReadOperands(NativeBattleEffect308Frame frame, uint pc, int count)
    {
        byte[] data = ReadBytes(frame, pc, checked(count * 2)).Read(0, count * 2);
        return Enumerable.Range(0, count).Select(index => BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(index * 2))).ToArray();
    }

    private static NativeBattleEffectArena AddOffset(NativeBattleEffect308Frame frame, NativeBattleEffectArena point, uint rows, int bone, bool forceYzero)
    {
        NativeBattleEffectArena offset = ReadBytes(frame, unchecked(rows + (uint)(bone * 16)), 6);
        for (int axis = 0; axis < 3; axis++)
        {
            point = PutWord308(point, axis * 2, forceYzero && axis == 1 ? 0 : unchecked((short)(Word308(point, axis * 2) + Word308(offset, axis * 2))));
        }
        return point;
    }

    public static NativeBattleEffectArena PutWord308(NativeBattleEffectArena data, int offset, int value)
    {
        return data.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
    }

    public static NativeBattleEffectArena Put308(NativeBattleEffectArena data, int offset, uint value)
    {
        return data.Write(offset, BitConverter.GetBytes(value));
    }

    public static NativeBattlePackedMatrix32 Packed(NativeBattleEffectArena data)
    {
        (byte[] bytes, bool[] known) = data.ReadSnapshot(0, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    public static NativeBattleGeometryRegisterStep LoadPacked(NativeBattleGeometryRegisterStep step, NativeBattleEffectArena data, int slot, int offset)
    {
        (byte[] bytes, bool[] known) = data.ReadSnapshot(offset, 8);
        short? w = known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)) : null;
        step = NativeBattleGeometry.LoadVector(step, slot, Vector(data, offset), w);
        return step with { Colors = step.Colors with { NormalInput = bytes.ToImmutableArray(), NormalInputKnown = known.ToImmutableArray() } };
    }
}
