using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePain34PlayerInputs(Func<int, NativeBattleVector> ReadBoneF1, Func<NativeBattlePain34Frame, NativeBattleGfAnimationSource, NativeBattleGfAnimationNode, NativeBattleVector, NativeBattlePain34Frame> DrawNode);

public static class NativeBattlePain34Players
{
    public static NativeBattlePain34LeafStep Advance(NativeBattlePain34Frame frame, uint address, NativeBattlePain34Resources resources, NativeBattlePain34PlayerInputs inputs)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback is not (0x8659D0 or 0x865DB0))
        {
            throw new InvalidDataException("Native Pain player requires its actual selected callback.");
        }
        NativeBattleGfAnimationSource source = resources.Program(task.ReadDword(116));
        switch (unchecked((sbyte)task.ReadByte(41)))
        {
            case 0:
                NativeBattleStackBytes initializedStack = frame.PlayerQueueStack ?? throw new NotSupportedException("Native Pain player requires its current queue stack owner.");

                NativeBattleGfAnimationWorkspace initialized = Workspace(task).Initialize7016B0(source, unchecked((int)task.ReadDword(120)));
                task = WithWorkspace(task, initialized);
                frame = frame with { PlayerQueueStack = initializedStack.StoreUInt32(-0x94, 0, task.Callback == 0x8659D0 ? 0x865A62u : 0x865E42u, "player-state0-bone-fraction") };
                NativeBattleVector position = inputs.ReadBoneF1(task.ReadByte(45));
                task = task.WriteWord(28, unchecked((ushort)position.X)).WriteWord(30, unchecked((ushort)position.Y)).WriteWord(32, unchecked((ushort)position.Z)).WriteByte(41, 1);
                break;
            case 1:
                NativeBattleVector anchor = new(unchecked((short)task.ReadWord(28)), unchecked((short)task.ReadWord(30)), unchecked((short)task.ReadWord(32)));
                if (task.ReadWord(130) == 1)
                {
                    NativeBattleStackBytes stack = frame.PlayerQueueStack ?? throw new NotSupportedException("Native Pain play requires its current queue stack owner.");
                    frame = frame with { PlayerQueueStack = stack.StoreUInt32(-0x94, 1, task.Callback == 0x8659D0 ? 0x865AA8u : 0x865E88u, "player-state1-block40") };
                }
                NativeBattleGfAnimationAdvance advanced = Workspace(task).Advance701970(source, 0, (current, node) =>
                {
                    frame = inputs.DrawNode(frame, source, node, anchor);
                    return current;
                });
                task = WithWorkspace(task, advanced.Workspace);
                if (advanced.Remaining == 0)
                {
                    task = task.WriteByte(38, (byte)(task.ReadByte(38) | 1)).WriteByte(41, unchecked((byte)(task.ReadByte(41) + 1)));
                }
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException("Native Pain player left its original phase table.");
        }
        bool complete = (task.ReadByte(38) & 1) != 0 && task.ReadByte(40) == 0;
        task = task.WriteWord(36, unchecked((ushort)(task.ReadWord(36) + 1)));
        hierarchy = new(frame.Effect.Pools);
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, task).Pools } };
        return new(frame, complete);
    }

    public static uint RequirePhysicalBlock40(NativeBattleStackBytes? current) => (current ?? throw new NotSupportedException("Native Pain drawing requires its current physical player-queue stack owner.")).ReadUInt32(-0x94);

    private static NativeBattleGfAnimationWorkspace Workspace(NativeBattleModulePoolTask task)
    {
        task.RequireKnownPayload(148, task.PayloadLength - (148 - 12));
        return new(task.ReadRawPayloadSpan().Slice(148 - 12).ToArray().ToImmutableArray());
    }
    private static NativeBattleModulePoolTask WithWorkspace(NativeBattleModulePoolTask task, NativeBattleGfAnimationWorkspace workspace)
    {
        return task.WriteBytes(148, workspace.Data.AsSpan());
    }
}