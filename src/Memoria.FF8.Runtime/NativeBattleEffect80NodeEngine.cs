using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect80NodeStep(NativeBattleEffect80Frame Frame, int Result);
public sealed record NativeBattleEffect80DeliverBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect80Operation;
public sealed record NativeBattleEffect80ObserveBone(int Actor, byte Selector) : NativeBattleEffect80Operation;

public static class NativeBattleEffect80NodeEngine
{
    public static NativeBattleEffect80Frame Initialize7B0B70(NativeBattleEffect80Frame frame, uint address, NativeBattleEffect80Resources resources, NativeBattleEffect80Inputs inputs)
    {
        uint sourceAddress = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address).ReadDword(0x30);
        frame = frame with { State = frame.State.BindNodeSource(resources, sourceAddress) };
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        if (task.Callback != 0x7B0AE0 || !NativeBattleEffect80Profile.NodeSourceLengths.ContainsKey(task.ReadDword(0x30)) || task.ReadWord(0x29C) != 4)
        {
            throw new InvalidDataException("Effect80 node initialization requires its actual source and mode4 task.");
        }
        ImmutableArray<byte> bank = frame.State.ReadNodeBank(sourceAddress);
        if (BinaryPrimitives.ReadUInt32LittleEndian(bank.AsSpan()) == 0)
        {
            byte[] relocated = NativeBattleEffectNodeBankFixups.Relocate(bank, sourceAddress);
            BinaryPrimitives.WriteUInt32LittleEndian(relocated, 1);
            frame = frame with { State = frame.State.ReplaceNodeBank(sourceAddress, relocated.ToImmutableArray()) };
        }
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        task = NativeBattleEffectNodeContext.Initialize(task, hierarchy.Read(task.ReadDword(0x10)), frame.Effect.Parameters ?? throw new NotSupportedException("Effect80 node context requires its actual action parameters."), inputs.ReadPackets(frame), actor => inputs.ReadModel(frame, actor), actor => inputs.ReadPoseSource(frame, actor), sourceAddress, observeBone: (actor, selector) => frame = inputs.Execute(frame, new NativeBattleEffect80ObserveBone(actor, selector)));
        return frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, task).Pools }, State = frame.State.WriteDword(0x25DF308, checked(address + 0x34)) };
    }

    public static NativeBattleEffect80NodeStep Advance7B1650(NativeBattleEffect80Frame frame, uint address, NativeBattleEffect80Resources resources, NativeBattleEffect80Inputs inputs)
    {
        uint sourceAddress = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address).ReadDword(0x30);
        frame = frame with { State = frame.State.WriteDword(0x25DF308, checked(address + 0x34)) };
        var context = NativeBattleEffectNodeLifecycle.AdvanceContext(ReadTask, WriteTask, () =>
        {
            int first = NativeBattleWaterExecutionFields.ArenaOffset(frame.State.ReadDword(0x25DF00C), 0x870);
            int second = NativeBattleWaterExecutionFields.ArenaOffset(frame.State.ReadDword(0x25DF2FC), 0x10680);
            using NativeBattleEffectNodeMemory memory = new(frame.Memory, frame.Effect, address, first, 0x870, second, 0x10680, useSharedScratch: true);
            NativeBattleEffectNodeBank bank = new(sourceAddress, frame.State.ReadNodeBank(sourceAddress));
            ushort emitterCursor = frame.State.ReadWord(0x25DCF30);
            ushort geometryCursor = frame.State.ReadWord(0x25DF018);
            NativeBattleActionMatrix camera = inputs.ReadView(frame).ReadMatrix();
            NativeBattleEffectNodeInputs nodeInputs = new(camera, actor => inputs.ReadModel(frame, actor), (parent, source, target) => NativeBattleEffectNodeAllocation.AllocateEmitter(memory, first, 20, ref emitterCursor, parent, source, target), (emitter, source) => NativeBattleEffectNodeAllocation.AllocateGeometry(memory, second, 100, ref geometryCursor, emitter, source));
            nodeInputs = nodeInputs with
            {
                SelectedMorph = (mesh, firstShape, secondShape, channel) =>
                {
                    frame = frame with { Memory = memory.CaptureProcessMemory() };
                    NativeBattleEffect80MorphStep morphed = NativeBattleEffect80Morph.Execute7B2E40(frame, bank.Data, sourceAddress, mesh, firstShape, secondShape, channel);
                    frame = morphed.Frame;
                    bank.ReplaceData(morphed.Bank);
                    memory.PublishProcessMemory(frame.Memory);
                }
            };
            NativeBattleEffectNodeLifecycle lifecycle = new(memory, bank, nodeInputs);
            List<(bool Common, NativeBattleGfGeometryBatch Batch)> deliveries = [];
            NativeCrtRandomState random = frame.Random;
            lifecycle.AdvanceAndDraw(ref random, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), PrepareMesh, (common, batch) => deliveries.Add((common, batch)));
            NativeBattleEffectNodeMemoryWrite written = memory.FlushStorage();
            frame = frame with { Memory = written.Memory, Effect = written.Effect, Random = random, State = frame.State.ReplaceNodeBank(sourceAddress, bank.Data).WriteWord(0x25DCF30, emitterCursor).WriteWord(0x25DF018, geometryCursor) };
            foreach ((bool common, NativeBattleGfGeometryBatch batch) in deliveries)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect80DeliverBatch(batch, common ? 0x1D8E054u : 0x25D493Cu));
            }

            NativeBattleEffectNodeMeshInstance PrepareMesh(int node, byte[] source)
            {
                return (resource, matrix) =>
                {
                    NativeBattleEffectNodeMemoryWrite current = memory.FlushStorage();
                    frame = frame with { Memory = current.Memory, Effect = current.Effect };
                    uint temporaryAddress = checked(frame.State.ReadDword(0x25DF2F8) - 104);
                    if (temporaryAddress != 0x2793DF0)
                    {
                        throw new NotSupportedException("Effect80 mesh context requires its current shared physical suffix.");
                    }
                    frame = frame with { State = frame.State.WriteDword(0x25DF2F8, temporaryAddress) };
                    NativeBattleEffectArena full = NativeBattleEffectNodeContextMemory.Capture(frame.Memory);
                    NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(full, 76, NativeBattleEffectArena.Clear().MarkUnknown(0, 104), 0, 104);
                    uint resourceAddress = memory.Dword(node + 0x170);
                    short fade = unchecked((short)memory.Word(node + 0x1CE));
                    uint flags = BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(0x3A)) == 0 ? 0x30u : 0;
                    temporary = temporary.Write(0, BitConverter.GetBytes(resourceAddress)).Write(0x10, BitConverter.GetBytes(0)).Write(0x14, BitConverter.GetBytes(flags | (fade == 0 ? 0u : 0xC0u)));
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
                    frame = frame with { Memory = frame.Memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect80 node mesh requires current geometry registers."), matrix, true, true, padding) } };
                    NativeBattleEffect80Mesh104Step drawn = NativeBattleEffect80Mesh104.DrawLoaded(frame.Memory, new(0, resourceAddress, resource), inputs.ReadProjection(frame), temporary, inputs.Colors);
                    full = NativeBattleEffectNodeContextMemory.Copy(drawn.Context104, 0, NativeBattleEffectNodeContextMemory.Capture(drawn.Memory), 76, 104);
                    frame = frame with { Memory = NativeBattleEffectNodeContextMemory.Publish(drawn.Memory, full), State = frame.State.WriteDword(0x25DF2F8, checked(temporaryAddress + 104)) };
                    memory.PublishProcessMemory(frame.Memory);
                    return drawn.Batch;
                };
            }
        });
        frame = frame with { State = frame.State.WriteWord(0x25DF2F0, unchecked((ushort)(frame.State.ReadWord(0x25DF2F0) + context.EmitterCount))).WriteWord(0x25DF008, unchecked((ushort)(frame.State.ReadWord(0x25DF008) + context.GeometryCount))) };
        return new(frame, context.Complete ? 1 : 0);

        NativeBattleModulePoolTask ReadTask() => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        void WriteTask(NativeBattleModulePoolTask task)
        {
            NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
            frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, task).Pools } };
        }
    }
}
