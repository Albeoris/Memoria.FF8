using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSigh46Controller
{
    public static NativeBattleSigh46Frame InitializeEngine832190(NativeBattleSigh46Frame frame, uint address, NativeBattleSigh46Resources resources, NativeBattleSigh46Inputs inputs)
    {
        _ = resources;
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.ReadDword(0x30) != 0x15C3398 || frame.State.ReadDword(0x15C3398) == 0)
        {
            throw new NotSupportedException("Sigh requires its actual already-addressed source header.");
        }
        frame = frame with { State = frame.State.WriteDword(0x26527B0, checked(address + 0x34)) };
        NativeBattleModulePoolTask root = Read(frame, task.ReadDword(0x10));
        NativeBattleEffectParameters parameters = Parameters(frame);
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        task = NativeBattleEffectNodeContext.Initialize(task, root, parameters, packets, actor => inputs.ReadModel(frame, actor), null, 0x15C3398, readBone: ReadBone);
        return Write(frame, address, task);

        (NativeBattleModulePoolTask Task, NativeBattleVector Point) ReadBone(NativeBattleModulePoolTask currentTask, BattleModelOwner actor, byte selector)
        {
            frame = Write(frame, address, currentTask);
            NativeBattleSigh46PointStep point = inputs.ReadBone502170(frame, actor, selector, 0);
            frame = point.Frame;
            byte[] bytes = point.Point8.Read(0, 6);
            NativeBattleVector anchor = new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
            return (Read(frame, address), anchor);
        }
    }
}
