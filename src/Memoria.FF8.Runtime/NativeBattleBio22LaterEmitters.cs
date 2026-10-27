using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBio22LaterEmitterInputs(Func<NativeBattleBio22EmitterFrame, uint, uint, NativeBattleBio22EmitterSpawn> Append, Func<NativeBattleBio22EmitterFrame, uint, NativeBattleModulePoolTask, NativeBattleBio22EmitterFrame> PublishChild);

/// <summary>The two later Bio emitters differ only in their actual count table, bound and child callback.</summary>
public static class NativeBattleBio22LaterEmitters
{
    public static NativeBattleBio22EmitterStep Advance89CD60(uint selfAddress, NativeBattleBio22EmitterFrame frame, NativeBattleBio22Resources resources, NativeBattleBio22LaterEmitterInputs inputs) => Advance(selfAddress, frame, resources, inputs, false);
    public static NativeBattleBio22EmitterStep Advance89D020(uint selfAddress, NativeBattleBio22EmitterFrame frame, NativeBattleBio22Resources resources, NativeBattleBio22LaterEmitterInputs inputs) => Advance(selfAddress, frame, resources, inputs, true);

    private static NativeBattleBio22EmitterStep Advance(uint selfAddress, NativeBattleBio22EmitterFrame frame, NativeBattleBio22Resources resources, NativeBattleBio22LaterEmitterInputs inputs, bool second)
    {
        NativeBattleModulePoolTask task = frame.Task;
        if (task.Callback != (second ? 0x89D020u : 0x89CD60u) || task.PayloadLength != 0x38 - 12)
        {
            throw new InvalidDataException("Native Bio later emitter requires its actual hierarchy callback and extent.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                frame = frame with { Task = task.WriteDword(0x1C, frame.Primary.ReadDword(0x78)).WriteDword(0x20, frame.Primary.ReadDword(0x7C)).WriteByte(0x29, 1) };
                break;
            case 1:
                int maximum = second ? 20 : 24;
                int counter = S(task, 0x24);
                if (counter > maximum)
                {
                    frame = frame with { Task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, 2) };
                    break;
                }
                ImmutableArray<byte> counts = second ? resources.LaterBillboardCounts.Data : resources.LaterPlanarCounts.Data;
                if (counts.IsDefault || counts.Length != maximum + 1 || (uint)counter > maximum)
                {
                    throw new InvalidDataException("Native Bio later emitter counter leaves its complete original table.");
                }
                int count = counts[counter];
                for (int index = 0; index < count; index++)
                {
                    NativeBattleBio22EmitterSpawn spawned = inputs.Append(frame, selfAddress, second ? 0x89D120u : 0x89CE60u);
                    if (spawned.Address == 0)
                    {
                        throw new NotSupportedException("Native Bio later emitter requires its mandatory appended particle.");
                    }
                    frame = spawned.Frame;
                    frame = frame with { Random = frame.Random.Draw(out ushort value) };
                    int q = S(frame.Primary, 0x86);
                    uint scale = unchecked((uint)((value & 1) != 0 ? q : q / 2));
                    NativeBattleModulePoolTask child = spawned.Child.WriteDword(0x30, scale).WriteDword(0x34, scale).WriteDword(0x38, scale);
                    frame = inputs.PublishChild(frame, spawned.Address, child);
                }
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException("Native Bio later emitter phase leaves its callback table.");
        }
        byte flags = frame.Task.ReadByte(0x26);
        task = frame.Task.WriteWord(0x24, unchecked((ushort)(frame.Task.ReadWord(0x24) + 1)));
        return new(frame with { Task = task }, (flags & 1) != 0 && task.ReadByte(0x28) == 0 ? 2u : 0u);
    }

    private static short S(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
