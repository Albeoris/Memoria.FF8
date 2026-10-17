using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCuragaNodeEngineInputs(Func<NativeBattleCuragaFrame, uint, int, NativeBattleCuragaFrame> AdvanceEmitter8817A0, Func<NativeBattleCuragaFrame, uint, int, NativeBattleCuragaFrame> AdvanceGeometry880510, Func<NativeBattleCuragaFrame, uint, NativeBattleCuragaFrame> DrawSprites880390, Func<NativeBattleCuragaFrame, uint, NativeBattleCuragaFrame> DrawMeshes87ED10);
public sealed record NativeBattleCuragaNodeEngineStep(NativeBattleCuragaFrame Frame, bool Complete);

/// <summary>87EC80's context phases and ordered birth/update/sprite/mesh execution. Persistent nodes remain in the original arena.</summary>
public static class NativeBattleCuragaNodeEngine
{
    public static NativeBattleCuragaLeafStep AdvanceGeneric87E1A0(NativeBattleCuragaFrame frame, uint contextTask, NativeBattleCuragaResources resources, NativeBattleCuragaControllerInputs controllerInputs, NativeBattleCuragaNodeEngineInputs inputs)
    {
        NativeBattleModulePoolTask task = ReadTask();
        if (task.Callback != 0x87E1A0)
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
        NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        bool complete = false;
        switch (unchecked((short)memory.ContextWord(0x20)))
        {
            case 0:
                memory.ContextWord(0x20, unchecked((ushort)(memory.ContextWord(0x20) + 1)));
                break;
            case 1:
                frame = Birth882E20(memory.Flush(), contextTask);
                frame = AdvanceList8804D0(frame, contextTask, inputs);
                frame = inputs.DrawSprites880390(frame, contextTask);
                frame = inputs.DrawMeshes87ED10(frame, contextTask);
                memory = new(frame, contextTask);
                short counter = unchecked((short)(memory.ContextWord(0x18) + 1));
                memory.ContextWord(0x18, unchecked((ushort)counter));
                if (counter >= unchecked((short)memory.ContextWord(0x1A)) && memory.ContextWord(0x14) == 0 && memory.ContextWord(0x16) == 0)
                {
                    memory.ContextWord(0x20, unchecked((ushort)(memory.ContextWord(0x20) + 1)));
                }
                break;
            case 2:
                complete = true;
                break;
        }
        ushort emitterCount = memory.State.PassCount26D82EA ?? throw new NotSupportedException("Native87EC80 requires87DC50's actual per-pass emitter-count reset.");
        ushort geometryCount = memory.State.PassCount26D82DA ?? throw new NotSupportedException("Native87EC80 requires87DC50's actual per-pass geometry-count reset.");
        memory.State = memory.State with { PassCount26D82EA = unchecked((ushort)(emitterCount + memory.ContextWord(0x14))), PassCount26D82DA = unchecked((ushort)(geometryCount + memory.ContextWord(0x16))) };
        return new(memory.Flush(), complete);
    }

    public static NativeBattleCuragaFrame Birth882E20(NativeBattleCuragaFrame frame, uint contextTask)
    {
        NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        NativeBattleCuragaExecutableState executable = frame.Memory.CuragaExecutable ?? throw new NotSupportedException("Native882E20 requires its retained executable-bank owner.");
        Birth(memory, executable);
        return memory.Flush();
    }

    internal static void Birth(NativeBattleCuragaNodeMemory memory, NativeBattleCuragaExecutableState executable)
    {
        uint directory = memory.ContextDword(0x224);
        for (int index = 0; index < 16; index++)
        {
            uint source = BinaryPrimitives.ReadUInt32LittleEndian(executable.Read(unchecked(directory + (uint)(index * 4)), 4));
            if (source == 0 || (memory.ContextWord(0x22) & (1 << index)) != 0)
            {
                continue;
            }
            byte[] record = executable.Read(source, 0x38);
            if (record[0x11] is not (0 or 1 or 4) || unchecked((short)(sbyte)record[0x13]) != unchecked((short)memory.ContextWord(0x18)))
            {
                continue;
            }
            if (record[0x37] == 1)
            {
                for (int target = 0; target < unchecked((short)memory.ContextWord(0x1C)); target++)
                {
                    NativeBattleCuragaNodes.AllocateEmitter(memory, 0, unchecked((sbyte)index), unchecked((sbyte)target));
                }
            }
            else
            {
                NativeBattleCuragaNodes.AllocateEmitter(memory, 0, unchecked((sbyte)index), 0);
            }
        }
    }

    public static NativeBattleCuragaFrame AdvanceList8804D0(NativeBattleCuragaFrame frame, uint contextTask, NativeBattleCuragaNodeEngineInputs inputs)
    {
        NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        WalkList(() => memory, (node, kind) =>
        {
            switch (kind)
            {
                case 0:
                    frame = inputs.AdvanceEmitter8817A0(frame, contextTask, node);
                    break;
                case 1:
                    frame = inputs.AdvanceGeometry880510(frame, contextTask, node);
                    break;
            }
            //8804D0 reads this link after the callback. Appended nodes run in this same walk, and882960 leaves a removed node's link intact.
            memory = new(frame, contextTask);
        });
        return frame;
    }

    internal static void WalkList(Func<NativeBattleCuragaNodeMemory> readMemory, Action<int, short> advance)
    {
        NativeBattleCuragaNodeMemory memory = readMemory();
        uint pointer = memory.ContextDword(0x2C);
        while (pointer != 0)
        {
            int node = memory.Offset(pointer);
            advance(node, unchecked((short)memory.Word(node + 8)));
            memory = readMemory();
            pointer = memory.Dword(node + 4);
        }
    }
}
