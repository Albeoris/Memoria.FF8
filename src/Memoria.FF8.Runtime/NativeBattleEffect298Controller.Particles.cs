using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect298Controller
{
    public static (NativeBattleEffect298Frame Frame, uint Next) BirthSprite671E40(NativeBattleEffect298Frame frame, uint script, uint model, uint offsets, NativeBattleEffect298Resources resources, NativeBattleEffect298Inputs inputs)
    {
        short[] words = Enumerable.Range(0, 11).Select(index => ScriptWord298(frame, resources, script + (uint)(index * 2))).ToArray();
        if (words[0] != 0)
        {
            throw new NotSupportedException("Effect298 sprite mutation requires its actual addressed model-field consumer.");
        }
        (frame, uint address) = Append(frame, 0x25078A0, 0x671F20);
        if (address == 0)
        {
            return (frame, script + 22);
        }
        frame = inputs.Execute(frame, new NativeBattleEffect298ApplyOffsetPose662C00(model, offsets));
        var bone = inputs.ReadBone502170(frame, model, words[1], words[2]);
        frame = bone.Frame;
        NativeBattleEffectArena point = OffsetPoint298(frame, bone.Point8, offsets, words[1]);
        NativeBattleModulePoolTask task = CopyTask(Read(frame, address), 12, point, 6).WriteWord(18, unchecked((ushort)words[4]));
        task = CopyTask(task, 20, NativeBattleEffectArena.Clear().Write(0, new byte[] { 0, unchecked((byte)words[3]), unchecked((byte)words[5]), unchecked((byte)words[6]), unchecked((byte)words[7]), unchecked((byte)words[8]), unchecked((byte)words[9]), unchecked((byte)words[10]) }), 8);
        return (Write(frame, address, task), script + 22);
    }

    public static (NativeBattleEffect298Frame Frame, uint Next) BirthSpriteBatch671FF0(NativeBattleEffect298Frame frame, uint script, uint model, uint offsets, NativeBattleEffect298Resources resources, NativeBattleEffect298Inputs inputs)
    {
        short[] words = Enumerable.Range(0, 24).Select(index => ScriptWord298(frame, resources, script + (uint)(index * 2))).ToArray();
        if (words[0] != 0 || words[5] < 0)
        {
            throw new NotSupportedException("Effect298 sprite batch requires its selected allocation mode and nonnegative native count.");
        }
        frame = inputs.Execute(frame, new NativeBattleEffect298ApplyOffsetPose662C00(model, offsets));
        var bone = inputs.ReadBone502170(frame, model, words[1], words[2]);
        frame = bone.Frame;
        NativeBattleEffectArena origin = OffsetPoint298(frame, bone.Point8, offsets, words[1]);
        for (int index = 0; index < words[5]; index++)
        {
            (frame, uint address) = Append(frame, 0x25078A0, 0x671F20);
            if (address == 0)
            {
                break;
            }
            NativeBattleModulePoolTask task = Read(frame, address);
            for (int axis = 0; axis < 3; axis++)
            {
                (frame, int value) = RandomRange298(frame, words[6 + axis * 2], words[7 + axis * 2]);
                short position = BinaryPrimitives.ReadInt16LittleEndian(origin.Read(axis * 2, 2));
                task = task.WriteWord(12 + axis * 2, unchecked((ushort)(position + value)));
            }
            task = task.WriteWord(18, unchecked((ushort)words[4]));
            byte[] fields = new byte[8];
            fields[1] = unchecked((byte)words[3]);
            for (int field = 0; field < 6; field++)
            {
                (frame, int value) = RandomRange298(frame, words[12 + field * 2], words[13 + field * 2]);
                fields[field + 2] = unchecked((byte)value);
            }
            task = CopyTask(task, 20, NativeBattleEffectArena.Clear().Write(0, fields), fields.Length);
            frame = Write(frame, address, task);
        }
        return (frame, script + 48);
    }

    public static (NativeBattleEffect298Frame Frame, int Result) AdvanceSprite671F20(NativeBattleEffect298Frame frame, uint address, NativeBattleEffect298Resources resources, NativeBattleEffect298Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        byte[] fields = TaskView(task, 20, 8).Read(0, 8);
        uint sourceAddress = BinaryPrimitives.ReadUInt32LittleEndian(resources.SpriteTable16.AsSpan().Slice(fields[1] * 4, 4));
        NativeBattleModuleResource source = resources.ReadSprite(sourceAddress);
        short size = Signed(task, 18);
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), new(Signed(task, 12), Signed(task, 14), Signed(task, 16)), size, size, -1024) };
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(sourceAddress)).Write(4, BitConverter.GetBytes((ushort)fields[0])).Write(0x24, new byte[2]);
        frame = PublishContext(frame, temporary, context, 180);
        frame = inputs.Execute(frame, new NativeBattleEffect298DrawSprite180(temporary, sourceAddress) { PilotIdentity = inputs.CapturePresentation ? NativeBattleOffsetBodyVisuals.At(298, frame.Effect.Pools, address, sourceAddress) : null });
        frame = Release(frame, 180);
        task = Read(frame, address);
        for (int axis = 0; axis < 3; axis++)
        {
            task = task.WriteWord(12 + axis * 2, unchecked((ushort)(task.ReadWord(12 + axis * 2) + (unchecked((sbyte)fields[axis + 2]) << 2))));
        }
        fields[2] = unchecked((byte)(fields[2] + fields[5]));
        fields[3] = unchecked((byte)(fields[3] + (fields[6] << 1)));
        fields[0]++;
        frame = Write(frame, address, CopyTask(task, 20, NativeBattleEffectArena.Clear().Write(0, fields), fields.Length));
        int count = BinaryPrimitives.ReadInt16LittleEndian(source.Bytes.AsSpan().Slice(8, 2));
        return (frame, fields[0] < count ? 0 : 2);
    }

    private static NativeBattleEffectArena OffsetPoint298(NativeBattleEffect298Frame frame, NativeBattleEffectArena point, uint offsets, int bone)
    {
        NativeBattleEffectArena offset = ReadBytes(frame, checked(offsets + (uint)(bone * 16)), 6);
        for (int axis = 0; axis < 3; axis++)
        {
            short value = unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(point.Read(axis * 2, 2)) + BinaryPrimitives.ReadInt16LittleEndian(offset.Read(axis * 2, 2))));
            point = point.Write(axis * 2, BitConverter.GetBytes(value));
        }
        return point;
    }

    private static (NativeBattleEffect298Frame Frame, int Value) RandomRange298(NativeBattleEffect298Frame frame, int range, int first)
    {
        if (range == 0)
        {
            return (frame, first);
        }
        frame = RandomLocal298(frame, out int value);
        return (frame, unchecked((unchecked(value * range) >> 15) + first));
    }

    private static NativeBattleEffect298Frame RandomLocal298(NativeBattleEffect298Frame frame, out int value)
    {
        value = unchecked((int)(frame.State.ReadDword(0x25078F8) * 125 + 14)) & 32767;
        return frame with { State = frame.State.WriteDword(0x25078F8, (uint)value) };
    }
}
