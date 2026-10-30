using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect83NodeStep(NativeBattleEffect83Frame Frame, int Result);
public sealed record NativeBattleEffect83ObserveBone(int Actor, byte Selector) : NativeBattleEffect83Operation;

public static class NativeBattleEffect83NodeEngine
{
    public static NativeBattleEffect83Frame Initialize797F60(NativeBattleEffect83Frame frame, uint address, NativeBattleEffect83Resources resources, NativeBattleEffect83Inputs inputs)
    {
        using NativeBattleTaskOperation? operation = NativeBattleTaskOperation.BeginStandalone(frame.Memory, frame.Effect);
        operation?.AdmitStaging(frame.Staging);
        return NativeBattleTaskOperation.CompleteStandalone(operation, Initialize797F60Core(frame, address, resources, inputs));
    }

    private static NativeBattleEffect83Frame Initialize797F60Core(NativeBattleEffect83Frame frame, uint address, NativeBattleEffect83Resources resources, NativeBattleEffect83Inputs inputs)
    {
        frame = frame with { State = frame.State.BindNodeSource(resources) };
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        if (task.Callback != 0x797ED0 || task.ReadDword(0x30) != NativeBattleEffect83Profile.NodeSourceAddress || task.ReadWord(0x29C) != 0)
        {
            throw new InvalidDataException("Effect83 node initialization requires its actual source and mode0 task.");
        }
        NativeBattleNodeSource source = frame.State.NodeData ?? throw new NotSupportedException("Effect83 node source is missing.");
        NativeBattleTaskOperation.AdmitCurrentRetainedMemoryStorage(source);
        using NativeBattleNodeBankOperation initialization = new(source);
        if (initialization.ReadUInt32(0) == 0)
        {
            NativeBattleEffectNodeBankFixups.Relocate(initialization, NativeBattleEffect83Profile.NodeSourceAddress);
            initialization.WriteUInt32(0, 1);
        }
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        task = NativeBattleEffectNodeContext.Initialize(task, hierarchy.Read(task.ReadDword(0x10)), frame.Effect.Parameters ?? throw new NotSupportedException("Effect83 node context requires its actual action parameters."), inputs.ReadPackets(frame), actor => inputs.ReadModel(frame, actor), actor => inputs.ReadPoseSource(frame, actor), NativeBattleEffect83Profile.NodeSourceAddress, observeBone: (actor, selector) => frame = inputs.Execute(frame, new NativeBattleEffect83ObserveBone(actor, selector)));
        initialization.Publish(false);
        return frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, task).Pools }, State = frame.State.WriteDword(0x25CFA50, checked(address + 0x34)) };
    }

    public static NativeBattleEffect83NodeStep Advance798A40(NativeBattleEffect83Frame frame, uint address, NativeBattleEffect83Resources resources, NativeBattleEffect83Inputs inputs)
    {
        using NativeBattleTaskOperation? operation = NativeBattleTaskOperation.BeginStandalone(frame.Memory, frame.Effect);
        operation?.AdmitStaging(frame.Staging);
        return NativeBattleTaskOperation.CompleteStandalone(operation, Advance798A40Core(frame, address, resources, inputs));
    }

    private static NativeBattleEffect83NodeStep Advance798A40Core(NativeBattleEffect83Frame frame, uint address, NativeBattleEffect83Resources resources, NativeBattleEffect83Inputs inputs)
    {
        frame = frame with { State = frame.State.WriteDword(0x25CFA50, checked(address + 0x34)) };
        var context = NativeBattleEffectNodeLifecycle.AdvanceContext(ReadTask, WriteTask, () =>
        {
            int first = NativeBattleWaterExecutionFields.ArenaOffset(frame.State.ReadDword(0x25CFA2C), 0x10E0);
            int second = NativeBattleWaterExecutionFields.ArenaOffset(frame.State.ReadDword(0x25CFA44), 0x10680);
            using NativeBattleEffectNodeMemory memory = new(frame.Memory, frame.Effect, address, first, 0x10E0, second, 0x10680, useSharedScratch: true);
            NativeBattleTaskOperation.AdmitCurrentRetainedMemoryStorage(frame.State.NodeData ?? throw new NotSupportedException("Effect83 node source is missing."));
            using NativeBattleEffectNodeBank bank = new(NativeBattleEffect83Profile.NodeSourceAddress, frame.State.NodeData ?? throw new NotSupportedException("Effect83 requires its current relocated source bank."));
            ushort emitterCursor = frame.State.ReadWord(0x25CA850);
            ushort geometryCursor = frame.State.ReadWord(0x25CFA38);
            NativeBattleActionMatrix camera = inputs.ReadView(frame).ReadMatrix();
            NativeBattleEffectNodeInputs nodeInputs = new(camera, actor => inputs.ReadModel(frame, actor), (parent, source, target) => NativeBattleEffectNodeAllocation.AllocateEmitter(memory, first, 40, ref emitterCursor, parent, source, target), (emitter, source) => NativeBattleEffectNodeAllocation.AllocateGeometry(memory, second, 100, ref geometryCursor, emitter, source));
            nodeInputs = nodeInputs with
            {
                SelectedMorph = (mesh, firstShape, secondShape, channel) =>
                {
                    frame = frame with { Memory = memory.CaptureProcessMemory() };
                    frame = NativeBattleEffect83Morph.Execute79BEC0(frame, bank, mesh, firstShape, secondShape, channel);
                    memory.PublishProcessMemory(frame.Memory);
                }
            };
            NativeBattleEffectNodeLifecycle lifecycle = new(memory, bank, nodeInputs);
            List<(bool Common, NativeBattleGfGeometryBatch Batch)> deliveries = [];
            NativeCrtRandomState random = frame.Random;
            NativeBattlePilotVisualIdentity? currentPilotIdentity = null;
            lifecycle.AdvanceAndDraw(ref random, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), PrepareMesh, (common, batch) => deliveries.Add((common, batch)), inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(83, ReadTask()) : null, inputs.CapturePresentation ? (node, source) => (resource, matrix, identity) =>
            {
                currentPilotIdentity = identity;
                try
                {
                    return PrepareMesh(node, source)(resource, matrix);
                }
                finally
                {
                    currentPilotIdentity = null;
                }
            } : null);
            NativeBattleEffectNodeMemoryWrite written = memory.FlushStorage();
            bank.Publish();
            frame = frame with { Memory = written.Memory, Effect = written.Effect, Random = random, State = frame.State.ReplaceNodeBank(bank.Source).WriteWord(0x25CA850, emitterCursor).WriteWord(0x25CFA38, geometryCursor) };
            foreach ((bool common, NativeBattleGfGeometryBatch batch) in deliveries)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect83DeliverBatch(NativeBattleRetainedPolygonDepths.Capture(batch), common ? 0x1D8E054u : 0x25C9F74u));
            }

            NativeBattleEffectNodeMeshInstance PrepareMesh(int node, byte[] source)
            {
                return (resource, matrix) =>
                {
                    NativeBattleEffectNodeMemoryWrite current = memory.FlushStorage();
                    frame = frame with { Memory = current.Memory, Effect = current.Effect };
                    uint temporaryAddress = checked(frame.State.ReadDword(0x25CFA40) - 104);
                    if (temporaryAddress != 0x2793DF0)
                    {
                        throw new NotSupportedException("Effect83 mesh context requires its current shared physical suffix.");
                    }
                    frame = frame with { State = frame.State.WriteDword(0x25CFA40, temporaryAddress) };
                    NativeBattleEffectArena full = NativeBattleEffectNodeContextMemory.Capture(frame.Memory);
                    NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(full, 76, NativeBattleEffectArena.Clear().MarkUnknown(0, 104), 0, 104);
                    uint resourceAddress = memory.Dword(node + 0x170);
                    short fade = unchecked((short)memory.Word(node + 0x1CE));
                    uint flags = BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(0x3A)) == 0 ? 0x30u : 0;
                    temporary = temporary.Write(0, BitConverter.GetBytes(resourceAddress)).Write(0x10, BitConverter.GetBytes(memory.Byte(node + 0x1D6) is 4 or 10 ? 256 : 0)).Write(0x14, BitConverter.GetBytes(flags | (fade == 0 ? 0u : 0xC0u)));
                    if (fade != 0)
                    {
                        temporary = temporary.Write(8, BitConverter.GetBytes(memory.Dword(node + 0x16C))).Write(12, BitConverter.GetBytes((int)fade));
                    }
                    for (int offset = 0x18; offset <= 0x2A; offset += 2)
                    {
                        temporary = temporary.Write(offset, BitConverter.GetBytes((ushort)(offset is 0x20 or 0x22 or 0x28 or 0x2A ? 256 : 0)));
                    }
                    uint packedAddress = memory.Pointer(node + 0xAC);
                    int matrixOffset = NativeBattleWaterExecutionFields.ArenaOffset(packedAddress, 32);
                    (byte[] packed, bool[] known) = frame.Memory.Arena!.ReadSnapshot(matrixOffset, 32);
                    short? padding = known[18] && known[19] ? BinaryPrimitives.ReadInt16LittleEndian(packed.AsSpan(18)) : null;
                    frame = frame with { Memory = frame.Memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect83 node mesh requires current geometry registers."), matrix, true, true, padding) } };
                    NativeBattleEffect83Mesh104Step drawn = NativeBattleEffect83Mesh104.DrawLoaded(frame.Memory, resourceAddress, resource, inputs.ReadProjection(frame), temporary, inputs.Colors, captureMatrix: matrix, pilotIdentity: currentPilotIdentity);
                    full = NativeBattleEffectNodeContextMemory.Copy(drawn.Context104, 0, NativeBattleEffectNodeContextMemory.Capture(drawn.Memory), 76, 104);
                    frame = frame with { Memory = NativeBattleEffectNodeContextMemory.Publish(drawn.Memory, full), State = frame.State.WriteDword(0x25CFA40, checked(temporaryAddress + 104)) };
                    memory.PublishProcessMemory(frame.Memory);
                    return drawn.Batch;
                };
            }
        });
        frame = frame with { State = frame.State.WriteWord(0x25CFA3A, unchecked((ushort)(frame.State.ReadWord(0x25CFA3A) + context.EmitterCount))).WriteWord(0x25CFA28, unchecked((ushort)(frame.State.ReadWord(0x25CFA28) + context.GeometryCount))) };
        return new(frame, context.Complete ? 1 : 0);

        NativeBattleModulePoolTask ReadTask() => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        void WriteTask(NativeBattleModulePoolTask task)
        {
            NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
            frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, task).Pools } };
        }
    }
}
