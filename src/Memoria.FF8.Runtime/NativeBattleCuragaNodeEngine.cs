using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCuragaNodeEngineInputs(Func<NativeBattleCuragaFrame, uint, NativeBattleCuragaFrame> AdvanceBirthUpdateAndDraw);
public sealed record NativeBattleCuragaNodeEngineStep(NativeBattleCuragaFrame Frame, bool Complete);

/// <summary>87EC80's context phases and ordered birth/update/sprite/mesh execution. Persistent nodes remain in the original arena.</summary>
public static class NativeBattleCuragaNodeEngine
{
    public static NativeBattleCuragaLeafStep AdvanceGeneric87E1A0(NativeBattleCuragaFrame frame, uint contextTask, NativeBattleCuragaResources resources, NativeBattleCuragaControllerInputs controllerInputs, NativeBattleCuragaNodeEngineInputs inputs)
    {
        NativeBattleModulePoolTask task = ReadTask();
        if (task.Callback != NativeBattleCuragaFamilyProfile.ForEffect(frame.Effect.Binding.Effect).Callback(0x87E1A0))
        {
            throw new InvalidDataException("Native Curaga generic task requires its original87E1A0 callback.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                if (unchecked((short)task.ReadWord(0x24)) >= unchecked((short)task.ReadWord(0x298)))
                {
                    frame = NativeBattleCuragaContext.Initialize87E230(frame, contextTask, resources, controllerInputs);
                    frame = Advance87EC80(frame, contextTask, inputs).Frame;
                    task = ReadTask();
                    WriteTask(task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
                }
                break;
            case 1:
                NativeBattleCuragaNodeEngineStep step = Advance87EC80(frame, contextTask, inputs);
                frame = step.Frame;
                if (step.Complete)
                {
                    task = ReadTask();
                    WriteTask(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Native87E1A0 signed task phase leaves its three original callbacks.");
        }
        task = ReadTask();
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        WriteTask(task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
            frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Complete8DC530(contextTask).Pools } };
        }
        return new(frame, complete ? 2 : 0);

        NativeBattleModulePoolTask ReadTask() => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(contextTask);
        void WriteTask(NativeBattleModulePoolTask updated)
        {
            NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
            frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(contextTask, updated).Pools } };
        }
    }

    public static NativeBattleCuragaNodeEngineStep Advance87EC80(NativeBattleCuragaFrame frame, uint contextTask, NativeBattleCuragaNodeEngineInputs inputs)
    {
        var step = NativeBattleEffectNodeLifecycle.AdvanceContext(ReadTask, WriteTask, () => frame = inputs.AdvanceBirthUpdateAndDraw(frame, contextTask));
        ushort emitterCount = frame.State.PassCount26D82EA ?? throw new NotSupportedException("Native87EC80 requires87DC50's actual per-pass emitter-count reset.");
        ushort geometryCount = frame.State.PassCount26D82DA ?? throw new NotSupportedException("Native87EC80 requires87DC50's actual per-pass geometry-count reset.");
        return new(frame with { State = frame.State with { PassCount26D82EA = unchecked((ushort)(emitterCount + step.EmitterCount)), PassCount26D82DA = unchecked((ushort)(geometryCount + step.GeometryCount)) } }, step.Complete);

        NativeBattleModulePoolTask ReadTask() => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(contextTask);
        void WriteTask(NativeBattleModulePoolTask updated)
        {
            NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
            frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(contextTask, updated).Pools } };
        }
    }

    public static NativeBattleCuragaFrame Birth882E20(NativeBattleCuragaFrame frame, uint contextTask)
    {
        NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        NativeBattleCuragaExecutableState executable = NativeBattleCuragaFamilyProfile.ForEffect(frame.Effect.Binding.Effect).RequireExecutable(frame.Memory);
        Birth(memory, executable);
        return memory.Flush();
    }

    internal static void Birth(NativeBattleCuragaNodeMemory memory, NativeBattleCuragaExecutableState executable)
    {
        NativeBattleCuragaNodeKinematics.CreateLifecycle(memory, NativeBattleCuragaNodeKinematics.CurrentBank(executable)).Birth();
    }

    public static NativeBattleCuragaFrame AdvanceList8804D0(NativeBattleCuragaFrame frame, uint contextTask, Func<NativeBattleCuragaFrame, uint, int, NativeBattleCuragaFrame> advanceEmitter, Func<NativeBattleCuragaFrame, uint, int, NativeBattleCuragaFrame> advanceGeometry)
    {
        NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        WalkList(() => memory, (node, kind) =>
        {
            switch (kind)
            {
                case 0:
                    frame = advanceEmitter(frame, contextTask, node);
                    break;
                case 1:
                    frame = advanceGeometry(frame, contextTask, node);
                    break;
            }
            //8804D0 reads this link after the callback. Appended nodes run in this same walk, and882960 leaves a removed node's link intact.
            memory = new(frame, contextTask);
        });
        return frame;
    }

    internal static void WalkList(Func<NativeBattleCuragaNodeMemory> readMemory, Action<int, short> advance)
    {
        NativeBattleEffectNodeAllocation.WalkList(() => readMemory(), advance);
    }
}
