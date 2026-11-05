using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect319Controller;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect319Lower
{
    public static NativeBattleEffect319Step Advance(NativeBattleEffect319Frame frame, uint address, NativeBattleEffect319Resources resources, NativeBattleEffect319Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            0x65E430 => NativeBattleEffect319Sprite.Advance(frame, address, resources, inputs),
            0x65F2B0 => AdvanceBone(frame, address, inputs),
            _ => throw new NotSupportedException("The effect requires its selected lower callback.")
        };
    }

    public static NativeBattleEffect319Frame Birth65E4B0(NativeBattleEffect319Frame frame, int boneIndex, int delay, int stepZ, int stepXY, NativeBattleEffect319Inputs inputs)
    {
        (frame, uint address) = Append(frame, 0x25053C0, 0x65F2B0);
        if (address == 0)
        {
            throw new InvalidDataException("The selected bone task requires its current available pool slot.");
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        uint caster = frame.State.ReadDword(0x25053AC);
        task = inputs.ResolveBoneRecordReference is { } resolve ? task.WriteResourcePointer(12, resolve(frame, caster, boneIndex)) : task.WriteDword(12, inputs.ResolveBoneRecord(frame, caster, boneIndex));
        task = task.WriteWord(16, unchecked((ushort)delay)).WriteWord(24, unchecked((ushort)stepXY)).WriteWord(20, unchecked((ushort)stepZ)).WriteWord(18, 0).WriteWord(22, 0);
        return Write(frame, address, task);
    }

    public static NativeBattleEffect319Step AdvanceBone(NativeBattleEffect319Frame frame, uint address, NativeBattleEffect319Inputs inputs)
    {
        NativeBattleBoneScaleTaskStep step = NativeBattleBoneScaleTask65F2B0.Advance(Read(frame, address));
        NativeBattleModulePoolTask task = step.Task;
        frame = Write(frame, address, task);
        if (step.Result == 2)
        {
            return new(frame, 2);
        }
        NativeBattleEffect319Operation operation = task.BorrowPayloadArena(12, 4).TryGetResourcePointer(0, out NativeBattleResourcePointer? record) ? new NativeBattleEffect319ScaleBoneReference56BEF0(record ?? throw new NotSupportedException("The current-pose bone cache record is NULL."), Signed(task, 22), Signed(task, 18)) : new NativeBattleEffect319ScaleBone56BEF0(task.ReadDword(12), Signed(task, 22), Signed(task, 18));
        frame = inputs.Execute(frame, operation);
        return new(frame, 0);
    }

    public static uint Dword319(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(value.Read(offset, 4));
    public static short Word319(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadInt16LittleEndian(value.Read(offset, 2));
    public static NativeBattleEffectArena Put319(NativeBattleEffectArena value, int offset, uint number) => value.Write(offset, BitConverter.GetBytes(number));
    public static NativeBattleEffectArena PutWord319(NativeBattleEffectArena value, int offset, int number) => value.Write(offset, BitConverter.GetBytes(unchecked((short)number)));
}
