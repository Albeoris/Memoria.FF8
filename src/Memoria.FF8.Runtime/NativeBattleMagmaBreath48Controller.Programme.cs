using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMagmaBreath48Controller
{
    public static NativeBattleMagmaBreath48Frame InitializeEngine825440(NativeBattleMagmaBreath48Frame frame, uint address, NativeBattleMagmaBreath48Resources resources, NativeBattleMagmaBreath48Inputs inputs)
    {
        _ = resources;
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.ReadDword(0x30) != 0x15B7C88 || frame.State.ReadDword(0x15B7C88) == 0)
        {
            throw new NotSupportedException("Magma Breath requires its actual already-addressed source header.");
        }
        frame = frame with { State = frame.State.WriteDword(0x26495E8, checked(address + 0x34)) };
        NativeBattleModulePoolTask root = Read(frame, task.ReadDword(0x10));
        NativeBattleEffectParameters parameters = Parameters(frame);
        NativeBattleActionPacketsState packets = inputs.Packets;
        task = NativeBattleEffectNodeContext.Initialize(task, root, parameters, packets, actor => inputs.ReadModel(frame, actor), null, 0x15B7C88, readBone: ReadBone);
        return Write(frame, address, task);

        (NativeBattleModulePoolTask Task, NativeBattleVector Point) ReadBone(NativeBattleModulePoolTask currentTask, BattleModelOwner actor, byte selector)
        {
            frame = Write(frame, address, currentTask);
            NativeBattleMagmaBreath48PointStep point = inputs.ReadBone502170(frame, actor, selector, 0);
            frame = point.Frame;
            byte[] bytes = point.Point8.Read(0, 6);
            NativeBattleVector anchor = new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
            return (Read(frame, address), anchor);
        }
    }
}
