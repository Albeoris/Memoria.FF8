using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSiren95NodeServices(NativeBattleActionPacketsState Packets, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, NativeBattleCameraProjection Projection, short CameraRoll, NativeBattleSummonColors Colors, NativeBattleEffectHeapAllocation? StaticSource);
public sealed record NativeBattleSiren95NodeStep(NativeBattleSiren95Frame Frame, bool Complete, NativeBattleLogicalOrdering Ordering);

public static class NativeBattleSiren95Nodes
{
    public static NativeBattleSiren95NodeStep Advance745180(NativeBattleSiren95Frame frame, uint address, NativeBattleLogicalOrdering ordering, NativeBattleSiren95NodeServices services)
    {
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        if (task.Callback is not (0x745180 or 0x73F900))
        {
            throw new InvalidDataException("Native Siren nodes require their actual context callback.");
        }
        sbyte phase = unchecked((sbyte)task.ReadByte(0x29));
        if (phase is < 0 or > 2)
        {
            throw new NotSupportedException($"Native Siren node signed phase {phase} is unsupported.");
        }
        if (phase == 0 && unchecked((short)task.ReadWord(0x24)) >= unchecked((short)task.ReadWord(0x298)) || phase == 1)
        {
            uint sourceAddress = task.ReadDword(0x30);
            NativeBattleSiren95State state = State(frame);
            ImmutableArray<byte> data;
            if (sourceAddress == 0x16A40EC)
            {
                if (state.StaticNodeData is not { } retained)
                {
                    if (!state.StaticNodeDataKnownCold)
                    {
                        throw new NotSupportedException("Native Siren static node bytes are unknown; migrate the named snapshot explicitly.");
                    }
                    retained = (services.StaticSource ?? throw new NotSupportedException("Native Siren nodes require their original static source.")).Data;
                    state = state with { StaticNodeData = retained, StaticNodeDataKnownCold = false };
                }
                data = retained;
            }
            else
            {
                data = task.Callback == 0x73F900 ? NativeBattleSiren95Io.ReadProgrammeSource(frame.Memory, sourceAddress).Data : NativeBattleProcessFileBanks.Read(frame.Memory).Read(sourceAddress, 0x1294).ToImmutableArray();
            }
            NativeBattleEffectNodeCallerPolicy policy = new(sourceAddress, data.Length, task.Callback, 40, 74, false, AllowMode3: true);
            int first = ArenaOffset(state.ReadDword(0x258EC54));
            int second = ArenaOffset(state.ReadDword(0x258FB6C));
            NativeBattleRecover93NodeState work = new(BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan()), data, first, second, state.ReadWord(0x2585AE0), state.ReadWord(0x258EDD0), state.ReadWord(0x258FB40), state.ReadWord(0x258EC50), (frame.Memory.SharedEffectView2793E58 ?? throw new NotSupportedException("Native Siren nodes require their copied camera bytes.")).ReadMatrix(), frame.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native Siren nodes require their actual private packet cursor."), null, null);
            NativeBattleRecover93NodeFrame current = new(frame.Memory with { Siren95 = state }, frame.Effect, work, frame.Random);
            if (phase == 0)
            {
                current = NativeBattleEffectNodeCaller.InitializeCurrent(policy, current, address, services.Packets, services.ReadModel, services.ReadPoseSource);
            }
            state = (current.Memory.Siren95 ?? throw new NotSupportedException("Native Siren node state is missing.")).WriteDword(0x258FB78, unchecked(address + 0x34));
            current = current with { Memory = current.Memory with { Siren95 = state } };
            uint? far = null;
            NativeBattleRecover93NodeStep step = NativeBattleEffectNodeCaller.Advance74F6E0(policy, current, address, services.ReadModel, services.Projection, services.CameraRoll, ordering, services.Colors, prepareMeshOverride: PrepareMesh);
            ordering = step.Ordering;
            current = step.Frame;
            state = current.Memory.Siren95 ?? throw new NotSupportedException("Native Siren node state is missing.");
            state = state.Write(0x2585AE0, BitConverter.GetBytes(current.State.EmitterScanCursor!.Value)).Write(0x258EDD0, BitConverter.GetBytes(current.State.GeometryScanCursor!.Value)).Write(0x258FB40, BitConverter.GetBytes(current.State.PassEmitterCount!.Value)).Write(0x258EC50, BitConverter.GetBytes(current.State.PassGeometryCount!.Value));
            NativeBattleEffectMemory memory = current.Memory;
            if (sourceAddress == 0x16A40EC)
            {
                state = state with { StaticNodeData = current.State.Data };
            }
            else
            {
                NativeBattleChoco97IoBanks banks = NativeBattleProcessFileBanks.Read(memory).Write(sourceAddress, current.State.Data!.Value);
                memory = NativeBattleProcessFileBanks.Write(memory, banks);
            }
            if (far is uint rgb)
            {
                NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native Siren mesh color requires its canonical renderer process.");
                NativeBattleModelRendererRegisters registers = process.ModelRendererRegisters ?? new(null, null, null, null, null);
                memory = memory with { PrimitiveProcess = process with { ModelRendererRegisters = registers.PublishModelColor(rgb) } };
            }
            frame = new(memory with { Siren95 = state }, current.Effect with { DrawArenaOffset = current.State.PrivateDrawCursor }, current.Random);
            task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
            if (phase == 0 || step.Complete)
            {
                if (phase == 1)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                }
                task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
            }

            NativeBattleEffectNodeMeshInstance PrepareMesh(NativeBattleEffectNodeMemory nodeMemory, int node, byte[] source)
            {
                if (unchecked((short)nodeMemory.Word(node + 0x1CE)) != 0)
                {
                    nodeMemory.WriteScratchPrefix(0, BitConverter.GetBytes(nodeMemory.Dword(node + 0x16C)));
                }
                return (bytes, matrix) =>
                {
                    _ = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan());
                    byte[] rgb = nodeMemory.ReadScratchPrefix(0, 3);
                    far = (uint)(rgb[0] | rgb[1] << 8 | rgb[2] << 16);
                    return new([], 0);
                };
            }
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
        return new(frame, (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0, ordering);
    }

    private static int ArenaOffset(uint pointer)
    {
        if (pointer < 0x20DFAB8 || pointer >= 0x21DFAB8)
        {
            throw new InvalidDataException("Native Siren node workspace leaves its actual arena.");
        }
        return checked((int)(pointer - 0x20DFAB8));
    }
    private static NativeBattleSiren95State State(NativeBattleSiren95Frame frame) => frame.Memory.Siren95 ?? throw new NotSupportedException("Native Siren nodes require their retained module state.");
}
