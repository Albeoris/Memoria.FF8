using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBio22EmitterFrame(NativeBattleModulePoolTask Task, NativeBattleModulePoolTask Primary, NativeBattleEffectMemory Memory, NativeCrtRandomState Random);
public sealed record NativeBattleBio22EmitterSpawn(NativeBattleBio22EmitterFrame Frame, uint Address, NativeBattleModulePoolTask Child);
public sealed record NativeBattleBio22EmitterInputs(Func<NativeBattleBio22EmitterFrame, uint, uint, NativeBattleBio22EmitterSpawn> Append, Func<NativeBattleBio22EmitterFrame, uint, NativeBattleModulePoolTask, NativeBattleBio22EmitterFrame> PublishChild, Func<NativeBattleBio22EmitterFrame, short> Height);
public sealed record NativeBattleBio22EmitterStep(NativeBattleBio22EmitterFrame Frame, uint Result);

/// <summary>Bio's first timed emitter, preserving native append, RNG and height-query order.</summary>
public static class NativeBattleBio22TimedEmitter
{
    public static NativeBattleBio22EmitterStep Advance89BA80(uint selfAddress, NativeBattleBio22EmitterFrame frame, NativeBattleBio22Resources resources, NativeBattleBio22EmitterInputs inputs)
    {
        NativeBattleModulePoolTask task = frame.Task;
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = task.WriteDword(0x1C, frame.Primary.ReadDword(0x78)).WriteDword(0x20, frame.Primary.ReadDword(0x7C)).WriteByte(0x29, 1);
                frame = frame with { Task = task };
                break;
            case 1:
                int counter = S(task, 0x24);
                if (counter > 20)
                {
                    frame = frame with { Task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, 2) };
                    break;
                }
                int planar = Count(resources.PlanarCounts.Data, counter);
                for (int index = 0; index < planar; index++)
                {
                    frame = Spawn(selfAddress, frame, false, inputs);
                }
                int billboard = Count(resources.BillboardCounts.Data, counter);
                for (int index = 0; index < billboard; index++)
                {
                    frame = Spawn(selfAddress, frame, true, inputs);
                }
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException("Native Bio timed emitter phase leaves its callback table.");
        }
        byte flags = frame.Task.ReadByte(0x26);
        task = frame.Task.WriteWord(0x24, W(frame.Task.ReadWord(0x24) + 1));
        return new(frame with { Task = task }, (flags & 1) != 0 && task.ReadByte(0x28) == 0 ? 2u : 0u);
    }

    private static NativeBattleBio22EmitterFrame Spawn(uint selfAddress, NativeBattleBio22EmitterFrame frame, bool billboard, NativeBattleBio22EmitterInputs inputs)
    {
        frame = Draw(frame, out ushort value);
        int angle = value & 0xFFF;
        frame = Draw(frame, out value);
        short radius = unchecked((short)(unchecked((value % 0x180 + 0x380) * S(frame.Primary, 0x86)) / 4096));
        NativeBattleBio22EmitterSpawn spawned = inputs.Append(frame, selfAddress, billboard ? 0x89BD20u : 0x89BF20u);
        frame = Draw(spawned.Frame, out value);
        NativeBattleModulePoolTask child = spawned.Child.WriteWord(0x68, (ushort)(value & 1));
        child = child.WriteWord(0x1C, W(child.ReadWord(0x1C) + unchecked(NativeBattleCameraMath.Sine(angle) * radius) / 4096)).WriteWord(0x20, W(child.ReadWord(0x20) + unchecked(NativeBattleCameraMath.Cosine(angle) * radius) / 4096));
        int scale = S(frame.Primary, 0x86);
        if (billboard)
        {
            short height = inputs.Height(frame);
            frame = Draw(frame, out value);
            child = child.WriteWord(0x1E, W(unchecked((value & 0xFFF) * height) / 4096));
            frame = Draw(frame, out value);
            scale = unchecked(scale - unchecked((value & 0x7FF) * scale) / 4096);
        }
        else
        {
            child = child.WriteWord(0x1E, 0);
        }
        child = child.WriteDword(0x30, unchecked((uint)scale)).WriteDword(0x34, unchecked((uint)scale)).WriteDword(0x38, unchecked((uint)scale));
        return inputs.PublishChild(frame, spawned.Address, child);
    }

    private static NativeBattleBio22EmitterFrame Draw(NativeBattleBio22EmitterFrame frame, out ushort value) => frame with { Random = frame.Random.Draw(out value) };

    private static int Count(ImmutableArray<byte> counts, int index)
    {
        if (counts.IsDefault || counts.Length != 21 || (uint)index >= 21)
        {
            throw new InvalidDataException("Native Bio timed emitter requires its complete count table and actual signed counter.");
        }
        return counts[index];
    }

    private static short S(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static ushort W(int value) => unchecked((ushort)value);
}
