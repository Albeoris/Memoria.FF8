using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect79NodeStep(NativeBattleEffect79Frame Frame, int Result);
public sealed record NativeBattleEffect79ObserveBone(int Actor, byte Selector) : NativeBattleEffect79Operation;
public sealed record NativeBattleEffect79DeliverBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect79Operation;

public static class NativeBattleEffect79NodeEngine
{
    public static NativeBattleEffect79Frame Initialize7B8350(NativeBattleEffect79Frame frame, uint address, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs)
    {
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        if (task.Callback != 0x7B82C0 || task.ReadWord(0x29C) != 0)
        {
            throw new InvalidDataException("Effect79 node initialization requires its actual source and mode0 task.");
        }
        uint sourceAddress = task.ReadDword(0x30);
        frame = frame with { State = frame.State.BindNodeSource(resources, sourceAddress) };
        ImmutableArray<byte> bank = frame.State.ReadNodeBank(sourceAddress);
        if (BinaryPrimitives.ReadUInt32LittleEndian(bank.AsSpan()) == 0)
        {
            byte[] relocated = NativeBattleEffectNodeBankFixups.Relocate(bank, sourceAddress);
            BinaryPrimitives.WriteUInt32LittleEndian(relocated, 1);
            frame = frame with { State = frame.State.ReplaceNodeBank(sourceAddress, relocated.ToImmutableArray()) };
        }
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        task = NativeBattleEffectNodeContext.Initialize(task, hierarchy.Read(task.ReadDword(0x10)), frame.Effect.Parameters ?? throw new NotSupportedException("Effect79 node context requires its actual action parameters."), inputs.ReadPackets(frame), actor => inputs.ReadModel(frame, actor), actor => inputs.ReadPoseSource(frame, actor), sourceAddress, observeBone: (actor, selector) => frame = inputs.Execute(frame, new NativeBattleEffect79ObserveBone(actor, selector)));
        return frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, task).Pools }, State = frame.State.WriteDword(0x25EB3BC, checked(address + 0x34)) };
    }

    public static NativeBattleEffect79NodeStep Advance7B8E30(NativeBattleEffect79Frame frame, uint address, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs)
    {
        uint sourceAddress = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address).ReadDword(0x30);
        frame = frame with { State = frame.State.WriteDword(0x25EB3BC, checked(address + 0x34)) };
        var context = NativeBattleEffectNodeLifecycle.AdvanceContext(ReadTask, WriteTask, () =>
        {
            int first = NativeBattleWaterExecutionFields.ArenaOffset(frame.State.ReadDword(0x25EAF34), 0x870);
            int second = NativeBattleWaterExecutionFields.ArenaOffset(frame.State.ReadDword(0x25EB3B0), 0x9D80);
            using NativeBattleEffectNodeMemory memory = new(frame.Memory, frame.Effect, address, first, 0x870, second, 0x9D80, useSharedScratch: true);
            NativeBattleEffectNodeBank bank = new(sourceAddress, frame.State.ReadNodeBank(sourceAddress));
            ushort emitterCursor = frame.State.ReadWord(0x25E8950);
            ushort geometryCursor = frame.State.ReadWord(0x25EAF40);
            NativeBattleActionMatrix camera = inputs.ReadView(frame).ReadMatrix();
            NativeBattleEffectNodeInputs nodeInputs = new(camera, actor => inputs.ReadModel(frame, actor), (parent, source, target) =>
            {
                ObserveStack(0x7BA3F0, null);
                return NativeBattleEffectNodeAllocation.AllocateEmitter(memory, first, 20, ref emitterCursor, parent, source, target);
            }, (emitter, source) =>
            {
                ObserveStack(0x7BAD70, null);
                return NativeBattleEffectNodeAllocation.AllocateGeometry(memory, second, 60, ref geometryCursor, emitter, source);
            });
            nodeInputs = nodeInputs with
            {
                EmitterAdvanced = () => ObserveStack(0x7BA490, null),
                GeometryAdvanced = (_, complete) => ObserveStack(complete ? 0x7B9250u : 0x7B9265u, complete ? 0x7B9255u : 0x7B926Au),
                ActorAnchor = (int actor, ref NativeCrtRandomState currentRandom) =>
                {
                    currentRandom = currentRandom.Draw(out ushort value);
                    byte selector = checked((byte)resources.PointSelectors[value % 18]);
                    frame = frame with { Memory = memory.CaptureProcessMemory(), Random = currentRandom };
                    frame = inputs.Execute(frame, new NativeBattleEffect79ObserveBone(actor, selector));
                    memory.PublishProcessMemory(frame.Memory);
                    return NativeBattlePoseMatrices.ReadBone(inputs.ReadModel(frame, actor), inputs.ReadPoseSource(frame, actor), selector, 0);
                },
                SelectedMorph = (mesh, firstShape, secondShape, channel) =>
                {
                    frame = frame with { Memory = memory.CaptureProcessMemory() };
                    NativeBattleEffect79MorphStep morphed = NativeBattleEffect79Morph.Execute7BA230(frame, sourceAddress, bank.Data, mesh, firstShape, secondShape, channel);
                    frame = morphed.Frame;
                    bank.ReplaceData(morphed.Bank);
                    memory.PublishProcessMemory(frame.Memory);
                }
            };
            NativeBattleEffectNodeLifecycle lifecycle = new(memory, bank, nodeInputs);
            List<(bool Common, NativeBattleGfGeometryBatch Batch)> deliveries = [];
            NativeCrtRandomState random = frame.Random;
            lifecycle.AdvanceAndDraw(ref random, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), PrepareMesh, (common, batch) =>
            {
                if (!common)
                {
                    ObserveStack(0x8DD260, null);
                }
                deliveries.Add((common, batch));
            });
            NativeBattleEffectNodeMemoryWrite written = memory.FlushStorage();
            frame = frame with { Memory = written.Memory, Effect = written.Effect, Random = random, State = frame.State.ReplaceNodeBank(sourceAddress, bank.Data).WriteWord(0x25E8950, emitterCursor).WriteWord(0x25EAF40, geometryCursor) };
            foreach ((bool common, NativeBattleGfGeometryBatch batch) in deliveries)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect79DeliverBatch(batch, common ? 0x1D8E054u : 0x25DF33Cu));
            }

            void ObserveStack(uint instruction, uint? firstDword)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect79ObserveNodeStack(instruction, firstDword));
            }

            NativeBattleEffectNodeMeshInstance PrepareMesh(int node, byte[] source)
            {
                return (resource, matrix) =>
                {
                    NativeBattleEffectNodeMemoryWrite current = memory.FlushStorage();
                    frame = frame with { Memory = current.Memory, Effect = current.Effect };
                    uint temporaryAddress = checked(frame.State.ReadDword(0x25EB3AC) - 104);
                    if (temporaryAddress != 0x2793DF0)
                    {
                        throw new NotSupportedException("Effect79 mesh context requires its current shared physical suffix.");
                    }
                    frame = frame with { State = frame.State.WriteDword(0x25EB3AC, temporaryAddress) };
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
                    frame = frame with { Memory = frame.Memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect79 node mesh requires current geometry registers."), matrix, true, true, padding) } };
                    ObserveStack(0x7B6020, null);
                    NativeBattleEffect79Mesh104Step drawn = NativeBattleEffect79Mesh104.DrawLoaded(frame.Memory, new(0, resourceAddress, resource), inputs.ReadProjection(frame), temporary, inputs.Colors);
                    full = NativeBattleEffectNodeContextMemory.Copy(drawn.Context104, 0, NativeBattleEffectNodeContextMemory.Capture(drawn.Memory), 76, 104);
                    frame = frame with { Memory = NativeBattleEffectNodeContextMemory.Publish(drawn.Memory, full), State = frame.State.WriteDword(0x25EB3AC, checked(temporaryAddress + 104)) };
                    memory.PublishProcessMemory(frame.Memory);
                    return drawn.Batch;
                };
            }
        });
        frame = frame with { State = frame.State.WriteWord(0x25EB390, unchecked((ushort)(frame.State.ReadWord(0x25EB390) + context.EmitterCount))).WriteWord(0x25EAF30, unchecked((ushort)(frame.State.ReadWord(0x25EAF30) + context.GeometryCount))) };
        return new(frame, context.Complete ? 1 : 0);

        NativeBattleModulePoolTask ReadTask() => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        void WriteTask(NativeBattleModulePoolTask task)
        {
            NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
            frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, task).Pools } };
        }
    }
}
